using Microsoft.AspNetCore.Mvc;
using ToledoMessage.Controllers;
using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Server.Tests.Controllers;

public class DevicesControllerTests
{
    private static (DevicesController controller, Data.ApplicationDbContext db) CreateController(decimal userId = 1m)
    {
        var db = TestDbContextFactory.Create();
        var preKeyService = new PreKeyService(db);
        var controller = new DevicesController(db, preKeyService);
        TestDbContextFactory.SetUser(controller, userId);
        return (controller, db);
    }

    [Fact]
    public async Task RegisterDevice_ValidRequest_ReturnsCreated()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);

        var request = new DeviceRegistrationRequest(
            "MyPhone",
            Convert.ToBase64String(new byte[32]),
            Convert.ToBase64String(new byte[1184]),
            Convert.ToBase64String(new byte[32]),
            Convert.ToBase64String(new byte[64]),
            1,
            Convert.ToBase64String(new byte[1184]),
            Convert.ToBase64String(new byte[64]),
            [new OneTimePreKeyDto(1, Convert.ToBase64String(new byte[32]))]);

        var result = await controller.RegisterDevice(request);
        Assert.IsType<CreatedResult>(result);
    }

    [Fact]
    public async Task RegisterDevice_MaxDevicesReached_ReturnsForbidden()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);

        // Seed 10 devices
        for (int i = 0; i < 10; i++)
        {
            await TestDbContextFactory.SeedDevice(db, 100m + i, 1m, $"Device{i}");
        }

        var request = new DeviceRegistrationRequest(
            "Device11",
            Convert.ToBase64String(new byte[32]),
            Convert.ToBase64String(new byte[1184]),
            Convert.ToBase64String(new byte[32]),
            Convert.ToBase64String(new byte[64]),
            1,
            Convert.ToBase64String(new byte[1184]),
            Convert.ToBase64String(new byte[64]),
            null);

        var result = await controller.RegisterDevice(request);
        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, status.StatusCode);
    }

    [Fact]
    public async Task ListDevices_ReturnsActiveDevicesOnly()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);
        var active = await TestDbContextFactory.SeedDevice(db, 10m, 1m, "Active");
        var inactive = await TestDbContextFactory.SeedDevice(db, 20m, 1m, "Inactive");
        inactive.IsActive = false;
        await db.SaveChangesAsync();

        var result = await controller.ListDevices();

        var ok = Assert.IsType<OkObjectResult>(result);
        var devices = Assert.IsAssignableFrom<List<DeviceInfoResponse>>(ok.Value);
        Assert.Single(devices);
        Assert.Equal("Active", devices[0].DeviceName);
    }

    [Fact]
    public async Task ListDevices_OtherUsersDevices_NotReturned()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedUser(db, 2m, "other");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m, "MyDevice");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m, "OtherDevice");

        var result = await controller.ListDevices();

        var ok = Assert.IsType<OkObjectResult>(result);
        var devices = Assert.IsAssignableFrom<List<DeviceInfoResponse>>(ok.Value);
        Assert.Single(devices);
        Assert.Equal("MyDevice", devices[0].DeviceName);
    }

    [Fact]
    public async Task RevokeDevice_OwnDevice_ReturnsNoContent()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);

        var result = await controller.RevokeDevice(10m);

        Assert.IsType<NoContentResult>(result);
        var device = await db.Devices.FindAsync(10m);
        Assert.False(device!.IsActive);
    }

    [Fact]
    public async Task RevokeDevice_OtherUsersDevice_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedUser(db, 2m, "other");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m);

        var result = await controller.RevokeDevice(20m);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RevokeDevice_NonExistent_ReturnsNotFound()
    {
        var (controller, _) = CreateController();
        var result = await controller.RevokeDevice(999m);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetPreKeyCount_OwnDevice_ReturnsCount()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        var preKeyService = new PreKeyService(db);
        await preKeyService.StoreOneTimePreKeys(10m,
        [
            new OneTimePreKeyDto(1, Convert.ToBase64String(new byte[32])),
            new OneTimePreKeyDto(2, Convert.ToBase64String(new byte[32]))
        ]);

        var result = await controller.GetPreKeyCount(10m);

        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic value = ok.Value!;
        Assert.Equal(2, (int)value.GetType().GetProperty("count")!.GetValue(value));
    }

    [Fact]
    public async Task GetPreKeyCount_OtherUsersDevice_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedUser(db, 2m, "other");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m);

        var result = await controller.GetPreKeyCount(20m);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ReplenishPreKeys_ValidRequest_ReturnsNoContent()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);

        var preKeys = new List<OneTimePreKeyDto>
        {
            new(100, Convert.ToBase64String(new byte[32])),
            new(101, Convert.ToBase64String(new byte[32]))
        };

        var result = await controller.ReplenishPreKeys(10m, preKeys);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(2, db.OneTimePreKeys.Count());
    }

    [Fact]
    public async Task ReplenishPreKeys_OtherUsersDevice_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedUser(db, 2m, "other");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m);

        var result = await controller.ReplenishPreKeys(20m, [new(1, Convert.ToBase64String(new byte[32]))]);
        Assert.IsType<NotFoundObjectResult>(result);
    }
}
