using Microsoft.AspNetCore.Mvc;
using ToledoMessage.Controllers;
using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Server.Tests.Controllers;

[TestClass]
public class UsersControllerTests
{
    private static (UsersController controller, Data.ApplicationDbContext db) CreateController(long userId = 1L)
    {
        var db = TestDbContextFactory.Create();
        var preKeyService = new PreKeyService(db);
        var controller = new UsersController(db, preKeyService);
        TestDbContextFactory.SetUser(controller, userId);
        return (controller, db);
    }

    [TestMethod]
    public async Task Search_EmptyQuery_ReturnsEmptyList()
    {
        var (controller, _) = CreateController();
        var result = await controller.Search("");
        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<UserSearchResponse>(ok.Value);
        var response = (UserSearchResponse)ok.Value!;
        Assert.IsFalse(response.Users.Any());
    }

    [TestMethod]
    public async Task Search_NullQuery_ReturnsEmptyList()
    {
        var (controller, _) = CreateController();
        var result = await controller.Search(null);
        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<UserSearchResponse>(ok.Value);
        var response = (UserSearchResponse)ok.Value!;
        Assert.IsFalse(response.Users.Any());
    }

    [TestMethod]
    public async Task Search_FindsMatchingUsers()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "currentuser");
        await TestDbContextFactory.SeedUser(db, 2L, "alice");
        await TestDbContextFactory.SeedUser(db, 3L, "bob");
        await TestDbContextFactory.SeedUser(db, 4L, "alice2");

        var result = await controller.Search("alice");
        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<UserSearchResponse>(ok.Value);
        var response = (UserSearchResponse)ok.Value!;
        Assert.AreEqual(2, response.Users.Count);
    }

    [TestMethod]
    public async Task Search_ExcludesCurrentUser()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "myself");

        var result = await controller.Search("myself");
        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<UserSearchResponse>(ok.Value);
        var response = (UserSearchResponse)ok.Value!;
        Assert.IsFalse(response.Users.Any());
    }

    [TestMethod]
    public async Task Search_ExcludesInactiveUsers()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "currentuser");
        await TestDbContextFactory.SeedUser(db, 2L, "inactive", isActive: false);

        var result = await controller.Search("inactive");
        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<UserSearchResponse>(ok.Value);
        var response = (UserSearchResponse)ok.Value!;
        Assert.IsFalse(response.Users.Any());
    }

    [TestMethod]
    public async Task GetPreKeyBundle_ValidDevice_ReturnsBundle()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "currentuser");
        await TestDbContextFactory.SeedUser(db, 2L, "target");
        await TestDbContextFactory.SeedDevice(db, 20L, 2L, "TargetDevice");

        var preKeyService = new PreKeyService(db);
        await preKeyService.StoreOneTimePreKeys(20L, [new OneTimePreKeyDto(1, Convert.ToBase64String(new byte[32]))]);

        var result = await controller.GetPreKeyBundle(2L, 20L);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<PreKeyBundleResponse>(ok.Value);
        var bundle = (PreKeyBundleResponse)ok.Value!;
        Assert.AreEqual(20L, bundle.DeviceId);
        Assert.IsNotNull(bundle.OneTimePreKey);
    }

    [TestMethod]
    public async Task GetPreKeyBundle_NoOtpAvailable_ReturnsNullOneTimePreKey()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "currentuser");
        await TestDbContextFactory.SeedUser(db, 2L, "target");
        await TestDbContextFactory.SeedDevice(db, 20L, 2L, "TargetDevice");

        var result = await controller.GetPreKeyBundle(2L, 20L);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<PreKeyBundleResponse>(ok.Value);
        var bundle = (PreKeyBundleResponse)ok.Value!;
        Assert.IsNull(bundle.OneTimePreKey);
    }

    [TestMethod]
    public async Task GetPreKeyBundle_InvalidDevice_ReturnsNotFound()
    {
        var (controller, _) = CreateController();
        var result = await controller.GetPreKeyBundle(2L, 999L);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task GetUserDevices_ReturnsActiveDevices()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "currentuser");
        await TestDbContextFactory.SeedUser(db, 2L, "target");
        await TestDbContextFactory.SeedDevice(db, 20L, 2L, "Active");
        var inactive = await TestDbContextFactory.SeedDevice(db, 21L, 2L, "Inactive");
        inactive.IsActive = false;
        await db.SaveChangesAsync();

        var result = await controller.GetUserDevices(2L);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<List<DeviceInfoResponse>>(ok.Value);
        var devices = (List<DeviceInfoResponse>)ok.Value!;
        Assert.AreEqual(1, devices.Count);
        Assert.AreEqual("Active", devices[0].DeviceName);
    }

    // --- Bounded Search Results ---

    [TestMethod]
    public async Task Search_ResultsAreBoundedByDefault()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "currentuser");

        // Create 60 users matching "testuser"
        for (int i = 2; i <= 61; i++) await TestDbContextFactory.SeedUser(db, (long)i, $"testuser{i:D3}");

        var result = await controller.Search("testuser");
        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<UserSearchResponse>(ok.Value);
        var response = (UserSearchResponse)ok.Value!;

        // Default take is 50
        Assert.AreEqual(50, response.Users.Count);
    }

    [TestMethod]
    public async Task Search_TakeLimitIsEnforced()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "currentuser");
        await TestDbContextFactory.SeedUser(db, 2L, "alice1");
        await TestDbContextFactory.SeedUser(db, 3L, "alice2");
        await TestDbContextFactory.SeedUser(db, 4L, "alice3");

        var result = await controller.Search("alice", take: 2);
        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<UserSearchResponse>(ok.Value);
        var response = (UserSearchResponse)ok.Value!;

        Assert.AreEqual(2, response.Users.Count);
    }

    [TestMethod]
    public async Task Search_SkipPaginationWorks()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L, "currentuser");
        await TestDbContextFactory.SeedUser(db, 2L, "alice1");
        await TestDbContextFactory.SeedUser(db, 3L, "alice2");
        await TestDbContextFactory.SeedUser(db, 4L, "alice3");

        var result = await controller.Search("alice", skip: 1, take: 10);
        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<UserSearchResponse>(ok.Value);
        var response = (UserSearchResponse)ok.Value!;

        Assert.AreEqual(2, response.Users.Count); // 3 total alice, skip 1, take 2 remaining
    }
}
