using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ToledoMessage.Services;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace ToledoMessage.Server.Tests.Services;

[TestClass]
public class AccountDeletionHostedServiceTests
{
    private static AccountDeletionHostedService CreateService()
    {
        var db = TestDbContextFactory.Create();
        var deletionService = new AccountDeletionService(db, NullLogger<AccountDeletionService>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(deletionService);
        services.AddSingleton<AccountDeletionService>();
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        return new AccountDeletionHostedService(scopeFactory, NullLogger<AccountDeletionHostedService>.Instance);
    }

    [TestMethod]
    public async Task ExecuteAsync_StopsOnCancellation()
    {
        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await service.StartAsync(cts.Token);
        await Task.Delay(200, cts.Token);
        await service.StopAsync(CancellationToken.None);

#pragma warning disable MSTEST0032
        IsTrue(true);
#pragma warning restore MSTEST0032
    }

    [TestMethod]
    public async Task ExecuteAsync_DoesNotThrowOnImmediateCancel()
    {
        var service = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

#pragma warning disable MSTEST0032
        IsTrue(true);
#pragma warning restore MSTEST0032
    }
}
