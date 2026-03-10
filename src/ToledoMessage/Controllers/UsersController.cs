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
public class UsersController(ApplicationDbContext db, PreKeyService preKeyService) : BaseApiController
{
    /// <summary>
    /// Search users by display name (case-insensitive contains).
    /// Excludes the requesting user from results. Results bounded to 50.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(new UserSearchResponse([]));

        if (q.Length > Shared.Constants.ProtocolConstants.MaxSearchQueryLength)
            return BadRequest($"Search query must not exceed {Shared.Constants.ProtocolConstants.MaxSearchQueryLength} characters.");

        // Clamp take to a maximum of 50
        take = Math.Clamp(take, 1, 50);
        skip = Math.Max(skip, 0);

        var userId = GetUserId();

        // FR-015: Use EF.Functions.Like for efficient case-insensitive search
        var pattern = $"%{q}%";
        var users = await db.Users
            .Where(u => u.IsActive && u.Id != userId &&
                        (EF.Functions.Like(u.Username, pattern) ||
                         EF.Functions.Like(u.DisplayName, pattern) ||
                         (u.DisplayNameSecondary != null && EF.Functions.Like(u.DisplayNameSecondary, pattern))))
            .OrderBy(static u => u.Username)
            .Skip(skip)
            .Take(take)
            .Select(static u => new UserSearchResult(
                u.Id,
                u.Username,
                u.DisplayName,
                u.Devices.Count(static d => d.IsActive),
                u.DisplayNameSecondary))
            .ToListAsync();

        return Ok(new UserSearchResponse(users));
    }

    /// <summary>
    /// Fetch pre-key bundle for a specific device of a user, consuming one one-time pre-key.
    /// </summary>
    [HttpGet("{userId}/prekey-bundle")]
    public async Task<IActionResult> GetPreKeyBundle(long userId, [FromQuery] long deviceId)
    {
        var device = await db.Devices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId && d.IsActive);

        if (device == null)
            return NotFound("Device not found.");

        var consumedKey = await preKeyService.ConsumeOneTimePreKey(deviceId);

        var oneTimePreKey = consumedKey != null
            ? new OneTimePreKeyDto(consumedKey.KeyId, Convert.ToBase64String(consumedKey.PublicKey))
            : null;

        var bundle = new PreKeyBundleResponse(
            device.Id,
            Convert.ToBase64String(device.IdentityPublicKeyClassical),
            Convert.ToBase64String(device.IdentityPublicKeyPostQuantum),
            Convert.ToBase64String(device.SignedPreKeyPublic),
            Convert.ToBase64String(device.SignedPreKeySignature),
            device.SignedPreKeyId,
            Convert.ToBase64String(device.KyberPreKeyPublic),
            Convert.ToBase64String(device.KyberPreKeySignature),
            oneTimePreKey);

        return Ok(bundle);
    }

    /// <summary>
    /// List all active devices for a specific user (used for fan-out encryption).
    /// </summary>
    [HttpGet("{userId}/devices")]
    public async Task<IActionResult> GetUserDevices(long userId)
    {
        var devices = await db.Devices
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(static d => new DeviceInfoResponse(d.Id, d.DeviceName, d.CreatedAt, d.LastSeenAt))
            .ToListAsync();

        return Ok(devices);
    }
}
