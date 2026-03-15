using ToledoVault.Shared.Enums;

namespace ToledoVault.Shared.DTOs;

public sealed record ParticipantResponse(long UserId, string DisplayName, ParticipantRole Role, string? DisplayNameSecondary = null);
