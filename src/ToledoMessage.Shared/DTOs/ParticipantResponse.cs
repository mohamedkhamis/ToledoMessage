using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Shared.DTOs;

public sealed record ParticipantResponse(decimal UserId, string DisplayName, ParticipantRole Role);
