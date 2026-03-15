using ToledoVault.Shared.Enums;

namespace ToledoVault.Models;

public class Conversation
{
    public long Id { get; set; }
    public ConversationType Type { get; set; }
    public string? GroupName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? DisappearingTimerSeconds { get; set; }

    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
    public ICollection<EncryptedMessage> Messages { get; set; } = new List<EncryptedMessage>();
}
