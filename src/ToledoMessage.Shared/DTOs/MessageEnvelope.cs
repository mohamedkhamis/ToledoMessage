using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Shared.DTOs;

public sealed record MessageEnvelope(
    decimal MessageId,
    decimal ConversationId,
    decimal SenderDeviceId,
    string Ciphertext,
    MessageType MessageType,
    ContentType ContentType,
    long SequenceNumber,
    DateTimeOffset ServerTimestamp,
    string? FileName = null,
    string? MimeType = null);
