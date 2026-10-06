using System.Text.Json.Serialization;

namespace nesimokau.lt.Models;

public sealed class UserProgressRow
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("avatar_id")]
    public string? AvatarId { get; set; }

    [JsonPropertyName("avatar_background")]
    public string? AvatarBackground { get; set; }

    [JsonPropertyName("class_group")]
    public string? ClassGroup { get; set; }

    [JsonPropertyName("theme")]
    public string? Theme { get; set; }

    [JsonPropertyName("level")]
    public int? Level { get; set; }

    [JsonPropertyName("xp")]
    public int? Xp { get; set; }

    [JsonPropertyName("coins")]
    public int? Coins { get; set; }

    [JsonPropertyName("streak")]
    public int? Streak { get; set; }

    [JsonPropertyName("best_score")]
    public int? BestScore { get; set; }

    [JsonPropertyName("games_completed")]
    public int? GamesCompleted { get; set; }

    [JsonPropertyName("unlocked_avatar_ids")]
    public List<string>? UnlockedAvatarIds { get; set; }

    [JsonPropertyName("unlocked_achievement_ids")]
    public List<string>? UnlockedAchievementIds { get; set; }

    [JsonPropertyName("bookmarked_game_ids")]
    public List<string>? BookmarkedGameIds { get; set; }

    [JsonPropertyName("practice_mistake_counts")]
    public Dictionary<string, int>? PracticeMistakeCounts { get; set; }

    [JsonPropertyName("solved_question_ids_by_game")]
    public Dictionary<string, List<int>>? SolvedQuestionIdsByGame { get; set; }

    [JsonPropertyName("hint_purchase_date_utc")]
    public string? HintPurchaseDateUtc { get; set; }

    [JsonPropertyName("hint_purchases_today")]
    public int? HintPurchasesToday { get; set; }

    [JsonPropertyName("custom_avatar_data_url")]
    public string? CustomAvatarDataUrl { get; set; }

    [JsonPropertyName("diagnostic_attempts")]
    public List<DiagnosticAttempt>? DiagnosticAttempts { get; set; }

    [JsonPropertyName("recent_results")]
    public List<GameResult>? RecentResults { get; set; }
}
