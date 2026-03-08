using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Models;

public class ConversationParticipant
{
    public long ConversationId { get; set; }
    public long UserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public ParticipantRole Role { get; set; }

    // ReSharper disable  NullableWarningSuppressionIsUsed
    public Conversation Conversation { get; set; } = null!;
    public User User { get; set; } = null!;
}
