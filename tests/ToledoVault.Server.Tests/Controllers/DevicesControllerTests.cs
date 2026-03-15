using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ToledoVault.Controllers;
using ToledoVault.Services;
using ToledoVault.Shared.Constants;
using ToledoVault.Shared.DTOs;
using ToledoVault.Server.Tests.Services;

#pragma warning disable MSTEST0049

namespace ToledoVault.Server.Tests.Controllers;

[TestClass]
[SuppressMessage("ReSharper", "UnusedVariable")]
public class DevicesControllerTests
{
    private static (DevicesController controller, Data.ApplicationDbContext db) CreateController(long userId = 1L)
    {
        var db = TestDbContextFactory.Create();
        var preKeyService = new PreKeyService(db);
        var hubContext = new StubHubContext();
        var relayService = new MessageRelayService(db, hubContext);
        var controller = new DevicesController(db, preKeyService, relayService, NullLogger<DevicesController>.Instance);
        TestDbContextFactory.SetUser(controller, userId);
        return (controller, db);
    }

    [TestMethod]
    public async Task RegisterDevice_ValidRequest_ReturnsCreated()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L);

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
        await TestDbContextFactory.SeedUser(db, 1L);

        // Seed 10 devices
        for (var i = 0; i < 10; i++) await TestDbContextFactory.SeedDevice(db, 100L + i, 1L, $"Device{i}");

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
        await TestDbContextFactory.SeedUser(db, 1L);
        var active = await TestDbContextFactory.SeedDevice(db, 10L, 1L, "Active");
        var inactive = await TestDbContextFactory.SeedDevice(db, 20L, 1L, "Inactive");
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
        await TestDbContextFactory.SeedUser(db, 1L);
        await TestDbContextFactory.SeedUser(db, 2L, "other");
        await TestDbContextFactory.SeedDevice(db, 10L, 1L, "MyDevice");
        await TestDbContextFactory.SeedDevice(db, 20L, 2L, "OtherDevice");

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
        await TestDbContextFactory.SeedUser(db, 1L);
        await TestDbContextFactory.SeedDevice(db, 10L, 1L);

        var result = await controller.RevokeDevice(10L);

        Assert.IsInstanceOfType<NoContentResult>(result);
        var device = await db.Devices.FindAsync(10L);
        Assert.IsFalse(device?.IsActive ?? true);
    }

    [TestMethod]
    public async Task RevokeDevice_OtherUsersDevice_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L);
        await TestDbContextFactory.SeedUser(db, 2L, "other");
        await TestDbContextFactory.SeedDevice(db, 20L, 2L);

        var result = await controller.RevokeDevice(20L);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task RevokeDevice_NonExistent_ReturnsNotFound()
    {
        var (controller, _) = CreateController();
        var result = await controller.RevokeDevice(999L);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task GetPreKeyCount_OwnDevice_ReturnsCount()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L);
        await TestDbContextFactory.SeedDevice(db, 10L, 1L);
        var preKeyService = new PreKeyService(db);
        await preKeyService.StoreOneTimePreKeys(10L,
        [
            new OneTimePreKeyDto(1, Convert.ToBase64String(new byte[32])),
            new OneTimePreKeyDto(2, Convert.ToBase64String(new byte[32]))
        ]);

        var result = await controller.GetPreKeyCount(10L);

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
        await TestDbContextFactory.SeedUser(db, 1L);
        await TestDbContextFactory.SeedUser(db, 2L, "other");
        await TestDbContextFactory.SeedDevice(db, 20L, 2L);

        var result = await controller.GetPreKeyCount(20L);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }

    [TestMethod]
    public async Task ReplenishPreKeys_ValidRequest_ReturnsNoContent()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L);
        await TestDbContextFactory.SeedDevice(db, 10L, 1L);

        var preKeys = new List<OneTimePreKeyDto>
        {
            new(100, Convert.ToBase64String(new byte[32])),
            new(101, Convert.ToBase64String(new byte[32]))
        };

        var result = await controller.ReplenishPreKeys(10L, preKeys);

        Assert.IsInstanceOfType<NoContentResult>(result);
        Assert.AreEqual(2, db.OneTimePreKeys.Count());
    }

    [TestMethod]
    public async Task ReplenishPreKeys_OtherUsersDevice_ReturnsNotFound()
    {
        var (controller, db) = CreateController();
        await TestDbContextFactory.SeedUser(db, 1L);
        await TestDbContextFactory.SeedUser(db, 2L, "other");
        await TestDbContextFactory.SeedDevice(db, 20L, 2L);

        var result = await controller.ReplenishPreKeys(20L, [new OneTimePreKeyDto(1, Convert.ToBase64String(new byte[32]))]);
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }
}
