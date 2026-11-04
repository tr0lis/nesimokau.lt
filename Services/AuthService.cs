using System.Text.Json;
using Microsoft.JSInterop;
using nesimokau.lt.Models;

namespace nesimokau.lt.Services;

public sealed class AuthService(ApiClient api, IJSRuntime js)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private AuthSession? session;
    private bool loaded;
    private Task? loading;

    public event Action? SessionChanged;

    public async Task<bool> IsAuthenticatedAsync() => await GetSessionAsync() is not null;

    /// <summary>Returns the cached session, validating it once against the server (the cookie is the only credential).</summary>
    public async Task<AuthSession?> GetSessionAsync()
    {
        if (!loaded)
        {
            loading ??= LoadAsync();
            await loading;
        }

        return session;
    }

    private async Task LoadAsync()
    {
        var response = await api.GetAsync("/auth/me");
        if (response.Ok)
        {
            session = response.GetBool("authenticated") ? ParseUser(response.Body) : null;
            loaded = true;
        }
        else if ((int)response.Status is 401 or 403)
        {
            session = null;
            loaded = true;
        }
        else
        {
            // Server unreachable: do not cache, try again on the next call.
            session = null;
        }

        loading = null;
    }

    public async Task<AuthResult> RegisterAsync(string username, string password, string avatarId, string classGroup)
    {
        var response = await api.PostAsync("/auth/register", new { username = username.Trim(), password, avatarId, classGroup });
        if (!response.Ok) return AuthResult.Fail(response.Error ?? "Registracija nepavyko.");
        return Accept(response);
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        var response = await api.PostAsync("/auth/login", new { username = username.Trim(), password });
        if (!response.Ok) return AuthResult.Fail(response.Error ?? "Prisijungti nepavyko.");
        if (response.GetBool("mfaRequired")) return AuthResult.NeedsMfa(response.GetString("ticket") ?? string.Empty);
        return Accept(response);
    }

    public async Task<AuthResult> VerifyMfaAsync(string ticket, string code)
    {
        var response = await api.PostAsync("/auth/mfa", new { ticket, code });
        if (!response.Ok) return AuthResult.Fail(response.Error ?? "Neteisingas kodas.");
        return Accept(response);
    }

    public async Task LogoutAsync()
    {
        await api.PostAsync("/auth/logout");
        session = null;
        loaded = true;
        await js.InvokeVoidAsync("localStorage.removeItem", "nesimokau-player-id");
        SessionChanged?.Invoke();
    }

    public async Task<bool> IsUsernameTakenAsync(string username)
    {
        var clean = username.Trim();
        if (clean.Length < 3) return false;

        var response = await api.GetAsync($"/auth/username-available?u={Uri.EscapeDataString(clean)}");
        if (!response.Ok || response.Body is not { ValueKind: JsonValueKind.Object } body) return false;
        return body.TryGetProperty("valid", out var valid) && valid.GetBoolean()
            && body.TryGetProperty("available", out var available) && !available.GetBoolean();
    }

    public async Task UpdateCurrentAvatarAsync(string avatarId)
    {
        if (string.IsNullOrWhiteSpace(avatarId) || await GetSessionAsync() is not { } current) return;

        var response = await api.PostAsync("/auth/avatar", new { avatarId });
        if (response.Ok) current.AvatarId = avatarId;
    }

    public async Task<(bool Ok, string Error, TwoFactorSetup? Setup)> BeginTwoFactorAsync(string password)
    {
        var response = await api.PostAsync("/auth/2fa/setup", new { password });
        return response.Ok
            ? (true, string.Empty, response.As<TwoFactorSetup>())
            : (false, response.Error ?? "Nepavyko pradėti.", null);
    }

    public async Task<(bool Ok, string Error, List<string> BackupCodes)> EnableTwoFactorAsync(string code)
    {
        var response = await api.PostAsync("/auth/2fa/enable", new { code });
        if (!response.Ok) return (false, response.Error ?? "Neteisingas kodas.", []);

        if (session is not null) session.TotpEnabled = true;
        SessionChanged?.Invoke();
        return (true, string.Empty, ReadCodes(response));
    }

    public async Task<(bool Ok, string Error)> DisableTwoFactorAsync(string password, string code)
    {
        var response = await api.PostAsync("/auth/2fa/disable", new { password, code });
        if (!response.Ok) return (false, response.Error ?? "Nepavyko išjungti.");

        if (session is not null) session.TotpEnabled = false;
        SessionChanged?.Invoke();
        return (true, string.Empty);
    }

    public async Task<(bool Ok, string Error, List<string> BackupCodes)> RegenerateBackupCodesAsync(string password, string code)
    {
        var response = await api.PostAsync("/auth/2fa/regenerate", new { password, code });
        return response.Ok ? (true, string.Empty, ReadCodes(response)) : (false, response.Error ?? "Nepavyko.", []);
    }

    public async Task<(bool Ok, string Error)> ChangePasswordAsync(string currentPassword, string newPassword, string? code)
    {
        var response = await api.PostAsync("/auth/password", new { currentPassword, newPassword, code });
        return response.Ok ? (true, string.Empty) : (false, response.Error ?? "Nepavyko pakeisti slaptažodžio.");
    }

    private AuthResult Accept(ApiClient.ApiResponse response)
    {
        var parsed = ParseUser(response.Body);
        if (parsed is null) return AuthResult.Fail("Netikėtas serverio atsakymas.");

        session = parsed;
        loaded = true;
        SessionChanged?.Invoke();
        return AuthResult.Ok(parsed);
    }

    private static AuthSession? ParseUser(JsonElement? body)
    {
        if (body is not { ValueKind: JsonValueKind.Object } b || !b.TryGetProperty("user", out var user)) return null;
        return user.Deserialize<AuthSession>(JsonOptions) is { } s && !string.IsNullOrWhiteSpace(s.UserId) ? s : null;
    }

    private static List<string> ReadCodes(ApiClient.ApiResponse response)
        => response.Body is { ValueKind: JsonValueKind.Object } b && b.TryGetProperty("backupCodes", out var codes)
            ? codes.Deserialize<List<string>>(JsonOptions) ?? []
            : [];
}
