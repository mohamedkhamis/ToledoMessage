using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Server.Tests.Services;

[TestClass]
public class PreKeyServiceTests
{
    [TestMethod]
    public async Task StoreOneTimePreKeys_StoresKeys()
    {
        var db = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedUser(db, 1L);
        await TestDbContextFactory.SeedDevice(db, 10L, 1L);
        var service = new PreKeyService(db);

        var preKeys = new List<OneTimePreKeyDto>
        {
            new(1, Convert.ToBase64String(new byte[32])),
            new(2, Convert.ToBase64String(new byte[32])),
            new(3, Convert.ToBase64String(new byte[32]))
        };

        await service.StoreOneTimePreKeys(10L, preKeys);

        var count = await service.CountRemainingPreKeys(10L);
        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public async Task ConsumeOneTimePreKey_ReturnsKeyInOrder()
    {
        var db = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedUser(db, 1L);
        await TestDbContextFactory.SeedDevice(db, 10L, 1L);
        var service = new PreKeyService(db);

        var preKeys = new List<OneTimePreKeyDto>
        {
            new(5, Convert.ToBase64String(new byte[32])),
            new(3, Convert.ToBase64String(new byte[32])),
            new(7, Convert.ToBase64String(new byte[32]))
        };
        await service.StoreOneTimePreKeys(10L, preKeys);

        var consumed = await service.ConsumeOneTimePreKey(10L);

        Assert.IsNotNull(consumed);
        Assert.AreEqual(3, consumed.KeyId); // lowest KeyId first
        Assert.IsTrue(consumed.IsUsed);
    }

    [TestMethod]
    public async Task ConsumeOneTimePreKey_MarksAsUsed()
    {
        var db = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedUser(db, 1L);
        await TestDbContextFactory.SeedDevice(db, 10L, 1L);
        var service = new PreKeyService(db);

        await service.StoreOneTimePreKeys(10L, [new OneTimePreKeyDto(1, Convert.ToBase64String(new byte[32]))]);

        await service.ConsumeOneTimePreKey(10L);
        var remaining = await service.CountRemainingPreKeys(10L);

        Assert.AreEqual(0, remaining);
    }

    [TestMethod]
    public async Task ConsumeOneTimePreKey_NoKeysAvailable_ReturnsNull()
    {
        var db = TestDbContextFactory.Create();
        var service = new PreKeyService(db);

        var result = await service.ConsumeOneTimePreKey(999L);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CountRemainingPreKeys_NoKeys_ReturnsZero()
    {
        var db = TestDbContextFactory.Create();
        var service = new PreKeyService(db);

        var count = await service.CountRemainingPreKeys(999L);

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task ConsumeOneTimePreKey_ConsumesSequentially()
    {
        var db = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedUser(db, 1L);
        await TestDbContextFactory.SeedDevice(db, 10L, 1L);
        var service = new PreKeyService(db);

        await service.StoreOneTimePreKeys(10L,
        [
            new OneTimePreKeyDto(1, Convert.ToBase64String(new byte[32])),
            new OneTimePreKeyDto(2, Convert.ToBase64String(new byte[32]))
        ]);

        var first = await service.ConsumeOneTimePreKey(10L);
        var second = await service.ConsumeOneTimePreKey(10L);
        var third = await service.ConsumeOneTimePreKey(10L);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.IsNull(third);
        Assert.AreEqual(1, first.KeyId);
        Assert.AreEqual(2, second.KeyId);
    }
}
