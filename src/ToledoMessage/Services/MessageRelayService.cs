using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Toledo.SharedKernel.Helpers;
using ToledoMessage.Data;
using ToledoMessage.Hubs;
using ToledoMessage.Models;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Services;

public class MessageRelayService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<ChatHub> _hubContext;

    public MessageRelayService(ApplicationDbContext db, IHubContext<ChatHub> hubContext)
    {
        _db = db;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Store an encrypted message in the database with an auto-incremented sequence number per conversation.
    /// </summary>
    public async Task<EncryptedMessage> StoreMessage(
        decimal senderDeviceId,
        SendMessageRequest request)
    {
        var now = DateTimeOffset.UtcNow;

        // Auto-increment sequence number per conversation
        var maxSequence = await _db.EncryptedMessages
            .Where(m => m.ConversationId == request.ConversationId)
            .Select(m => (long?)m.SequenceNumber)
            .MaxAsync() ?? 0;

        var message = new EncryptedMessage
        {
            Id = DecimalTools.GetNewId(),
            ConversationId = request.ConversationId,
            SenderDeviceId = senderDeviceId,
            RecipientDeviceId = request.RecipientDeviceId,
            Ciphertext = Convert.FromBase64String(request.Ciphertext),
            MessageType = request.MessageType,
            ContentType = request.ContentType,
            SequenceNumber = maxSequence + 1,
            ServerTimestamp = now,
            IsDelivered = false
        };

        _db.EncryptedMessages.Add(message);
        await _db.SaveChangesAsync();

        return message;
    }

    /// <summary>
    /// Try to relay a message to the recipient if they are connected via SignalR.
    /// Sends to the device-specific group.
    /// </summary>
    public async Task TryRelayToOnlineRecipient(EncryptedMessage message)
    {
        var envelope = new MessageEnvelope(
            message.Id,
            message.ConversationId,
            message.SenderDeviceId,
            Convert.ToBase64String(message.Ciphertext),
            message.MessageType,
            message.ContentType,
            message.SequenceNumber,
            message.ServerTimestamp);

        await _hubContext.Clients
            .Group($"device_{message.RecipientDeviceId}")
            .SendAsync("ReceiveMessage", envelope);
    }

    /// <summary>
    /// Get all pending (undelivered) messages for a specific device.
    /// </summary>
    public async Task<List<EncryptedMessage>> GetPendingMessages(decimal deviceId)
    {
        return await _db.EncryptedMessages
            .Where(m => m.RecipientDeviceId == deviceId && !m.IsDelivered)
            .OrderBy(m => m.SequenceNumber)
            .ToListAsync();
    }

    /// <summary>
    /// Mark a message as delivered.
    /// </summary>
    public async Task<EncryptedMessage?> AcknowledgeDelivery(decimal messageId)
    {
        var message = await _db.EncryptedMessages.FindAsync(messageId);
        if (message == null)
            return null;

        message.IsDelivered = true;
        message.DeliveredAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return message;
    }
}
