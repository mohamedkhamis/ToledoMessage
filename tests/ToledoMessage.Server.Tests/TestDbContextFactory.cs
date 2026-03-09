using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToledoMessage.Data;
using ToledoMessage.Models;
using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Server.Tests;

public static class TestDbContextFactory
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(static w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static ClaimsPrincipal CreateUserPrincipal(long userId, string displayName = "testuser")
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, displayName)
        ], "TestScheme"));
    }

    public static void SetUser(ControllerBase controller, long userId, string displayName = "testuser")
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreateUserPrincipal(userId, displayName)
            }
        };
    }

    public static async Task<User> SeedUser(ApplicationDbContext db, long id, string displayName = "testuser", bool isActive = true)
    {
        var user = new User
        {
            Id = id,
            Username = displayName,
            DisplayName = displayName,
            PasswordHash = "hashed",
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = isActive
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task<Device> SeedDevice(ApplicationDbContext db, long id, long userId, string name = "TestDevice")
    {
        var device = new Device
        {
            Id = id,
            UserId = userId,
            DeviceName = name,
            IdentityPublicKeyClassical = new byte[32],
            IdentityPublicKeyPostQuantum = new byte[1184],
            SignedPreKeyPublic = new byte[32],
            SignedPreKeySignature = new byte[64],
            SignedPreKeyId = 1,
            KyberPreKeyPublic = new byte[1184],
            KyberPreKeySignature = new byte[64],
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return device;
    }

    public static async Task<Conversation> SeedConversation(ApplicationDbContext db, long id, ConversationType type = ConversationType.OneToOne, string? groupName = null)
    {
        var conversation = new Conversation
        {
            Id = id,
            Type = type,
            GroupName = groupName,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        return conversation;
    }

    public static async Task SeedParticipant(ApplicationDbContext db, long conversationId, long userId, ParticipantRole role = ParticipantRole.Member)
    {
        db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conversationId,
            UserId = userId,
            JoinedAt = DateTimeOffset.UtcNow,
            Role = role
        });
        await db.SaveChangesAsync();
    }
}
