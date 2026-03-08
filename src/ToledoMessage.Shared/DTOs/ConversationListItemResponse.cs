using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Shared.DTOs;

public sealed record ConversationListItemResponse(
    long ConversationId,
    ConversationType Type,
    string DisplayName,
    DateTimeOffset? LastMessageTime,
    int UnreadCount,
    string? LastMessage = null,
    string? DisplayNameSecondary = null);
