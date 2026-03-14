namespace ToledoVault.Shared.DTOs;

public sealed record CreateGroupConversationRequest(string GroupName, List<long> ParticipantUserIds);
