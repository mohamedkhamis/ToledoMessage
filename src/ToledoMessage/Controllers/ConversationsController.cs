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
public class ConversationsController : BaseApiController
{
    private readonly ApplicationDbContext _db;

    public ConversationsController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// List all conversations the current user participates in.
    /// Returns conversation metadata with display names, last message time, and unread counts.
    /// Uses a single query to avoid N+1 performance issues.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListConversations()
    {
        var userId = GetUserId();

        // Get the user's device IDs for unread count calculation
        var userDeviceIds = await _db.Devices
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(d => d.Id)
            .ToListAsync();

        // Single query: join conversations with participants, messages for last-message-time,
        // and unread counts — avoids the previous N+1 loop
        var conversationIds = await _db.ConversationParticipants
            .Where(p => p.UserId == userId)
            .Select(p => p.ConversationId)
            .ToListAsync();

        var conversations = await _db.Conversations
            .Where(c => conversationIds.Contains(c.Id))
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
            .ToListAsync();

        if (conversations.Count == 0)
            return Ok(Array.Empty<ConversationListItemResponse>());

        // Batch: get last message timestamps per conversation
        var lastMessageTimes = await _db.EncryptedMessages
            .Where(m => conversationIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, LastMessageTime = g.Max(m => m.ServerTimestamp) })
            .ToDictionaryAsync(x => x.ConversationId, x => (DateTimeOffset?)x.LastMessageTime);

        // Batch: get unread counts per conversation
        var unreadCounts = userDeviceIds.Count > 0
            ? await _db.EncryptedMessages
                .Where(m => conversationIds.Contains(m.ConversationId)
                    && userDeviceIds.Contains(m.RecipientDeviceId)
                    && !m.IsDelivered)
                .GroupBy(m => m.ConversationId)
                .Select(g => new { ConversationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ConversationId, x => x.Count)
            : new Dictionary<decimal, int>();

        var results = conversations.Select(conversation =>
        {
            string displayName;
            if (conversation.Type == ConversationType.Group)
            {
                displayName = conversation.GroupName ?? "Group";
            }
            else
            {
                var otherParticipant = conversation.Participants
                    .FirstOrDefault(p => p.UserId != userId);
                displayName = otherParticipant?.User.DisplayName ?? "Unknown";
            }

            lastMessageTimes.TryGetValue(conversation.Id, out var lastMessageTime);
            unreadCounts.TryGetValue(conversation.Id, out var unreadCount);

            return new ConversationListItemResponse(
                conversation.Id,
                conversation.Type,
                displayName,
                lastMessageTime,
                unreadCount);
        })
        .OrderByDescending(r => r.LastMessageTime.HasValue)
        .ThenByDescending(r => r.LastMessageTime)
        .ToList();

        return Ok(results);
    }

    /// <summary>
    /// Create a one-to-one conversation between the requesting user and another user.
    /// Returns the existing conversation if one already exists.
    /// Uses a transaction to prevent TOCTOU race conditions where concurrent requests
    /// could create duplicate conversations.
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

        // Use a transaction to atomically check-then-create
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // Check for an existing OneToOne conversation between these two users
            var existingConversationId = await _db.Conversations
                .Where(c => c.Type == ConversationType.OneToOne)
                .Where(c => c.Participants.Any(p => p.UserId == userId))
                .Where(c => c.Participants.Any(p => p.UserId == request.ParticipantUserId))
                .Select(c => (decimal?)c.Id)
                .FirstOrDefaultAsync();

            if (existingConversationId.HasValue)
            {
                await transaction.CommitAsync();
                return Ok(new ConversationResponse(existingConversationId.Value, false));
            }

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
            await transaction.CommitAsync();

