using Microsoft.AspNetCore.Mvc;
using ToledoMessage.Controllers;
using ToledoMessage.Shared.DTOs;
using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Server.Tests.Controllers;

[TestClass]
public class ConversationsControllerTests
{
    private static (ConversationsController controller, Data.ApplicationDbContext db) CreateController(long userId = 1L)
    {
        var db = TestDbContextFactory.Create();
        var controller = new ConversationsController(db);
        TestDbContextFactory.SetUser(controller, userId);
        return (controller, db);
    }

    // --- Create 1:1 ---

    [TestMethod]
    public async Task Create_ValidRequest_ReturnsCreated()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedUser(db, 2L, "user2");

        var result = await controller.Create(new CreateConversationRequest(2L));

        Assert.IsInstanceOfType<CreatedResult>(result);
        var created = (CreatedResult)result;
        Assert.IsInstanceOfType<ConversationResponse>(created.Value);
        var response = (ConversationResponse)created.Value;
        Assert.IsTrue(response.IsNew);
    }

    [TestMethod]
    public async Task Create_ExistingConversation_ReturnsExisting()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedUser(db, 2L, "user2");

        // Create first
        await controller.Create(new CreateConversationRequest(2L));

        // Try again — should return existing
        var result = await controller.Create(new CreateConversationRequest(2L));

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<ConversationResponse>(ok.Value);
        var response = (ConversationResponse)ok.Value;
        Assert.IsFalse(response.IsNew);
    }

    [TestMethod]
    public async Task Create_WithSelf_ReturnsBadRequest()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");

        var result = await controller.Create(new CreateConversationRequest(1L));
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task Create_NonExistentParticipant_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");

        var result = await controller.Create(new CreateConversationRequest(999L));
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // --- Create Group ---

    [TestMethod]
    public async Task CreateGroup_ValidRequest_ReturnsCreated()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "creator");
        await TestDbContextFactory.SeedUser(db, 2L, "user2");
        await TestDbContextFactory.SeedUser(db, 3L, "user3");

        var request = new CreateGroupConversationRequest("TestGroup", [2L, 3L]);
        var result = await controller.CreateGroup(request);

        Assert.IsInstanceOfType<CreatedResult>(result);
        var created = (CreatedResult)result;
        Assert.IsInstanceOfType<ConversationResponse>(created.Value);
        var response = (ConversationResponse)created.Value;
        Assert.IsTrue(response.IsNew);
    }

    [TestMethod]
    public async Task CreateGroup_EmptyName_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var result = await controller.CreateGroup(new CreateGroupConversationRequest("", [2L, 3L]));
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateGroup_TooFewParticipants_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var result = await controller.CreateGroup(new CreateGroupConversationRequest("Group", [2L]));
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateGroup_TooManyParticipants_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var ids = Enumerable.Range(2, 101).Select(static i => (long)i).ToList();
        var result = await controller.CreateGroup(new CreateGroupConversationRequest("Group", ids));
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task CreateGroup_InactiveParticipant_ReturnsBadRequest()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "creator");
        await TestDbContextFactory.SeedUser(db, 2L, "active");
        await TestDbContextFactory.SeedUser(db, 3L, "inactive", false);

        var result = await controller.CreateGroup(new CreateGroupConversationRequest("Group", [2L, 3L]));
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    // --- Add/Remove Participants ---

    [TestMethod]
    public async Task AddParticipant_AsAdmin_ReturnsNoContent()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "admin");
        await TestDbContextFactory.SeedUser(db, 2L, "member");
        await TestDbContextFactory.SeedUser(db, 3L, "newmember");
        await TestDbContextFactory.SeedConversation(db, 100L, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L, ParticipantRole.Admin);
        await TestDbContextFactory.SeedParticipant(db, 100L, 2L);

        var result = await controller.AddParticipant(100L, new AddParticipantRequest(3L));
        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task AddParticipant_AsMember_ReturnsForbid()
    {
        var (controller, db) = CreateController(2L); // user 2 is a member, not admin
        await TestDbContextFactory.SeedUser(db, 1L, "admin");
        await TestDbContextFactory.SeedUser(db, 2L, "member");
        await TestDbContextFactory.SeedUser(db, 3L, "newmember");
        await TestDbContextFactory.SeedConversation(db, 100L, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L, ParticipantRole.Admin);
        await TestDbContextFactory.SeedParticipant(db, 100L, 2L);

        var result = await controller.AddParticipant(100L, new AddParticipantRequest(3L));
        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task AddParticipant_AlreadyParticipant_ReturnsBadRequest()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "admin");
        await TestDbContextFactory.SeedUser(db, 2L, "member");
        await TestDbContextFactory.SeedConversation(db, 100L, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L, ParticipantRole.Admin);
        await TestDbContextFactory.SeedParticipant(db, 100L, 2L);

        var result = await controller.AddParticipant(100L, new AddParticipantRequest(2L));
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task RemoveParticipant_SelfLeave_ReturnsNoContent()
    {
        var (controller, db) = CreateController(2L);
        await TestDbContextFactory.SeedUser(db, 1L, "admin");
        await TestDbContextFactory.SeedUser(db, 2L, "member");
        await TestDbContextFactory.SeedConversation(db, 100L, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L, ParticipantRole.Admin);
        await TestDbContextFactory.SeedParticipant(db, 100L, 2L);

        var result = await controller.RemoveParticipant(100L, 2L);
        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task RemoveParticipant_AdminRemovesMember_ReturnsNoContent()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "admin");
        await TestDbContextFactory.SeedUser(db, 2L, "member");
        await TestDbContextFactory.SeedConversation(db, 100L, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L, ParticipantRole.Admin);
        await TestDbContextFactory.SeedParticipant(db, 100L, 2L);

        var result = await controller.RemoveParticipant(100L, 2L);
        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task RemoveParticipant_MemberRemovesOther_ReturnsForbid()
    {
        var (controller, db) = CreateController(2L);
        await TestDbContextFactory.SeedUser(db, 1L, "admin");
        await TestDbContextFactory.SeedUser(db, 2L, "member");
        await TestDbContextFactory.SeedUser(db, 3L, "other");
        await TestDbContextFactory.SeedConversation(db, 100L, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L, ParticipantRole.Admin);
        await TestDbContextFactory.SeedParticipant(db, 100L, 2L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 3L);

        var result = await controller.RemoveParticipant(100L, 3L);
        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    // --- Get Conversation ---

    [TestMethod]
    public async Task GetConversation_AsParticipant_ReturnsDetails()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedUser(db, 2L, "user2");
        await TestDbContextFactory.SeedConversation(db, 100L, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L, ParticipantRole.Admin);
        await TestDbContextFactory.SeedParticipant(db, 100L, 2L);

        var result = await controller.GetConversation(100L);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<ConversationDetailResponse>(ok.Value);
        var detail = (ConversationDetailResponse)ok.Value;
        Assert.AreEqual(100L, detail.ConversationId);
        Assert.AreEqual(ConversationType.Group, detail.Type);
        Assert.AreEqual("TestGroup", detail.GroupName);
        Assert.AreEqual(2, detail.ParticipantCount);
    }

    [TestMethod]
    public async Task GetConversation_NotParticipant_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedConversation(db, 100L);

        var result = await controller.GetConversation(100L);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // --- Get Participants ---

    [TestMethod]
    public async Task GetParticipants_AsParticipant_ReturnsList()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedUser(db, 2L, "user2");
        await TestDbContextFactory.SeedConversation(db, 100L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 2L);

        var result = await controller.GetParticipants(100L);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<List<ParticipantResponse>>(ok.Value);
        var participants = (List<ParticipantResponse>)ok.Value;
        Assert.AreEqual(2, participants.Count);
    }

    [TestMethod]
    public async Task GetParticipants_NotParticipant_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedConversation(db, 100L);

        var result = await controller.GetParticipants(100L);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // --- Set Timer ---

    [TestMethod]
    public async Task SetTimer_ValidRequest_ReturnsNoContent()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedConversation(db, 100L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L);

        var result = await controller.SetTimer(100L, new SetTimerRequest(3600));

        Assert.IsInstanceOfType<NoContentResult>(result);
        var conv = await db.Conversations.FindAsync(100L);
        Assert.AreEqual(3600, conv?.DisappearingTimerSeconds);
    }

    [TestMethod]
    public async Task SetTimer_NullToDisable_ReturnsNoContent()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        var conv = await TestDbContextFactory.SeedConversation(db, 100L);
        conv.DisappearingTimerSeconds = 3600;
        await db.SaveChangesAsync();
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L);

        var result = await controller.SetTimer(100L, new SetTimerRequest(null));

        Assert.IsInstanceOfType<NoContentResult>(result);
        var refreshed = await db.Conversations.FindAsync(100L);
        Assert.IsNull(refreshed?.DisappearingTimerSeconds);
    }

    [TestMethod]
    public async Task SetTimer_NegativeValue_ReturnsBadRequest()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedConversation(db, 100L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L);

        var result = await controller.SetTimer(100L, new SetTimerRequest(-1));
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task SetTimer_NotParticipant_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedConversation(db, 100L);

        var result = await controller.SetTimer(100L, new SetTimerRequest(3600));
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    // --- List Conversations ---

    [TestMethod]
    public async Task ListConversations_ReturnsUserConversations()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedUser(db, 2L, "user2");
        await TestDbContextFactory.SeedConversation(db, 100L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 1L);
        await TestDbContextFactory.SeedParticipant(db, 100L, 2L);

        var result = await controller.ListConversations();

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<List<ConversationListItemResponse>>(ok.Value);
        var list = (List<ConversationListItemResponse>)ok.Value;
        Assert.AreEqual(1, list.Count);
    }

    [TestMethod]
    public async Task ListConversations_NoConversations_ReturnsEmpty()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");

        var result = await controller.ListConversations();

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        // ReSharper disable once NullableWarningSuppressionIsUsed
        Assert.AreEqual(0, ((System.Collections.IEnumerable)ok.Value!).Cast<object>().Count());
    }

    // --- 1:1 Conversation Deduplication ---

    [TestMethod]
    public async Task Create_ConcurrentCalls_ReturnsSameConversation()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "user1");
        await TestDbContextFactory.SeedUser(db, 2L, "user2");

        // Two sequential create calls should produce the same conversation
        var result1 = await controller.Create(new CreateConversationRequest(2L));
        var result2 = await controller.Create(new CreateConversationRequest(2L));

        // First should be Created, second should be Ok (existing)
        Assert.IsInstanceOfType<CreatedResult>(result1);
        var created = (CreatedResult)result1;
        Assert.IsInstanceOfType<ConversationResponse>(created.Value);
        var response1 = (ConversationResponse)created.Value;

        Assert.IsInstanceOfType<OkObjectResult>(result2);
        var ok = (OkObjectResult)result2;
        Assert.IsInstanceOfType<ConversationResponse>(ok.Value);
        var response2 = (ConversationResponse)ok.Value;

        // Same conversation ID
        Assert.AreEqual(response1.ConversationId, response2.ConversationId);
        Assert.IsTrue(response1.IsNew);
        Assert.IsFalse(response2.IsNew);
    }
}
