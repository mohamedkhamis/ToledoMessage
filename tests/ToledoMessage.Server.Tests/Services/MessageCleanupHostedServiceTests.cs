using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ToledoMessage.Services;

namespace ToledoMessage.Server.Tests.Services;

public class MessageCleanupHostedServiceTests
{
    private static (MessageCleanupHostedService service, IServiceScopeFactory scopeFactory) CreateService()
    {
        var db = TestDbContextFactory.Create();
        var hubContext = new StubHubContext();
        var relayService = new MessageRelayService(db, hubContext);

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(relayService);
        services.AddSingleton<MessageRelayService>();
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var service = new MessageCleanupHostedService(scopeFactory, NullLogger<MessageCleanupHostedService>.Instance);
        return (service, scopeFactory);
    }

    [Fact]
    public async Task ExecuteAsync_StopsOnCancellation()
    {
        var (service, _) = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // StartAsync internally calls ExecuteAsync; it should exit gracefully when cancelled
        await service.StartAsync(cts.Token);
        await Task.Delay(200); // Give it time to observe cancellation
        await service.StopAsync(CancellationToken.None);

        // If we reach here without hanging, the service correctly handled cancellation
        Assert.True(true);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotThrowOnNormalStop()
    {
        var (service, _) = CreateService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Service should complete without throwing
        Assert.True(true);
    }
}
