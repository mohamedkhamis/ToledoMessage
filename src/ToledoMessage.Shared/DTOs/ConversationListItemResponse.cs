using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Shared.DTOs;

public sealed record ConversationListItemResponse(
    decimal ConversationId,
    ConversationType Type,
    string DisplayName,
    DateTimeOffset? LastMessageTime,
    int UnreadCount,
    string? LastMessage = null);
