using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using nesimokau.lt.Models;

namespace nesimokau.lt.Services;

public sealed class ActivityLogService(HttpClient http, IConfiguration config, AuthService auth)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string? supabaseUrl = config["Supabase:Url"];
    private readonly string? supabaseKey = config["Supabase:PublishableKey"];

    public async Task LogAsync(string eventType, Action<ActivityEventBuilder>? build = null)
    {
        if (!IsReady() || string.IsNullOrWhiteSpace(eventType)) return;

        try
        {
            var session = await auth.GetSessionAsync();
            var builder = new ActivityEventBuilder
            {
                EventType = eventType,
                UserId = session?.UserId,
                Username = session?.Username
            };

            build?.Invoke(builder);

            var payload = new[]
            {
                new ActivityLogRow
                {
                    EventType = builder.EventType,
                    UserId = builder.UserId,
                    Username = builder.Username,
                    Subject = builder.Subject,
                    GameId = builder.GameId,
                    GameTitle = builder.GameTitle,
                    ClassGroup = builder.ClassGroup,
                    Score = builder.Score,
                    Accuracy = builder.Accuracy,
                    CorrectAnswers = builder.CorrectAnswers,
                    IncorrectAnswers = builder.IncorrectAnswers,
                    XpEarned = builder.XpEarned,
                    CoinsEarned = builder.CoinsEarned,
                    CoinsSpent = builder.CoinsSpent,
                    HintCost = builder.HintCost,
                    IsCorrect = builder.IsCorrect,
                    QuestionId = builder.QuestionId,
                    MistakeType = builder.MistakeType,
                    Payload = builder.Payload,
                    CreatedAt = DateTime.UtcNow
                }
            };

            using var request = CreateRequest(HttpMethod.Post, "rest/v1/user_activity_log");
            request.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
            _ = await http.SendAsync(request);
        }
        catch
        {
            // logging must never block gameplay
        }
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

    private bool IsReady() => !string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(supabaseKey);

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
        public DateTime CreatedAt { get; set; }
        public string? UserId { get; set; }
        public string? Username { get; set; }
        public string EventType { get; set; } = string.Empty;
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
}
