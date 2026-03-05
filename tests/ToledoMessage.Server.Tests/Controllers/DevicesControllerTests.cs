using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using ToledoMessage.Controllers;
using ToledoMessage.Services;
using ToledoMessage.Shared.Constants;
using ToledoMessage.Shared.DTOs;
#pragma warning disable MSTEST0049

namespace ToledoMessage.Server.Tests.Controllers;

[TestClass, SuppressMessage("ReSharper", "UnusedVariable")]
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

    [TestMethod]
    public async Task RegisterDevice_ValidRequest_ReturnsCreated()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);

        var request = new DeviceRegistrationRequest(
            "MyPhone",
            Convert.ToBase64String(new byte[ProtocolConstants.Ed25519PublicKeySize]),
            Convert.ToBase64String(new byte[ProtocolConstants.MlDsa65PublicKeySize]),
            Convert.ToBase64String(new byte[ProtocolConstants.X25519PublicKeySize]),
            Convert.ToBase64String(new byte[ProtocolConstants.HybridSignatureSize]),
            1,
            Convert.ToBase64String(new byte[ProtocolConstants.MlKem768PublicKeySize]),
            Convert.ToBase64String(new byte[ProtocolConstants.HybridSignatureSize]),
            [new OneTimePreKeyDto(1, Convert.ToBase64String(new byte[ProtocolConstants.X25519PublicKeySize]))]);

        var result = await controller.RegisterDevice(request);
        Assert.IsInstanceOfType<CreatedResult>(result);
    }

    [TestMethod]
    public async Task RegisterDevice_MaxDevicesReached_ReturnsForbidden()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);

        // Seed 10 devices
        for (var i = 0; i < 10; i++) await TestDbContextFactory.SeedDevice(db, 100m + i, 1m, $"Device{i}");

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
        Assert.IsInstanceOfType<ObjectResult>(result);
        var status = (ObjectResult)result;
        Assert.AreEqual(403, status.StatusCode);
    }

    [TestMethod]
    public async Task ListDevices_ReturnsActiveDevicesOnly()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);
        var active = await TestDbContextFactory.SeedDevice(db, 10m, 1m, "Active");
        var inactive = await TestDbContextFactory.SeedDevice(db, 20m, 1m, "Inactive");
        inactive.IsActive = false;
        await db.SaveChangesAsync();

        var result = await controller.ListDevices();

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<List<DeviceInfoResponse>>(ok.Value);
        var devices = (List<DeviceInfoResponse>)ok.Value;
        Assert.AreEqual(1, devices.Count);
        Assert.AreEqual("Active", devices[0].DeviceName);
    }

    [TestMethod]
    public async Task ListDevices_OtherUsersDevices_NotReturned()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedUser(db, 2m, "other");
        await TestDbContextFactory.SeedDevice(db, 10m, 1m, "MyDevice");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m, "OtherDevice");

        var result = await controller.ListDevices();

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        Assert.IsInstanceOfType<List<DeviceInfoResponse>>(ok.Value);
        var devices = (List<DeviceInfoResponse>)ok.Value;
        Assert.AreEqual(1, devices.Count);
        Assert.AreEqual("MyDevice", devices[0].DeviceName);
    }

    [TestMethod]
    public async Task RevokeDevice_OwnDevice_ReturnsNoContent()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);

        var result = await controller.RevokeDevice(10m);

        Assert.IsInstanceOfType<NoContentResult>(result);
        var device = await db.Devices.FindAsync(10m);
        Assert.IsFalse(device?.IsActive ?? true);
    }

    [TestMethod]
    public async Task RevokeDevice_OtherUsersDevice_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedUser(db, 2m, "other");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m);

        var result = await controller.RevokeDevice(20m);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task RevokeDevice_NonExistent_ReturnsNotFound()
    {
        var (controller, _) = CreateController();
        var result = await controller.RevokeDevice(999m);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
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

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var ok = (OkObjectResult)result;
        dynamic? value = ok.Value;
        var dynamicObject = value?.GetType().GetProperty("count").GetValue(value);
        Assert.AreEqual(2, (int)(dynamicObject ?? throw new InvalidOperationException()));
    }

    [TestMethod]
    public async Task GetPreKeyCount_OtherUsersDevice_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedUser(db, 2m, "other");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m);

        var result = await controller.GetPreKeyCount(20m);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
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

        Assert.IsInstanceOfType<NoContentResult>(result);
        Assert.AreEqual(2, db.OneTimePreKeys.Count());
    }

    [TestMethod]
    public async Task ReplenishPreKeys_OtherUsersDevice_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedUser(db, 2m, "other");
        await TestDbContextFactory.SeedDevice(db, 20m, 2m);

        var result = await controller.ReplenishPreKeys(20m, [new OneTimePreKeyDto(1, Convert.ToBase64String(new byte[32]))]);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }
}
