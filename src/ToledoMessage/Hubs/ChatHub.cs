using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ToledoMessage.Data;
using ToledoMessage.Models;
using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Hubs;

[Authorize]
public class ChatHub(MessageRelayService relayService, ApplicationDbContext db, PresenceService presence) : Hub
{
    /// <summary>
    /// Register the current connection with a specific device, adding it to device and user groups.
    /// </summary>
    public async Task RegisterDevice(decimal deviceId)
    {
        var userId = GetUserId();

        // Verify the device belongs to the requesting user
        var deviceOwned = await db.Devices.AnyAsync(d => d.Id == deviceId && d.UserId == userId && d.IsActive);
        if (!deviceOwned)
            throw new HubException("Device not found or does not belong to the current user.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"device_{deviceId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

        // Track presence
        presence.AddConnection(userId, Context.ConnectionId);

        // Broadcast online status to contacts
        var contactUserIds = await GetContactUserIds(userId);
        foreach (var contactId in contactUserIds)
            await Clients.Group($"user_{contactId}").SendAsync("UserOnline", userId);
    }

    /// <summary>
    /// Send an encrypted message to a recipient device.
    /// </summary>
    public async Task<SendMessageResult> SendMessage(SendMessageRequest request)
    {
        var userId = GetUserId();

        // Validate ciphertext size (content-type-aware)
        if (!MessageRelayService.IsValidBase64(request.Ciphertext, out var ciphertextBytes))
            throw new HubException("Invalid Base64 ciphertext.");

        var maxSize = MessageRelayService.GetMaxCiphertextSize(request.ContentType);
        if (ciphertextBytes.Length > maxSize)
            throw new HubException("Message exceeds the maximum allowed size.");

        // Validate sender is a participant in the conversation
        var isParticipant = await db.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == request.ConversationId && cp.UserId == userId);
        if (!isParticipant)
            throw new HubException("You are not a participant in this conversation.");

        // Verify the SenderDeviceId belongs to the calling user
        var senderDeviceOwned = await db.Devices
            .AnyAsync(d => d.Id == request.SenderDeviceId && d.UserId == userId && d.IsActive);
        if (!senderDeviceOwned)
            throw new HubException("Sender device not found or does not belong to the current user.");

        // Validate recipient device is active and recipient user is not deactivated
        var recipientDevice = await db.Devices
            .Include(static d => d.User)
            .FirstOrDefaultAsync(d => d.Id == request.RecipientDeviceId && d.IsActive);
        if (recipientDevice == null || !recipientDevice.User.IsActive)
            throw new HubException("Recipient device is not available.");

        // Store the message
        var message = await relayService.StoreMessage(request.SenderDeviceId, request);

        // Try to relay to online recipient
        await relayService.TryRelayToOnlineRecipient(message);

        return new SendMessageResult(message.Id, message.ServerTimestamp, message.SequenceNumber);
    }

    /// <summary>
    /// Acknowledge that a message has been delivered to the recipient device.
    /// </summary>
    public async Task AcknowledgeDelivery(decimal messageId)
    {
        var userId = GetUserId();

        var msg = await db.EncryptedMessages
            .Include(static m => m.RecipientDevice)
            .FirstOrDefaultAsync(m => m.Id == messageId);
        if (msg == null)
            throw new HubException("Message not found.");
        if (msg.RecipientDevice.UserId != userId)
            throw new HubException("Message does not belong to the current user.");

        var message = await relayService.AcknowledgeDelivery(messageId);
        if (message == null)
            throw new HubException("Message not found.");

        // Notify the sender's device that the message was delivered
        await Clients.Group($"device_{message.SenderDeviceId}")
            .SendAsync("MessageDelivered", message.Id);
    }

    /// <summary>
    /// Acknowledge that a message has been read by the recipient.
    /// </summary>
    public async Task AcknowledgeRead(decimal messageId)
    {
        var userId = GetUserId();

        var message = await db.EncryptedMessages
            .Include(static m => m.RecipientDevice)
            .FirstOrDefaultAsync(m => m.Id == messageId);
        if (message == null)
            throw new HubException("Message not found.");
        if (message.RecipientDevice.UserId != userId)
            throw new HubException("Message does not belong to the current user.");

        // Notify the sender's device that the message was read
        await Clients.Group($"device_{message.SenderDeviceId}")
            .SendAsync("MessageRead", messageId);
    }

    /// <summary>
    /// Broadcast a typing indicator to other participants in the conversation.
    /// </summary>
    public async Task TypingIndicator(decimal conversationId)
    {
        var userId = GetUserId();

        var displayName = await db.Users
            .Where(u => u.Id == userId)
            .Select(static u => u.DisplayName)
            .FirstOrDefaultAsync() ?? string.Empty;

        // Get all other participants in the conversation
        var otherParticipantUserIds = await db.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId && cp.UserId != userId)
            .Select(static cp => cp.UserId)
            .ToListAsync();

        // Send typing indicator to each participant's user group
        foreach (var participantUserId in otherParticipantUserIds)
            await Clients.Group($"user_{participantUserId}")
                .SendAsync("UserTyping", conversationId, displayName);
    }

