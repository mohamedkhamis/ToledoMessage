namespace ToledoVault.Shared.DTOs;

public sealed record AccountDeletionResponse(
    DateTimeOffset DeletionScheduledAt,
    DateTimeOffset GracePeriodEndsAt);
