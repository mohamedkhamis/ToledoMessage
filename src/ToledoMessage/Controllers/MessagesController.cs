using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToledoMessage.Data;
using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;

// ReSharper disable RemoveRedundantBraces

namespace ToledoMessage.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController(ApplicationDbContext db, MessageRelayService relayService) : BaseApiController
{
    /// <summary>
    /// Store and relay an encrypted message (REST fallback when SignalR is unavailable).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        var userId = GetUserId();

        // Validate Base64 ciphertext before processing
        if (!MessageRelayService.IsValidBase64(request.Ciphertext, out var ciphertextBytes))
            return BadRequest("Invalid Base64 ciphertext.");

        // Defensive fallback: if ContentType deserialized as Text but ciphertext exceeds text limit,
        // treat as media (enum serialization can fail across JSON boundaries)
        var effectiveContentType = request.ContentType;
        if (effectiveContentType == Shared.Enums.ContentType.Text
            && ciphertextBytes.Length > Shared.Constants.ProtocolConstants.MaxCiphertextSizeBytes)
        {
            effectiveContentType = Shared.Enums.ContentType.File;
        }

        var maxSize = MessageRelayService.GetMaxCiphertextSize(effectiveContentType);
        if (ciphertextBytes.Length > maxSize)
            return BadRequest($"Message exceeds the maximum allowed size ({maxSize / 1_048_576} MB).");

        // Validate sender is a participant in the conversation
        var isParticipant = await db.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == request.ConversationId && cp.UserId == userId);
        if (!isParticipant)
            return Forbid();

        // Verify the SenderDeviceId belongs to the calling user
        var senderDeviceOwned = await db.Devices
            .AnyAsync(d => d.Id == request.SenderDeviceId && d.UserId == userId && d.IsActive);
        if (!senderDeviceOwned)
            return BadRequest("Sender device not found or does not belong to the current user.");

        // Validate recipient device is active and recipient user is not deactivated
        var recipientDevice = await db.Devices
            .Include(static d => d.User)
            .FirstOrDefaultAsync(d => d.Id == request.RecipientDeviceId && d.IsActive);
        if (recipientDevice == null || !recipientDevice.User.IsActive)
            return BadRequest("Recipient device is not available.");

        // Store the message
        var message = await relayService.StoreMessage(request.SenderDeviceId, request);

        // Try to relay to online recipient via SignalR
        await relayService.TryRelayToOnlineRecipient(message);

        var result = new SendMessageResult(message.Id, message.ServerTimestamp, message.SequenceNumber);
        return Ok(result);
    }

    /// <summary>
    /// Get pending (undelivered) messages for a specific device.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingMessages([FromQuery] decimal deviceId)
    {
        var userId = GetUserId();

        // Validate the device belongs to the requesting user
        var deviceOwned = await db.Devices.AnyAsync(d => d.Id == deviceId && d.UserId == userId && d.IsActive);
        if (!deviceOwned)
            return NotFound("Device not found or does not belong to the current user.");

        var messages = await relayService.GetPendingMessages(deviceId);

        var envelopes = messages.Select(static m => new MessageEnvelope(
            m.Id,
            m.ConversationId,
            m.SenderDeviceId,
            Convert.ToBase64String(m.Ciphertext),
            m.MessageType,
            m.ContentType,
            m.SequenceNumber,
            m.ServerTimestamp,
            m.FileName,
            m.MimeType,
            m.ReplyToMessageId)).ToList();

        return Ok(envelopes);
    }

    /// <summary>
    /// Acknowledge delivery of a message.
    /// </summary>
    [HttpPost("{messageId}/acknowledge")]
    public async Task<IActionResult> AcknowledgeDelivery(decimal messageId)
    {
        var userId = GetUserId();

        // Validate the message recipient is one of the requesting user's devices
        var message = await db.EncryptedMessages
            .Include(static m => m.RecipientDevice)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
            return NotFound("Message not found.");

        if (message.RecipientDevice.UserId != userId)
            return Forbid();

        var acknowledged = await relayService.AcknowledgeDelivery(messageId);
        if (acknowledged == null)
            return NotFound("Message not found.");

        return Ok(new { messageId, deliveredAt = acknowledged.DeliveredAt });
    }
}
