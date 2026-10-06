using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using Microsoft.Extensions.Configuration;
using nesimokau.lt.Models;

namespace nesimokau.lt.Services;

public sealed class ProgressService(IJSRuntime js, ActivityLogService activityLog, AuthService auth, HttpClient http, IConfiguration config)
{
    public enum HintPurchaseResult
    {
        Success,
        NotEnoughCoins,
        DailyLimitReached
    }

    private const string StorageKey = "nesimokau-progress";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string? supabaseUrl = config["Supabase:Url"];
    private readonly string? supabaseKey = config["Supabase:PublishableKey"];
    private StudentProgress? cached;
    public event Action? Changed;

    public async Task<StudentProgress> GetAsync()
    {
        if (cached is not null) return cached;
        var local = await LoadLocalAsync();
        var userId = await GetCurrentUserIdAsync();

        if (!string.IsNullOrWhiteSpace(userId) && IsDbReady())
        {
            var remote = await LoadFromDbAsync(userId);
            if (remote.Progress is not null)
            {
                cached = remote.Progress;
            }
            else if (remote.RequestSucceeded)
            {
                cached = local;
                await UpsertToDbAsync(userId, cached);
            }
            else
            {
                cached = local;
            }
        }
        else
        {
            cached = local;
        }

        Normalize(cached);
        await PersistLocalAsync(cached);
        return cached;
    }

    public async Task SaveDiagnosticAttemptAsync(DiagnosticAttempt attempt)
    {
        var progress = await GetAsync();
        progress.DiagnosticAttempts.Insert(0, attempt);
        if (progress.DiagnosticAttempts.Count > 30) progress.DiagnosticAttempts.RemoveAt(30);
        await SaveAsync(progress);

        await activityLog.LogAsync("diagnostic_saved", evt =>
        {
            evt.Subject = attempt.Subject;
            evt.GameId = "diagnostic";
            evt.GameTitle = $"{attempt.Subject} diagnostika";
            evt.ClassGroup = progress.ClassGroup;
            evt.Accuracy = attempt.Accuracy;
            evt.CorrectAnswers = attempt.Answers.Count(answer => answer.IsCorrect);
            evt.IncorrectAnswers = attempt.Answers.Count(answer => !answer.IsCorrect);
            evt.Payload["durationSeconds"] = attempt.DurationSeconds;
            evt.Payload["answerCount"] = attempt.Answers.Count;
        });
    }

    public async Task<LevelDefinition?> SaveResultAsync(GameResult result)
    {
        var progress = await GetAsync();
        var previousLevel = LevelCatalog.All.Last(level => level.RequiredXp <= progress.Xp);
        progress.Xp += result.Energy;
        progress.Coins += result.Coins;
        progress.BestScore = Math.Max(progress.BestScore, result.Score);
        progress.GamesCompleted++;
        progress.RecentResults.Insert(0, result);
        if (progress.RecentResults.Count > 120) progress.RecentResults.RemoveAt(120);
        progress.Streak = CalculateCurrentStreak(progress.RecentResults);

        var currentLevel = LevelCatalog.All.Last(level => level.RequiredXp <= progress.Xp);
        progress.Level = currentLevel.Number;

        await SaveAsync(progress);

        await activityLog.LogAsync("game_result_saved", evt =>
        {
            evt.Subject = InferSubject(result.GameId, result.GameTitle);
            evt.GameId = result.GameId;
            evt.GameTitle = result.GameTitle;
            evt.ClassGroup = string.IsNullOrWhiteSpace(result.ClassGroup) ? progress.ClassGroup : result.ClassGroup;
            evt.Score = result.Score;
            evt.Accuracy = result.Accuracy;
            evt.CorrectAnswers = result.CorrectAnswers >= 0 ? result.CorrectAnswers : null;
            evt.IncorrectAnswers = result.IncorrectAnswers >= 0 ? result.IncorrectAnswers : null;
            evt.XpEarned = result.Energy;
            evt.CoinsEarned = result.Coins;
            evt.Payload["playedAt"] = result.PlayedAt;
            evt.Payload["bestScoreAfter"] = progress.BestScore;
            evt.Payload["gamesCompletedAfter"] = progress.GamesCompleted;
        });

        return currentLevel.Number > previousLevel.Number ? currentLevel : null;
    }

    public async Task SaveProfileAsync(string name)
    {
        var progress = await GetAsync();
        progress.Name = name.Trim();
        await SaveAsync(progress);
    }

