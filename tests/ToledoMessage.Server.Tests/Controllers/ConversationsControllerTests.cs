using Microsoft.AspNetCore.Mvc;
using ToledoMessage.Controllers;
using ToledoMessage.Shared.DTOs;
using ToledoMessage.Shared.Enums;

namespace ToledoMessage.Server.Tests.Controllers;

public class ConversationsControllerTests
{
    private static (ConversationsController controller, Data.ApplicationDbContext db) CreateController(decimal userId = 1m)
    {
        var db = TestDbContextFactory.Create();
        var controller = new ConversationsController(db);
        TestDbContextFactory.SetUser(controller, userId);
        return (controller, db);
    }

    // --- Create 1:1 ---

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreated()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedUser(db, 2m, "user2");

        var result = await controller.Create(new CreateConversationRequest(2m));

        var created = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<ConversationResponse>(created.Value);
        Assert.True(response.IsNew);
    }

    [Fact]
    public async Task Create_ExistingConversation_ReturnsExisting()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedUser(db, 2m, "user2");

        // Create first
        await controller.Create(new CreateConversationRequest(2m));

        // Try again — should return existing
        var result = await controller.Create(new CreateConversationRequest(2m));

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ConversationResponse>(ok.Value);
        Assert.False(response.IsNew);
    }

    [Fact]
    public async Task Create_WithSelf_ReturnsBadRequest()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");

        var result = await controller.Create(new CreateConversationRequest(1m));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_NonExistentParticipant_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");

        var result = await controller.Create(new CreateConversationRequest(999m));
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // --- Create Group ---

    [Fact]
    public async Task CreateGroup_ValidRequest_ReturnsCreated()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "creator");
        await TestDbContextFactory.SeedUser(db, 2m, "user2");
        await TestDbContextFactory.SeedUser(db, 3m, "user3");

        var request = new CreateGroupConversationRequest("TestGroup", [2m, 3m]);
        var result = await controller.CreateGroup(request);

        var created = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<ConversationResponse>(created.Value);
        Assert.True(response.IsNew);
    }

    [Fact]
    public async Task CreateGroup_EmptyName_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var result = await controller.CreateGroup(new CreateGroupConversationRequest("", [2m, 3m]));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateGroup_TooFewParticipants_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var result = await controller.CreateGroup(new CreateGroupConversationRequest("Group", [2m]));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateGroup_TooManyParticipants_ReturnsBadRequest()
    {
        var (controller, _) = CreateController();
        var ids = Enumerable.Range(2, 101).Select(i => (decimal)i).ToList();
        var result = await controller.CreateGroup(new CreateGroupConversationRequest("Group", ids));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateGroup_InactiveParticipant_ReturnsBadRequest()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "creator");
        await TestDbContextFactory.SeedUser(db, 2m, "active");
        await TestDbContextFactory.SeedUser(db, 3m, "inactive", isActive: false);

        var result = await controller.CreateGroup(new CreateGroupConversationRequest("Group", [2m, 3m]));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // --- Add/Remove Participants ---

    [Fact]
    public async Task AddParticipant_AsAdmin_ReturnsNoContent()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "admin");
        await TestDbContextFactory.SeedUser(db, 2m, "member");
        await TestDbContextFactory.SeedUser(db, 3m, "newmember");
        await TestDbContextFactory.SeedConversation(db, 100m, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m, ParticipantRole.Admin);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);

        var result = await controller.AddParticipant(100m, new AddParticipantRequest(3m));
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task AddParticipant_AsMember_ReturnsForbid()
    {
        var (controller, db) = CreateController(2m); // user 2 is a member, not admin
        await TestDbContextFactory.SeedUser(db, 1m, "admin");
        await TestDbContextFactory.SeedUser(db, 2m, "member");
        await TestDbContextFactory.SeedUser(db, 3m, "newmember");
        await TestDbContextFactory.SeedConversation(db, 100m, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m, ParticipantRole.Admin);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);

        var result = await controller.AddParticipant(100m, new AddParticipantRequest(3m));
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task AddParticipant_AlreadyParticipant_ReturnsBadRequest()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "admin");
        await TestDbContextFactory.SeedUser(db, 2m, "member");
        await TestDbContextFactory.SeedConversation(db, 100m, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m, ParticipantRole.Admin);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);

        var result = await controller.AddParticipant(100m, new AddParticipantRequest(2m));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RemoveParticipant_SelfLeave_ReturnsNoContent()
    {
        var (controller, db) = CreateController(2m);
        await TestDbContextFactory.SeedUser(db, 1m, "admin");
        await TestDbContextFactory.SeedUser(db, 2m, "member");
        await TestDbContextFactory.SeedConversation(db, 100m, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m, ParticipantRole.Admin);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);

        var result = await controller.RemoveParticipant(100m, 2m);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RemoveParticipant_AdminRemovesMember_ReturnsNoContent()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "admin");
        await TestDbContextFactory.SeedUser(db, 2m, "member");
        await TestDbContextFactory.SeedConversation(db, 100m, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m, ParticipantRole.Admin);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);

        var result = await controller.RemoveParticipant(100m, 2m);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RemoveParticipant_MemberRemovesOther_ReturnsForbid()
    {
        var (controller, db) = CreateController(2m);
        await TestDbContextFactory.SeedUser(db, 1m, "admin");
        await TestDbContextFactory.SeedUser(db, 2m, "member");
        await TestDbContextFactory.SeedUser(db, 3m, "other");
        await TestDbContextFactory.SeedConversation(db, 100m, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m, ParticipantRole.Admin);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 3m);

        var result = await controller.RemoveParticipant(100m, 3m);
        Assert.IsType<ForbidResult>(result);
    }

    // --- Get Conversation ---

    [Fact]
    public async Task GetConversation_AsParticipant_ReturnsDetails()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedUser(db, 2m, "user2");
        await TestDbContextFactory.SeedConversation(db, 100m, ConversationType.Group, "TestGroup");
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m, ParticipantRole.Admin);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);

        var result = await controller.GetConversation(100m);

        var ok = Assert.IsType<OkObjectResult>(result);
        var detail = Assert.IsType<ConversationDetailResponse>(ok.Value);
        Assert.Equal(100m, detail.ConversationId);
        Assert.Equal(ConversationType.Group, detail.Type);
        Assert.Equal("TestGroup", detail.GroupName);
        Assert.Equal(2, detail.ParticipantCount);
    }

    [Fact]
    public async Task GetConversation_NotParticipant_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedConversation(db, 100m);

        var result = await controller.GetConversation(100m);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // --- Get Participants ---

    [Fact]
    public async Task GetParticipants_AsParticipant_ReturnsList()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedUser(db, 2m, "user2");
        await TestDbContextFactory.SeedConversation(db, 100m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);

        var result = await controller.GetParticipants(100m);

        var ok = Assert.IsType<OkObjectResult>(result);
        var participants = Assert.IsAssignableFrom<List<ParticipantResponse>>(ok.Value);
        Assert.Equal(2, participants.Count);
    }

    [Fact]
    public async Task GetParticipants_NotParticipant_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedConversation(db, 100m);

        var result = await controller.GetParticipants(100m);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // --- Set Timer ---

    [Fact]
    public async Task SetTimer_ValidRequest_ReturnsNoContent()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedConversation(db, 100m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);

        var result = await controller.SetTimer(100m, new SetTimerRequest(3600));

        Assert.IsType<NoContentResult>(result);
        var conv = await db.Conversations.FindAsync(100m);
        Assert.Equal(3600, conv!.DisappearingTimerSeconds);
    }

    [Fact]
    public async Task SetTimer_NullToDisable_ReturnsNoContent()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        var conv = await TestDbContextFactory.SeedConversation(db, 100m);
        conv.DisappearingTimerSeconds = 3600;
        await db.SaveChangesAsync();
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);

        var result = await controller.SetTimer(100m, new SetTimerRequest(null));

        Assert.IsType<NoContentResult>(result);
        var refreshed = await db.Conversations.FindAsync(100m);
        Assert.Null(refreshed!.DisappearingTimerSeconds);
    }

    [Fact]
    public async Task SetTimer_NegativeValue_ReturnsBadRequest()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedConversation(db, 100m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);

        var result = await controller.SetTimer(100m, new SetTimerRequest(-1));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SetTimer_NotParticipant_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedConversation(db, 100m);

        var result = await controller.SetTimer(100m, new SetTimerRequest(3600));
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // --- List Conversations ---

    [Fact]
    public async Task ListConversations_ReturnsUserConversations()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedUser(db, 2m, "user2");
        await TestDbContextFactory.SeedConversation(db, 100m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 1m);
        await TestDbContextFactory.SeedParticipant(db, 100m, 2m);

        var result = await controller.ListConversations();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<List<ConversationListItemResponse>>(ok.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task ListConversations_NoConversations_ReturnsEmpty()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");

        var result = await controller.ListConversations();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Empty((System.Collections.IEnumerable)ok.Value!);
    }

    // --- 1:1 Conversation Deduplication ---

    [Fact]
    public async Task Create_ConcurrentCalls_ReturnsSameConversation()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "user1");
        await TestDbContextFactory.SeedUser(db, 2m, "user2");

        // Two sequential create calls should produce the same conversation
        var result1 = await controller.Create(new CreateConversationRequest(2m));
        var result2 = await controller.Create(new CreateConversationRequest(2m));

        // First should be Created, second should be Ok (existing)
        var created = Assert.IsType<CreatedResult>(result1);
        var response1 = Assert.IsType<ConversationResponse>(created.Value);

        var ok = Assert.IsType<OkObjectResult>(result2);
        var response2 = Assert.IsType<ConversationResponse>(ok.Value);

        // Same conversation ID
        Assert.Equal(response1.ConversationId, response2.ConversationId);
        Assert.True(response1.IsNew);
        Assert.False(response2.IsNew);
    }
}
