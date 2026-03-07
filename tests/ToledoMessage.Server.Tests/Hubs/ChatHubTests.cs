using System.Diagnostics.CodeAnalysis;
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
public class StubHubCallerContext(decimal userId, string connectionId = "test-connection") : HubCallerContext
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
[SuppressMessage("ReSharper", "RemoveRedundantBraces")]
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

[SuppressMessage("ReSharper", "RemoveRedundantBraces")]
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
    private static (ChatHub hub, ApplicationDbContext db, StubGroupManager groups, StubHubCallerClients clients) CreateHub(decimal userId = 1m)
    {
        var db = TestDbContextFactory.Create();
        var hubContext = new StubHubContext();
        var relayService = new MessageRelayService(db, hubContext);
        var presence = new PresenceService();
        var hub = new ChatHub(relayService, db, presence);

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
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m, "MyDevice");

        await hub.RegisterDevice(10m);

        Assert.AreEqual(2, groups.Added.Count);
        Assert.IsTrue(groups.Added.Any(static g => g.groupName == "device_10"));
        Assert.IsTrue(groups.Added.Any(static g => g.groupName == "user_1"));
    }

    [TestMethod]
    public async Task RegisterDevice_OtherUsersDevice_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedUser(db, 2m, "user2");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m, "OtherDevice");

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.RegisterDevice(20m));
        StringAssert.Contains(ex.Message, "not found");
    }

    [TestMethod]
    public async Task RegisterDevice_InactiveDevice_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        var device = await TestDbContextFactory.SeedDevice(db, 10m, 1m, "Inactive");
        device.IsActive = false;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.RegisterDevice(10m));
        StringAssert.Contains(ex.Message, "not found");
    }

    [TestMethod]
    public async Task RegisterDevice_NonExistentDevice_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.RegisterDevice(999m));
        StringAssert.Contains(ex.Message, "not found");
    }

    // --- SendMessage ---

    [TestMethod]
    public async Task SendMessage_AsParticipant_ReturnsResult()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1m, "sender");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m, "SenderDevice");
        await TestDbContextFactory.SeedUser(db, 2m, "recipient");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m, "RecipientDevice");
        await TestDbContextFactory.SeedConversation(db, 100m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);

        var request = new SendMessageRequest { ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m, Ciphertext = Convert.ToBase64String(new byte[] { 1, 2, 3 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        var result = await hub.SendMessage(request);

        Assert.AreNotEqual(0m, result.MessageId);
        Assert.AreEqual(1, result.SequenceNumber);
    }

    [TestMethod]
    public async Task SendMessage_NotParticipant_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1m, "sender");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m, "SenderDevice");
        await TestDbContextFactory.SeedConversation(db, 100m);
        // User 1 is NOT a participant

        var request = new SendMessageRequest { ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m, Ciphertext = Convert.ToBase64String(new byte[] { 1 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SendMessage(request));
        StringAssert.Contains(ex.Message, "not a participant");
    }

    [TestMethod]
    public async Task SendMessage_DeviceNotOwnedBySender_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1m, "sender");
        await TestDbContextFactory.SeedUser(db, 2m, "other");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m, "OtherDevice"); // Device belongs to user 2
        await TestDbContextFactory.SeedConversation(db, 100m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);

        var request = new SendMessageRequest { ConversationId = 100m, SenderDeviceId = 20m, RecipientDeviceId = 30m, Ciphertext = Convert.ToBase64String(new byte[] { 1 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SendMessage(request));
        StringAssert.Contains(ex.Message, "Sender device not found");
    }

    // --- AcknowledgeDelivery ---

    [TestMethod]
    public async Task AcknowledgeDelivery_ValidMessage_MarksDelivered()
    {
        var (hub, db, _, clients) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);

        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 500m, ConversationId = 100m, SenderDeviceId = 20m, RecipientDeviceId = 10m,
            Ciphertext = [1, 2], SequenceNumber = 1, ServerTimestamp = DateTimeOffset.UtcNow,
            IsDelivered = false
        });
        await db.SaveChangesAsync(TestContext.CancellationToken);

        await hub.AcknowledgeDelivery(500m);

        var msg = await db.EncryptedMessages.FindAsync(500m);
        Assert.IsTrue(msg?.IsDelivered);
        // Verify notification was sent to the sender's device group
        Assert.IsTrue(clients.SentToGroups.ContainsKey("device_20"));
        Assert.IsTrue(clients.SentToGroups["device_20"].Any(static s => s.method == "MessageDelivered"));
    }

    [TestMethod]
    public async Task AcknowledgeDelivery_MessageNotFound_ThrowsHubException()
    {
        var (hub, _, _, _) = CreateHub();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.AcknowledgeDelivery(999m));
        StringAssert.Contains(ex.Message, "not found");
    }

    // --- AdvanceReadPointer ---

    [TestMethod]
    public async Task AdvanceReadPointer_ValidMessage_NotifiesSenderDevice()
    {
        var (hub, db, _, clients) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m); // Recipient device belongs to hub user
        await TestDbContextFactory.SeedConversation(db, 100m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);

        // Register device so GetDeviceId() works
        await hub.RegisterDevice(10m);

        var ts = DateTimeOffset.UtcNow;
        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 500m, ConversationId = 100m, SenderDeviceId = 20m, RecipientDeviceId = 10m,
            Ciphertext = [1, 2], SequenceNumber = 1, ServerTimestamp = ts,
            IsDelivered = true
        });
        await db.SaveChangesAsync();

        await hub.AdvanceReadPointer(100m, 1);

        Assert.IsTrue(clients.SentToGroups.ContainsKey("device_20"));
        Assert.IsTrue(clients.SentToGroups["device_20"].Any(static s => s.method == "MessageRead"));

        // Verify read pointer was created
        var pointer = await db.ConversationReadPointers
            .FirstOrDefaultAsync(p => p.UserId == 1m && p.ConversationId == 100m);
        Assert.IsNotNull(pointer);
        Assert.AreEqual(1L, pointer.LastReadSequenceNumber);
    }

    // --- TypingIndicator ---

    [TestMethod]
    public async Task TypingIndicator_NotifiesOtherParticipants()
    {
        var (hub, db, _, clients) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedUser(db, 2m, "user2");
        await TestDbContextFactory.SeedUser(db, 3m, "user3");
        await TestDbContextFactory.SeedConversation(db, 100m, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 3m);

        await hub.TypingIndicator(100m);

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
        await TestDbContextFactory.SeedUser(db, 1m, "sender");
        await TestDbContextFactory.SeedUser(db, 2m, "other");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m, "MyDevice");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m, "OtherDevice");
        await TestDbContextFactory.SeedConversation(db, 100m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);

        // Try to send from user 2's device as user 1
        var request = new SendMessageRequest { ConversationId = 100m, SenderDeviceId = 20m, RecipientDeviceId = 10m, Ciphertext = Convert.ToBase64String(new byte[] { 1, 2, 3 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SendMessage(request));
        StringAssert.Contains(ex.Message, "Sender device not found");
    }

    [TestMethod]
    public async Task AcknowledgeDelivery_OtherUsersMessage_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub(); // userId = 1
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedUser(db, 2m, "user2");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        await TestDbContextFactory.SeedDevice(db, 20m, 2m); // Belongs to user 2

        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 500m, ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m,
            Ciphertext = [1, 2], SequenceNumber = 1, ServerTimestamp = DateTimeOffset.UtcNow,
            IsDelivered = false
        });
        await db.SaveChangesAsync();

        // User 1 tries to acknowledge a message destined for user 2's device
        var ex = await Assert.ThrowsAsync<HubException>(() => hub.AcknowledgeDelivery(500m));
        StringAssert.Contains(ex.Message, "does not belong");
    }

    [TestMethod]
    public async Task AdvanceReadPointer_NoUnreadMessages_CreatesPointerWithZeroUnread()
    {
        var (hub, db, _, _) = CreateHub(); // userId = 1
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedUser(db, 2m, "user2");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        await TestDbContextFactory.SeedDevice(db, 20m, 2m);
        await TestDbContextFactory.SeedConversation(db, 100m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);

        // Register device so GetDeviceId() works
        await hub.RegisterDevice(10m);

        var ts = DateTimeOffset.UtcNow;
        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 500m, ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m,
            Ciphertext = [1, 2], SequenceNumber = 1, ServerTimestamp = ts,
            IsDelivered = true
        });
        await db.SaveChangesAsync();

        // User 1 sent message 500, so advancing their pointer should not notify anyone
        await hub.AdvanceReadPointer(100m, 1);

        // Pointer should exist with 0 unread
        var pointer = await db.ConversationReadPointers
            .FirstOrDefaultAsync(p => p.UserId == 1m && p.ConversationId == 100m);
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
        await TestDbContextFactory.SeedUser(db, 1m, "sender");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m, "SenderDevice");
        await TestDbContextFactory.SeedUser(db, 2m, "recipient");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m, "RecipientDevice");
        await TestDbContextFactory.SeedConversation(db, 100m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);

        // Send media message with null FileName and MimeType (metadata inside ciphertext)
        var request = new SendMessageRequest { ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m, Ciphertext = Convert.ToBase64String(new byte[] { 1, 2, 3 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Image };

        var result = await hub.SendMessage(request);

        Assert.AreNotEqual(0m, result.MessageId);
    }

    public TestContext TestContext { get; set; }
}
