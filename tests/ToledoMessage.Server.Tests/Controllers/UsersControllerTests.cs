using Microsoft.AspNetCore.Mvc;
using ToledoMessage.Controllers;
using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Server.Tests.Controllers;

public class UsersControllerTests
{
    private static (UsersController controller, Data.ApplicationDbContext db) CreateController(decimal userId = 1m)
    {
        var db = TestDbContextFactory.Create();
        var preKeyService = new PreKeyService(db);
        var controller = new UsersController(db, preKeyService);
        TestDbContextFactory.SetUser(controller, userId);
        return (controller, db);
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsEmptyList()
    {
        var (controller, _) = CreateController();
        var result = await controller.Search("");
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UserSearchResponse>(ok.Value);
        Assert.Empty(response.Users);
    }

    [Fact]
    public async Task Search_NullQuery_ReturnsEmptyList()
    {
        var (controller, _) = CreateController();
        var result = await controller.Search(null);
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UserSearchResponse>(ok.Value);
        Assert.Empty(response.Users);
    }

    [Fact]
    public async Task Search_FindsMatchingUsers()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "currentuser");
        await TestDbContextFactory.SeedUser(db, 2m, "alice");
        await TestDbContextFactory.SeedUser(db, 3m, "bob");
        await TestDbContextFactory.SeedUser(db, 4m, "alice2");

        var result = await controller.Search("alice");
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UserSearchResponse>(ok.Value);
        Assert.Equal(2, response.Users.Count);
    }

    [Fact]
    public async Task Search_ExcludesCurrentUser()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "myself");

        var result = await controller.Search("myself");
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UserSearchResponse>(ok.Value);
        Assert.Empty(response.Users);
    }

    [Fact]
    public async Task Search_ExcludesInactiveUsers()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "currentuser");
        await TestDbContextFactory.SeedUser(db, 2m, "inactive", isActive: false);

        var result = await controller.Search("inactive");
        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UserSearchResponse>(ok.Value);
        Assert.Empty(response.Users);
    }

    [Fact]
    public async Task GetPreKeyBundle_ValidDevice_ReturnsBundle()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "currentuser");
        await TestDbContextFactory.SeedUser(db, 2m, "target");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m, "TargetDevice");

        var preKeyService = new PreKeyService(db);
        await preKeyService.StoreOneTimePreKeys(20m, [new OneTimePreKeyDto(1, Convert.ToBase64String(new byte[32]))]);

        var result = await controller.GetPreKeyBundle(2m, 20m);

        var ok = Assert.IsType<OkObjectResult>(result);
        var bundle = Assert.IsType<PreKeyBundleResponse>(ok.Value);
        Assert.Equal(20m, bundle.DeviceId);
        Assert.NotNull(bundle.OneTimePreKey);
    }

    [Fact]
    public async Task GetPreKeyBundle_NoOtpAvailable_ReturnsNullOneTimePreKey()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "currentuser");
        await TestDbContextFactory.SeedUser(db, 2m, "target");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m, "TargetDevice");

        var result = await controller.GetPreKeyBundle(2m, 20m);

        var ok = Assert.IsType<OkObjectResult>(result);
        var bundle = Assert.IsType<PreKeyBundleResponse>(ok.Value);
        Assert.Null(bundle.OneTimePreKey);
    }

    [Fact]
    public async Task GetPreKeyBundle_InvalidDevice_ReturnsNotFound()
    {
        var (controller, _) = CreateController();
        var result = await controller.GetPreKeyBundle(2m, 999m);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetUserDevices_ReturnsActiveDevices()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m, "currentuser");
        await TestDbContextFactory.SeedUser(db, 2m, "target");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m, "Active");
        var inactive = await TestDbContextFactory.SeedDevice(db, 21m, 2m, "Inactive");
        inactive.IsActive = false;
        await db.SaveChangesAsync();

        var result = await controller.GetUserDevices(2m);

        var ok = Assert.IsType<OkObjectResult>(result);
        var devices = Assert.IsAssignableFrom<List<DeviceInfoResponse>>(ok.Value);
        Assert.Single(devices);
        Assert.Equal("Active", devices[0].DeviceName);
    }
}
