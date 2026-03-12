namespace ToledoMessage.Models;

public class RefreshToken
{
    // ReSharper disable  NullableWarningSuppressionIsUsed

    public long Id { get; set; }
    public long UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public long? DeviceId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsRevoked { get; set; }
    public bool IsPersistent { get; set; }

    public User User { get; set; } = null!;
    public Device Device { get; set; } = null!;
}
