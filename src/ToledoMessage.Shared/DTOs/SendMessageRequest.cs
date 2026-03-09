using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Shared.DTOs;

public sealed class SendMessageRequest
{
    public required long ConversationId { get; init; }
    public required long SenderDeviceId { get; init; }
    public required long RecipientDeviceId { get; init; }
    public required string Ciphertext { get; init; }
    public required MessageType MessageType { get; init; }
    public required ContentType ContentType { get; init; }
    public string? FileName { get; init; }
    public string? MimeType { get; init; }
    public long? ReplyToMessageId { get; init; }
}