    public async Task<IReadOnlyList<string>> UnlockAchievementsAsync(IEnumerable<string> achievementIds)
    {
        var progress = await GetAsync();
        var newIds = achievementIds.Where(id => !progress.UnlockedAchievementIds.Contains(id)).Distinct().ToList();
        if (newIds.Count == 0) return [];

        progress.UnlockedAchievementIds.AddRange(newIds);
        await SaveAsync(progress);
        return newIds;
    }

    public async Task ToggleBookmarkAsync(string gameId)
    {
        var progress = await GetAsync();
        if (!progress.BookmarkedGameIds.Remove(gameId)) progress.BookmarkedGameIds.Add(gameId);
        await SaveAsync(progress);
    }

    public async Task SaveCustomAvatarAsync(string dataUrl)
    {
        var progress = await GetAsync();
        if (progress.CustomAvatarDataUrl is null && progress.Coins < 500) return;
        if (progress.CustomAvatarDataUrl is null) progress.Coins -= 500;
        progress.CustomAvatarDataUrl = dataUrl;
        await SaveAsync(progress);
    }

    public async Task<bool> SpendCoinsAsync(int amount)
    {
        if (amount <= 0) return true;

        var progress = await GetAsync();
        if (progress.Coins < amount) return false;

        progress.Coins -= amount;
        await SaveAsync(progress);
        return true;
    }

    public async Task<int> GetRemainingHintsTodayAsync(int dailyLimit)
    {
        var progress = await GetAsync();
        EnsureHintCounterForToday(progress);
        return Math.Max(0, dailyLimit - progress.HintPurchasesToday);
    }

    public async Task<HintPurchaseResult> TryPurchaseHintAsync(int cost, int dailyLimit)
    {
        var progress = await GetAsync();
        EnsureHintCounterForToday(progress);

        if (progress.HintPurchasesToday >= dailyLimit) return HintPurchaseResult.DailyLimitReached;
        if (progress.Coins < cost) return HintPurchaseResult.NotEnoughCoins;

        progress.Coins -= cost;
        progress.HintPurchasesToday++;
        await SaveAsync(progress);

        await activityLog.LogAsync("hint_used", evt =>
        {
            evt.ClassGroup = progress.ClassGroup;
            evt.CoinsSpent = cost;
            evt.HintCost = cost;
            evt.Payload["hintsUsedToday"] = progress.HintPurchasesToday;
            evt.Payload["dailyLimit"] = dailyLimit;
        });

        return HintPurchaseResult.Success;
    }

    public async Task SavePracticeTargetAsync(string gameId, int mistakes)
    {
        var progress = await GetAsync();
        progress.PracticeMistakeCounts[gameId] = mistakes;
        await SaveAsync(progress);
    }

    public async Task<IReadOnlyCollection<int>> GetSolvedQuestionIdsAsync(string gameId)
    {
        var progress = await GetAsync();
        return progress.SolvedQuestionIdsByGame.TryGetValue(gameId, out var solved)
            ? solved.Distinct().ToArray()
            : [];
    }

    public async Task SaveSolvedQuestionIdsAsync(string gameId, IEnumerable<int> solvedQuestionIds)
    {
        var progress = await GetAsync();
        if (!progress.SolvedQuestionIdsByGame.TryGetValue(gameId, out var existing))
        {
            existing = [];
            progress.SolvedQuestionIdsByGame[gameId] = existing;
        }

        var before = existing.Count;
        foreach (var id in solvedQuestionIds)
        {
            if (!existing.Contains(id)) existing.Add(id);
        }

        if (existing.Count != before)
        {
            await SaveAsync(progress);

            await activityLog.LogAsync("exercise_progress_updated", evt =>
            {
                evt.Subject = InferSubject(gameId, gameId);
                evt.GameId = gameId;
                evt.ClassGroup = progress.ClassGroup;
                evt.Payload["addedCount"] = existing.Count - before;
                evt.Payload["solvedTotal"] = existing.Count;
            });
        }
    }

    public async Task ClearPracticeTargetAsync(string gameId)
    {
        var progress = await GetAsync();
        if (progress.PracticeMistakeCounts.Remove(gameId)) await SaveAsync(progress);
    }

    private async Task SaveAsync(StudentProgress progress)
    {
        Normalize(progress);
        await PersistLocalAsync(progress);
        var userId = await GetCurrentUserIdAsync();
        if (!string.IsNullOrWhiteSpace(userId) && IsDbReady())
        {
            await UpsertToDbAsync(userId, progress);
        }

        Changed?.Invoke();
    }

