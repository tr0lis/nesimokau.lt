using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.JSInterop;
using Microsoft.Extensions.Configuration;
using nesimokau.lt.Models;

namespace nesimokau.lt.Services;

public sealed class ProgressService(IJSRuntime js, ActivityLogService activityLog, AuthService auth, HttpClient http, IConfiguration config)
{
    public sealed record DbSyncStatus(string State, string Message, DateTime UpdatedAtUtc);

    public enum HintPurchaseResult
    {
        Success,
        NotEnoughCoins,
        DailyLimitReached
    }

    private const string StorageKey = "nesimokau-progress";
    private const string PendingQueueKey = "nesimokau-progress-sync-queue";
    private const string LastSuccessKey = "nesimokau-progress-last-sync-success-utc";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string? supabaseUrl = config["Supabase:Url"];
    private readonly string? supabaseKey = config["Supabase:PublishableKey"];
    private StudentProgress? cached;
    private DbSyncStatus syncStatus = new("unknown", "Tikrinama...", DateTime.UtcNow);
    private int pendingSyncCount;
    private DateTime? lastSuccessfulSyncUtc;
    private PeriodicTimer? autoSyncTimer;
    private CancellationTokenSource? autoSyncCts;
    private bool autoSyncStarted;
    private bool syncInProgress;
    public event Action? Changed;
    public event Action? SyncStatusChanged;

    public DbSyncStatus GetDbSyncStatus() => syncStatus;
    public int GetPendingSyncCount() => pendingSyncCount;
    public DateTime? GetLastSuccessfulSyncUtc() => lastSuccessfulSyncUtc;

