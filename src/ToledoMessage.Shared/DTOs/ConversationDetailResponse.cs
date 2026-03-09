using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Shared.DTOs;

public sealed record ConversationDetailResponse(
    long ConversationId,
    ConversationType Type,
    string? GroupName,
    int ParticipantCount,
    DateTimeOffset CreatedAt,
    int? DisappearingTimerSeconds);
