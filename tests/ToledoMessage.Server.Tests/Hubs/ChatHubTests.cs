using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using ToledoMessage.Data;
using ToledoMessage.Hubs;
using ToledoMessage.Models;
using ToledoMessage.Services;
using ToledoMessage.Server.Tests.Services;
using ToledoMessage.Shared.DTOs;
using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Server.Tests.Hubs;

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

/// <summary>
/// Stub HubCallerContext with configurable user and connection ID.
/// </summary>
public class StubHubCallerContext : HubCallerContext
{
    private readonly ClaimsPrincipal _user;

    public StubHubCallerContext(decimal userId, string connectionId = "test-connection")
    {
        _user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "test"));
        ConnectionId = connectionId;
    }

    public override string ConnectionId { get; }
    public override string? UserIdentifier => null;
    public override ClaimsPrincipal? User => _user;
    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
    public override IFeatureCollection Features => throw new NotImplementedException();
    public override CancellationToken ConnectionAborted => CancellationToken.None;
    public override void Abort() { }
}

/// <summary>
/// Stub IHubCallerClients that records sent messages for assertions.
/// </summary>
public class StubHubCallerClients : IHubCallerClients
{
    public List<(string method, object?[] args)> SentToAll { get; } = [];
    public Dictionary<string, List<(string method, object?[] args)>> SentToGroups { get; } = [];

    private IClientProxy CreateProxy(string? groupName = null)
    {
        return new RecordingClientProxy(groupName != null ? SentToGroups : null, groupName);
    }

    public IClientProxy Caller => CreateProxy();
    public IClientProxy All => CreateProxy();
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => CreateProxy();
    public IClientProxy Client(string connectionId) => CreateProxy();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => CreateProxy();

    public IClientProxy Group(string groupName)
    {
        if (!SentToGroups.ContainsKey(groupName))
            SentToGroups[groupName] = [];
        return CreateProxy(groupName);
    }

    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => CreateProxy(groupName);
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => CreateProxy();
    public IClientProxy OthersInGroup(string groupName) => CreateProxy(groupName);
    public IClientProxy Others => CreateProxy();
    public IClientProxy User(string userId) => CreateProxy();
    public IClientProxy Users(IReadOnlyList<string> userIds) => CreateProxy();
}

public class RecordingClientProxy : IClientProxy
{
    private readonly Dictionary<string, List<(string method, object?[] args)>>? _groups;
    private readonly string? _groupName;

    public RecordingClientProxy(Dictionary<string, List<(string method, object?[] args)>>? groups, string? groupName)
    {
        _groups = groups;
        _groupName = groupName;
    }

    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        if (_groups != null && _groupName != null)
        {
            if (!_groups.ContainsKey(_groupName))
                _groups[_groupName] = [];
            _groups[_groupName].Add((method, args));
        }
        return Task.CompletedTask;
    }
}

public class ChatHubTests
{
    private static (ChatHub hub, ApplicationDbContext db, StubGroupManager groups, StubHubCallerClients clients) CreateHub(decimal userId = 1m)
    {
        var db = TestDbContextFactory.Create();
        var hubContext = new StubHubContext();
        var relayService = new MessageRelayService(db, hubContext);
        var hub = new ChatHub(relayService, db);

        var groups = new StubGroupManager();
        var clients = new StubHubCallerClients();
        var context = new StubHubCallerContext(userId);

        // Set hub context using reflection (Hub properties are set by SignalR runtime normally)
        typeof(Hub).GetProperty("Context")!.SetValue(hub, context);
        typeof(Hub).GetProperty("Groups")!.SetValue(hub, groups);
        typeof(Hub).GetProperty("Clients")!.SetValue(hub, clients);

        return (hub, db, groups, clients);
    }

    // --- RegisterDevice ---

    [Fact]
    public async Task RegisterDevice_OwnDevice_AddsToGroups()
    {
        var (hub, db, groups, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m, "MyDevice");

        await hub.RegisterDevice(10m);

        Assert.Equal(2, groups.Added.Count);
        Assert.Contains(groups.Added, g => g.groupName == "device_10");
        Assert.Contains(groups.Added, g => g.groupName == "user_1");
    }