    public async Task<bool> RetrySyncNowAsync()
    {
        var progress = await GetAsync();
        var userId = await GetCurrentUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId) || !IsDbReady())
        {
            SetSyncStatus("local", "Nėra aktyvios DB sesijos");
            return false;
        }

        var persisted = await UpsertToDbAsync(userId, progress);
        if (persisted)
        {
            await MarkSuccessfulSyncAsync();
            await FlushPendingQueueAsync(userId);
            SetSyncStatus("synced", "Priverstinė sinchronizacija pavyko");
        }
        else
        {
            await EnqueuePendingSyncAsync(userId, progress);
            SetSyncStatus("failed", "Priverstinė sinchronizacija nepavyko");
        }

        return persisted;
    }

    public async Task<StudentProgress> GetAsync()
    {
        EnsureAutoSyncStarted();
        if (cached is not null) return cached;

        await LoadSyncMetaAsync();
        var local = await LoadLocalAsync();
        var userId = await GetCurrentUserIdAsync();

        if (!string.IsNullOrWhiteSpace(userId) && IsDbReady())
        {
            var remote = await LoadFromDbAsync(userId);
            if (remote.Progress is not null)
            {
                cached = remote.Progress;
                await FlushPendingQueueAsync(userId);
                SetSyncStatus("synced", "Sinchronizuota su DB");
            }
            else if (remote.RequestSucceeded)
            {
                cached = local;
                var seeded = await UpsertToDbAsync(userId, cached);
                if (seeded) await MarkSuccessfulSyncAsync();
                SetSyncStatus(seeded ? "synced" : "failed", seeded ? "Sukurta DB būsena" : "DB įrašymas nepavyko");
            }
            else
            {
                cached = local;
                SetSyncStatus("failed", "DB nepasiekiama, naudojama lokali būsena");
            }
        }
        else
        {
            cached = local;
            SetSyncStatus("local", "Nėra aktyvios DB sesijos");
        }

        Normalize(cached);
        await PersistLocalAsync(cached);
        return cached;
    }

    private void EnsureAutoSyncStarted()
    {
        if (autoSyncStarted) return;
        autoSyncStarted = true;
        autoSyncCts = new CancellationTokenSource();
        autoSyncTimer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        _ = RunAutoSyncLoopAsync(autoSyncCts.Token);
    }

    private async Task RunAutoSyncLoopAsync(CancellationToken token)
    {
        try
        {
            while (autoSyncTimer is not null && await autoSyncTimer.WaitForNextTickAsync(token))
            {
                if (syncInProgress) continue;
                syncInProgress = true;
                try
                {
                    _ = await RetrySyncNowAsync();
                }
                finally
                {
                    syncInProgress = false;
                }
            }
        }
        catch
        {
            // keep silent in WASM background sync
        }
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
            var remote = await LoadFromDbAsync(userId);
            var merged = remote.Progress is null ? progress : MergeProgress(progress, remote.Progress);
            var persisted = await UpsertToDbAsync(userId, merged);
            cached = merged;
            await PersistLocalAsync(merged);
            if (persisted)
            {
                await MarkSuccessfulSyncAsync();
                await FlushPendingQueueAsync(userId);
            }
            else
            {
                await EnqueuePendingSyncAsync(userId, merged);
            }

            SetSyncStatus(persisted ? "synced" : "failed", persisted ? "Sinchronizuota su DB" : "DB įrašymas nepavyko");
        }
        else
        {
            SetSyncStatus("local", "Nėra aktyvios DB sesijos");
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

    private async Task<List<PendingSyncItem>> LoadPendingQueueAsync()
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", PendingQueueKey);
        var queue = string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<PendingSyncItem>>(json, JsonOptions) ?? [];
        pendingSyncCount = queue.Count;
        return queue;
    }

    private async Task SavePendingQueueAsync(List<PendingSyncItem> queue)
    {
        var json = JsonSerializer.Serialize(queue, JsonOptions);
        await js.InvokeVoidAsync("localStorage.setItem", PendingQueueKey, json);
        pendingSyncCount = queue.Count;
        SyncStatusChanged?.Invoke();
    }

    private async Task EnqueuePendingSyncAsync(string userId, StudentProgress progress)
    {
        var queue = await LoadPendingQueueAsync();
        queue.RemoveAll(item => string.Equals(item.UserId, userId, StringComparison.Ordinal));
        queue.Add(new PendingSyncItem
        {
            UserId = userId,
            Progress = progress,
            EnqueuedAtUtc = DateTime.UtcNow
        });
        await SavePendingQueueAsync(queue);
    }

    private async Task FlushPendingQueueAsync(string currentUserId)
    {
        var queue = await LoadPendingQueueAsync();
        if (queue.Count == 0) return;

        var pending = queue
            .Where(item => string.Equals(item.UserId, currentUserId, StringComparison.Ordinal))
            .OrderBy(item => item.EnqueuedAtUtc)
            .ToList();

        if (pending.Count == 0) return;

        var stillPending = new List<PendingSyncItem>();
        foreach (var item in pending)
        {
            var ok = await UpsertToDbAsync(item.UserId, item.Progress);
            if (!ok) stillPending.Add(item);
        }

        queue.RemoveAll(item => string.Equals(item.UserId, currentUserId, StringComparison.Ordinal));
        queue.AddRange(stillPending);
        await SavePendingQueueAsync(queue);
        if (stillPending.Count == 0)
        {
            await MarkSuccessfulSyncAsync();
        }
    }

    private async Task LoadSyncMetaAsync()
    {
        if (lastSuccessfulSyncUtc is null)
        {
            var value = await js.InvokeAsync<string?>("localStorage.getItem", LastSuccessKey);
            if (DateTime.TryParse(value, out var parsed))
            {
                lastSuccessfulSyncUtc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }
        }

        _ = await LoadPendingQueueAsync();
    }

    private async Task MarkSuccessfulSyncAsync()
    {
        lastSuccessfulSyncUtc = DateTime.UtcNow;
        await js.InvokeVoidAsync("localStorage.setItem", LastSuccessKey, lastSuccessfulSyncUtc.Value.ToString("O"));
        SyncStatusChanged?.Invoke();
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

    private async Task<bool> UpsertToDbAsync(string userId, StudentProgress progress)
    {
        try
        {
            var payload = new[] { ToRow(userId, progress) };
            using var request = CreateRequest(HttpMethod.Post, "rest/v1/user_progress?on_conflict=user_id");
            request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
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

    private static StudentProgress MergeProgress(StudentProgress local, StudentProgress remote)
    {
        var mergedSolved = new Dictionary<string, List<int>>(remote.SolvedQuestionIdsByGame);
        foreach (var (gameId, ids) in local.SolvedQuestionIdsByGame)
        {
            if (!mergedSolved.TryGetValue(gameId, out var existing))
            {
                mergedSolved[gameId] = ids.Distinct().ToList();
                continue;
            }

            mergedSolved[gameId] = existing.Concat(ids).Distinct().ToList();
        }

        var mergedRecent = remote.RecentResults
            .Concat(local.RecentResults)
            .OrderByDescending(x => x.PlayedAt)
            .DistinctBy(x => $"{x.GameId}|{x.GameTitle}|{x.Score}|{x.PlayedAt:O}")
            .Take(120)
            .ToList();

        var merged = new StudentProgress
        {
            Name = !string.IsNullOrWhiteSpace(local.Name) ? local.Name : remote.Name,
            AvatarId = !string.IsNullOrWhiteSpace(local.AvatarId) ? local.AvatarId : remote.AvatarId,
            AvatarBackground = !string.IsNullOrWhiteSpace(local.AvatarBackground) ? local.AvatarBackground : remote.AvatarBackground,
            ClassGroup = !string.IsNullOrWhiteSpace(local.ClassGroup) ? local.ClassGroup : remote.ClassGroup,
            Theme = !string.IsNullOrWhiteSpace(local.Theme) ? local.Theme : remote.Theme,
            Level = Math.Max(local.Level, remote.Level),
            Xp = Math.Max(local.Xp, remote.Xp),
            Coins = Math.Max(local.Coins, remote.Coins),
            Streak = Math.Max(local.Streak, remote.Streak),
            BestScore = Math.Max(local.BestScore, remote.BestScore),
            GamesCompleted = Math.Max(local.GamesCompleted, remote.GamesCompleted),
            UnlockedAvatarIds = remote.UnlockedAvatarIds.Concat(local.UnlockedAvatarIds).Distinct().ToList(),
            UnlockedAchievementIds = remote.UnlockedAchievementIds.Concat(local.UnlockedAchievementIds).Distinct().ToList(),
            BookmarkedGameIds = remote.BookmarkedGameIds.Concat(local.BookmarkedGameIds).Distinct().ToList(),
            PracticeMistakeCounts = remote.PracticeMistakeCounts
                .Concat(local.PracticeMistakeCounts)
                .GroupBy(x => x.Key)
                .ToDictionary(g => g.Key, g => g.Max(x => x.Value)),
            SolvedQuestionIdsByGame = mergedSolved,
            HintPurchaseDateUtc = string.CompareOrdinal(local.HintPurchaseDateUtc, remote.HintPurchaseDateUtc) >= 0 ? local.HintPurchaseDateUtc : remote.HintPurchaseDateUtc,
            HintPurchasesToday = Math.Max(local.HintPurchasesToday, remote.HintPurchasesToday),
            CustomAvatarDataUrl = !string.IsNullOrWhiteSpace(local.CustomAvatarDataUrl) ? local.CustomAvatarDataUrl : remote.CustomAvatarDataUrl,
            DiagnosticAttempts = remote.DiagnosticAttempts.Concat(local.DiagnosticAttempts).DistinctBy(x => x.Id).Take(30).ToList(),
            RecentResults = mergedRecent
        };

        Normalize(merged);
        return merged;
    }

    private void SetSyncStatus(string state, string message)
    {
        var next = new DbSyncStatus(state, message, DateTime.UtcNow);
        if (next == syncStatus) return;
        syncStatus = next;
        SyncStatusChanged?.Invoke();
    }

    private sealed record DbLoadResult(bool RequestSucceeded, StudentProgress? Progress)
    {
        public static DbLoadResult Success(StudentProgress? progress) => new(true, progress);
        public static DbLoadResult Failed() => new(false, null);
    }

    private sealed class PendingSyncItem
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime EnqueuedAtUtc { get; set; }
        public StudentProgress Progress { get; set; } = new();
    }
}