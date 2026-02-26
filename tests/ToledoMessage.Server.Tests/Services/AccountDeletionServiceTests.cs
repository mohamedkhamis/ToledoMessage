using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ToledoMessage.Services;
using ToledoMessage.Shared.Constants;

namespace ToledoMessage.Server.Tests.Services;

public class AccountDeletionServiceTests
{
    private static AccountDeletionService CreateService(Data.ApplicationDbContext db)
    {
        return new AccountDeletionService(db, NullLogger<AccountDeletionService>.Instance);
    }

    [Fact]
    public async Task InitiateDeletionAsync_SetsDeletionRequestedAt()
    {
        var db = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedUser(db, 1m);
        var service = CreateService(db);

        var result = await service.InitiateDeletionAsync(1m);

        var user = await db.Users.FindAsync(1m);
        Assert.NotNull(user!.DeletionRequestedAt);
        Assert.Equal(result, user.DeletionRequestedAt.Value);
    }

    [Fact]
    public async Task InitiateDeletionAsync_UserNotFound_Throws()
    {
        var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.InitiateDeletionAsync(999m));
    }

    [Fact]
    public async Task CancelDeletionAsync_ClearsDeletionRequestedAt()
    {
        var db = TestDbContextFactory.Create();
        var user = await TestDbContextFactory.SeedUser(db, 1m);
        user.DeletionRequestedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.CancelDeletionAsync(1m);

        var refreshedUser = await db.Users.FindAsync(1m);
        Assert.Null(refreshedUser!.DeletionRequestedAt);
    }

    [Fact]
    public async Task CancelDeletionAsync_NoDeletionPending_DoesNothing()
    {
        var db = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedUser(db, 1m);
        var service = CreateService(db);

        // Should not throw
        await service.CancelDeletionAsync(1m);

        var user = await db.Users.FindAsync(1m);
        Assert.Null(user!.DeletionRequestedAt);
    }

    [Fact]
    public async Task CancelDeletionAsync_UserNotFound_DoesNothing()
    {
        var db = TestDbContextFactory.Create();
        var service = CreateService(db);

        // Should not throw
        await service.CancelDeletionAsync(999m);
    }

    [Fact]
    public async Task ProcessExpiredDeletionsAsync_DeactivatesExpiredAccounts()
    {
        var db = TestDbContextFactory.Create();
        var user = await TestDbContextFactory.SeedUser(db, 1m);
        var device = await TestDbContextFactory.SeedDevice(db, 10m, 1m);

        // Set deletion to well past the grace period
        user.DeletionRequestedAt = DateTimeOffset.UtcNow.AddDays(-(ProtocolConstants.AccountDeletionGracePeriodDays + 1));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.ProcessExpiredDeletionsAsync(CancellationToken.None);

        var refreshedUser = await db.Users.FindAsync(1m);
        Assert.False(refreshedUser!.IsActive);

        var refreshedDevice = await db.Devices.FindAsync(10m);
        Assert.False(refreshedDevice!.IsActive);
    }

    [Fact]
    public async Task ProcessExpiredDeletionsAsync_RevokesRefreshTokens()
    {
        var db = TestDbContextFactory.Create();
        var user = await TestDbContextFactory.SeedUser(db, 1m);

        user.DeletionRequestedAt = DateTimeOffset.UtcNow.AddDays(-(ProtocolConstants.AccountDeletionGracePeriodDays + 1));
        await db.SaveChangesAsync();

        db.RefreshTokens.Add(new Models.RefreshToken
        {
            Id = 100m,
            UserId = 1m,
            Token = "test-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow,
            IsRevoked = false
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.ProcessExpiredDeletionsAsync(CancellationToken.None);

        var token = await db.RefreshTokens.FindAsync(100m);
        Assert.True(token!.IsRevoked);
    }

    [Fact]
    public async Task ProcessExpiredDeletionsAsync_SkipsAccountsWithinGracePeriod()
    {
        var db = TestDbContextFactory.Create();
        var user = await TestDbContextFactory.SeedUser(db, 1m);

        // Set deletion to only 1 day ago (within 7-day grace period)
        user.DeletionRequestedAt = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.ProcessExpiredDeletionsAsync(CancellationToken.None);

        var refreshedUser = await db.Users.FindAsync(1m);
        Assert.True(refreshedUser!.IsActive); // Should still be active
    }

    [Fact]
    public async Task ProcessExpiredDeletionsAsync_SkipsAlreadyDeactivated()
    {
        var db = TestDbContextFactory.Create();
        var user = await TestDbContextFactory.SeedUser(db, 1m, isActive: false);
        user.DeletionRequestedAt = DateTimeOffset.UtcNow.AddDays(-30);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Should not throw and should not process already-inactive users
        await service.ProcessExpiredDeletionsAsync(CancellationToken.None);
    }
}