    public async Task<bool> UnlockAvatarAsync(string avatarId)
    {
        var progress = await GetAsync();
        if (progress.UnlockedAvatarIds.Contains(avatarId)) return true;

        var avatar = AvatarCatalog.All.FirstOrDefault(a => a.Id == avatarId);
        if (avatar is null) return false;
        if (progress.Coins < avatar.Cost) return false;

        progress.Coins -= avatar.Cost;
        progress.UnlockedAvatarIds.Add(avatarId);
        await SaveAsync(progress);
        return true;
    }

    public async Task SaveThemeAsync(string theme)
    {
        var progress = await GetAsync();
        progress.Theme = theme;
        await SaveAsync(progress);
    }

    public async Task SaveClassGroupAsync(string classGroup)
    {
        var progress = await GetAsync();
        progress.ClassGroup = classGroup;
        await SaveAsync(progress);
    }

    public async Task SaveAvatarAsync(string avatarId)
    {
        var progress = await GetAsync();
        progress.AvatarId = avatarId;
        await SaveAsync(progress);
    }

    public async Task SaveAvatarBackgroundAsync(string avatarBackground)
    {
        var progress = await GetAsync();
        progress.AvatarBackground = avatarBackground;
        await SaveAsync(progress);
    }

    public async Task ApplyAccountProfileAsync(string username, string avatarId, string classGroup)
    {
        var progress = await GetAsync();

        if (!string.IsNullOrWhiteSpace(username))
        {
            progress.Name = username.Trim();
        }

        if (!string.IsNullOrWhiteSpace(avatarId))
        {
            progress.AvatarId = avatarId;
            if (!progress.UnlockedAvatarIds.Contains(avatarId)) progress.UnlockedAvatarIds.Add(avatarId);
        }

        if (!string.IsNullOrWhiteSpace(classGroup))
        {
            progress.ClassGroup = classGroup;
        }

        await SaveAsync(progress);
    }

    public async Task ResetAsync()
    {
        cached = new StudentProgress();
        await SaveAsync(cached);

        await activityLog.LogAsync("progress_reset", evt =>
        {
            evt.Payload["reason"] = "user_triggered";
        });
    }

    private static string InferSubject(string gameId, string title)
        => gameId switch
        {
            "dictation" => "Lietuvių kalba",
            "missing-letters" => "Lietuvių kalba",
            _ when title.Contains("diagnost", StringComparison.OrdinalIgnoreCase) => "Diagnostika",
            _ => "Bendra"
        };

    private static int CalculateCurrentStreak(IEnumerable<GameResult> results)
    {
        var activeDates = results.Select(result => result.PlayedAt.ToLocalTime().Date).ToHashSet();
        var date = DateTime.Today;
        var streak = 0;

        while (activeDates.Contains(date))
        {
            streak++;
            date = date.AddDays(-1);
        }

        return streak;
    }

    private static void EnsureHintCounterForToday(StudentProgress progress)
    {
        var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        if (!string.Equals(progress.HintPurchaseDateUtc, today, StringComparison.Ordinal))
        {
            progress.HintPurchaseDateUtc = today;
            progress.HintPurchasesToday = 0;
        }
    }