    /// <summary>
    /// Add a reaction to a message.
    /// </summary>
    public async Task AddReaction(decimal messageId, string emoji)
    {
        var userId = GetUserId();

        // Validate the message exists and user is a participant in its conversation
        var message = await db.EncryptedMessages
            .FirstOrDefaultAsync(m => m.Id == messageId);
        if (message == null)
            throw new HubException("Message not found.");

        var isParticipant = await db.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == message.ConversationId && cp.UserId == userId);
        if (!isParticipant)
            throw new HubException("You are not a participant in this conversation.");

        // Check if reaction already exists
        var existing = await db.MessageReactions
            .AnyAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji);
        if (existing) return;

        var reaction = new MessageReaction
        {
            Id = Toledo.SharedKernel.Helpers.DecimalTools.GetNewId(),
            MessageId = messageId,
            UserId = userId,
            Emoji = emoji,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.MessageReactions.Add(reaction);
        await db.SaveChangesAsync();

        var displayName = await db.Users
            .Where(u => u.Id == userId)
            .Select(static u => u.DisplayName)
            .FirstOrDefaultAsync() ?? "";

        // Broadcast to all participants in the conversation
        var participantUserIds = await db.ConversationParticipants
            .Where(cp => cp.ConversationId == message.ConversationId)
            .Select(static cp => cp.UserId)
            .ToListAsync();

        foreach (var pid in participantUserIds)
            await Clients.Group($"user_{pid}")
                .SendAsync("ReactionAdded", messageId, userId, displayName, emoji);
    }

    /// <summary>
    /// Remove a reaction from a message.
    /// </summary>
    public async Task RemoveReaction(decimal messageId, string emoji)
    {
        var userId = GetUserId();

        var reaction = await db.MessageReactions
            .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji);
        if (reaction == null) return;

        var conversationId = await db.EncryptedMessages
            .Where(m => m.Id == messageId)
            .Select(static m => m.ConversationId)
            .FirstOrDefaultAsync();

        db.MessageReactions.Remove(reaction);
        await db.SaveChangesAsync();

        // Broadcast to all participants
        var participantUserIds = await db.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId)
            .Select(static cp => cp.UserId)
            .ToListAsync();

        foreach (var pid in participantUserIds)
            await Clients.Group($"user_{pid}")
                .SendAsync("ReactionRemoved", messageId, userId, emoji);
    }

    /// <summary>
    /// Check if a specific user is online.
    /// </summary>
    public Task<bool> IsUserOnline(decimal userId)
    {
        return Task.FromResult(presence.IsOnline(userId));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        var isNowOffline = presence.RemoveConnection(userId, Context.ConnectionId);

        if (isNowOffline)
        {
            // Update LastSeenAt
            var user = await db.Users.FindAsync(userId);
            if (user is not null)
            {
                user.LastSeenAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
            }

            // Broadcast offline status
            var contactUserIds = await GetContactUserIds(userId);
            foreach (var contactId in contactUserIds)
                await Clients.Group($"user_{contactId}")
                    .SendAsync("UserOffline", userId, user?.LastSeenAt ?? DateTimeOffset.UtcNow);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task<List<decimal>> GetContactUserIds(decimal userId)
    {
        // Get all users that share at least one conversation with this user
        var conversationIds = await db.ConversationParticipants
            .Where(cp => cp.UserId == userId)
            .Select(static cp => cp.ConversationId)
            .ToListAsync();

        return await db.ConversationParticipants
            .Where(cp => conversationIds.Contains(cp.ConversationId) && cp.UserId != userId)
            .Select(static cp => cp.UserId)
            .Distinct()
            .ToListAsync();
    }

    private decimal GetUserId()
    {
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? Context.User?.FindFirstValue("sub");
        if (string.IsNullOrEmpty(sub) || !decimal.TryParse(sub, out var userId))
            throw new HubException("Authentication required.");

        return userId;
    }
}
