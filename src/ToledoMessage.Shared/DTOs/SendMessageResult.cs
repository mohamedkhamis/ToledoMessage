namespace ToledoMessage.Shared.DTOs;

public sealed record SendMessageResult(
    decimal MessageId,
    DateTimeOffset ServerTimestamp,
    long SequenceNumber);
