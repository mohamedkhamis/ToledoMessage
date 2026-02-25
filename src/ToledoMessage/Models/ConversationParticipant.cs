using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Models;

public class ConversationParticipant
{
    public decimal ConversationId { get; set; }
    public decimal UserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public ParticipantRole Role { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public User User { get; set; } = null!;
}
