namespace ToledoMessage.Shared.DTOs;

public sealed record DeviceInfoResponse(
    long DeviceId,
    string DeviceName,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt);
