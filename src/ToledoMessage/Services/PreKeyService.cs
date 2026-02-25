using Microsoft.EntityFrameworkCore;
using Toledo.SharedKernel.Helpers;
using ToledoMessage.Data;
using ToledoMessage.Models;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Services;

public class PreKeyService
{
    private readonly ApplicationDbContext _db;

    public PreKeyService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Store one-time pre-keys for a device.</summary>
    public async Task StoreOneTimePreKeys(decimal deviceId, List<OneTimePreKeyDto> preKeys)
    {
        foreach (var pk in preKeys)
        {
            _db.OneTimePreKeys.Add(new OneTimePreKey
            {
                Id = DecimalTools.GetNewId(),
                DeviceId = deviceId,
                KeyId = pk.KeyId,
                PublicKey = Convert.FromBase64String(pk.PublicKey),
                IsUsed = false
            });
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>Consume one unused one-time pre-key for a device. Returns null if exhausted.</summary>
    public async Task<OneTimePreKey?> ConsumeOneTimePreKey(decimal deviceId)
    {
        var key = await _db.OneTimePreKeys
            .Where(k => k.DeviceId == deviceId && !k.IsUsed)
            .OrderBy(k => k.KeyId)
            .FirstOrDefaultAsync();

        if (key != null)
        {
            key.IsUsed = true;
            await _db.SaveChangesAsync();
        }

        return key;
    }

    /// <summary>Count remaining unused pre-keys for a device.</summary>
    public async Task<int> CountRemainingPreKeys(decimal deviceId)
    {
        return await _db.OneTimePreKeys.CountAsync(k => k.DeviceId == deviceId && !k.IsUsed);
    }
}
