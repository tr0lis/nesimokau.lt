namespace nesimokau.lt.Models;

public enum GameStatus
{
    Ready,
    ComingSoon
}

public sealed record GameDefinition(
    string Id,
    string Title,
    string Description,
    string Icon,
    string Accent,
    string Subject,
    string Grades,
    string Difficulty,
    GameStatus Status,
    int EstimatedMinutes);

public sealed record DictationQuestion(
    int Id,
    string Prompt,
    string Answer,
    string Explanation,
    string Difficulty,
    string Category);

public sealed record MissingLettersQuestion(
    int Id,
    string Pattern,
    string Answer,
    string Explanation,
    string Difficulty,
    string MissingLetters);

public sealed record LevelDefinition(int Number, string Title, string Badge, int RequiredXp, string Color);

public static class LevelCatalog
{
    public static IReadOnlyList<LevelDefinition> All { get; } =
    [
        new(1, "Naujokas", "🌱", 0, "mint"),
        new(2, "Smalsuolis", "🔎", 1000, "blue"),
        new(3, "Nuotykių ieškotojas", "🧭", 2000, "violet"),
        new(4, "Žinių rinkėjas", "📚", 3500, "gold"),
        new(5, "Gudruolis", "🦊", 5000, "orange"),
        new(6, "Išminčius", "🦉", 7000, "plum"),
        new(7, "Proto meistras", "🧠", 9000, "pink"),
        new(8, "Mokslininkas", "🔬", 11500, "cyan"),
        new(9, "Supermokslininkas", "🚀", 14000, "rainbow"),
        new(10, "Genijus", "💡", 17000, "gold")
    ];
}

public sealed record AvatarDefinition(string Id, string Symbol, string Accent, int Cost);

public static class AvatarCatalog
{
    public static IReadOnlyList<AvatarDefinition> All { get; } =
    [
        new("a1", "🦊", "orange", 0),
        new("a2", "🐼", "violet", 0),
        new("a3", "🐯", "yellow", 80),
        new("a4", "🐸", "mint", 120),
        new("a5", "🐨", "blue", 160),
        new("a6", "🦉", "plum", 200),
        new("a7", "🦁", "gold", 240),
        new("a8", "🐧", "cyan", 280),
        new("a9", "🐙", "pink", 320),
        new("a10", "🐺", "slate", 360)
    ];
}

public sealed class StudentProgress
{
    public string Name { get; set; } = string.Empty;
    public string AvatarId { get; set; } = "a1";
    public string AvatarBackground { get; set; } = "bg-violet";
    public string ClassGroup { get; set; } = "5-6";
    public string Theme { get; set; } = "light";
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public int Coins { get; set; }
    public int Streak { get; set; }
    public int BestScore { get; set; }
    public int GamesCompleted { get; set; }
    public List<string> UnlockedAvatarIds { get; set; } = ["a1", "a2"];
    public List<string> UnlockedAchievementIds { get; set; } = [];
    public List<string> BookmarkedGameIds { get; set; } = [];
    public Dictionary<string, string> PracticeDifficulties { get; set; } = [];
    public Dictionary<string, int> PracticeMistakeCounts { get; set; } = [];
    public string? CustomAvatarDataUrl { get; set; }
    public List<DiagnosticAttempt> DiagnosticAttempts { get; set; } = [];
    public List<GameResult> RecentResults { get; set; } = [];
}

public sealed record GameResult(string GameId, string GameTitle, int Score, int Accuracy, int Energy, int Coins, DateTime PlayedAt)
{
    public string ClassGroup { get; init; } = string.Empty;
    public int CorrectAnswers { get; init; } = -1;
    public int IncorrectAnswers { get; init; } = -1;
}
public sealed record DiagnosticAttempt(string Id, string Subject, DateTime CompletedAt, int Accuracy, int DurationSeconds, List<DiagnosticAnswer> Answers);
public sealed record DiagnosticAnswer(string Topic, string Prompt, string CorrectAnswer, string StudentAnswer, bool IsCorrect);
