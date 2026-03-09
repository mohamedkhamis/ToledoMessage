using Microsoft.AspNetCore.Mvc;
using ToledoMessage.Controllers;
using ToledoMessage.Models;
using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;
using ToledoMessage.Shared.Enums;
using ToledoMessage.Server.Tests.Services;

namespace ToledoMessage.Server.Tests.Controllers;

[TestClass]
public class MessagesControllerTests
{
    private static (MessagesController controller, Data.ApplicationDbContext db) CreateController(long userId = 1L)
    {
        var db = TestDbContextFactory.Create();
        var hubContext = new StubHubContext();
        var relayService = new MessageRelayService(db, hubContext);
        var controller = new MessagesController(db, relayService, hubContext);
        TestDbContextFactory.SetUser(controller, userId);
        return (controller, db);
    }

    private static async Task SeedMessagingContext(Data.ApplicationDbContext db)
    {
        await TestDbContextFactory.SeedUser(db, 1L, "sender");
        await TestDbContextFactory.SeedUser(db, 2L, "recipient");
        await TestDbContextFactory.SeedDevice(db, 10L, 1L, "SenderDevice");
        await TestDbContextFactory.SeedDevice(db, 20L, 2L, "RecipientDevice");
        await TestDbContextFactory.SeedConversation(db, 100L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 2L);
    }

    [TestMethod]
    public async Task SendMessage_ValidRequest_ReturnsOk()
    {
        var (controller, db) = CreateController();
        await SeedMessagingContext(db);

        var request = new SendMessageRequest { ConversationId = 100L, SenderDeviceId = 10L, RecipientDeviceId = 20L, Ciphertext = Convert.ToBase64String(new byte[] { 1, 2, 3 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        var result = await controller.SendMessage(request);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<SendMessageResult>(ok.Value);
        var response = (SendMessageResult)ok.Value;
        Assert.AreNotEqual(0L, response.MessageId);
        Assert.AreEqual(1, response.SequenceNumber);
    }

    [TestMethod]
    public async Task SendMessage_NotParticipant_ReturnsForbid()
    {
        var (controller, db) = CreateController(3L); // user 3 is not a participant
        await SeedMessagingContext(db);
        await TestDbContextFactory.SeedUser(db, 3L, "outsider");
        await TestDbContextFactory.SeedDevice(db, 30L, 3L);

        var request = new SendMessageRequest { ConversationId = 100L, SenderDeviceId = 30L, RecipientDeviceId = 20L, Ciphertext = Convert.ToBase64String(new byte[] { 1, 2, 3 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        var result = await controller.SendMessage(request);
        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task SendMessage_NoActiveDevice_ReturnsBadRequest()
    {
        var (controller, db) = CreateController(3L);
        await TestDbContextFactory.SeedUser(db, 3L, "nodevice");
        await TestDbContextFactory.SeedConversation(db, 100L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 3L);

        var request = new SendMessageRequest { ConversationId = 100L, SenderDeviceId = 0L, RecipientDeviceId = 20L, Ciphertext = Convert.ToBase64String(new byte[] { 1, 2, 3 }), MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        var result = await controller.SendMessage(request);
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task GetPendingMessages_OwnDevice_ReturnsPending()
    {
        var (controller, db) = CreateController();
        await SeedMessagingContext(db);

        // Add a pending message for device 10
        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 500L, ConversationId = 100L, SenderDeviceId = 20L, RecipientDeviceId = 10L,
            Ciphertext = [1, 2], SequenceNumber = 1, ServerTimestamp = DateTimeOffset.UtcNow,
            IsDelivered = false
        });
        await db.SaveChangesAsync();

        var result = await controller.GetPendingMessages(10L);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<List<MessageEnvelope>>(ok.Value);
        var envelopes = (List<MessageEnvelope>)ok.Value;
        Assert.AreEqual(1, envelopes.Count);
    }

    [TestMethod]
    public async Task GetPendingMessages_OtherUsersDevice_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await SeedMessagingContext(db);

        var result = await controller.GetPendingMessages(20L); // device 20 belongs to user 2
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task AcknowledgeDelivery_ValidMessage_ReturnsOk()
    {
        var (controller, db) = CreateController();
        await SeedMessagingContext(db);

        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 500L, ConversationId = 100L, SenderDeviceId = 20L, RecipientDeviceId = 10L,
            Ciphertext = [1, 2], SequenceNumber = 1, ServerTimestamp = DateTimeOffset.UtcNow,
            IsDelivered = false
        });
        await db.SaveChangesAsync();

        var result = await controller.AcknowledgeDelivery(500L);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var msg = await db.EncryptedMessages.FindAsync(500L);
        Assert.IsTrue(msg?.IsDelivered);
    }

    [TestMethod]
    public async Task AcknowledgeDelivery_NotRecipient_ReturnsForbid()
    {
        var (controller, db) = CreateController();
        await SeedMessagingContext(db);

        // Message recipient is device 20 (user 2), but controller user is 1
        db.EncryptedMessages.Add(new EncryptedMessage
        {
            Id = 500L, ConversationId = 100L, SenderDeviceId = 10L, RecipientDeviceId = 20L,
            Ciphertext = [1, 2], SequenceNumber = 1, ServerTimestamp = DateTimeOffset.UtcNow,
            IsDelivered = false
        });
        await db.SaveChangesAsync();

        var result = await controller.AcknowledgeDelivery(500L);
        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task AcknowledgeDelivery_MessageNotFound_ReturnsNotFound()
    {
        var (controller, _) = CreateController();
        var result = await controller.AcknowledgeDelivery(999L);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // --- Base64 Validation ---

    [TestMethod]
    public async Task SendMessage_InvalidBase64Ciphertext_ReturnsBadRequest()
    {
        var (controller, db) = CreateController();
        await SeedMessagingContext(db);

        var request = new SendMessageRequest { ConversationId = 100L, SenderDeviceId = 10L, RecipientDeviceId = 20L, Ciphertext = "not-valid-base64!!!", MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        var result = await controller.SendMessage(request);
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task SendMessage_EmptyCiphertext_ReturnsBadRequest()
    {
        var (controller, db) = CreateController();
        await SeedMessagingContext(db);

        var request = new SendMessageRequest { ConversationId = 100L, SenderDeviceId = 10L, RecipientDeviceId = 20L, Ciphertext = "", MessageType = MessageType.NormalMessage, ContentType = ContentType.Text };

        var result = await controller.SendMessage(request);
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }
}
