namespace ToledoMessage.Models;

public class MessageReaction
{
    public decimal Id { get; set; }
    public decimal MessageId { get; set; }
    public decimal UserId { get; set; }
    public string Emoji { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }

    // ReSharper disable  NullableWarningSuppressionIsUsed
    public EncryptedMessage Message { get; set; } = null!;
    public User User { get; set; } = null!;
}
