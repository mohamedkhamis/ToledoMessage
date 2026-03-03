namespace ToledoMessage.Shared.DTOs;

public sealed record ReactionDto(
    decimal MessageId,
    decimal UserId,
    string DisplayName,
    string Emoji);
