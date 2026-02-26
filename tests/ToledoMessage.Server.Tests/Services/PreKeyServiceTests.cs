using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Server.Tests.Services;

public class PreKeyServiceTests
{
    [Fact]
    public async Task StoreOneTimePreKeys_StoresKeys()
    {
        var db = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        var service = new PreKeyService(db);

        var preKeys = new List<OneTimePreKeyDto>
        {
            new(1, Convert.ToBase64String(new byte[32])),
            new(2, Convert.ToBase64String(new byte[32])),
            new(3, Convert.ToBase64String(new byte[32]))
        };

        await service.StoreOneTimePreKeys(10m, preKeys);

        var count = await service.CountRemainingPreKeys(10m);
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task ConsumeOneTimePreKey_ReturnsKeyInOrder()
    {
        var db = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        var service = new PreKeyService(db);

        var preKeys = new List<OneTimePreKeyDto>
        {
            new(5, Convert.ToBase64String(new byte[32])),
            new(3, Convert.ToBase64String(new byte[32])),
            new(7, Convert.ToBase64String(new byte[32]))
        };
        await service.StoreOneTimePreKeys(10m, preKeys);

        var consumed = await service.ConsumeOneTimePreKey(10m);

        Assert.NotNull(consumed);
        Assert.Equal(3, consumed.KeyId); // lowest KeyId first
        Assert.True(consumed.IsUsed);
    }

    [Fact]
    public async Task ConsumeOneTimePreKey_MarksAsUsed()
    {
        var db = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        var service = new PreKeyService(db);

        await service.StoreOneTimePreKeys(10m, [new(1, Convert.ToBase64String(new byte[32]))]);

        await service.ConsumeOneTimePreKey(10m);
        var remaining = await service.CountRemainingPreKeys(10m);

        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task ConsumeOneTimePreKey_NoKeysAvailable_ReturnsNull()
    {
        var db = TestDbContextFactory.Create();
        var service = new PreKeyService(db);

        var result = await service.ConsumeOneTimePreKey(999m);

        Assert.Null(result);
    }

    [Fact]
    public async Task CountRemainingPreKeys_NoKeys_ReturnsZero()
    {
        var db = TestDbContextFactory.Create();
        var service = new PreKeyService(db);

        var count = await service.CountRemainingPreKeys(999m);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ConsumeOneTimePreKey_ConsumesSequentially()
    {
        var db = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedUser(db, 1m);
        await TestDbContextFactory.SeedDevice(db, 10m, 1m);
        var service = new PreKeyService(db);

        await service.StoreOneTimePreKeys(10m,
        [
            new(1, Convert.ToBase64String(new byte[32])),
            new(2, Convert.ToBase64String(new byte[32]))
        ]);

        var first = await service.ConsumeOneTimePreKey(10m);
        var second = await service.ConsumeOneTimePreKey(10m);
        var third = await service.ConsumeOneTimePreKey(10m);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Null(third);
        Assert.Equal(1, first.KeyId);
        Assert.Equal(2, second.KeyId);
    }
}
