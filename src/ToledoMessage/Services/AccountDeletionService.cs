using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ToledoMessage.Data;
using ToledoMessage.Shared.Constants;

namespace ToledoMessage.Services;

public class AccountDeletionService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(ApplicationDbContext db, ILogger<AccountDeletionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DateTimeOffset> InitiateDeletionAsync(decimal userId)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        user.DeletionRequestedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Account deletion initiated for user {UserId}. Grace period ends {EndsAt}.",
            userId, user.DeletionRequestedAt.Value.AddDays(ProtocolConstants.AccountDeletionGracePeriodDays));

        return user.DeletionRequestedAt.Value;
    }

    public async Task CancelDeletionAsync(decimal userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user?.DeletionRequestedAt is not null)
        {
            user.DeletionRequestedAt = null;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Account deletion cancelled for user {UserId} via login.", userId);
        }
    }

    public async Task ProcessExpiredDeletionsAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-ProtocolConstants.AccountDeletionGracePeriodDays);

        var expiredUsers = await _db.Users
            .Where(u => u.IsActive && u.DeletionRequestedAt != null && u.DeletionRequestedAt <= cutoff)
            .Include(u => u.Devices)
            .ToListAsync(cancellationToken);

        foreach (var user in expiredUsers)
        {
            user.IsActive = false;

            // Anonymize PII so deleted accounts don't retain plaintext display names
            var hash = Convert.ToHexString(
                SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(user.Id.ToString())))
                [..16].ToLowerInvariant();
            user.DisplayName = $"deleted_{hash}";
            user.PasswordHash = string.Empty;

            foreach (var device in user.Devices)
            {
                device.IsActive = false;
            }

            // Revoke all refresh tokens
            var tokens = await _db.RefreshTokens
                .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
                .ToListAsync(cancellationToken);
            foreach (var token in tokens)
            {
                token.IsRevoked = true;
            }

            _logger.LogInformation("Account permanently deactivated and anonymized for user {UserId}.", user.Id);
        }

        if (expiredUsers.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}

public class AccountDeletionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccountDeletionHostedService> _logger;

    public AccountDeletionHostedService(IServiceScopeFactory scopeFactory, ILogger<AccountDeletionHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<AccountDeletionService>();
                await service.ProcessExpiredDeletionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired account deletions.");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