    private async Task<StudentProgress> LoadLocalAsync()
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        return string.IsNullOrWhiteSpace(json)
            ? new StudentProgress()
            : JsonSerializer.Deserialize<StudentProgress>(json) ?? new StudentProgress();
    }

    private async Task PersistLocalAsync(StudentProgress progress)
    {
        var json = JsonSerializer.Serialize(progress);
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    private async Task<string?> GetCurrentUserIdAsync()
    {
        var session = await auth.GetSessionAsync();
        return string.IsNullOrWhiteSpace(session?.UserId) ? null : session.UserId;
    }

    private bool IsDbReady() => !string.IsNullOrWhiteSpace(supabaseUrl) && !string.IsNullOrWhiteSpace(supabaseKey);

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var baseUrl = supabaseUrl?.TrimEnd('/') ?? string.Empty;
        var request = new HttpRequestMessage(method, $"{baseUrl}/{path}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("apikey", supabaseKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
        return request;
    }

    private async Task<DbLoadResult> LoadFromDbAsync(string userId)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"rest/v1/user_progress?select=*&user_id=eq.{Uri.EscapeDataString(userId)}&limit=1");
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return DbLoadResult.Failed();

            var json = await response.Content.ReadAsStringAsync();
            var rows = JsonSerializer.Deserialize<List<UserProgressRow>>(json, JsonOptions) ?? [];
            var row = rows.FirstOrDefault();
            return row is null
                ? DbLoadResult.Success(null)
                : DbLoadResult.Success(ToStudentProgress(row));
        }
        catch
        {
            return DbLoadResult.Failed();
        }
    }

    private async Task UpsertToDbAsync(string userId, StudentProgress progress)
    {
        try
        {
            var payload = new[] { ToRow(userId, progress) };
            using var request = CreateRequest(HttpMethod.Post, "rest/v1/user_progress?on_conflict=user_id");
            request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
            _ = await http.SendAsync(request);
        }
        catch
        {
            // fallback remains local persistence
        }
    }

    private static UserProgressRow ToRow(string userId, StudentProgress progress)
        => new()
        {
            UserId = userId,
            UpdatedAt = DateTime.UtcNow,
            Name = progress.Name,
            AvatarId = progress.AvatarId,
            AvatarBackground = progress.AvatarBackground,
            ClassGroup = progress.ClassGroup,
            Theme = progress.Theme,
            Level = progress.Level,
            Xp = progress.Xp,
            Coins = progress.Coins,
            Streak = progress.Streak,
            BestScore = progress.BestScore,
            GamesCompleted = progress.GamesCompleted,
            UnlockedAvatarIds = progress.UnlockedAvatarIds,
            UnlockedAchievementIds = progress.UnlockedAchievementIds,
            BookmarkedGameIds = progress.BookmarkedGameIds,
            PracticeMistakeCounts = progress.PracticeMistakeCounts,
            SolvedQuestionIdsByGame = progress.SolvedQuestionIdsByGame,
            HintPurchaseDateUtc = progress.HintPurchaseDateUtc,
            HintPurchasesToday = progress.HintPurchasesToday,
            CustomAvatarDataUrl = progress.CustomAvatarDataUrl,
            DiagnosticAttempts = progress.DiagnosticAttempts,
            RecentResults = progress.RecentResults
        };

    private static StudentProgress ToStudentProgress(UserProgressRow row)
    {
        var progress = new StudentProgress
        {
            Name = row.Name ?? string.Empty,
            AvatarId = row.AvatarId ?? "a1",
            AvatarBackground = row.AvatarBackground ?? "bg-violet",
            ClassGroup = row.ClassGroup ?? "5-6",
            Theme = row.Theme ?? "light",
            Level = row.Level ?? 1,
            Xp = row.Xp ?? 0,
            Coins = row.Coins ?? 0,
            Streak = row.Streak ?? 0,
            BestScore = row.BestScore ?? 0,
            GamesCompleted = row.GamesCompleted ?? 0,
            UnlockedAvatarIds = row.UnlockedAvatarIds ?? ["a1", "a2"],
            UnlockedAchievementIds = row.UnlockedAchievementIds ?? [],
            BookmarkedGameIds = row.BookmarkedGameIds ?? [],
            PracticeMistakeCounts = row.PracticeMistakeCounts ?? [],
            SolvedQuestionIdsByGame = row.SolvedQuestionIdsByGame ?? [],
            HintPurchaseDateUtc = row.HintPurchaseDateUtc,
            HintPurchasesToday = row.HintPurchasesToday ?? 0,
            CustomAvatarDataUrl = row.CustomAvatarDataUrl,
            DiagnosticAttempts = row.DiagnosticAttempts ?? [],
            RecentResults = row.RecentResults ?? []
        };

        Normalize(progress);
        return progress;
    }

    private static void Normalize(StudentProgress progress)
    {
        if (progress.UnlockedAvatarIds is null || progress.UnlockedAvatarIds.Count == 0)
        {
            progress.UnlockedAvatarIds = ["a1", "a2"];
        }

        progress.UnlockedAchievementIds ??= [];
        progress.BookmarkedGameIds ??= [];
        progress.PracticeMistakeCounts ??= [];
        progress.SolvedQuestionIdsByGame ??= [];
        progress.DiagnosticAttempts ??= [];
        progress.RecentResults ??= [];
        EnsureHintCounterForToday(progress);
    }

    private sealed record DbLoadResult(bool RequestSucceeded, StudentProgress? Progress)
    {
        public static DbLoadResult Success(StudentProgress? progress) => new(true, progress);
        public static DbLoadResult Failed() => new(false, null);
    }
}