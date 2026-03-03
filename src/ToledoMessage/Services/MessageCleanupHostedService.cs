using Microsoft.EntityFrameworkCore;

namespace ToledoMessage.Services;

/// <inheritdoc />
/// <summary>
/// Background service that periodically cleans up expired disappearing messages
/// from the database. Runs every 5 minutes.
/// </summary>
public class MessageCleanupHostedService(IServiceScopeFactory scopeFactory, ILogger<MessageCleanupHostedService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MessageCleanupHostedService started. Cleanup interval: {Interval}", Interval);

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
                using var scope = scopeFactory.CreateScope();
                var relayService = scope.ServiceProvider.GetRequiredService<MessageRelayService>();
                var deletedCount = await relayService.CleanupExpiredMessages();

                if (deletedCount > 0) logger.LogInformation("Cleaned up {Count} expired disappearing message(s).", deletedCount);

                // Also clean up expired/revoked refresh tokens
                var db = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
                var expiredTokenCount = await db.RefreshTokens
                    .Where(static rt => rt.IsRevoked || rt.ExpiresAt <= DateTimeOffset.UtcNow)
                    .ExecuteDeleteAsync(stoppingToken);

                if (expiredTokenCount > 0) logger.LogInformation("Cleaned up {Count} expired/revoked refresh token(s).", expiredTokenCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during cleanup.");
            }
        }

        logger.LogInformation("MessageCleanupHostedService stopped.");
    }
}
