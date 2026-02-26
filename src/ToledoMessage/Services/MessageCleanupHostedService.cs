using Microsoft.EntityFrameworkCore;

namespace ToledoMessage.Services;

/// <summary>
/// Background service that periodically cleans up expired disappearing messages
/// from the database. Runs every 5 minutes.
/// </summary>
public class MessageCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MessageCleanupHostedService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public MessageCleanupHostedService(IServiceScopeFactory scopeFactory, ILogger<MessageCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MessageCleanupHostedService started. Cleanup interval: {Interval}", Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var relayService = scope.ServiceProvider.GetRequiredService<MessageRelayService>();
                var deletedCount = await relayService.CleanupExpiredMessages();

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} expired disappearing message(s).", deletedCount);
                }

                // Also clean up expired/revoked refresh tokens
                var db = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
                var expiredTokenCount = await db.RefreshTokens
                    .Where(rt => rt.IsRevoked || rt.ExpiresAt <= DateTimeOffset.UtcNow)
                    .ExecuteDeleteAsync(stoppingToken);

                if (expiredTokenCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} expired/revoked refresh token(s).", expiredTokenCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during cleanup.");
            }
        }

        _logger.LogInformation("MessageCleanupHostedService stopped.");
    }
}
