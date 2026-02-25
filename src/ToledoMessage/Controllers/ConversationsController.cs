using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Toledo.SharedKernel.Helpers;
using ToledoMessage.Data;
using ToledoMessage.Models;
using ToledoMessage.Shared.DTOs;
using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ConversationsController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Create a one-to-one conversation between the requesting user and another user.
    /// Returns the existing conversation if one already exists.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConversationRequest request)
    {
        var userId = GetUserId();

        if (userId == request.ParticipantUserId)
            return BadRequest("Cannot create a conversation with yourself.");

        var participantExists = await _db.Users.AnyAsync(u => u.Id == request.ParticipantUserId && u.IsActive);
        if (!participantExists)
            return NotFound("Participant user not found.");

        // Check for an existing OneToOne conversation between these two users
        var existingConversationId = await _db.Conversations
            .Where(c => c.Type == ConversationType.OneToOne)
            .Where(c => c.Participants.Any(p => p.UserId == userId))
            .Where(c => c.Participants.Any(p => p.UserId == request.ParticipantUserId))
            .Select(c => (decimal?)c.Id)
            .FirstOrDefaultAsync();

        if (existingConversationId.HasValue)
            return Ok(new ConversationResponse(existingConversationId.Value, false));

        // Create new conversation
        var conversationId = DecimalTools.GetNewId();
        var now = DateTimeOffset.UtcNow;

        var conversation = new Conversation
        {
            Id = conversationId,
            Type = ConversationType.OneToOne,
            CreatedAt = now
        };

        _db.Conversations.Add(conversation);

        _db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conversationId,
            UserId = userId,
            JoinedAt = now,
            Role = ParticipantRole.Member
        });

        _db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conversationId,
            UserId = request.ParticipantUserId,
            JoinedAt = now,
            Role = ParticipantRole.Member
        });

        await _db.SaveChangesAsync();

        return Created(string.Empty, new ConversationResponse(conversationId, true));
    }

    private decimal GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return decimal.Parse(sub!);
    }
}
