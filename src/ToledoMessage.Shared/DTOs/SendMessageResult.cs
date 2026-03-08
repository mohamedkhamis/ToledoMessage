namespace ToledoMessage.Shared.DTOs;

public sealed record SendMessageResult(
    long MessageId,
    DateTimeOffset ServerTimestamp,
    long SequenceNumber);
