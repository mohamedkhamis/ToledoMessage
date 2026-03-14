using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ToledoVault.Data;
using ToledoVault.Shared.Constants;

// ReSharper disable RemoveRedundantBraces

namespace ToledoVault.Services;

public class AccountDeletionService(ApplicationDbContext db, ILogger<AccountDeletionService> logger)
{
    public async Task<DateTimeOffset> InitiateDeletionAsync(long userId)
    {
        var user = await db.Users.FindAsync(userId)
                   ?? throw new InvalidOperationException("User not found.");

        user.DeletionRequestedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Account deletion initiated for user {UserId}. Grace period ends {EndsAt}.",
            userId, user.DeletionRequestedAt.Value.AddDays(ProtocolConstants.AccountDeletionGracePeriodDays));

        return user.DeletionRequestedAt.Value;
    }

    public async Task CancelDeletionAsync(long userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user?.DeletionRequestedAt is not null)
        {
            user.DeletionRequestedAt = null;
            await db.SaveChangesAsync();
            logger.LogInformation("Account deletion cancelled for user {UserId} via login.", userId);
        }
    }

    public async Task ProcessExpiredDeletionsAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-ProtocolConstants.AccountDeletionGracePeriodDays);

        var expiredUsers = await db.Users
            .Where(u => u.IsActive && u.DeletionRequestedAt != null && u.DeletionRequestedAt <= cutoff)
            .Include(static u => u.Devices)
            .ToListAsync(cancellationToken);

        foreach (var user in expiredUsers)
        {
            user.IsActive = false;

            // Anonymize PII so deleted accounts don't retain plaintext display names
            var hash = Convert.ToHexString(
                    SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(user.Id.ToString(CultureInfo.InvariantCulture))))
                [..16].ToLowerInvariant();
            user.DisplayName = $"deleted_{hash}";
            user.PasswordHash = string.Empty;

            foreach (var device in user.Devices)
            {
                device.IsActive = false;
            }

            // Revoke all refresh tokens
            var tokens = await db.RefreshTokens
                .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
                .ToListAsync(cancellationToken);
            foreach (var token in tokens)
            {
                token.IsRevoked = true;
            }

            logger.LogInformation("Account permanently deactivated and anonymized for user {UserId}.", user.Id);
        }

        if (expiredUsers.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

public class AccountDeletionHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<AccountDeletionHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<AccountDeletionService>();
                await service.ProcessExpiredDeletionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing expired account deletions.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
