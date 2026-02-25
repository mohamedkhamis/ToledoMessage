namespace ToledoMessage.Shared.DTOs;

public sealed record DeviceInfoResponse(
    decimal DeviceId,
    string DeviceName,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt);
