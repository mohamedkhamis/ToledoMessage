namespace ToledoMessage.Shared.DTOs;

public sealed record AccountDeletionResponse(
    DateTimeOffset DeletionScheduledAt,
    DateTimeOffset GracePeriodEndsAt);
