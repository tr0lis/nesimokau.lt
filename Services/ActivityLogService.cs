using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using nesimokau.lt.Models;

namespace nesimokau.lt.Services;

public sealed class ActivityLogService(ApiClient api, AuthService auth)
{
    public async Task LogAsync(string eventType, Action<ActivityEventBuilder>? build = null)
    {
        if (string.IsNullOrWhiteSpace(eventType)) return;

        try
        {
            if (await auth.GetSessionAsync() is null) return;

            var builder = new ActivityEventBuilder { EventType = eventType };
            build?.Invoke(builder);

            _ = await api.PostAsync("/activity", new
            {
                event_type = builder.EventType,
                subject = builder.Subject,
                game_id = builder.GameId,
                game_title = builder.GameTitle,
                class_group = builder.ClassGroup,
                score = builder.Score,
                accuracy = builder.Accuracy,
                correct_answers = builder.CorrectAnswers,
                incorrect_answers = builder.IncorrectAnswers,
                xp_earned = builder.XpEarned,
                coins_earned = builder.CoinsEarned,
                coins_spent = builder.CoinsSpent,
                hint_cost = builder.HintCost,
                is_correct = builder.IsCorrect,
                question_id = builder.QuestionId,
                mistake_type = builder.MistakeType,
                payload = builder.Payload
            });
        }
        catch
        {
            // logging must never block gameplay
        }
    }

    public sealed class ActivityEventBuilder
    {
        public string EventType { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? Username { get; set; }
        public string? Subject { get; set; }
        public string? GameId { get; set; }
        public string? GameTitle { get; set; }
        public string? ClassGroup { get; set; }
        public int? Score { get; set; }
        public int? Accuracy { get; set; }
        public int? CorrectAnswers { get; set; }
        public int? IncorrectAnswers { get; set; }
        public int? XpEarned { get; set; }
        public int? CoinsEarned { get; set; }
        public int? CoinsSpent { get; set; }
        public int? HintCost { get; set; }
        public bool? IsCorrect { get; set; }
        public string? QuestionId { get; set; }
        public string? MistakeType { get; set; }
        public Dictionary<string, object?> Payload { get; set; } = [];
    }

    private sealed class ActivityLogRow
    {
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("user_id")]
        public string? UserId { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("event_type")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("game_id")]
        public string? GameId { get; set; }

        [JsonPropertyName("game_title")]
        public string? GameTitle { get; set; }

        [JsonPropertyName("class_group")]
        public string? ClassGroup { get; set; }

        [JsonPropertyName("score")]
        public int? Score { get; set; }

        [JsonPropertyName("accuracy")]
        public int? Accuracy { get; set; }

        [JsonPropertyName("correct_answers")]
        public int? CorrectAnswers { get; set; }

        [JsonPropertyName("incorrect_answers")]
        public int? IncorrectAnswers { get; set; }

        [JsonPropertyName("xp_earned")]
        public int? XpEarned { get; set; }

        [JsonPropertyName("coins_earned")]
        public int? CoinsEarned { get; set; }

        [JsonPropertyName("coins_spent")]
        public int? CoinsSpent { get; set; }

        [JsonPropertyName("hint_cost")]
        public int? HintCost { get; set; }

        [JsonPropertyName("is_correct")]
        public bool? IsCorrect { get; set; }

        [JsonPropertyName("question_id")]
        public string? QuestionId { get; set; }

        [JsonPropertyName("mistake_type")]
        public string? MistakeType { get; set; }

        [JsonPropertyName("payload")]
        public Dictionary<string, object?> Payload { get; set; } = [];
    }
}
