using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Models;

public class EncryptedMessage
{
    // ReSharper disable  NullableWarningSuppressionIsUsed

    public decimal Id { get; set; }
    public decimal ConversationId { get; set; }
    public decimal SenderDeviceId { get; set; }
    public decimal RecipientDeviceId { get; set; }
    public byte[] Ciphertext { get; set; } = [];
    public MessageType MessageType { get; set; }
    public ContentType ContentType { get; set; }
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public long SequenceNumber { get; set; }
    public DateTimeOffset ServerTimestamp { get; set; }
    public bool IsDelivered { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public decimal? ReplyToMessageId { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public Device SenderDevice { get; set; } = null!;
    public Device RecipientDevice { get; set; } = null!;
}
