using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Shared.DTOs;

public sealed record MessageEnvelope(
    long MessageId,
    long ConversationId,
    long SenderDeviceId,
    string Ciphertext,
    MessageType MessageType,
    ContentType ContentType,
    long SequenceNumber,
    DateTimeOffset ServerTimestamp,
    string? FileName = null,
    string? MimeType = null,
    long? ReplyToMessageId = null);
