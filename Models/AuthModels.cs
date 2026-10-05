using System.Text.Json.Serialization;

namespace nesimokau.lt.Models;

public sealed class AuthSession
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarId { get; set; } = "a1";
    public string ClassGroup { get; set; } = "5-6";
    public DateTime LastLoginUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddDays(7);
}

public sealed class AuthProfile
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarId { get; set; } = "a1";
    public string ClassGroup { get; set; } = "5-6";
}

public sealed class AuthResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public AuthUser? User { get; set; }
}

public sealed class AuthUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

public sealed class AuthResult
{
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public AuthSession? Session { get; init; }

    public static AuthResult Ok(AuthSession session) => new() { Success = true, Session = session };
    public static AuthResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
