using Microsoft.EntityFrameworkCore;
using ToledoVault.Data;

namespace ToledoVault.Services;

/// <inheritdoc />
/// <summary>
/// Background service that periodically cleans up expired disappearing messages
/// and consumed pre-keys from the database. Runs every 5 minutes with ±30s jitter.
/// </summary>
public class MessageCleanupHostedService(IServiceScopeFactory scopeFactory, ILogger<MessageCleanupHostedService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Jitter = TimeSpan.FromSeconds(30);
    private readonly Random _random = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MessageCleanupHostedService started. Cleanup interval: {Interval}, Jitter: ±{JitterSeconds}s", Interval, Jitter.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            // FR-017: Add random jitter to prevent all instances running at exactly the same time
            var jitterMs = (int)((_random.NextDouble() * 2 - 1) * Jitter.TotalMilliseconds);
            var delayWithJitter = Interval + TimeSpan.FromMilliseconds(jitterMs);

            try
            {
                await Task.Delay(delayWithJitter, stoppingToken);
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
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var expiredTokenCount = await db.RefreshTokens
                    .Where(static rt => rt.IsRevoked || rt.ExpiresAt <= DateTimeOffset.UtcNow)
                    .ExecuteDeleteAsync(stoppingToken);

                if (expiredTokenCount > 0) logger.LogInformation("Cleaned up {Count} expired/revoked refresh token(s).", expiredTokenCount);

                // FR-018: Clean up consumed one-time pre-keys
                // Once a pre-key is consumed (IsUsed=true), it can never be used again.
                // Clean them up to prevent unbounded table growth.
                var consumedPreKeyCount = await db.OneTimePreKeys
                    .Where(static k => k.IsUsed)
                    .ExecuteDeleteAsync(stoppingToken);

                if (consumedPreKeyCount > 0) logger.LogInformation("Cleaned up {Count} consumed one-time pre-key(s).", consumedPreKeyCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during cleanup.");
            }
        }

        logger.LogInformation("MessageCleanupHostedService stopped.");
    }
}
