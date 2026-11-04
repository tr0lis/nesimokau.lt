using System.Text.Json.Serialization;

namespace nesimokau.lt.Models;

public sealed class AuthSession
{
    [JsonPropertyName("id")]
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarId { get; set; } = "a1";
    public string ClassGroup { get; set; } = "5-6";
    public bool TotpEnabled { get; set; }
}

public sealed class AuthResult
{
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public AuthSession? Session { get; init; }

    /// <summary>When true the password was accepted and a 2FA code must be submitted with <see cref="MfaTicket"/>.</summary>
    public bool MfaRequired { get; init; }
    public string MfaTicket { get; init; } = string.Empty;

    public static AuthResult Ok(AuthSession session) => new() { Success = true, Session = session };
    public static AuthResult Fail(string message) => new() { Success = false, ErrorMessage = message };
    public static AuthResult NeedsMfa(string ticket) => new() { MfaRequired = true, MfaTicket = ticket };
}

public sealed class TwoFactorSetup
{
    [JsonPropertyName("secret")] public string Secret { get; set; } = string.Empty;
    [JsonPropertyName("otpauthUri")] public string OtpAuthUri { get; set; } = string.Empty;
    [JsonPropertyName("qr")] public QrMatrix Qr { get; set; } = new();
}

public sealed class QrMatrix
{
    [JsonPropertyName("size")] public int Size { get; set; }
    [JsonPropertyName("rows")] public List<string> Rows { get; set; } = [];
}
