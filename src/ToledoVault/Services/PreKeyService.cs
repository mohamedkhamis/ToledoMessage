using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Toledo.SharedKernel.Helpers;
using ToledoVault.Data;
using ToledoVault.Models;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Services;

public class PreKeyService(ApplicationDbContext db)
{
    /// <summary>Store one-time pre-keys for a device. Validates Base64 input.</summary>
    public async Task StoreOneTimePreKeys(long deviceId, List<OneTimePreKeyDto> preKeys)
    {
        foreach (var pk in preKeys)
        {
            byte[] publicKeyBytes;
            try
            {
                publicKeyBytes = Convert.FromBase64String(pk.PublicKey);
            }
            catch (FormatException)
            {
                throw new ArgumentException($"Invalid Base64 in one-time pre-key {pk.KeyId}.");
            }

            db.OneTimePreKeys.Add(new OneTimePreKey
            {
                Id = IdGenerator.GetNewId(),
                DeviceId = deviceId,
                KeyId = pk.KeyId,
                PublicKey = publicKeyBytes,
                IsUsed = false
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Consume one unused one-time pre-key for a device. Returns null if exhausted.</summary>
    /// <remarks>
    /// Uses raw SQL with OUTPUT clause for atomic claim to prevent race conditions
    /// where two concurrent requests could consume the same one-time pre-key.
    /// Falls back to EF-based approach for in-memory provider (testing).
    /// </remarks>
    public async Task<OneTimePreKey?> ConsumeOneTimePreKey(long deviceId)
    {
        if (db.Database.IsRelational())
        {
            // Atomic: claim the lowest-KeyId unused pre-key in a single UPDATE+OUTPUT statement.
            // This prevents two concurrent requests from consuming the same key.
            // Must use explicit SqlParameter with precision/scale for decimal(28,8) columns.
            var deviceParam = new SqlParameter("@deviceId", System.Data.SqlDbType.BigInt)
            {
                Value = deviceId
            };
            var claimed = await db.Database.SqlQueryRaw<long>(
                // ReSharper disable once FormatStringProblem
                """
                WITH cte AS (
                    SELECT TOP(1) Id, IsUsed
                    FROM OneTimePreKeys
                    WHERE DeviceId = @deviceId AND IsUsed = 0
                    ORDER BY KeyId
                )
                UPDATE cte
                SET IsUsed = 1
                OUTPUT inserted.Id
                """, deviceParam).ToListAsync();

            if (claimed.Count == 0)
                return null;

            return await db.OneTimePreKeys.FirstAsync(k => k.Id == claimed[0]);
        }

        // Fallback for in-memory provider (unit tests)
        var key = await db.OneTimePreKeys
            .Where(k => k.DeviceId == deviceId && !k.IsUsed)
            .OrderBy(static k => k.KeyId)
            .FirstOrDefaultAsync();

        // ReSharper disable once InvertIf
        if (key != null)
        {
            key.IsUsed = true;
            await db.SaveChangesAsync();
        }

        return key;
    }

    /// <summary>Count remaining unused pre-keys for a device.</summary>
    public async Task<int> CountRemainingPreKeys(long deviceId)
    {
        return await db.OneTimePreKeys.CountAsync(k => k.DeviceId == deviceId && !k.IsUsed);
    }
}
