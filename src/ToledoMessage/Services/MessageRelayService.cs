using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Toledo.SharedKernel.Helpers;
using ToledoMessage.Data;
using ToledoMessage.Hubs;
using ToledoMessage.Models;
using ToledoMessage.Shared.Constants;
using ToledoMessage.Shared.DTOs;
using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Services;

[SuppressMessage("ReSharper", "RemoveRedundantBraces")]
public class MessageRelayService(ApplicationDbContext db, IHubContext<ChatHub> hubContext)
{
    /// <summary>
    /// Store an encrypted message in the database with an auto-incremented sequence number per conversation.
    /// Uses atomic SQL to prevent race conditions where concurrent messages get the same sequence number.
    /// Falls back to EF-based approach for in-memory provider (testing).
    /// </summary>
    public async Task<EncryptedMessage> StoreMessage(
        long senderDeviceId,
        SendMessageRequest request)
    {
        var now = DateTimeOffset.UtcNow;

        if (!IsValidBase64(request.Ciphertext, out var ciphertext))
            throw new ArgumentException("Invalid Base64 ciphertext.");

        var messageId = IdGenerator.GetNewId();

        if (db.Database.IsRelational())
        {
            // Atomic: INSERT with subquery that computes MAX+1 in a single statement.
            // This prevents two concurrent messages from getting the same sequence number.
            static SqlParameter BigIntParam(string name, long value)
            {
                return new SqlParameter(name, System.Data.SqlDbType.BigInt) { Value = value };
            }

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO EncryptedMessages (Id, ConversationId, SenderDeviceId, RecipientDeviceId, Ciphertext, MessageType, ContentType, FileName, MimeType, ReplyToMessageId, SequenceNumber, ServerTimestamp, IsDelivered)
                VALUES (@id, @convId, @senderDevId, @recipDevId, @cipher, @msgType, @contentType, @fileName, @mimeType, @replyTo, ISNULL((SELECT MAX(SequenceNumber) FROM EncryptedMessages WITH (UPDLOCK) WHERE ConversationId = @convId), 0) + 1, @ts, 0)
                """,
                BigIntParam("@id", messageId),
                BigIntParam("@convId", request.ConversationId),
                BigIntParam("@senderDevId", senderDeviceId),
                BigIntParam("@recipDevId", request.RecipientDeviceId),
                new SqlParameter("@cipher", System.Data.SqlDbType.VarBinary) { Value = ciphertext },
                new SqlParameter("@msgType", System.Data.SqlDbType.Int) { Value = (int)request.MessageType },
                new SqlParameter("@contentType", System.Data.SqlDbType.Int) { Value = (int)request.ContentType },
                new SqlParameter("@fileName", System.Data.SqlDbType.NVarChar, 256) { Value = (object?)request.FileName ?? DBNull.Value },
                new SqlParameter("@mimeType", System.Data.SqlDbType.NVarChar, 128) { Value = (object?)request.MimeType ?? DBNull.Value },
                new SqlParameter("@replyTo", System.Data.SqlDbType.BigInt) { Value = (object?)request.ReplyToMessageId ?? DBNull.Value },
                new SqlParameter("@ts", System.Data.SqlDbType.DateTimeOffset) { Value = now });

            var message = await db.EncryptedMessages.FirstAsync(m => m.Id == messageId);
            return message;
        }

        // Fallback for in-memory provider (unit tests)
        var maxSequence = await db.EncryptedMessages
            .Where(m => m.ConversationId == request.ConversationId)
            .Select(static m => (long?)m.SequenceNumber)
            .MaxAsync() ?? 0;

        var msg = new EncryptedMessage
        {
            Id = messageId,
            ConversationId = request.ConversationId,
            SenderDeviceId = senderDeviceId,
            RecipientDeviceId = request.RecipientDeviceId,
            Ciphertext = ciphertext,
            MessageType = request.MessageType,
            ContentType = request.ContentType,
            FileName = request.FileName,
            MimeType = request.MimeType,
            ReplyToMessageId = request.ReplyToMessageId,
            SequenceNumber = maxSequence + 1,
            ServerTimestamp = now,
            IsDelivered = false
        };

        db.EncryptedMessages.Add(msg);
        await db.SaveChangesAsync();

        return msg;
    }

    /// <summary>
    /// Validates a Base64 string and decodes it.
    /// </summary>
    public static bool IsValidBase64(string input, out byte[] result)
    {
        result = [];
        if (string.IsNullOrEmpty(input))
            return false;

        var buffer = new Span<byte>(new byte[GetMaxBase64DecodedLength(input.Length)]);
        return Convert.TryFromBase64String(input, buffer, out var bytesWritten)
               && (result = buffer[..bytesWritten].ToArray()) is not null;
    }

    private static int GetMaxBase64DecodedLength(int base64Length)
    {
        return (base64Length * 3 + 3) / 4;
    }

    /// <summary>
    /// Try to relay a message to the recipient if they are connected via SignalR.
    /// Sends to the device-specific group.
    /// </summary>
    /// <summary>
    /// Returns the maximum allowed ciphertext size in bytes based on content type.
    /// </summary>
    public static int GetMaxCiphertextSize(ContentType contentType)
    {
        return contentType is ContentType.Text
            ? ProtocolConstants.MaxCiphertextSizeBytes
            : ProtocolConstants.MaxMediaCiphertextSizeBytes;
    }

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
            message.ServerTimestamp,
            message.FileName,
            message.MimeType,
            message.ReplyToMessageId);

        await hubContext.Clients
            .Group($"device_{message.RecipientDeviceId}")
            .SendAsync("ReceiveMessage", envelope);
    }

    /// <summary>
    /// Get all pending (undelivered) messages for a specific device.
    /// </summary>
    public async Task<List<EncryptedMessage>> GetPendingMessages(long deviceId)
    {
        return await db.EncryptedMessages
            .Where(m => m.RecipientDeviceId == deviceId && !m.IsDelivered)
            .OrderBy(static m => m.SequenceNumber)
            .ToListAsync();
    }

    /// <summary>
    /// Mark a message as delivered.
    /// </summary>
    public async Task<EncryptedMessage?> AcknowledgeDelivery(long messageId)
    {
        var message = await db.EncryptedMessages.FindAsync(messageId);
        if (message == null)
            return null;

        message.IsDelivered = true;
        message.DeliveredAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return message;
    }

    /// <summary>
    /// Bulk-acknowledge delivery for all pending messages of a device.
    /// Returns list of (messageId, senderDeviceId) for sending notifications.
    /// </summary>
    public async Task<List<(long MessageId, long SenderDeviceId)>> BulkAcknowledgeDelivery(long deviceId)
    {
        var now = DateTimeOffset.UtcNow;
        List<(long, long)> result;

        if (db.Database.IsRelational())
        {
            // Get messages to notify before updating
            var toNotify = await db.EncryptedMessages
                .Where(m => m.RecipientDeviceId == deviceId && !m.IsDelivered)
                .Select(static m => new { m.Id, m.SenderDeviceId })
                .ToListAsync();

            await db.Database.ExecuteSqlRawAsync(
                "UPDATE EncryptedMessages SET IsDelivered = 1, DeliveredAt = @now WHERE RecipientDeviceId = @deviceId AND IsDelivered = 0",
                new SqlParameter("@now", System.Data.SqlDbType.DateTimeOffset) { Value = now },
                new SqlParameter("@deviceId", System.Data.SqlDbType.BigInt) { Value = deviceId });

            result = toNotify.Select(static m => (m.Id, m.SenderDeviceId)).ToList();
        }
        else
        {
            // Fallback for in-memory provider
            var messages = await db.EncryptedMessages
                .Where(m => m.RecipientDeviceId == deviceId && !m.IsDelivered)
                .ToListAsync();
            foreach (var m in messages)
            {
                m.IsDelivered = true;
                m.DeliveredAt = now;
            }

            await db.SaveChangesAsync();
            result = messages.Select(static m => (m.Id, m.SenderDeviceId)).ToList();
        }

        return result;
    }

    /// <summary>
    /// Advance the read pointer for a user in a conversation up to the given sequence number.
    /// Returns the list of newly-read message IDs + sender device IDs (for notifying senders).
    /// O(1) pointer update + O(k) query for newly-read messages to notify senders.
    /// </summary>
    public async Task<List<(long MessageId, long SenderDeviceId)>> AdvanceReadPointer(
        long userId, long conversationId, long upToSequenceNumber)
    {
        var pointer = await db.ConversationReadPointers
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ConversationId == conversationId);

        if (pointer is null)
        {
            pointer = new ConversationReadPointer
            {
                UserId = userId,
                ConversationId = conversationId,
                LastReadSequenceNumber = 0,
                UnreadCount = 0
            };
            db.ConversationReadPointers.Add(pointer);
        }

        // FR-006: Clamp read pointer to actual max sequence in conversation
        var maxSeq = await db.EncryptedMessages
            .Where(m => m.ConversationId == conversationId)
            .MaxAsync(static m => (long?)m.SequenceNumber) ?? 0;
        upToSequenceNumber = Math.Min(upToSequenceNumber, maxSeq);

        if (upToSequenceNumber <= pointer.LastReadSequenceNumber)
            return [];

        var previousSeqNum = pointer.LastReadSequenceNumber;

        // Get the user's device IDs
        var userDeviceIds = await db.Devices
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(static d => d.Id)
            .ToListAsync();

        // Find newly-read messages (between old pointer and new pointer) sent TO this user
        var newlyReadMessages = await db.EncryptedMessages
            .Where(m => m.ConversationId == conversationId
                        && userDeviceIds.Contains(m.RecipientDeviceId)
                        && m.SequenceNumber > previousSeqNum
                        && m.SequenceNumber <= upToSequenceNumber)
            .Select(static m => new { m.Id, m.SenderDeviceId })
            .ToListAsync();

        // Count remaining unread: messages after the new pointer sent TO this user
        var remainingUnread = await db.EncryptedMessages
            .CountAsync(m => m.ConversationId == conversationId
                             && userDeviceIds.Contains(m.RecipientDeviceId)
                             && m.SequenceNumber > upToSequenceNumber);

        // Update pointer — single row update
        pointer.LastReadSequenceNumber = upToSequenceNumber;
        pointer.UnreadCount = remainingUnread;
        pointer.LastReadAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return newlyReadMessages.Select(static m => (m.Id, m.SenderDeviceId)).ToList();
    }

    /// <summary>
    /// Get the unread count for a user in a conversation from the read pointer.
    /// Falls back to computing from messages if no pointer exists yet.
    /// </summary>
    public async Task<int> GetUnreadCount(long userId, long conversationId)
    {
        var pointer = await db.ConversationReadPointers
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ConversationId == conversationId);

        if (pointer is not null)
            return pointer.UnreadCount;

        // No pointer yet — count all messages in conversation sent to this user's devices
        var userDeviceIds = await db.Devices
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(static d => d.Id)
            .ToListAsync();

        return await db.EncryptedMessages
            .CountAsync(m => m.ConversationId == conversationId
                             && userDeviceIds.Contains(m.RecipientDeviceId)); // BUG-CR-008 FIX: count all messages (delivered & undelivered)
    }

    /// <summary>
    /// Increment the unread count for all participants (except sender) when a new message is sent.
    /// Creates pointers for participants who don't have one yet.
    /// </summary>
    public async Task IncrementUnreadCountsForNewMessage(long conversationId, long senderUserId)
    {
        var participantUserIds = await db.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId && cp.UserId != senderUserId)
            .Select(static cp => cp.UserId)
            .ToListAsync();

        foreach (var participantId in participantUserIds)
        {
            var pointer = await db.ConversationReadPointers
                .FirstOrDefaultAsync(p => p.UserId == participantId && p.ConversationId == conversationId);

            if (pointer is null)
            {
                pointer = new ConversationReadPointer
                {
                    UserId = participantId,
                    ConversationId = conversationId,
                    LastReadSequenceNumber = 0,
                    UnreadCount = 0
                };
                db.ConversationReadPointers.Add(pointer);
            }

            pointer.UnreadCount++;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Mark all pending messages for a deactivated device as delivered.
    /// </summary>
    public async Task CleanupDeactivatedDeviceMessages(long deviceId)
    {
        var now = DateTimeOffset.UtcNow;

        if (db.Database.IsRelational())
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE EncryptedMessages SET IsDelivered = 1, DeliveredAt = COALESCE(DeliveredAt, @now) WHERE RecipientDeviceId = @deviceId AND IsDelivered = 0",
                new SqlParameter("@now", System.Data.SqlDbType.DateTimeOffset) { Value = now },
                new SqlParameter("@deviceId", System.Data.SqlDbType.BigInt) { Value = deviceId });
        }
        else
        {
            var messages = await db.EncryptedMessages
                .Where(m => m.RecipientDeviceId == deviceId && !m.IsDelivered)
                .ToListAsync();
            foreach (var m in messages)
            {
                m.IsDelivered = true;
                m.DeliveredAt ??= now;
            }

            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Delete expired messages that have already been delivered and whose conversation
    /// has a disappearing timer set. Returns the count of deleted messages.
    /// Uses batched deletion to avoid loading all expired messages into memory at once.
    /// Falls back to EF-based approach for in-memory provider (testing).
    /// </summary>
    public async Task<int> CleanupExpiredMessages()
    {
        var now = DateTimeOffset.UtcNow;

        if (db.Database.IsRelational())
        {
            // Batched deletion: delete up to 1000 rows at a time to avoid memory pressure
            var totalDeleted = 0;
            int batchDeleted;
            do
            {
                var nowParam = new SqlParameter("@now", System.Data.SqlDbType.DateTimeOffset) { Value = now };
                batchDeleted = await db.Database.ExecuteSqlRawAsync(
                    """
                    DELETE TOP(1000) em
                    FROM EncryptedMessages em
                    INNER JOIN Conversations c ON em.ConversationId = c.Id
                    WHERE em.IsDelivered = 1
                      AND c.DisappearingTimerSeconds IS NOT NULL
                      AND DATEADD(SECOND, c.DisappearingTimerSeconds, em.ServerTimestamp) < @now
                    """, nowParam);
                totalDeleted += batchDeleted;
            } while (batchDeleted == 1000);

            return totalDeleted;
        }

        // Fallback for in-memory provider (unit tests)
        var expiredMessages = await db.EncryptedMessages
            .Include(static m => m.Conversation)
            .Where(static m => m.IsDelivered)
            .Where(static m => m.Conversation.DisappearingTimerSeconds != null)
            .Where(m => m.Conversation.DisappearingTimerSeconds != null && m.ServerTimestamp.AddSeconds(m.Conversation.DisappearingTimerSeconds.Value) < now)
            .ToListAsync();

        if (expiredMessages.Count == 0)
            return 0;

        db.EncryptedMessages.RemoveRange(expiredMessages);
        await db.SaveChangesAsync();

        return expiredMessages.Count;
    }
}
