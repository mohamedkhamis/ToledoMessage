namespace ToledoMessage.Models;

public class Device
{
    // ReSharper disable  NullableWarningSuppressionIsUsed

    public long Id { get; set; }
    public long UserId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public byte[] IdentityPublicKeyClassical { get; set; } = [];
    public byte[] IdentityPublicKeyPostQuantum { get; set; } = [];
    public byte[] SignedPreKeyPublic { get; set; } = [];
    public byte[] SignedPreKeySignature { get; set; } = [];
    public int SignedPreKeyId { get; set; }
    public byte[] KyberPreKeyPublic { get; set; } = [];
    public byte[] KyberPreKeySignature { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;
    public ICollection<OneTimePreKey> OneTimePreKeys { get; set; } = new List<OneTimePreKey>();
}
