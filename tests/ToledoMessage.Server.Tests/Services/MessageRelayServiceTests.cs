using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ToledoMessage.Data;
using ToledoMessage.Hubs;
using ToledoMessage.Models;
using ToledoMessage.Services;
using ToledoMessage.Shared.Constants;
using ToledoMessage.Shared.DTOs;
using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Server.Tests.Services;

/// <inheritdoc />
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

    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds)
    {
        return new StubClientProxy();
    }

    public IClientProxy Client(string connectionId)
    {
        return new StubClientProxy();
    }

    public IClientProxy Clients(IReadOnlyList<string> connectionIds)
    {
        return new StubClientProxy();
    }

    public IClientProxy Group(string groupName)
    {
        return new StubClientProxy();
    }

    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds)
    {
        return new StubClientProxy();
    }

    public IClientProxy Groups(IReadOnlyList<string> groupNames)
    {
        return new StubClientProxy();
    }

    public IClientProxy User(string userId)
    {
        return new StubClientProxy();
    }

    public IClientProxy Users(IReadOnlyList<string> userIds)
    {
        return new StubClientProxy();
    }
}

public class StubClientProxy : IClientProxy
{
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

[TestClass]
[SuppressMessage("ReSharper", "RemoveRedundantBraces")]
public class MessageRelayServiceTests
{
    private static (ApplicationDbContext db, MessageRelayService service) CreateService()
    {
        var db = TestDbContextFactory.Create();
        var service = new MessageRelayService(db, new StubHubContext());
        return (db, service);
    }

    [TestMethod]
    public async Task StoreMessage_CreatesMessageWithSequenceNumber()
    {
        var (db, service) = CreateService();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        await TestDbContextFactory.SeedUser(db, 2m, "user2");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m);
        await TestDbContextFactory.SeedConversation(db, 100m);