            return Created(string.Empty, new ConversationResponse(conversationId, true));
        }
        catch
        {
            await transaction.RollbackAsync();

            // If we hit a race condition, the other request won — return the existing conversation
            var existingId = await _db.Conversations
                .Where(c => c.Type == ConversationType.OneToOne)
                .Where(c => c.Participants.Any(p => p.UserId == userId))
                .Where(c => c.Participants.Any(p => p.UserId == request.ParticipantUserId))
                .Select(c => (decimal?)c.Id)
                .FirstOrDefaultAsync();

            if (existingId.HasValue)
                return Ok(new ConversationResponse(existingId.Value, false));

            throw;
        }
    }

    /// <summary>
    /// Create a group conversation with the specified participants.
    /// The creator is automatically included as Admin; all others are Members.
    /// </summary>
    [HttpPost("group")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupConversationRequest request)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(request.GroupName))
            return BadRequest("Group name is required.");

        if (request.GroupName.Trim().Length > Shared.Constants.ProtocolConstants.MaxGroupNameLength)
            return BadRequest($"Group name must not exceed {Shared.Constants.ProtocolConstants.MaxGroupNameLength} characters.");

        if (request.ParticipantUserIds is null || request.ParticipantUserIds.Count < 2)
            return BadRequest("At least 2 participant user IDs are required.");

        if (request.ParticipantUserIds.Count > 100)
            return BadRequest("A group conversation can have at most 100 participants.");

        // Remove duplicates and ensure the creator is not in the list (they will be added automatically)
        var participantIds = request.ParticipantUserIds
            .Distinct()
            .Where(id => id != userId)
            .ToList();

        if (participantIds.Count < 1)
            return BadRequest("At least one other participant is required.");

        // Validate all participants exist and are active
        var activeUserCount = await _db.Users
            .CountAsync(u => participantIds.Contains(u.Id) && u.IsActive);

        if (activeUserCount != participantIds.Count)
            return BadRequest("One or more participants do not exist or are inactive.");

        var conversationId = DecimalTools.GetNewId();
        var now = DateTimeOffset.UtcNow;

        var conversation = new Conversation
        {
            Id = conversationId,
            Type = ConversationType.Group,
            GroupName = request.GroupName.Trim(),
            CreatedAt = now
        };

        _db.Conversations.Add(conversation);

        // Add creator as Admin
        _db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conversationId,
            UserId = userId,
            JoinedAt = now,
            Role = ParticipantRole.Admin
        });

        // Add all other participants as Members
        foreach (var participantId in participantIds)
        {
            _db.ConversationParticipants.Add(new ConversationParticipant
            {
                ConversationId = conversationId,
                UserId = participantId,
                JoinedAt = now,
                Role = ParticipantRole.Member
            });
        }

        await _db.SaveChangesAsync();

        return Created(string.Empty, new ConversationResponse(conversationId, true));
    }

    /// <summary>
    /// Add a participant to a group conversation. Only Admins may add participants.
    /// </summary>
    [HttpPost("{conversationId}/participants")]
    public async Task<IActionResult> AddParticipant(decimal conversationId, [FromBody] AddParticipantRequest request)
    {
        var userId = GetUserId();

        // Verify the conversation exists and is a group
        var conversation = await _db.Conversations.FindAsync(conversationId);
        if (conversation is null || conversation.Type != ConversationType.Group)
            return NotFound("Group conversation not found.");

        // Verify the requesting user is an Admin
        var requesterParticipant = await _db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (requesterParticipant is null)
            return NotFound("Group conversation not found.");

        if (requesterParticipant.Role != ParticipantRole.Admin)
            return Forbid();

        // Validate the target user exists and is active
        var targetUserExists = await _db.Users.AnyAsync(u => u.Id == request.UserId && u.IsActive);
        if (!targetUserExists)
            return NotFound("User not found or inactive.");

        // Validate the target user isn't already a participant
        var alreadyParticipant = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == request.UserId);

        if (alreadyParticipant)
            return BadRequest("User is already a participant in this conversation.");

        // Validate max 100 participants
        var currentCount = await _db.ConversationParticipants
            .CountAsync(p => p.ConversationId == conversationId);

        if (currentCount >= 100)
            return BadRequest("Group conversation has reached the maximum of 100 participants.");

        _db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conversationId,
            UserId = request.UserId,
            JoinedAt = DateTimeOffset.UtcNow,
            Role = ParticipantRole.Member
        });

        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Remove a participant from a group conversation.
    /// Admins can remove any member. Any user can remove themselves (leave the group).
    /// </summary>
    [HttpDelete("{conversationId}/participants/{targetUserId}")]
    public async Task<IActionResult> RemoveParticipant(decimal conversationId, decimal targetUserId)
    {
        var userId = GetUserId();

        // Verify the conversation exists and is a group
        var conversation = await _db.Conversations.FindAsync(conversationId);
        if (conversation is null || conversation.Type != ConversationType.Group)
            return NotFound("Group conversation not found.");

        // Verify the requesting user is a participant
        var requesterParticipant = await _db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (requesterParticipant is null)
            return NotFound("Group conversation not found.");

        // If not self-removal, must be Admin
        if (userId != targetUserId && requesterParticipant.Role != ParticipantRole.Admin)
            return Forbid();

        // Find the target participant
        var targetParticipant = await _db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == targetUserId);

        if (targetParticipant is null)
            return NotFound("Participant not found in this conversation.");

        _db.ConversationParticipants.Remove(targetParticipant);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Get conversation details including type, group name, and participant count.
    /// </summary>
    [HttpGet("{conversationId}")]
    public async Task<IActionResult> GetConversation(decimal conversationId)
    {
        var userId = GetUserId();

        // Verify the requesting user is a participant
        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (!isParticipant)
            return NotFound("Conversation not found.");

        var conversation = await _db.Conversations
            .Where(c => c.Id == conversationId)
            .Select(c => new ConversationDetailResponse(
                c.Id,
                c.Type,
                c.GroupName,
                c.Participants.Count,
                c.CreatedAt,
                c.DisappearingTimerSeconds))
            .FirstOrDefaultAsync();

        if (conversation is null)
            return NotFound("Conversation not found.");

        return Ok(conversation);
    }

    /// <summary>
    /// Get the list of participants in a conversation.
    /// </summary>
    [HttpGet("{conversationId}/participants")]
    public async Task<IActionResult> GetParticipants(decimal conversationId)
    {
        var userId = GetUserId();

        // Verify the requesting user is a participant
        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (!isParticipant)
            return NotFound("Conversation not found.");

        var participants = await _db.ConversationParticipants
            .Where(p => p.ConversationId == conversationId)
            .Select(p => new ParticipantResponse(p.UserId, p.User.DisplayName, p.Role))
            .ToListAsync();

        return Ok(participants);
    }

    /// <summary>
    /// Set or disable the disappearing messages timer for a conversation.
    /// Only participants may change the timer.
    /// </summary>
    [HttpPut("{conversationId}/timer")]
    public async Task<IActionResult> SetTimer(decimal conversationId, [FromBody] SetTimerRequest request)
    {
        var userId = GetUserId();

        // Validate the requesting user is a participant
        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (!isParticipant)
            return NotFound("Conversation not found.");

        // Validate timer value: null (disable) or positive integer within allowed range
        if (request.TimerSeconds.HasValue && request.TimerSeconds.Value <= 0)
            return BadRequest("Timer must be a positive number of seconds, or null to disable.");

        if (request.TimerSeconds.HasValue && request.TimerSeconds.Value > Shared.Constants.ProtocolConstants.MaxTimerSeconds)
            return BadRequest($"Timer cannot exceed {Shared.Constants.ProtocolConstants.MaxTimerSeconds} seconds (1 year).");

        var conversation = await _db.Conversations.FindAsync(conversationId);
        if (conversation == null)
            return NotFound("Conversation not found.");

        conversation.DisappearingTimerSeconds = request.TimerSeconds;
        await _db.SaveChangesAsync();

        return NoContent();
    }

}
