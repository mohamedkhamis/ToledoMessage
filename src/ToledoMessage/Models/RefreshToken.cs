namespace ToledoMessage.Models;

public class RefreshToken
{
    public decimal Id { get; set; }
    public decimal UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public decimal? DeviceId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsRevoked { get; set; }

    public User User { get; set; } = null!;
    public Device Device { get; set; } = null!;
}
