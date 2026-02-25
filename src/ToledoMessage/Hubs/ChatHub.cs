using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ToledoMessage.Data;
using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly MessageRelayService _relayService;
    private readonly ApplicationDbContext _db;

    public ChatHub(MessageRelayService relayService, ApplicationDbContext db)
    {
        _relayService = relayService;
        _db = db;
    }

    /// <summary>
    /// Register the current connection with a specific device, adding it to device and user groups.
    /// </summary>
    public async Task RegisterDevice(decimal deviceId)
    {
        var userId = GetUserId();

        // Verify the device belongs to the requesting user
        var deviceOwned = await _db.Devices.AnyAsync(d => d.Id == deviceId && d.UserId == userId && d.IsActive);
        if (!deviceOwned)
            throw new HubException("Device not found or does not belong to the current user.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"device_{deviceId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
    }

    /// <summary>
    /// Send an encrypted message to a recipient device.
    /// </summary>
    public async Task<SendMessageResult> SendMessage(SendMessageRequest request)
    {
        var userId = GetUserId();

        // Validate sender is a participant in the conversation
        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == request.ConversationId && cp.UserId == userId);
        if (!isParticipant)
            throw new HubException("You are not a participant in this conversation.");

        // Retrieve a sender device belonging to this user (for the SenderDeviceId on the stored message)
        var senderDevice = await _db.Devices
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(d => d.Id)
            .FirstOrDefaultAsync();
        if (senderDevice == 0)
            throw new HubException("No active device found for the current user.");

        // Store the message
        var message = await _relayService.StoreMessage(senderDevice, request);

        // Try to relay to online recipient
        await _relayService.TryRelayToOnlineRecipient(message);

        return new SendMessageResult(message.Id, message.ServerTimestamp, message.SequenceNumber);
    }

    /// <summary>
    /// Acknowledge that a message has been delivered to the recipient device.
    /// </summary>
    public async Task AcknowledgeDelivery(decimal messageId)
    {
        var message = await _relayService.AcknowledgeDelivery(messageId);
        if (message == null)
            throw new HubException("Message not found.");

        // Notify the sender's device that the message was delivered
        await Clients.Group($"device_{message.SenderDeviceId}")
            .SendAsync("MessageDelivered", new { messageId, deliveredAt = message.DeliveredAt });
    }

    /// <summary>
    /// Acknowledge that a message has been read by the recipient.
    /// </summary>
    public async Task AcknowledgeRead(decimal messageId)
    {
        var message = await _db.EncryptedMessages.FindAsync(messageId);
        if (message == null)
            throw new HubException("Message not found.");

        // Notify the sender's device that the message was read
        await Clients.Group($"device_{message.SenderDeviceId}")
            .SendAsync("MessageRead", new { messageId, readAt = DateTimeOffset.UtcNow });
    }

    /// <summary>
    /// Broadcast a typing indicator to other participants in the conversation.
    /// </summary>
    public async Task TypingIndicator(decimal conversationId)
    {
        var userId = GetUserId();

        // Get all other participants in the conversation
        var otherParticipantUserIds = await _db.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId && cp.UserId != userId)
            .Select(cp => cp.UserId)
            .ToListAsync();

        // Send typing indicator to each participant's user group
        foreach (var participantUserId in otherParticipantUserIds)
        {
            await Clients.Group($"user_{participantUserId}")
                .SendAsync("UserTyping", new { conversationId, userId });
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Groups are automatically cleaned up by SignalR when a connection is removed.
        // No explicit cleanup needed for group membership.
        await base.OnDisconnectedAsync(exception);
    }

    private decimal GetUserId()
    {
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        return decimal.Parse(sub!);
    }
}
