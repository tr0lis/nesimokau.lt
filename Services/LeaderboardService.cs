using nesimokau.lt.Models;

namespace nesimokau.lt.Services;

public sealed class LeaderboardService(ApiClient api, AuthService auth)
{
    public Task<IReadOnlyList<LeaderboardEntry>> GetEntriesAsync(string period = "all") => LoadAsync(period);

    public async Task<string> GetCurrentPlayerIdAsync()
    {
        var session = await auth.GetSessionAsync();
        return session is null ? string.Empty : $"user-{session.UserId}";
    }

    public Task<IReadOnlyList<LeaderboardEntry>> RefreshRealtimeAsync(string period = "all") => LoadAsync(period);

    public async Task ResetCurrentUserScoreAsync() => _ = await api.PostAsync("/leaderboard/reset");

    private async Task<IReadOnlyList<LeaderboardEntry>> LoadAsync(string period)
    {
        var response = await api.GetAsync($"/leaderboard?period={Uri.EscapeDataString(period)}");
        if (!response.Ok) return [];

        var rows = response.As<List<LeaderboardRow>>() ?? [];
        return rows
            .Where(x => !string.IsNullOrWhiteSpace(x.PlayerId))
            .Select(x => new LeaderboardEntry(
                x.PlayerId!,
                string.IsNullOrWhiteSpace(x.Nickname) ? "Svečias" : x.Nickname,
                x.Score,
                "🥇",
                AvatarCatalog.All.FirstOrDefault(a => a.Id == x.AvatarId)?.Symbol ?? "🙂",
                "🇱🇹",
                string.IsNullOrWhiteSpace(x.AvatarBackground) ? "bg-violet" : x.AvatarBackground))
            .ToList();
    }

    private sealed class LeaderboardRow
    {
        [System.Text.Json.Serialization.JsonPropertyName("player_id")] public string? PlayerId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("nickname")] public string? Nickname { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("score")] public int Score { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("avatar_id")] public string? AvatarId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("avatar_background")] public string? AvatarBackground { get; set; }
    }
}
