using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using nesimokau.lt.Models;
using Microsoft.JSInterop;

namespace nesimokau.lt.Services;

public sealed class AuthService(HttpClient http, IConfiguration config, IJSRuntime js, NavigationManager navigation)
{
    private const string SessionKey = "nesimokau-auth-session";
    private const string ProgressKey = "nesimokau-progress";
    private const string LeaderboardPlayerIdKey = "nesimokau-player-id";
    private const string LocalhostInitKey = "nesimokau-localhost-init";
    private const int PasswordIterations = 120_000;
    private const int SessionDays = 7;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(7);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string? supabaseUrl = config["Supabase:Url"];
    private readonly string? supabaseKey = config["Supabase:PublishableKey"];

    public async Task<bool> IsAuthenticatedAsync() => await GetSessionAsync() is not null;

    public async Task<AuthSession?> GetSessionAsync()
    {
        await EnsureLocalhostIsolationAsync();

        var json = await js.InvokeAsync<string?>("localStorage.getItem", SessionKey);
        if (string.IsNullOrWhiteSpace(json)) return null;

        var session = JsonSerializer.Deserialize<AuthSession>(json, JsonOptions);
        if (session is null) return null;

        var now = DateTime.UtcNow;
        if (session.ExpiresAtUtc <= now)
        {
            await LogoutAsync();
            return null;
        }

        var extendedExpiry = now.AddDays(SessionDays);
        if (session.ExpiresAtUtc < extendedExpiry.AddHours(-1))
        {
            session.ExpiresAtUtc = extendedExpiry;
            await SaveSessionAsync(session);
        }

        return session;
    }

