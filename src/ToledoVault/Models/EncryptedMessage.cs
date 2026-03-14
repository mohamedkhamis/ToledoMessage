using ToledoVault.Shared.Enums;

namespace ToledoVault.Models;

public class EncryptedMessage
{
    // ReSharper disable  NullableWarningSuppressionIsUsed

    public long Id { get; set; }
    public long ConversationId { get; set; }
    public long SenderDeviceId { get; set; }
    public long RecipientDeviceId { get; set; }
    public byte[] Ciphertext { get; set; } = [];
    public MessageType MessageType { get; set; }
    public ContentType ContentType { get; set; }
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public long SequenceNumber { get; set; }
    public DateTimeOffset ServerTimestamp { get; set; }
    public bool IsDelivered { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public long? ReplyToMessageId { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public Device SenderDevice { get; set; } = null!;
    public Device RecipientDevice { get; set; } = null!;
}