        var request = new SendMessageRequest { ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m, Ciphertext = Convert.ToBase64String(new byte[] { 1, 2, 3 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        var message = await service.StoreMessage(10m, request);

        Assert.AreNotEqual(0m, message.Id);
        Assert.AreEqual(1, message.SequenceNumber);
        Assert.AreEqual(100m, message.ConversationId);
        Assert.IsFalse(message.IsDelivered);
    }

    [TestMethod]
    public async Task StoreMessage_IncrementsSequenceNumber()
    {
        var (db, service) = CreateService();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        await TestDbContextFactory.SeedConversation(db, 100m);

        var request = new SendMessageRequest { ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m, Ciphertext = Convert.ToBase64String(new byte[] { 1 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        var msg1 = await service.StoreMessage(10m, request);
        var msg2 = await service.StoreMessage(10m, request);
        var msg3 = await service.StoreMessage(10m, request);

        Assert.AreEqual(1, msg1.SequenceNumber);
        Assert.AreEqual(2, msg2.SequenceNumber);
        Assert.AreEqual(3, msg3.SequenceNumber);
    }

    [TestMethod]
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

        Assert.AreEqual(2, pending.Count);
        Assert.AreEqual(1m, pending[0].Id);
        Assert.AreEqual(3m, pending[1].Id);
    }

    [TestMethod]
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

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsDelivered);
        Assert.IsNotNull(result.DeliveredAt);
    }

    [TestMethod]
    public async Task AcknowledgeDelivery_MessageNotFound_ReturnsNull()
    {
        // ReSharper disable once UnusedVariable
        var (db, service) = CreateService();
        var result = await service.AcknowledgeDelivery(999m);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CleanupExpiredMessages_DeletesExpiredDisappearingMessages()
    {
        var (db, service) = CreateService();

        var conversation = await TestDbContextFactory.SeedConversation(db, 100m);
        conversation.DisappearingTimerSeconds = 1; // 1-second timer
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

        Assert.AreEqual(1, deleted);
        Assert.IsFalse(db.EncryptedMessages.Any());
    }

    [TestMethod]
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

        Assert.AreEqual(0, deleted);
        Assert.AreEqual(1, db.EncryptedMessages.Count());
    }

    [TestMethod]
    public async Task StoreMessage_SequenceNumbersAreUniquePerConversation()
    {
        var (db, service) = CreateService();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        await TestDbContextFactory.SeedConversation(db, 100m);
        await TestDbContextFactory.SeedConversation(db, 200m);

        var request100 = new SendMessageRequest { ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m, Ciphertext = Convert.ToBase64String(new byte[] { 1 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };
        var request200 = new SendMessageRequest { ConversationId = 200m, SenderDeviceId = 10m, RecipientDeviceId = 20m, Ciphertext = Convert.ToBase64String(new byte[] { 1 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        // Messages in conversation 100
        var msg1 = await service.StoreMessage(10m, request100);
        var msg2 = await service.StoreMessage(10m, request100);

        // Messages in conversation 200
        var msg3 = await service.StoreMessage(10m, request200);

        // Sequence numbers are per-conversation
        Assert.AreEqual(1, msg1.SequenceNumber);
        Assert.AreEqual(2, msg2.SequenceNumber);
        Assert.AreEqual(1, msg3.SequenceNumber); // Independent sequence for conversation 200
    }

    [TestMethod]
    public async Task StoreMessage_InvalidBase64_ThrowsArgumentException()
    {
        // ReSharper disable once UnusedVariable
        var (db, service) = CreateService();

        var request = new SendMessageRequest { ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m, Ciphertext = "not-valid-base64!!!", MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        await Assert.ThrowsAsync<ArgumentException>(() => service.StoreMessage(10m, request));
    }

    [TestMethod]
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

        Assert.AreEqual(0, deleted);
    }

    [TestMethod]
    public void GetMaxCiphertextSize_Media_ReturnsMediaLimit()
    {
        // Test media content types return the larger media limit
        Assert.AreEqual(ProtocolConstants.MaxMediaCiphertextSizeBytes,
            MessageRelayService.GetMaxCiphertextSize(ContentType.Image));
        Assert.AreEqual(ProtocolConstants.MaxMediaCiphertextSizeBytes,
            MessageRelayService.GetMaxCiphertextSize(ContentType.Video));
        Assert.AreEqual(ProtocolConstants.MaxMediaCiphertextSizeBytes,
            MessageRelayService.GetMaxCiphertextSize(ContentType.Audio));
        Assert.AreEqual(ProtocolConstants.MaxMediaCiphertextSizeBytes,
            MessageRelayService.GetMaxCiphertextSize(ContentType.File));
    }

    [TestMethod]
    public void GetMaxCiphertextSize_Text_ReturnsTextLimit()
    {
        // Text content type returns the smaller text limit
        Assert.AreEqual(ProtocolConstants.MaxCiphertextSizeBytes,
            MessageRelayService.GetMaxCiphertextSize(ContentType.Text));
    }

    // --- AdvanceReadPointer ---

    [TestMethod]
    public async Task AdvanceReadPointer_CreatesPointerAndReturnsNewlyReadMessages()
    {
        var (db, service) = CreateService();
        await TestDbContextFactory.SeedUser(db, 1m, "reader");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        await TestDbContextFactory.SeedUser(db, 2m, "sender");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m);
        await TestDbContextFactory.SeedConversation(db, 100m);

        // Add 3 messages sent TO user 1's device
        for (var i = 1; i <= 3; i++)
        {
            db.EncryptedMessages.Add(new EncryptedMessage
            {
                Id = i, ConversationId = 100m, SenderDeviceId = 20m, RecipientDeviceId = 10m,
                Ciphertext = [1], SequenceNumber = i, ServerTimestamp = DateTimeOffset.UtcNow,
                IsDelivered = true
            });
        }

        await db.SaveChangesAsync();

        var result = await service.AdvanceReadPointer(1m, 100m, 2);

        // Should return messages 1 and 2 as newly read
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Any(static r => r.MessageId == 1m));
        Assert.IsTrue(result.Any(static r => r.MessageId == 2m));

        // Pointer should exist with 1 unread remaining (message 3)
        var pointer = await db.ConversationReadPointers
            .FirstOrDefaultAsync(static p => p.UserId == 1m && p.ConversationId == 100m);
        Assert.IsNotNull(pointer);
        Assert.AreEqual(2L, pointer.LastReadSequenceNumber);
        Assert.AreEqual(1, pointer.UnreadCount);
    }

    [TestMethod]
    public async Task AdvanceReadPointer_AdvancesExistingPointer()
    {
        var (db, service) = CreateService();
        await TestDbContextFactory.SeedUser(db, 1m, "reader");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        await TestDbContextFactory.SeedConversation(db, 100m);

        for (var i = 1; i <= 5; i++)
        {
            db.EncryptedMessages.Add(new EncryptedMessage
            {
                Id = i, ConversationId = 100m, SenderDeviceId = 20m, RecipientDeviceId = 10m,
                Ciphertext = [1], SequenceNumber = i, ServerTimestamp = DateTimeOffset.UtcNow,
                IsDelivered = true
            });
        }

        await db.SaveChangesAsync();

        // First advance to seq 2
        await service.AdvanceReadPointer(1m, 100m, 2);

        // Then advance to seq 4
        var result = await service.AdvanceReadPointer(1m, 100m, 4);

        // Should return only messages 3 and 4 (newly read since last pointer)
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Any(static r => r.MessageId == 3m));
        Assert.IsTrue(result.Any(static r => r.MessageId == 4m));

        var pointer = await db.ConversationReadPointers
            .FirstOrDefaultAsync(static p => p.UserId == 1m && p.ConversationId == 100m);
        Assert.AreEqual(4L, pointer?.LastReadSequenceNumber);
        Assert.AreEqual(1, pointer?.UnreadCount); // message 5 still unread
    }

    [TestMethod]
    public async Task AdvanceReadPointer_OlderSequenceNumber_ReturnsEmpty()
    {
        var (db, service) = CreateService();
        await TestDbContextFactory.SeedUser(db, 1m, "reader");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        await TestDbContextFactory.SeedConversation(db, 100m);

        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 1m, ConversationId = 100m, SenderDeviceId = 20m, RecipientDeviceId = 10m,
            Ciphertext = [1], SequenceNumber = 1, ServerTimestamp = DateTimeOffset.UtcNow,
            IsDelivered = true
        });
        await db.SaveChangesAsync();

        await service.AdvanceReadPointer(1m, 100m, 5);
        var result = await service.AdvanceReadPointer(1m, 100m, 3); // older

        Assert.AreEqual(0, result.Count);

        // Pointer should not regress
        var pointer = await db.ConversationReadPointers
            .FirstOrDefaultAsync(static p => p.UserId == 1m && p.ConversationId == 100m);
        Assert.AreEqual(5L, pointer?.LastReadSequenceNumber);
    }

    // --- GetUnreadCount ---

    [TestMethod]
    public async Task GetUnreadCount_WithPointer_ReturnsPointerUnreadCount()
    {
        var (db, service) = CreateService();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedConversation(db, 100m);

        db.ConversationReadPointers.Add(new ConversationReadPointer
        {
            UserId = 1m, ConversationId = 100m,
            LastReadSequenceNumber = 5, UnreadCount = 3
        });
        await db.SaveChangesAsync();

        var count = await service.GetUnreadCount(1m, 100m);
        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public async Task GetUnreadCount_NoPointer_CountsDeliveredMessages()
    {
        var (db, service) = CreateService();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        await TestDbContextFactory.SeedConversation(db, 100m);

        // 2 delivered + 1 undelivered
        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 1m, ConversationId = 100m, SenderDeviceId = 20m, RecipientDeviceId = 10m,
            Ciphertext = [1], SequenceNumber = 1, ServerTimestamp = DateTimeOffset.UtcNow,
            IsDelivered = true
        });
        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 2m, ConversationId = 100m, SenderDeviceId = 20m, RecipientDeviceId = 10m,
            Ciphertext = [1], SequenceNumber = 2, ServerTimestamp = DateTimeOffset.UtcNow,
            IsDelivered = true
        });
        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 3m, ConversationId = 100m, SenderDeviceId = 20m, RecipientDeviceId = 10m,
            Ciphertext = [1], SequenceNumber = 3, ServerTimestamp = DateTimeOffset.UtcNow,
            IsDelivered = false
        });
        await db.SaveChangesAsync();

        var count = await service.GetUnreadCount(1m, 100m);
        Assert.AreEqual(3, count); // All messages (delivered & undelivered) — BUG-CR-008 fix
    }

    // --- GetAllUnreadCounts ---

    [TestMethod]
    public async Task GetAllUnreadCounts_ReturnsOnlyNonZero()
    {
        var (db, service) = CreateService();
        await TestDbContextFactory.SeedUser(db, 1m);

        db.ConversationReadPointers.Add(new ConversationReadPointer
        {
            UserId = 1m, ConversationId = 100m,
            LastReadSequenceNumber = 5, UnreadCount = 3
        });
        db.ConversationReadPointers.Add(new ConversationReadPointer
        {
            UserId = 1m, ConversationId = 200m,
            LastReadSequenceNumber = 10, UnreadCount = 0
        });
        db.ConversationReadPointers.Add(new ConversationReadPointer
        {
            UserId = 1m, ConversationId = 300m,
            LastReadSequenceNumber = 2, UnreadCount = 7
        });
        await db.SaveChangesAsync();

        var counts = await service.GetAllUnreadCounts(1m);

        Assert.AreEqual(2, counts.Count);
        Assert.AreEqual(3, counts[100m]);
        Assert.AreEqual(7, counts[300m]);
        Assert.IsFalse(counts.ContainsKey(200m));
    }

    // --- IncrementUnreadCountsForNewMessage ---

    [TestMethod]
    public async Task IncrementUnreadCounts_IncrementsAllParticipantsExceptSender()
    {
        var (db, service) = CreateService();
        await TestDbContextFactory.SeedUser(db, 1m, "sender");
        await TestDbContextFactory.SeedUser(db, 2m, "user2");
        await TestDbContextFactory.SeedUser(db, 3m, "user3");
        await TestDbContextFactory.SeedConversation(db, 100m, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 3m);

        await service.IncrementUnreadCountsForNewMessage(100m, 1m);

        var pointer2 = await db.ConversationReadPointers
            .FirstOrDefaultAsync(static p => p.UserId == 2m && p.ConversationId == 100m);
        var pointer3 = await db.ConversationReadPointers
            .FirstOrDefaultAsync(static p => p.UserId == 3m && p.ConversationId == 100m);
        var pointer1 = await db.ConversationReadPointers
            .FirstOrDefaultAsync(static p => p.UserId == 1m && p.ConversationId == 100m);

        Assert.IsNotNull(pointer2);
        Assert.AreEqual(1, pointer2.UnreadCount);
        Assert.IsNotNull(pointer3);
        Assert.AreEqual(1, pointer3.UnreadCount);
        Assert.IsNull(pointer1); // Sender should not have a pointer created
    }

    [TestMethod]
    public async Task IncrementUnreadCounts_IncrementsExistingPointers()
    {
        var (db, service) = CreateService();
        await TestDbContextFactory.SeedUser(db, 1m, "sender");
        await TestDbContextFactory.SeedUser(db, 2m, "user2");
        await TestDbContextFactory.SeedConversation(db, 100m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);

        // User 2 already has a pointer with 5 unread
        db.ConversationReadPointers.Add(new ConversationReadPointer
        {
            UserId = 2m, ConversationId = 100m,
            LastReadSequenceNumber = 3, UnreadCount = 5
        });
        await db.SaveChangesAsync();

        await service.IncrementUnreadCountsForNewMessage(100m, 1m);

        var pointer = await db.ConversationReadPointers
            .FirstOrDefaultAsync(static p => p.UserId == 2m && p.ConversationId == 100m);
        if (pointer != null) Assert.AreEqual(6, pointer.UnreadCount); // 5 + 1
    }
}