    public async Task<AuthResult> RegisterAsync(string username, string password, string avatarId, string classGroup)
    {
        try
        {
            if (!IsReady()) return AuthResult.Fail("Auth konfigūracija nerasta.");

            var cleanName = username.Trim();
            if (string.IsNullOrWhiteSpace(cleanName)) return AuthResult.Fail("Įvesk slapyvardį.");
            if (cleanName.Length < 3) return AuthResult.Fail("Slapyvardis per trumpas.");
            if (!IsUsernameAllowed(cleanName)) return AuthResult.Fail("Slapyvardyje leidžiamos tik raidės ir skaičiai (be tarpų ir simbolių).");

            var existing = await GetAccountByUsernameAsync(cleanName);
            if (existing is not null) return AuthResult.Fail("Toks slapyvardis jau užimtas.");

            var passwordHash = HashPassword(password);
            var payload = new[]
            {
                new PlayerAccountRow
                {
                    Username = cleanName,
                    PasswordHash = passwordHash,
                    AvatarId = avatarId,
                    ClassGroup = classGroup,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            using var createRequest = CreateRestRequest(HttpMethod.Post, "rest/v1/player_accounts");
            createRequest.Headers.TryAddWithoutValidation("Prefer", "return=representation");
            createRequest.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

            using var createResponse = await SendWithTimeoutAsync(createRequest);
            if (!createResponse.IsSuccessStatusCode)
            {
                var body = await createResponse.Content.ReadAsStringAsync();
                return AuthResult.Fail(ParseError(body, "Registracija nepavyko."));
            }

            var createdJson = await createResponse.Content.ReadAsStringAsync();
            var created = JsonSerializer.Deserialize<List<PlayerAccountRow>>(createdJson, JsonOptions) ?? [];
            var account = created.FirstOrDefault();
            if (account is null) return AuthResult.Fail("Nepavyko sukurti paskyros.");

            var userId = account.Id ?? $"acct-{Guid.NewGuid():N}";

            var session = new AuthSession
            {
                AccessToken = CreateSessionToken(),
                RefreshToken = CreateSessionToken(),
                UserId = userId,
                Username = cleanName,
                AvatarId = avatarId,
                ClassGroup = classGroup,
                LastLoginUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(SessionDays)
            };

            await SaveSessionAsync(session);
            return AuthResult.Ok(session);
        }
        catch (TaskCanceledException)
        {
            return AuthResult.Fail("Serveris neatsako laiku. Patikrink internetą arba Supabase nustatymus.");
        }
        catch (HttpRequestException)
        {
            return AuthResult.Fail("Nepavyko prisijungti prie serverio. Patikrink Supabase URL/Key.");
        }
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        try
        {
            if (!IsReady()) return AuthResult.Fail("Auth konfigūracija nerasta.");

            var cleanName = username.Trim();
            if (!IsUsernameAllowed(cleanName)) return AuthResult.Fail("Neteisingas slapyvardžio formatas.");
            var account = await GetAccountByUsernameAsync(cleanName);
            if (account is null)
            {
                return AuthResult.Fail("Tokio vartotojo nėra.");
            }

            if (string.IsNullOrWhiteSpace(account.PasswordHash) || !VerifyPassword(password, account.PasswordHash))
            {
                return AuthResult.Fail("Neteisingas slaptažodis.");
            }

            var userId = account.Id ?? $"acct-{Guid.NewGuid():N}";
            var resolvedAvatar = string.IsNullOrWhiteSpace(account.AvatarId) ? "a1" : account.AvatarId;
            var resolvedClass = string.IsNullOrWhiteSpace(account.ClassGroup) ? "5-6" : account.ClassGroup;

            var session = new AuthSession
            {
                AccessToken = CreateSessionToken(),
                RefreshToken = CreateSessionToken(),
                UserId = userId,
                Username = account.Username ?? cleanName,
                AvatarId = resolvedAvatar,
                ClassGroup = resolvedClass,
                LastLoginUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(SessionDays)
            };

            await SaveSessionAsync(session);
            return AuthResult.Ok(session);
        }
        catch (TaskCanceledException)
        {
            return AuthResult.Fail("Serveris neatsako laiku. Patikrink internetą arba Supabase nustatymus.");
        }
        catch (HttpRequestException)
        {
            return AuthResult.Fail("Nepavyko prisijungti prie serverio. Patikrink Supabase URL/Key.");
        }
    }

    public async Task LogoutAsync()
    {
        await js.InvokeVoidAsync("localStorage.removeItem", SessionKey);
    }

    public async Task<bool> IsUsernameTakenAsync(string username)
    {
        var cleanName = username.Trim();
        if (string.IsNullOrWhiteSpace(cleanName)) return false;
        if (!IsReady()) return false;

        var existing = await GetAccountByUsernameAsync(cleanName);
        return existing is not null;
    }

    public async Task UpdateCurrentAvatarAsync(string avatarId)
    {
        if (string.IsNullOrWhiteSpace(avatarId) || !IsReady()) return;

        var session = await GetSessionAsync();
        if (session is null || string.IsNullOrWhiteSpace(session.UserId)) return;

        var payload = new PlayerAccountRow
        {
            AvatarId = avatarId,
            UpdatedAt = DateTime.UtcNow
        };

        using var request = CreateRestRequest(HttpMethod.Patch, $"rest/v1/player_accounts?id=eq.{Uri.EscapeDataString(session.UserId)}");
        request.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        _ = await SendWithTimeoutAsync(request);

        session.AvatarId = avatarId;
        await SaveSessionAsync(session);
    }

    private async Task SaveSessionAsync(AuthSession session)
    {
        var json = JsonSerializer.Serialize(session, JsonOptions);
        await js.InvokeVoidAsync("localStorage.setItem", SessionKey, json);
    }

    private async Task EnsureLocalhostIsolationAsync()
    {
        if (!IsLocalhost()) return;

        var initialized = await js.InvokeAsync<string?>("sessionStorage.getItem", LocalhostInitKey);
        if (!string.IsNullOrWhiteSpace(initialized)) return;

        await js.InvokeVoidAsync("localStorage.removeItem", SessionKey);
        await js.InvokeVoidAsync("localStorage.removeItem", ProgressKey);
        await js.InvokeVoidAsync("localStorage.removeItem", LeaderboardPlayerIdKey);
        await js.InvokeVoidAsync("sessionStorage.setItem", LocalhostInitKey, "1");
    }

    private bool IsLocalhost()
    {
        if (!Uri.TryCreate(navigation.BaseUri, UriKind.Absolute, out var uri)) return false;
        return uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<PlayerAccountRow?> GetAccountByUsernameAsync(string username)
    {
        using var request = CreateRestRequest(HttpMethod.Get, $"rest/v1/player_accounts?select=id,username,password_hash,avatar_id,class_group,updated_at&username=eq.{Uri.EscapeDataString(username)}&limit=1");
        using var response = await SendWithTimeoutAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var items = JsonSerializer.Deserialize<List<PlayerAccountRow>>(json, JsonOptions) ?? [];
        return items.FirstOrDefault();
    }

    private HttpRequestMessage CreateRestRequest(HttpMethod method, string path, string? accessToken = null)
    {
        var request = new HttpRequestMessage(method, BuildUrl(path));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("apikey", supabaseKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", string.IsNullOrWhiteSpace(accessToken) ? supabaseKey : accessToken);
        return request;
    }

    private string BuildUrl(string path) => $"{supabaseUrl?.TrimEnd('/')}/{path}";

    private bool IsReady() => !string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(supabaseKey);

    private static string CreateSessionToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256${PasswordIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || !parts[0].Equals("pbkdf2-sha256", StringComparison.Ordinal)) return false;
        if (!int.TryParse(parts[1], out var iterations)) return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static bool IsUsernameAllowed(string value)
        => !string.IsNullOrWhiteSpace(value)
           && value.All(char.IsLetterOrDigit);

    private static string ParseError(string body, string fallback)
    {
        if (string.IsNullOrWhiteSpace(body)) return fallback;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                var text = message.GetString();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }

            if (doc.RootElement.TryGetProperty("msg", out var msg) && msg.ValueKind == JsonValueKind.String)
            {
                return msg.GetString() ?? fallback;
            }

            if (doc.RootElement.TryGetProperty("error_description", out var desc) && desc.ValueKind == JsonValueKind.String)
            {
                return desc.GetString() ?? fallback;
            }

            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
            {
                return error.GetString() ?? fallback;
            }

            if (doc.RootElement.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.String)
            {
                var text = details.GetString();
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        }
        catch
        {
            // ignored
        }

        return fallback;
    }

    private async Task<HttpResponseMessage> SendWithTimeoutAsync(HttpRequestMessage request)
    {
        using var cts = new CancellationTokenSource(RequestTimeout);
        return await http.SendAsync(request, cts.Token);
    }

    private sealed class PlayerAccountRow
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("username")]
        public string? Username { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("password_hash")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? PasswordHash { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("avatar_id")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? AvatarId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("class_group")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? ClassGroup { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("updated_at")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? UpdatedAt { get; set; }
    }
}
