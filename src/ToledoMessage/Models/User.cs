namespace ToledoMessage.Models;

public class User
{
    // ReSharper disable  NullableWarningSuppressionIsUsed

    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? DisplayNameSecondary { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeletionRequestedAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }

    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<ConversationParticipant> ConversationParticipants { get; set; } = new List<ConversationParticipant>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
