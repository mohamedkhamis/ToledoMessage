using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ToledoMessage.Data;
using ToledoMessage.Hubs;
using ToledoMessage.Models;
using ToledoMessage.Services;
using ToledoMessage.Server.Tests.Services;
using ToledoMessage.Shared.DTOs;
using ToledoMessage.Shared.Enums;


// ReSharper disable RemoveRedundantBraces

#pragma warning disable CA1854
#pragma warning disable CA1859

namespace ToledoMessage.Server.Tests.Hubs;

/// <inheritdoc />
/// <summary>
/// Stub IGroupManager that tracks group additions for assertions.
/// </summary>
public class StubGroupManager : IGroupManager
{
    public List<(string connectionId, string groupName)> Added { get; } = [];

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Added.Add((connectionId, groupName));
        return Task.CompletedTask;
    }

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

/// <inheritdoc />
/// <summary>
/// Stub HubCallerContext with configurable user and connection ID.
/// </summary>
public class StubHubCallerContext(long userId, string connectionId = "test-connection") : HubCallerContext
{
    public override string ConnectionId { get; } = connectionId;
    public override string? UserIdentifier => null;

    public override ClaimsPrincipal User { get; } = new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, userId.ToString(CultureInfo.InvariantCulture))
    ], "test"));

    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
    public override IFeatureCollection Features => new FeatureCollection();
    public override CancellationToken ConnectionAborted => CancellationToken.None;

    public override void Abort()
    {
    }
}

/// <inheritdoc />
/// <summary>
/// Stub IHubCallerClients that records sent messages for assertions.
/// </summary>
public class StubHubCallerClients : IHubCallerClients
{
    public Dictionary<string, List<(string method, object?[] args)>> SentToGroups { get; } = [];

    private IClientProxy CreateProxy(string? groupName = null)
    {
        return new RecordingClientProxy(groupName != null ? SentToGroups : null, groupName);
    }

    public IClientProxy Caller => CreateProxy();
    public IClientProxy All => CreateProxy();

    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds)
    {
        return CreateProxy();
    }

    public IClientProxy Client(string connectionId)
    {
        return CreateProxy();
    }

    public IClientProxy Clients(IReadOnlyList<string> connectionIds)
    {
        return CreateProxy();
    }

    public IClientProxy Group(string groupName)
    {
        if (!SentToGroups.ContainsKey(groupName))
        {
            SentToGroups[groupName] = [];
        }

        return CreateProxy(groupName);
    }

    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds)
    {
        return CreateProxy(groupName);
    }

    public IClientProxy Groups(IReadOnlyList<string> groupNames)
    {
        return CreateProxy();
    }

    public IClientProxy OthersInGroup(string groupName)
    {
        return CreateProxy(groupName);
    }

    public IClientProxy Others => CreateProxy();

    public IClientProxy User(string userId)
    {
        return CreateProxy();
    }

    public IClientProxy Users(IReadOnlyList<string> userIds)
    {
        return CreateProxy();
    }
}

public class RecordingClientProxy(Dictionary<string, List<(string method, object?[] args)>>? groups, string? groupName)
    : IClientProxy
{
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        // ReSharper disable once InvertIf
        if (groups != null && groupName != null)
        {
            if (!groups.ContainsKey(groupName))
            {
                groups[groupName] = [];
            }

            groups[groupName].Add((method, args));
        }

        return Task.CompletedTask;
    }
}

[TestClass]
public class ChatHubTests
{
    private static (ChatHub hub, ApplicationDbContext db, StubGroupManager groups, StubHubCallerClients clients) CreateHub(long userId = 1L)
    {
        var db = TestDbContextFactory.Create();
        var hubContext = new StubHubContext();
        var relayService = new MessageRelayService(db, hubContext);
        var presence = new PresenceService();
        var rateLimitService = new RateLimitService();
        var hub = new ChatHub(relayService, db, presence, rateLimitService);

        var groups = new StubGroupManager();
        var clients = new StubHubCallerClients();
        var context = new StubHubCallerContext(userId);

        // Set hub context using reflection (Hub properties are set by SignalR runtime normally)
        typeof(Hub).GetProperty("Context")?.SetValue(hub, context);
        typeof(Hub).GetProperty("Groups")?.SetValue(hub, groups);
        typeof(Hub).GetProperty("Clients")?.SetValue(hub, clients);

        return (hub, db, groups, clients);
    }

