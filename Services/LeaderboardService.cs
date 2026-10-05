using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using nesimokau.lt.Models;

namespace nesimokau.lt.Services;

public sealed class LeaderboardService(IJSRuntime js, ProgressService progress, AuthService auth, HttpClient http, IConfiguration config)
{
    private const string PlayerIdKey = "nesimokau-player-id";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string? supabaseUrl = config["Supabase:Url"];
    private readonly string? supabaseKey = config["Supabase:PublishableKey"];

    public async Task<IReadOnlyList<LeaderboardEntry>> GetEntriesAsync()
    {
        await UpsertCurrentUserAsync();
        return await LoadAsync();
    }

    public Task<string> GetCurrentPlayerIdAsync() => GetOrCreatePlayerIdAsync();

    public async Task<IReadOnlyList<LeaderboardEntry>> RefreshRealtimeAsync()
    {
        await UpsertCurrentUserAsync();
        return await LoadAsync();
    }

    public async Task ResetCurrentUserScoreAsync()
    {
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey)) return;

        var playerId = await GetOrCreatePlayerIdAsync();
        var playerProgress = await progress.GetAsync();
        var playerName = string.IsNullOrWhiteSpace(playerProgress.Name) ? "Svečias" : playerProgress.Name.Trim();
        var avatar = AvatarCatalog.All.FirstOrDefault(x => x.Id == playerProgress.AvatarId)?.Symbol ?? "🙂";

        await RemoveDuplicateRowsByNicknameAsync(playerName, playerId);

        var payload = new[]
        {
            new SupabaseLeaderboardRow
            {
                PlayerId = playerId,
                Nickname = playerName,
                Score = 0,
                Avatar = avatar,
                CountryFlag = "🇱🇹",
                UpdatedAt = DateTime.UtcNow
            }
        };

        using var request = CreateRequest(HttpMethod.Post, "rest/v1/leaderboard?on_conflict=player_id");
        request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");
        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        _ = await http.SendAsync(request);
    }

    private async Task UpsertCurrentUserAsync()
    {
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey)) return;

        var playerId = await GetOrCreatePlayerIdAsync();
        var playerProgress = await progress.GetAsync();
        var playerName = string.IsNullOrWhiteSpace(playerProgress.Name) ? "Svečias" : playerProgress.Name.Trim();
        var score = Math.Max(playerProgress.RecentResults.Sum(result => result.Score), playerProgress.BestScore);
        var avatar = AvatarCatalog.All.FirstOrDefault(x => x.Id == playerProgress.AvatarId)?.Symbol ?? "🙂";
        var countryFlag = "🇱🇹";

        await RemoveDuplicateRowsByNicknameAsync(playerName, playerId);

        var payload = new[]
        {
            new SupabaseLeaderboardRow
            {
                PlayerId = playerId,
                Nickname = playerName,
                Score = score,
                Avatar = avatar,
                CountryFlag = countryFlag,
                UpdatedAt = DateTime.UtcNow
            }
        };

        using var request = CreateRequest(HttpMethod.Post, "rest/v1/leaderboard?on_conflict=player_id");
        request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");
        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        _ = await http.SendAsync(request);
    }

    private async Task<IReadOnlyList<LeaderboardEntry>> LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey)) return [];

        using var request = CreateRequest(HttpMethod.Get, "rest/v1/leaderboard?select=player_id,nickname,score,avatar,country_flag,updated_at&order=score.desc,updated_at.asc&limit=200");
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var json = await response.Content.ReadAsStringAsync();
        var rows = JsonSerializer.Deserialize<List<SupabaseLeaderboardRow>>(json, JsonOptions) ?? [];
        return rows
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.UpdatedAt ?? DateTime.MaxValue)
            .Select(x => new LeaderboardEntry(x.PlayerId ?? string.Empty, x.Nickname ?? "Svečias", x.Score, "🥇", x.Avatar ?? "🙂", x.CountryFlag ?? "🇱🇹"))
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .ToList();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var baseUrl = supabaseUrl?.TrimEnd('/') ?? string.Empty;
        var request = new HttpRequestMessage(method, $"{baseUrl}/{path}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("apikey", supabaseKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
        return request;
    }

    private async Task<string> GetOrCreatePlayerIdAsync()
    {
        var authSession = await auth.GetSessionAsync();
        if (authSession is not null && !string.IsNullOrWhiteSpace(authSession.UserId))
        {
            var stablePlayerId = $"user-{authSession.UserId}";
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", PlayerIdKey);
            if (!string.Equals(stored, stablePlayerId, StringComparison.Ordinal))
            {
                await js.InvokeVoidAsync("localStorage.setItem", PlayerIdKey, stablePlayerId);
            }

            return stablePlayerId;
        }

        var playerId = await js.InvokeAsync<string?>("localStorage.getItem", PlayerIdKey);
        if (!string.IsNullOrWhiteSpace(playerId)) return playerId;

        playerId = $"player-{Guid.NewGuid():N}";
        await js.InvokeVoidAsync("localStorage.setItem", PlayerIdKey, playerId);
        return playerId;
    }

    private async Task RemoveDuplicateRowsByNicknameAsync(string nickname, string keepPlayerId)
    {
        if (string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(keepPlayerId)) return;

        var encodedName = Uri.EscapeDataString(nickname);
        var encodedPlayer = Uri.EscapeDataString(keepPlayerId);
        using var request = CreateRequest(HttpMethod.Delete, $"rest/v1/leaderboard?nickname=eq.{encodedName}&player_id=neq.{encodedPlayer}");
        _ = await http.SendAsync(request);
    }

    private sealed class SupabaseLeaderboardRow
    {
        [JsonPropertyName("player_id")]
        public string? PlayerId { get; set; }

        [JsonPropertyName("nickname")]
        public string? Nickname { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }

        [JsonPropertyName("country_flag")]
        public string? CountryFlag { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
