namespace ToledoMessage.Shared.DTOs;

public sealed record CreateGroupConversationRequest(string GroupName, List<decimal> ParticipantUserIds);
