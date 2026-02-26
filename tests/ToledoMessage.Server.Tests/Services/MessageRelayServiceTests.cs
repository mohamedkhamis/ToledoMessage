using Microsoft.AspNetCore.SignalR;
using ToledoMessage.Data;
using ToledoMessage.Hubs;
using ToledoMessage.Models;
using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;
using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Server.Tests.Services;

/// <summary>
/// Stub IHubContext that does nothing (messages sent to clients are ignored).
/// </summary>
public class StubHubContext : IHubContext<ChatHub>
{
    public IHubClients Clients { get; } = new StubHubClients();
    public IGroupManager Groups => throw new NotImplementedException();
}

public class StubHubClients : IHubClients
{
    public IClientProxy All => new StubClientProxy();
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new StubClientProxy();
    public IClientProxy Client(string connectionId) => new StubClientProxy();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new StubClientProxy();
    public IClientProxy Group(string groupName) => new StubClientProxy();
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new StubClientProxy();
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => new StubClientProxy();
    public IClientProxy User(string userId) => new StubClientProxy();
    public IClientProxy Users(IReadOnlyList<string> userIds) => new StubClientProxy();
}

public class StubClientProxy : IClientProxy
{
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class MessageRelayServiceTests
{
    private static (ApplicationDbContext db, MessageRelayService service) CreateService()
    {
        var db = TestDbContextFactory.Create();
        var service = new MessageRelayService(db, new StubHubContext());
        return (db, service);
    }

    [Fact]
    public async Task StoreMessage_CreatesMessageWithSequenceNumber()
    {
        var (db, service) = CreateService();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        await TestDbContextFactory.SeedUser(db, 2m, "user2");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m);
        await TestDbContextFactory.SeedConversation(db, 100m);

        var request = new SendMessageRequest(100m, 10m, 20m,
            Convert.ToBase64String(new byte[] { 1, 2, 3 }), MessageType.NormalMessage, ContentType.Text);

        var message = await service.StoreMessage(10m, request);

        Assert.NotEqual(0m, message.Id);
        Assert.Equal(1, message.SequenceNumber);
        Assert.Equal(100m, message.ConversationId);
        Assert.False(message.IsDelivered);
    }

    [Fact]
    public async Task StoreMessage_IncrementsSequenceNumber()
    {
        var (db, service) = CreateService();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        await TestDbContextFactory.SeedConversation(db, 100m);

        var request = new SendMessageRequest(100m, 10m, 20m,
            Convert.ToBase64String(new byte[] { 1 }), MessageType.NormalMessage, ContentType.Text);

        var msg1 = await service.StoreMessage(10m, request);
        var msg2 = await service.StoreMessage(10m, request);
        var msg3 = await service.StoreMessage(10m, request);

        Assert.Equal(1, msg1.SequenceNumber);
        Assert.Equal(2, msg2.SequenceNumber);
        Assert.Equal(3, msg3.SequenceNumber);
    }

    [Fact]
    public async Task GetPendingMessages_ReturnsUndeliveredOnly()
    {
        var (db, service) = CreateService();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        await TestDbContextFactory.SeedConversation(db, 100m);

        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 1m, ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m,
            Ciphertext = [1], SequenceNumber = 1, ServerTimestamp = DateTimeOffset.UtcNow, IsDelivered = false
        });
        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 2m, ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m,
            Ciphertext = [2], SequenceNumber = 2, ServerTimestamp = DateTimeOffset.UtcNow, IsDelivered = true
        });
        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 3m, ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m,
            Ciphertext = [3], SequenceNumber = 3, ServerTimestamp = DateTimeOffset.UtcNow, IsDelivered = false
        });
        await db.SaveChangesAsync();

        var pending = await service.GetPendingMessages(20m);

        Assert.Equal(2, pending.Count);
        Assert.Equal(1m, pending[0].Id);
        Assert.Equal(3m, pending[1].Id);
    }

    [Fact]
    public async Task AcknowledgeDelivery_MarksAsDelivered()
    {
        var (db, service) = CreateService();
        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 1m, ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m,
            Ciphertext = [1], SequenceNumber = 1, ServerTimestamp = DateTimeOffset.UtcNow, IsDelivered = false
        });
        await db.SaveChangesAsync();

        var result = await service.AcknowledgeDelivery(1m);

        Assert.NotNull(result);
        Assert.True(result.IsDelivered);
        Assert.NotNull(result.DeliveredAt);
    }

    [Fact]
    public async Task AcknowledgeDelivery_MessageNotFound_ReturnsNull()
    {
        var (db, service) = CreateService();
        var result = await service.AcknowledgeDelivery(999m);
        Assert.Null(result);
    }

    [Fact]
    public async Task CleanupExpiredMessages_DeletesExpiredDisappearingMessages()
    {
        var (db, service) = CreateService();

        var conversation = await TestDbContextFactory.SeedConversation(db, 100m);
        conversation.DisappearingTimerSeconds = 1; // 1 second timer
        await db.SaveChangesAsync();

        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 1m, ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m,
            Ciphertext = [1], SequenceNumber = 1,
            ServerTimestamp = DateTimeOffset.UtcNow.AddSeconds(-10), // 10 seconds ago
            IsDelivered = true
        });
        await db.SaveChangesAsync();

        var deleted = await service.CleanupExpiredMessages();

        Assert.Equal(1, deleted);
        Assert.Empty(db.EncryptedMessages);
    }

    [Fact]
    public async Task CleanupExpiredMessages_KeepsNonExpiredMessages()
    {
        var (db, service) = CreateService();

        var conversation = await TestDbContextFactory.SeedConversation(db, 100m);
        conversation.DisappearingTimerSeconds = 3600; // 1 hour timer
        await db.SaveChangesAsync();

        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 1m, ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m,
            Ciphertext = [1], SequenceNumber = 1,
            ServerTimestamp = DateTimeOffset.UtcNow, // just now
            IsDelivered = true
        });
        await db.SaveChangesAsync();

        var deleted = await service.CleanupExpiredMessages();

        Assert.Equal(0, deleted);
        Assert.Single(db.EncryptedMessages);
    }

    [Fact]
    public async Task CleanupExpiredMessages_SkipsUndeliveredMessages()
    {
        var (db, service) = CreateService();

        var conversation = await TestDbContextFactory.SeedConversation(db, 100m);
        conversation.DisappearingTimerSeconds = 1;
        await db.SaveChangesAsync();

        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 1m, ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m,
            Ciphertext = [1], SequenceNumber = 1,
            ServerTimestamp = DateTimeOffset.UtcNow.AddSeconds(-10),
            IsDelivered = false // Not delivered yet
        });
        await db.SaveChangesAsync();

        var deleted = await service.CleanupExpiredMessages();

        Assert.Equal(0, deleted);
    }
}
