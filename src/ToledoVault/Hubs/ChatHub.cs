using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ToledoVault.Data;
using ToledoVault.Models;
using ToledoVault.Services;
using ToledoVault.Shared.Constants;
using ToledoVault.Shared.DTOs;
using ToledoVault.Shared.Enums;

namespace ToledoVault.Hubs;

[Authorize]
[SuppressMessage("ReSharper", "RemoveRedundantBraces")]
public class ChatHub(MessageRelayService relayService, ApplicationDbContext db, PresenceService presence, RateLimitService rateLimitService) : Hub
{
    // FR-013: Cache display names and participant lists to avoid DB queries per typing indicator
    private static readonly ConcurrentDictionary<string, string> ConnectionDisplayNameMap = new();
    private static readonly ConcurrentDictionary<long, (List<long> UserIds, DateTimeOffset CachedAt)> ParticipantCache = new();

    /// <summary>
    /// Register the current connection with a specific device, adding it to device and user groups.
    /// </summary>
    public async Task RegisterDevice(long deviceId)
    {
        var userId = GetUserId();

        // Verify the device belongs to the requesting user
        var deviceOwned = await db.Devices.AnyAsync(d => d.Id == deviceId && d.UserId == userId && d.IsActive);
        if (!deviceOwned)
            throw new HubException("Device not found or does not belong to the current user.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"device_{deviceId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

        // Track connection → device mapping for read acknowledgment
        ConnectionDeviceMap[Context.ConnectionId] = deviceId;

        // Track presence
        presence.AddConnection(userId, Context.ConnectionId);

        // FR-013: Cache display name for typing indicator
        var displayName = await db.Users.Where(u => u.Id == userId).Select(static u => u.DisplayName).FirstOrDefaultAsync();
        if (displayName is not null)
        {
            ConnectionDisplayNameMap[Context.ConnectionId] = displayName;
        }

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

        // FR-001: Rate limit SendMessage to 60 per minute
        if (rateLimitService.IsRateLimited($"signalr:send:{userId}", 60, TimeSpan.FromMinutes(1)))
            throw new HubException("RATE_LIMIT_EXCEEDED");

        // Validate ciphertext size (content-type-aware)
        if (!MessageRelayService.IsValidBase64(request.Ciphertext, out var ciphertextBytes))
            throw new HubException("Invalid Base64 ciphertext.");

        // Defensive fallback: if ContentType deserialized as Text but ciphertext exceeds text limit,
        // treat as media (enum serialization can fail across SignalR JSON protocol boundaries)
        var effectiveContentType = request.ContentType;
        if (effectiveContentType == ContentType.Text
            && ciphertextBytes.Length > ProtocolConstants.MaxCiphertextSizeBytes)
        {
            effectiveContentType = ContentType.File;
        }

        var maxSize = MessageRelayService.GetMaxCiphertextSize(effectiveContentType);
        if (ciphertextBytes.Length > maxSize)
            throw new HubException($"Message exceeds the maximum allowed size ({maxSize / 1_048_576} MB).");

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
        if (recipientDevice is null || !recipientDevice.User.IsActive)
            throw new HubException("Recipient device is not available.");

        // Store the message
        var message = await relayService.StoreMessage(request.SenderDeviceId, request);

        // Increment unread counts for all other participants
        await relayService.IncrementUnreadCountsForNewMessage(request.ConversationId, userId);

        // Try to relay to online recipient
        await relayService.TryRelayToOnlineRecipient(message);

        return new SendMessageResult(message.Id, message.ServerTimestamp, message.SequenceNumber);
    }

    /// <summary>
    /// Acknowledge that a message has been delivered to the recipient device.
    /// </summary>
    public async Task AcknowledgeDelivery(long messageId)
    {
        var userId = GetUserId();

        var msg = await db.EncryptedMessages
                      .Include(static m => m.RecipientDevice)
                      .FirstOrDefaultAsync(m => m.Id == messageId)
                  ?? throw new HubException("Message not found.");
        if (msg.RecipientDevice.UserId != userId)
            throw new HubException("Message does not belong to the current user.");

        var message = await relayService.AcknowledgeDelivery(messageId)
                      ?? throw new HubException("Message not found.");

        // Notify the sender's device that the message was delivered
        await Clients.Group($"device_{message.SenderDeviceId}")
            .SendAsync("MessageDelivered", message.Id);
    }

    /// <summary>
    /// Advance the read pointer for the current user in a conversation.
    /// Notifies senders that their messages were read.
    /// </summary>
    public async Task AdvanceReadPointer(long conversationId, long upToSequenceNumber)
    {
        var userId = GetUserId();

        // BUG-CR-006 FIX: Verify user is a participant
        var isParticipant = await db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);
        if (!isParticipant)
            throw new HubException("User is not a participant in this conversation");

        var readMessages = await relayService.AdvanceReadPointer(userId, conversationId, upToSequenceNumber);

        // Notify each sender's device that their messages were read
        foreach (var (messageId, senderDeviceId) in readMessages)
        {
            await Clients.Group($"device_{senderDeviceId}")
                .SendAsync("MessageRead", messageId);
        }
    }

    // Track connection → device mapping for delivery acknowledgment
    private static readonly ConcurrentDictionary<string, long> ConnectionDeviceMap = new();

    /// <summary>
    /// Broadcast a typing indicator to other participants in the conversation.
    /// </summary>
    public async Task TypingIndicator(long conversationId)
    {
        var userId = GetUserId();

        // FR-002: Rate limit TypingIndicator to 10 per minute (silent drop if exceeded)
        if (rateLimitService.IsRateLimited($"signalr:typing:{userId}", 10, TimeSpan.FromMinutes(1)))
            return; // Silently drop

        // FR-013: Use cached display name if available
        var displayName = ConnectionDisplayNameMap.TryGetValue(Context.ConnectionId, out var cachedName)
            ? cachedName
            : await db.Users.Where(u => u.Id == userId).Select(static u => u.DisplayName).FirstOrDefaultAsync() ?? string.Empty;

        // FR-013: Use cached participant list (60s TTL)
        List<long> otherParticipantUserIds;
        if (ParticipantCache.TryGetValue(conversationId, out var cached) && (DateTimeOffset.UtcNow - cached.CachedAt).TotalSeconds < 60)
        {
            otherParticipantUserIds = cached.UserIds.Where(id => id != userId).ToList();
        }
        else
        {
            otherParticipantUserIds = await db.ConversationParticipants
                .Where(cp => cp.ConversationId == conversationId && cp.UserId != userId)
                .Select(static cp => cp.UserId)
                .ToListAsync();

            // Update cache
            ParticipantCache[conversationId] = (otherParticipantUserIds, DateTimeOffset.UtcNow);
        }

        // Send typing indicator to each participant's user group
        foreach (var participantUserId in otherParticipantUserIds)
            await Clients.Group($"user_{participantUserId}")
                .SendAsync("UserTyping", conversationId, displayName);
    }

    /// <summary>
    /// Add a reaction to a message.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public async Task AddReaction(long messageId, string emoji)
    {
        if (string.IsNullOrWhiteSpace(emoji) || emoji.Length > 32)
            throw new HubException("Invalid emoji.");

        var userId = GetUserId();

        // Validate the message exists and user is a participant in its conversation
        var message = await db.EncryptedMessages
                          .FirstOrDefaultAsync(m => m.Id == messageId)
                      ?? throw new HubException("Message not found.");

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
            Id = Toledo.SharedKernel.Helpers.IdGenerator.GetNewId(),
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
    // ReSharper disable once UnusedMember.Global
    public async Task RemoveReaction(long messageId, string emoji)
    {
        if (string.IsNullOrWhiteSpace(emoji) || emoji.Length > 32)
            throw new HubException("Invalid emoji.");

        var userId = GetUserId();

        var reaction = await db.MessageReactions
            .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji);
        if (reaction is null) return;

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
    /// Delete a message for everyone in the conversation.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public async Task DeleteForEveryone(long messageId)
    {
        var userId = GetUserId();

        var message = await db.EncryptedMessages
                          .Include(static m => m.SenderDevice)
                          .FirstOrDefaultAsync(m => m.Id == messageId)
                      ?? throw new HubException("Message not found.");

        // Only the sender can delete for everyone
        if (message.SenderDevice.UserId != userId)
            throw new HubException("You can only delete your own messages for everyone.");

        // FR-005: Verify conversation participation before allowing delete
        var conversationId = message.ConversationId;
        var isParticipant = await db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);
        if (!isParticipant)
            throw new HubException("You are not a participant in this conversation.");

        // Remove reactions for this message
        var reactions = await db.MessageReactions.Where(r => r.MessageId == messageId).ToListAsync();
        db.MessageReactions.RemoveRange(reactions);

        db.EncryptedMessages.Remove(message);
        await db.SaveChangesAsync();

        // Broadcast to all participants
        var participantUserIds = await db.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId)
            .Select(static cp => cp.UserId)
            .ToListAsync();

        foreach (var pid in participantUserIds)
            await Clients.Group($"user_{pid}")
                .SendAsync("MessageDeleted", messageId, conversationId);
    }

