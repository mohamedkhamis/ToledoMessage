namespace ToledoVault.Models;

public class MessageReaction
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public long UserId { get; set; }
    public string Emoji { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }

    // ReSharper disable  NullableWarningSuppressionIsUsed
    public EncryptedMessage Message { get; set; } = null!;
    public User User { get; set; } = null!;
}
