namespace ToledoMessage.Models;

public class ConversationReadPointer
{
    public long UserId { get; set; }
    public long ConversationId { get; set; }
    public long LastReadSequenceNumber { get; set; }
    public int UnreadCount { get; set; }
    public DateTimeOffset? LastReadAt { get; set; }

    // ReSharper disable  NullableWarningSuppressionIsUsed
    public User User { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
}