    /// <summary>
    /// Clear messages in a conversation up to a given cutoff time. Server-side deletion for the requesting user.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public async Task ClearMessages(long conversationId, DateTimeOffset from, DateTimeOffset to)
    {
        var userId = GetUserId();

        var isParticipant = await db.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId);
        if (!isParticipant)
            throw new HubException("You are not a participant in this conversation.");

        // Get user's device IDs to find messages they sent or received
        var userDeviceIds = await db.Devices
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(static d => d.Id)
            .ToListAsync();

        var messagesToDelete = await db.EncryptedMessages
            .Where(m => m.ConversationId == conversationId
                        && m.ServerTimestamp >= from && m.ServerTimestamp <= to
                        && (userDeviceIds.Contains(m.SenderDeviceId) || userDeviceIds.Contains(m.RecipientDeviceId)))
            .ToListAsync();

        if (messagesToDelete.Count > 0)
        {
            var messageIds = messagesToDelete.Select(static m => m.Id).ToList();
            var reactions = await db.MessageReactions.Where(r => messageIds.Contains(r.MessageId)).ToListAsync();
            db.MessageReactions.RemoveRange(reactions);
            db.EncryptedMessages.RemoveRange(messagesToDelete);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Check if a specific user is online.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public async Task<bool> IsUserOnline(long targetUserId)
    {
        // BUG-CR-007 FIX: Verify caller shares a conversation with the target user
        var callerId = GetUserId();

        // Get all conversation IDs the caller participates in
        var callerConvos = await db.ConversationParticipants
            .Where(p => p.UserId == callerId)
            .Select(static p => p.ConversationId)
            .ToListAsync();

        // Check if target user participates in any of those conversations
        var sharedConversation = await db.ConversationParticipants
            .AnyAsync(p => p.UserId == targetUserId && callerConvos.Contains(p.ConversationId));

        return sharedConversation &&
               // Privacy: don't reveal online status to non-contacts
               presence.IsOnline(targetUserId);
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

        ConnectionDeviceMap.TryRemove(Context.ConnectionId, out _);
        ConnectionDisplayNameMap.TryRemove(Context.ConnectionId, out _); // FR-013: Clear cached display name
        await base.OnDisconnectedAsync(exception);
    }

    private async Task<List<long>> GetContactUserIds(long userId)
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

    private long GetUserId()
    {
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? Context.User?.FindFirstValue("sub");
        if (string.IsNullOrEmpty(sub) || !long.TryParse(sub, out var userId))
            throw new HubException("Authentication required.");

        return userId;
    }
}
