namespace ToledoMessage.Models;

public class EncryptedKeyBackup
{
    // ReSharper disable  NullableWarningSuppressionIsUsed
    public decimal Id { get; set; }
    public decimal UserId { get; set; }
    public byte[] EncryptedBlob { get; set; } = [];
    public byte[] Salt { get; set; } = [];
    public byte[] Nonce { get; set; } = [];
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