    [Fact]
    public async Task RegisterDevice_OtherUsersDevice_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedUser(db, 2m, "user2");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m, "OtherDevice");

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.RegisterDevice(20m));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task RegisterDevice_InactiveDevice_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        var device = await TestDbContextFactory.SeedDevice(db, 10m, 1m, "Inactive");
        device.IsActive = false;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.RegisterDevice(10m));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task RegisterDevice_NonExistentDevice_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.RegisterDevice(999m));
        Assert.Contains("not found", ex.Message);
    }

    // --- SendMessage ---

    [Fact]
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

        var request = new SendMessageRequest(100m, 10m, 20m,
            Convert.ToBase64String(new byte[] { 1, 2, 3 }), MessageType.NormalMessage, ContentType.Text);

        var result = await hub.SendMessage(request);

        Assert.NotEqual(0m, result.MessageId);
        Assert.Equal(1, result.SequenceNumber);
    }

    [Fact]
    public async Task SendMessage_NotParticipant_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1m, "sender");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m, "SenderDevice");
        await TestDbContextFactory.SeedConversation(db, 100m);
        // User 1 is NOT a participant

        var request = new SendMessageRequest(100m, 10m, 20m,
            Convert.ToBase64String(new byte[] { 1 }), MessageType.NormalMessage, ContentType.Text);

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SendMessage(request));
        Assert.Contains("not a participant", ex.Message);
    }

    [Fact]
    public async Task SendMessage_NoActiveDevice_ThrowsHubException()
    {
        var (hub, db, _, _) = CreateHub();
        await TestDbContextFactory.SeedUser(db, 1m, "sender");
        // No device seeded for user 1
        await TestDbContextFactory.SeedConversation(db, 100m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);

        var request = new SendMessageRequest(100m, 0m, 20m,
            Convert.ToBase64String(new byte[] { 1 }), MessageType.NormalMessage, ContentType.Text);

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SendMessage(request));
        Assert.Contains("No active device", ex.Message);
    }

    // --- AcknowledgeDelivery ---

    [Fact]
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
        await db.SaveChangesAsync();

        await hub.AcknowledgeDelivery(500m);

        var msg = await db.EncryptedMessages.FindAsync(500m);
        Assert.True(msg!.IsDelivered);
        // Verify notification was sent to the sender's device group
        Assert.True(clients.SentToGroups.ContainsKey("device_20"));
        Assert.Contains(clients.SentToGroups["device_20"], s => s.method == "MessageDelivered");
    }

    [Fact]
    public async Task AcknowledgeDelivery_MessageNotFound_ThrowsHubException()
    {
        var (hub, _, _, _) = CreateHub();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.AcknowledgeDelivery(999m));
        Assert.Contains("not found", ex.Message);
    }

    // --- AcknowledgeRead ---

    [Fact]
    public async Task AcknowledgeRead_ValidMessage_NotifiesSenderDevice()
    {
        var (hub, db, _, clients) = CreateHub();

        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 500m, ConversationId = 100m, SenderDeviceId = 20m, RecipientDeviceId = 10m,
            Ciphertext = [1, 2], SequenceNumber = 1, ServerTimestamp = DateTimeOffset.UtcNow,
            IsDelivered = true
        });
        await db.SaveChangesAsync();

        await hub.AcknowledgeRead(500m);

        Assert.True(clients.SentToGroups.ContainsKey("device_20"));
        Assert.Contains(clients.SentToGroups["device_20"], s => s.method == "MessageRead");
    }

    [Fact]
    public async Task AcknowledgeRead_MessageNotFound_ThrowsHubException()
    {
        var (hub, _, _, _) = CreateHub();

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.AcknowledgeRead(999m));
        Assert.Contains("not found", ex.Message);
    }

    // --- TypingIndicator ---

    [Fact]
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
        Assert.True(clients.SentToGroups.ContainsKey("user_2"));
        Assert.True(clients.SentToGroups.ContainsKey("user_3"));
        Assert.False(clients.SentToGroups.ContainsKey("user_1"));
        Assert.Contains(clients.SentToGroups["user_2"], s => s.method == "UserTyping");
        Assert.Contains(clients.SentToGroups["user_3"], s => s.method == "UserTyping");
    }

    // --- OnDisconnectedAsync ---

    [Fact]
    public async Task OnDisconnectedAsync_CompletesWithoutError()
    {
        var (hub, _, _, _) = CreateHub();

        // Should not throw
        await hub.OnDisconnectedAsync(null);
        Assert.True(true);
    }
}