    // --- RegisterDevice ---

    [TestMethod]
    public async Task RegisterDevice_OwnDevice_AddsToGroups()
    {
        var (hub, db, groups, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedDevice(db, 10L, 1L, "MyDevice");

        await hub.RegisterDevice(10L);

        Assert.AreEqual(2, groups.Added.Count);
        Assert.IsTrue(groups.Added.Any(static g => g.groupName == "device_10"));
        Assert.IsTrue(groups.Added.Any(static g => g.groupName == "user_1"));
    }

    [TestMethod]
    public async Task RegisterDevice_OtherUsersDevice_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedUser(db, 2L, "user2");
        await TestDbContextFactory.SeedDevice(db, 20L, 2L, "OtherDevice");

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.RegisterDevice(20L));
        StringAssert.Contains(ex.Message, "not found");
    }

    [TestMethod]
    public async Task RegisterDevice_InactiveDevice_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        var device = await TestDbContextFactory.SeedDevice(db, 10L, 1L, "Inactive");
        device.IsActive = false;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.RegisterDevice(10L));
        StringAssert.Contains(ex.Message, "not found");
    }

    [TestMethod]
    public async Task RegisterDevice_NonExistentDevice_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.RegisterDevice(999L));
        StringAssert.Contains(ex.Message, "not found");
    }

    // --- SendMessage ---

    [TestMethod]
    public async Task SendMessage_AsParticipant_ReturnsResult()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1L, "sender");
        await TestDbContextFactory.SeedDevice(db, 10L, 1L, "SenderDevice");
        await TestDbContextFactory.SeedUser(db, 2L, "recipient");
        await TestDbContextFactory.SeedDevice(db, 20L, 2L, "RecipientDevice");
        await TestDbContextFactory.SeedConversation(db, 100L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 2L);

        var request = new SendMessageRequest { ConversationId = 100L, SenderDeviceId = 10L, RecipientDeviceId = 20L, Ciphertext = Convert.ToBase64String(new byte[] { 1, 2, 3 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        var result = await hub.SendMessage(request);

        Assert.AreNotEqual(0L, result.MessageId);
        Assert.AreEqual(1, result.SequenceNumber);
    }

    [TestMethod]
    public async Task SendMessage_NotParticipant_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1L, "sender");
        await TestDbContextFactory.SeedDevice(db, 10L, 1L, "SenderDevice");
        await TestDbContextFactory.SeedConversation(db, 100L);
        // User 1 is NOT a participant

        var request = new SendMessageRequest { ConversationId = 100L, SenderDeviceId = 10L, RecipientDeviceId = 20L, Ciphertext = Convert.ToBase64String(new byte[] { 1 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SendMessage(request));
        StringAssert.Contains(ex.Message, "not a participant");
    }

    [TestMethod]
    public async Task SendMessage_DeviceNotOwnedBySender_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1L, "sender");
        await TestDbContextFactory.SeedUser(db, 2L, "other");
        await TestDbContextFactory.SeedDevice(db, 20L, 2L, "OtherDevice"); // Device belongs to user 2
        await TestDbContextFactory.SeedConversation(db, 100L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L);

        var request = new SendMessageRequest { ConversationId = 100L, SenderDeviceId = 20L, RecipientDeviceId = 30L, Ciphertext = Convert.ToBase64String(new byte[] { 1 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SendMessage(request));
        StringAssert.Contains(ex.Message, "Sender device not found");
    }

    // --- AcknowledgeDelivery ---

    [TestMethod]
    public async Task AcknowledgeDelivery_ValidMessage_MarksDelivered()
    {
        var (hub, db, _, clients) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedDevice(db, 10L, 1L);

        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 500L, ConversationId = 100L, SenderDeviceId = 20L, RecipientDeviceId = 10L,
            Ciphertext = [1, 2], SequenceNumber = 1, ServerTimestamp = DateTimeOffset.UtcNow,
            IsDelivered = false
        });
        if (TestContext != null) await db.SaveChangesAsync(TestContext.CancellationToken);

        await hub.AcknowledgeDelivery(500L);

        var msg = await db.EncryptedMessages.FindAsync(500L);
        Assert.IsTrue(msg?.IsDelivered);
        // Verify notification was sent to the sender's device group
        Assert.IsTrue(clients.SentToGroups.ContainsKey("device_20"));
        Assert.IsTrue(clients.SentToGroups["device_20"].Any(static s => s.method == "MessageDelivered"));
    }

    [TestMethod]
    public async Task AcknowledgeDelivery_MessageNotFound_ThrowsHubException()
    {
        var (hub, _, _, _) = CreateHub();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.AcknowledgeDelivery(999L));
        StringAssert.Contains(ex.Message, "not found");
    }

    // --- AdvanceReadPointer ---

    [TestMethod]
    public async Task AdvanceReadPointer_ValidMessage_NotifiesSenderDevice()
    {
        var (hub, db, _, clients) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedDevice(db, 10L, 1L); // Recipient device belongs to hub user
        await TestDbContextFactory.SeedConversation(db, 100L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L);

        // Register device so GetDeviceId() works
        await hub.RegisterDevice(10L);

        var ts = DateTimeOffset.UtcNow;
        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 500L, ConversationId = 100L, SenderDeviceId = 20L, RecipientDeviceId = 10L,
            Ciphertext = [1, 2], SequenceNumber = 1, ServerTimestamp = ts,
            IsDelivered = true
        });
        await db.SaveChangesAsync();

        await hub.AdvanceReadPointer(100L, 1);

        Assert.IsTrue(clients.SentToGroups.ContainsKey("device_20"));
        Assert.IsTrue(clients.SentToGroups["device_20"].Any(static s => s.method == "MessageRead"));

        // Verify read pointer was created
        var pointer = await db.ConversationReadPointers
            .FirstOrDefaultAsync(static p => p.UserId == 1L && p.ConversationId == 100L);
        Assert.IsNotNull(pointer);
        Assert.AreEqual(1L, pointer.LastReadSequenceNumber);
    }

    // --- TypingIndicator ---

    [TestMethod]
    public async Task TypingIndicator_NotifiesOtherParticipants()
    {
        var (hub, db, _, clients) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedUser(db, 2L, "user2");
        await TestDbContextFactory.SeedUser(db, 3L, "user3");
        await TestDbContextFactory.SeedConversation(db, 100L, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 2L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 3L);

        await hub.TypingIndicator(100L);

        // Should notify user_2 and user_3 but NOT user_1 (the sender)
        Assert.IsTrue(clients.SentToGroups.ContainsKey("user_2"));
        Assert.IsTrue(clients.SentToGroups.ContainsKey("user_3"));
        Assert.IsFalse(clients.SentToGroups.ContainsKey("user_1"));
        Assert.IsTrue(clients.SentToGroups["user_2"].Any(static s => s.method == "UserTyping"));
        Assert.IsTrue(clients.SentToGroups["user_3"].Any(static s => s.method == "UserTyping"));
    }

    // --- Ownership verification tests ---

    [TestMethod]
    public async Task SendMessage_OtherUsersDevice_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub(); // userId = 1
        await TestDbContextFactory.SeedUser(db, 1L, "sender");
        await TestDbContextFactory.SeedUser(db, 2L, "other");
        await TestDbContextFactory.SeedDevice(db, 10L, 1L, "MyDevice");
        await TestDbContextFactory.SeedDevice(db, 20L, 2L, "OtherDevice");
        await TestDbContextFactory.SeedConversation(db, 100L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 2L);

        // Try to send from user 2's device as user 1
        var request = new SendMessageRequest { ConversationId = 100L, SenderDeviceId = 20L, RecipientDeviceId = 10L, Ciphertext = Convert.ToBase64String(new byte[] { 1, 2, 3 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SendMessage(request));
        StringAssert.Contains(ex.Message, "Sender device not found");
    }

    [TestMethod]
    public async Task AcknowledgeDelivery_OtherUsersMessage_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub(); // userId = 1
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedUser(db, 2L, "user2");
        await TestDbContextFactory.SeedDevice(db, 10L, 1L);
        await TestDbContextFactory.SeedDevice(db, 20L, 2L); // Belongs to user 2

        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 500L, ConversationId = 100L, SenderDeviceId = 10L, RecipientDeviceId = 20L,
            Ciphertext = [1, 2], SequenceNumber = 1, ServerTimestamp = DateTimeOffset.UtcNow,
            IsDelivered = false
        });
        await db.SaveChangesAsync();

        // User 1 tries to acknowledge a message destined for user 2's device
        var ex = await Assert.ThrowsAsync<HubException>(() => hub.AcknowledgeDelivery(500L));
        StringAssert.Contains(ex.Message, "does not belong");
    }

    [TestMethod]
    public async Task AdvanceReadPointer_NoUnreadMessages_CreatesPointerWithZeroUnread()
    {
        var (hub, db, _, _) = CreateHub(); // userId = 1
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedUser(db, 2L, "user2");
        await TestDbContextFactory.SeedDevice(db, 10L, 1L);
        await TestDbContextFactory.SeedDevice(db, 20L, 2L);
        await TestDbContextFactory.SeedConversation(db, 100L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L);

        // Register device so GetDeviceId() works
        await hub.RegisterDevice(10L);

        var ts = DateTimeOffset.UtcNow;
        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 500L, ConversationId = 100L, SenderDeviceId = 10L, RecipientDeviceId = 20L,
            Ciphertext = [1, 2], SequenceNumber = 1, ServerTimestamp = ts,
            IsDelivered = true
        });
        await db.SaveChangesAsync();

        // User 1 sent message 500, so advancing their pointer should not notify anyone
        await hub.AdvanceReadPointer(100L, 1);

        // Pointer should exist with 0 unread
        var pointer = await db.ConversationReadPointers
            .FirstOrDefaultAsync(static p => p.UserId == 1L && p.ConversationId == 100L);
        Assert.IsNotNull(pointer);
        Assert.AreEqual(0, pointer.UnreadCount);
    }

    // --- OnDisconnectedAsync ---

    [TestMethod]
    public async Task OnDisconnectedAsync_CompletesWithoutError()
    {
        var (hub, _, _, _) = CreateHub();

        // Should not throw
        await hub.OnDisconnectedAsync(null);
    }

    // --- Media Message Tests ---

    [TestMethod]
    public async Task SendMessage_Media_FileName_MimeType_Null_On_Request()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1L, "sender");
        await TestDbContextFactory.SeedDevice(db, 10L, 1L, "SenderDevice");
        await TestDbContextFactory.SeedUser(db, 2L, "recipient");
        await TestDbContextFactory.SeedDevice(db, 20L, 2L, "RecipientDevice");
        await TestDbContextFactory.SeedConversation(db, 100L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 2L);

        // Send media message with null FileName and MimeType (metadata inside ciphertext)
        var request = new SendMessageRequest { ConversationId = 100L, SenderDeviceId = 10L, RecipientDeviceId = 20L, Ciphertext = Convert.ToBase64String(new byte[] { 1, 2, 3 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Image };

        var result = await hub.SendMessage(request);

        Assert.AreNotEqual(0L, result.MessageId);
    }

    public TestContext? TestContext { get; set; }
}
