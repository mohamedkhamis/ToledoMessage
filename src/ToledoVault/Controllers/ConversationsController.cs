using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Toledo.SharedKernel.Helpers;
using ToledoVault.Data;
using ToledoVault.Models;
using ToledoVault.Shared.DTOs;
using ToledoVault.Shared.Enums;

namespace ToledoVault.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController(ApplicationDbContext db) : BaseApiController
{
    /// <summary>
    /// List all conversations the current user participates in.
    /// Returns conversation metadata with display names, last message time, and unread counts.
    /// Uses a single query to avoid N+1 performance issues.
    /// </summary>
    // ReSharper disable  RemoveRedundantBraces
    [HttpGet]
    public async Task<IActionResult> ListConversations()
    {
        var userId = GetUserId();

        // Single query: join conversations with participants, messages for last-message-time,
        // and unread counts — avoids the previous N+1 loop
        var conversationIds = await db.ConversationParticipants
            .Where(p => p.UserId == userId)
            .Select(static p => p.ConversationId)
            .ToListAsync();

        var conversations = await db.Conversations
            .Where(c => conversationIds.Contains(c.Id))
            .Include(static c => c.Participants)
            .ThenInclude(static p => p.User)
            .ToListAsync();

        if (conversations.Count == 0)
            return Ok(Array.Empty<ConversationListItemResponse>());

        // Batch: get last message timestamps per conversation
        var lastMessageTimes = await db.EncryptedMessages
            .Where(m => conversationIds.Contains(m.ConversationId))
            .GroupBy(static m => m.ConversationId)
            .Select(static g => new { ConversationId = g.Key, LastMessageTime = g.Max(static m => m.ServerTimestamp) })
            .ToDictionaryAsync(static x => x.ConversationId, static x => (DateTimeOffset?)x.LastMessageTime);

        // Batch: get unread counts from read pointers — O(1) per conversation
        var unreadCounts = await db.ConversationReadPointers
            .Where(p => p.UserId == userId && conversationIds.Contains(p.ConversationId) && p.UnreadCount > 0)
            .ToDictionaryAsync(static p => p.ConversationId, static p => p.UnreadCount);

        var results = conversations.Select(conversation =>
            {
                string displayName;
                string? displayNameSecondary = null;
                if (conversation.Type == ConversationType.Group)
                {
                    displayName = conversation.GroupName ?? "Group";
                }
                else
                {
                    var otherParticipant = conversation.Participants
                        .FirstOrDefault(p => p.UserId != userId);
                    displayName = otherParticipant?.User.DisplayName ?? "Unknown";
                    displayNameSecondary = otherParticipant?.User.DisplayNameSecondary;
                }

                lastMessageTimes.TryGetValue(conversation.Id, out var lastMessageTime);
                unreadCounts.TryGetValue(conversation.Id, out var unreadCount);

                return new ConversationListItemResponse(
                    conversation.Id,
                    conversation.Type,
                    displayName,
                    lastMessageTime,
                    unreadCount,
                    DisplayNameSecondary: displayNameSecondary);
            })
            .OrderByDescending(static r => r.LastMessageTime.HasValue)
            .ThenByDescending(static r => r.LastMessageTime)
            .ToList();

        return Ok(results);
    }

