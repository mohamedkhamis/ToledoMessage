using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Shared.DTOs;

public sealed record ParticipantResponse(long UserId, string DisplayName, ParticipantRole Role, string? DisplayNameSecondary = null);
