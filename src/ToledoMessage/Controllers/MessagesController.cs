using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToledoMessage.Data;
using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly MessageRelayService _relayService;

    public MessagesController(ApplicationDbContext db, MessageRelayService relayService)
    {
        _db = db;
        _relayService = relayService;
    }

    /// <summary>
    /// Store and relay an encrypted message (REST fallback when SignalR is unavailable).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        var userId = GetUserId();

        // Validate sender is a participant in the conversation
        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == request.ConversationId && cp.UserId == userId);
        if (!isParticipant)
            return Forbid();

        // Find an active sender device for this user
        var senderDeviceId = await _db.Devices
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(d => d.Id)
            .FirstOrDefaultAsync();
        if (senderDeviceId == 0)
            return BadRequest("No active device found for the current user.");

        // Store the message
        var message = await _relayService.StoreMessage(senderDeviceId, request);

        // Try to relay to online recipient via SignalR
        await _relayService.TryRelayToOnlineRecipient(message);

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
        var deviceOwned = await _db.Devices.AnyAsync(d => d.Id == deviceId && d.UserId == userId && d.IsActive);
        if (!deviceOwned)
            return NotFound("Device not found or does not belong to the current user.");

        var messages = await _relayService.GetPendingMessages(deviceId);

        var envelopes = messages.Select(m => new MessageEnvelope(
            m.Id,
            m.ConversationId,
            m.SenderDeviceId,
            Convert.ToBase64String(m.Ciphertext),
            m.MessageType,
            m.ContentType,
            m.SequenceNumber,
            m.ServerTimestamp)).ToList();

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
        var message = await _db.EncryptedMessages
            .Include(m => m.RecipientDevice)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
            return NotFound("Message not found.");

        if (message.RecipientDevice.UserId != userId)
            return Forbid();

        var acknowledged = await _relayService.AcknowledgeDelivery(messageId);
        if (acknowledged == null)
            return NotFound("Message not found.");

        return Ok(new { messageId, deliveredAt = acknowledged.DeliveredAt });
    }

    private decimal GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return decimal.Parse(sub!);
    }
}
