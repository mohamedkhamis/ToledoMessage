using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ToledoMessage.Controllers;
using ToledoMessage.Hubs;
using ToledoMessage.Models;
using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;
using ToledoMessage.Shared.Enums;
using ToledoMessage.Server.Tests.Services;

namespace ToledoMessage.Server.Tests.Controllers;

public class MessagesControllerTests
{
    private static (MessagesController controller, Data.ApplicationDbContext db) CreateController(decimal userId = 1m)
    {
        var db = TestDbContextFactory.Create();
        var hubContext = new StubHubContext();
        var relayService = new MessageRelayService(db, hubContext);
        var controller = new MessagesController(db, relayService);
        TestDbContextFactory.SetUser(controller, userId);
        return (controller, db);
    }

    private static async Task SeedMessagingContext(Data.ApplicationDbContext db)
    {
        await TestDbContextFactory.SeedUser(db, 1m, "sender");
        await TestDbContextFactory.SeedUser(db, 2m, "recipient");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m, "SenderDevice");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m, "RecipientDevice");
        await TestDbContextFactory.SeedConversation(db, 100m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);
    }

    [Fact]
    public async Task SendMessage_ValidRequest_ReturnsOk()
    {
        var (controller, db) = CreateController();
        await SeedMessagingContext(db);

        var request = new SendMessageRequest(100m, 10m, 20m,
            Convert.ToBase64String(new byte[] { 1, 2, 3 }), MessageType.NormalMessage, ContentType.Text);

        var result = await controller.SendMessage(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SendMessageResult>(ok.Value);
        Assert.NotEqual(0m, response.MessageId);
        Assert.Equal(1, response.SequenceNumber);
    }

    [Fact]
    public async Task SendMessage_NotParticipant_ReturnsForbid()
    {
        var (controller, db) = CreateController(3m); // user 3 is not a participant
        await SeedMessagingContext(db);
        await TestDbContextFactory.SeedUser(db, 3m, "outsider");
        await TestDbContextFactory.SeedDevice(db, 30m, 3m);

        var request = new SendMessageRequest(100m, 30m, 20m,
            Convert.ToBase64String(new byte[] { 1, 2, 3 }), MessageType.NormalMessage, ContentType.Text);

        var result = await controller.SendMessage(request);
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task SendMessage_NoActiveDevice_ReturnsBadRequest()
    {
        var (controller, db) = CreateController(3m);
        await TestDbContextFactory.SeedUser(db, 3m, "nodevice");
        await TestDbContextFactory.SeedConversation(db, 100m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 3m);

        var request = new SendMessageRequest(100m, 0m, 20m,
            Convert.ToBase64String(new byte[] { 1, 2, 3 }), MessageType.NormalMessage, ContentType.Text);

        var result = await controller.SendMessage(request);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetPendingMessages_OwnDevice_ReturnsPending()
    {
        var (controller, db) = CreateController();
        await SeedMessagingContext(db);

        // Add a pending message for device 10
        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 500m, ConversationId = 100m, SenderDeviceId = 20m, RecipientDeviceId = 10m,
            Ciphertext = [1, 2], SequenceNumber = 1, ServerTimestamp = DateTimeOffset.UtcNow,
            IsDelivered = false
        });
        await db.SaveChangesAsync();

        var result = await controller.GetPendingMessages(10m);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelopes = Assert.IsAssignableFrom<List<MessageEnvelope>>(ok.Value);
        Assert.Single(envelopes);
    }

    [Fact]
    public async Task GetPendingMessages_OtherUsersDevice_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await SeedMessagingContext(db);

        var result = await controller.GetPendingMessages(20m); // device 20 belongs to user 2
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task AcknowledgeDelivery_ValidMessage_ReturnsOk()
    {
        var (controller, db) = CreateController();
        await SeedMessagingContext(db);

        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 500m, ConversationId = 100m, SenderDeviceId = 20m, RecipientDeviceId = 10m,
            Ciphertext = [1, 2], SequenceNumber = 1, ServerTimestamp = DateTimeOffset.UtcNow,
            IsDelivered = false
        });
        await db.SaveChangesAsync();

        var result = await controller.AcknowledgeDelivery(500m);

        Assert.IsType<OkObjectResult>(result);
        var msg = await db.EncryptedMessages.FindAsync(500m);
        Assert.True(msg!.IsDelivered);
    }

    [Fact]
    public async Task AcknowledgeDelivery_NotRecipient_ReturnsForbid()
    {
        var (controller, db) = CreateController();
        await SeedMessagingContext(db);

        // Message recipient is device 20 (user 2), but controller user is 1
        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 500m, ConversationId = 100m, SenderDeviceId = 10m, RecipientDeviceId = 20m,
            Ciphertext = [1, 2], SequenceNumber = 1, ServerTimestamp = DateTimeOffset.UtcNow,
            IsDelivered = false
        });
        await db.SaveChangesAsync();

        var result = await controller.AcknowledgeDelivery(500m);
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task AcknowledgeDelivery_MessageNotFound_ReturnsNotFound()
    {
        var (controller, _) = CreateController();
        var result = await controller.AcknowledgeDelivery(999m);
        Assert.IsType<NotFoundObjectResult>(result);
    }
}
