using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ToledoMessage.Data;
using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly PreKeyService _preKeyService;

    public UsersController(ApplicationDbContext db, PreKeyService preKeyService)
    {
        _db = db;
        _preKeyService = preKeyService;
    }

    /// <summary>
    /// Search users by display name (case-insensitive contains).
    /// Excludes the requesting user from results.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(new UserSearchResponse([]));

        var userId = GetUserId();

        var users = await _db.Users
            .Where(u => u.IsActive && u.Id != userId && u.DisplayName.Contains(q))
            .Select(u => new UserSearchResult(
                u.Id,
                u.DisplayName,
                u.Devices.Count(d => d.IsActive)))
            .ToListAsync();

        return Ok(new UserSearchResponse(users));
    }

    /// <summary>
    /// Fetch pre-key bundle for a specific device of a user, consuming one one-time pre-key.
    /// </summary>
    [HttpGet("{userId}/prekey-bundle")]
    public async Task<IActionResult> GetPreKeyBundle(decimal userId, [FromQuery] decimal deviceId)
    {
        var device = await _db.Devices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId && d.IsActive);

        if (device == null)
            return NotFound("Device not found.");

        var consumedKey = await _preKeyService.ConsumeOneTimePreKey(deviceId);

        OneTimePreKeyDto? oneTimePreKey = consumedKey != null
            ? new OneTimePreKeyDto(consumedKey.KeyId, Convert.ToBase64String(consumedKey.PublicKey))
            : null;

        var bundle = new PreKeyBundleResponse(
            DeviceId: device.Id,
            IdentityPublicKeyClassical: Convert.ToBase64String(device.IdentityPublicKeyClassical),
            IdentityPublicKeyPostQuantum: Convert.ToBase64String(device.IdentityPublicKeyPostQuantum),
            SignedPreKeyPublic: Convert.ToBase64String(device.SignedPreKeyPublic),
            SignedPreKeySignature: Convert.ToBase64String(device.SignedPreKeySignature),
            SignedPreKeyId: device.SignedPreKeyId,
            KyberPreKeyPublic: Convert.ToBase64String(device.KyberPreKeyPublic),
            KyberPreKeySignature: Convert.ToBase64String(device.KyberPreKeySignature),
            OneTimePreKey: oneTimePreKey);

        return Ok(bundle);
    }

    /// <summary>
    /// List all active devices for a specific user (used for fan-out encryption).
    /// </summary>
    [HttpGet("{userId}/devices")]
    public async Task<IActionResult> GetUserDevices(decimal userId)
    {
        var devices = await _db.Devices
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(d => new DeviceInfoResponse(d.Id, d.DeviceName, d.CreatedAt, d.LastSeenAt))
            .ToListAsync();

        return Ok(devices);
    }

    private decimal GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return decimal.Parse(sub!);
    }
}
