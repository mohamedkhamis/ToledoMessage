namespace ToledoMessage.Shared.DTOs;

public sealed record ReactionDto(
    long MessageId,
    long UserId,
    string DisplayName,
    string Emoji);
