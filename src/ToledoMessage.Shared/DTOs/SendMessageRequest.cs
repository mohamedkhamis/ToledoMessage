using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Shared.DTOs;

public sealed record SendMessageRequest(
    decimal ConversationId,
    decimal SenderDeviceId,
    decimal RecipientDeviceId,
    string Ciphertext,
    MessageType MessageType,
    ContentType ContentType,
    string? FileName = null,
    string? MimeType = null,
    decimal? ReplyToMessageId = null);