    /// <summary>
    /// Create a one-to-one conversation between the requesting user and another user.
    /// Returns the existing conversation if one already exists.
    /// Atomicity is handled by the global TransactionFilter.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConversationRequest request)
    {
        var userId = GetUserId();

        if (userId == request.ParticipantUserId)
            return BadRequest("Cannot create a conversation with yourself.");

        var participantExists = await db.Users.AnyAsync(u => u.Id == request.ParticipantUserId && u.IsActive);
        if (!participantExists)
            return NotFound("Participant user not found.");

        // Check for an existing OneToOne conversation between these two users
        var existingConversationId = await db.Conversations
            .Where(static c => c.Type == ConversationType.OneToOne)
            .Where(c => c.Participants.Any(p => p.UserId == userId))
            .Where(c => c.Participants.Any(p => p.UserId == request.ParticipantUserId))
            .Select(static c => (long?)c.Id)
            .FirstOrDefaultAsync();

        if (existingConversationId.HasValue)
            return Ok(new ConversationResponse(existingConversationId.Value, false));

        // Create new conversation
        var conversationId = IdGenerator.GetNewId();
        var now = DateTimeOffset.UtcNow;

        var conversation = new Conversation
        {
            Id = conversationId,
            Type = ConversationType.OneToOne,
            CreatedAt = now
        };

        db.Conversations.Add(conversation);

        db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conversationId,
            UserId = userId,
            JoinedAt = now,
            Role = ParticipantRole.Member
        });

        db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conversationId,
            UserId = request.ParticipantUserId,
            JoinedAt = now,
            Role = ParticipantRole.Member
        });

        await db.SaveChangesAsync();

        return Created(string.Empty, new ConversationResponse(conversationId, true));
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

        switch (request.ParticipantUserIds.Count)
        {
            case < 2:
                return BadRequest("At least 2 participant user IDs are required.");
            case > 100:
                return BadRequest("A group conversation can have at most 100 participants.");
        }

        // Remove duplicates and ensure the creator is not in the list (they will be added automatically)
        var participantIds = request.ParticipantUserIds
            .Distinct()
            .Where(id => id != userId)
            .ToList();

        if (participantIds.Count < 1)
            return BadRequest("At least one other participant is required.");

        // Validate all participants exist and are active
        var activeUserCount = await db.Users
            .CountAsync(u => participantIds.Contains(u.Id) && u.IsActive);

        if (activeUserCount != participantIds.Count)
            return BadRequest("One or more participants do not exist or are inactive.");

        var conversationId = IdGenerator.GetNewId();
        var now = DateTimeOffset.UtcNow;

        var conversation = new Conversation
        {
            Id = conversationId,
            Type = ConversationType.Group,
            GroupName = request.GroupName.Trim(),
            CreatedAt = now
        };

        db.Conversations.Add(conversation);

        // Add creator as Admin
        db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conversationId,
            UserId = userId,
            JoinedAt = now,
            Role = ParticipantRole.Admin
        });

        // Add all other participants as Members
        foreach (var participantId in participantIds)
        {
            db.ConversationParticipants.Add(new ConversationParticipant
            {
                ConversationId = conversationId,
                UserId = participantId,
                JoinedAt = now,
                Role = ParticipantRole.Member
            });
        }

        await db.SaveChangesAsync();

        return Created(string.Empty, new ConversationResponse(conversationId, true));
    }

    /// <summary>
    /// Add a participant to a group conversation. Only Admins may add participants.
    /// </summary>
    [HttpPost("{conversationId}/participants")]
    public async Task<IActionResult> AddParticipant(long conversationId, [FromBody] AddParticipantRequest request)
    {
        var userId = GetUserId();

        // Verify the conversation exists and is a group
        var conversation = await db.Conversations.FindAsync(conversationId);
        if (conversation is null || conversation.Type != ConversationType.Group)
            return NotFound("Group conversation not found.");

        // Verify the requesting user is an Admin
        var requesterParticipant = await db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (requesterParticipant is null)
            return NotFound("Group conversation not found.");

        if (requesterParticipant.Role != ParticipantRole.Admin)
            return Forbid();

        // Validate the target user exists and is active
        var targetUserExists = await db.Users.AnyAsync(u => u.Id == request.UserId && u.IsActive);
        if (!targetUserExists)
            return NotFound("User not found or inactive.");

        // Validate the target user isn't already a participant
        var alreadyParticipant = await db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == request.UserId);

        if (alreadyParticipant)
            return BadRequest("User is already a participant in this conversation.");

        // Validate max 100 participants
        var currentCount = await db.ConversationParticipants
            .CountAsync(p => p.ConversationId == conversationId);

        if (currentCount >= 100)
            return BadRequest("Group conversation has reached the maximum of 100 participants.");

        db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conversationId,
            UserId = request.UserId,
            JoinedAt = DateTimeOffset.UtcNow,
            Role = ParticipantRole.Member
        });

        await db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Remove a participant from a group conversation.
    /// Admins can remove any member. Any user can remove themselves (leave the group).
    /// </summary>
    [HttpDelete("{conversationId}/participants/{targetUserId}")]
    public async Task<IActionResult> RemoveParticipant(long conversationId, long targetUserId)
    {
        var userId = GetUserId();

        // Verify the conversation exists and is a group
        var conversation = await db.Conversations.FindAsync(conversationId);
        if (conversation is null || conversation.Type != ConversationType.Group)
            return NotFound("Group conversation not found.");

        // Verify the requesting user is a participant
        var requesterParticipant = await db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (requesterParticipant is null)
            return NotFound("Group conversation not found.");

        // If not self-removal, must be Admin
        if (userId != targetUserId && requesterParticipant.Role != ParticipantRole.Admin)
            return Forbid();

        // Find the target participant
        var targetParticipant = await db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == targetUserId);

        if (targetParticipant is null)
            return NotFound("Participant not found in this conversation.");

        db.ConversationParticipants.Remove(targetParticipant);
        await db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Promote a participant to Admin in a group conversation. Only Admins may promote.
    /// </summary>
    [HttpPut("{conversationId}/participants/{targetUserId}/promote")]
    public async Task<IActionResult> PromoteToAdmin(long conversationId, long targetUserId)
    {
        var userId = GetUserId();

        var conversation = await db.Conversations.FindAsync(conversationId);
        if (conversation is null || conversation.Type != ConversationType.Group)
            return NotFound("Group conversation not found.");

        var requester = await db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);
        if (requester is null)
            return NotFound("Group conversation not found.");
        if (requester.Role != ParticipantRole.Admin)
            return Forbid();

        var target = await db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == targetUserId);
        if (target is null)
            return NotFound("Participant not found in this conversation.");
        if (target.Role == ParticipantRole.Admin)
            return BadRequest("User is already an admin.");

        target.Role = ParticipantRole.Admin;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Demote an Admin to Member in a group conversation. Only Admins may demote.
    /// Cannot demote yourself if you are the last admin.
    /// </summary>
    [HttpPut("{conversationId}/participants/{targetUserId}/demote")]
    public async Task<IActionResult> DemoteToMember(long conversationId, long targetUserId)
    {
        var userId = GetUserId();

        var conversation = await db.Conversations.FindAsync(conversationId);
        if (conversation is null || conversation.Type != ConversationType.Group)
            return NotFound("Group conversation not found.");

        var requester = await db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);
        if (requester is null)
            return NotFound("Group conversation not found.");
        if (requester.Role != ParticipantRole.Admin)
            return Forbid();

        var target = await db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == targetUserId);
        if (target is null)
            return NotFound("Participant not found in this conversation.");
        if (target.Role != ParticipantRole.Admin)
            return BadRequest("User is not an admin.");

        // Prevent demoting the last admin
        var adminCount = await db.ConversationParticipants
            .CountAsync(p => p.ConversationId == conversationId && p.Role == ParticipantRole.Admin);
        if (adminCount <= 1)
            return BadRequest("Cannot demote the last admin. Promote another member first.");

        target.Role = ParticipantRole.Member;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Get conversation details including type, group name, and participant count.
    /// </summary>
    [HttpGet("{conversationId}")]
    public async Task<IActionResult> GetConversation(long conversationId)
    {
        var userId = GetUserId();

        // Verify the requesting user is a participant
        var isParticipant = await db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (!isParticipant)
            return NotFound("Conversation not found.");

        var conversation = await db.Conversations
            .Where(c => c.Id == conversationId)
            .Select(static c => new ConversationDetailResponse(
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
    public async Task<IActionResult> GetParticipants(long conversationId)
    {
        var userId = GetUserId();

        // Verify the requesting user is a participant
        var isParticipant = await db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (!isParticipant)
            return NotFound("Conversation not found.");

        var participants = await db.ConversationParticipants
            .Where(p => p.ConversationId == conversationId)
            .Select(static p => new ParticipantResponse(p.UserId, p.User.DisplayName, p.Role, p.User.DisplayNameSecondary))
            .ToListAsync();

        return Ok(participants);
    }

    /// <summary>
    /// Set or disable the disappearing messages timer for a conversation.
    /// Only participants may change the timer.
    /// </summary>
    [HttpPut("{conversationId}/timer")]
    public async Task<IActionResult> SetTimer(long conversationId, [FromBody] SetTimerRequest request)
    {
        var userId = GetUserId();

        // Validate the requesting user is a participant
        var isParticipant = await db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (!isParticipant)
            return NotFound("Conversation not found.");

        switch (request.TimerSeconds)
        {
            // Validate timer value: null (disable) or positive integer within allowed range
            case <= 0:
                return BadRequest("Timer must be a positive number of seconds, or null to disable.");
            case > Shared.Constants.ProtocolConstants.MaxTimerSeconds:
                return BadRequest($"Timer cannot exceed {Shared.Constants.ProtocolConstants.MaxTimerSeconds} seconds (1 year).");
        }

        var conversation = await db.Conversations.FindAsync(conversationId);
        if (conversation == null)
            return NotFound("Conversation not found.");

        conversation.DisappearingTimerSeconds = request.TimerSeconds;
        await db.SaveChangesAsync();

        return NoContent();
    }
}
