using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Toledo.SharedKernel.Helpers;
using ToledoMessage.Data;
using ToledoMessage.Models;
using ToledoMessage.Services;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize]
public class DevicesController : BaseApiController
{
    private readonly ApplicationDbContext _db;
    private readonly PreKeyService _preKeyService;

    public DevicesController(ApplicationDbContext db, PreKeyService preKeyService)
    {
        _db = db;
        _preKeyService = preKeyService;
    }

    /// <summary>
    /// Register a new device for the current user.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RegisterDevice([FromBody] DeviceRegistrationRequest request)
    {
        var userId = GetUserId();

        var activeDeviceCount = await _db.Devices.CountAsync(d => d.UserId == userId && d.IsActive);
        if (activeDeviceCount >= 10)
            return StatusCode(403, "Maximum number of devices (10) reached.");

        // Validate all Base64 inputs
        try
        {
            Convert.FromBase64String(request.IdentityPublicKeyClassical);
            Convert.FromBase64String(request.IdentityPublicKeyPostQuantum);
            Convert.FromBase64String(request.SignedPreKeyPublic);
            Convert.FromBase64String(request.SignedPreKeySignature);
            Convert.FromBase64String(request.KyberPreKeyPublic);
            Convert.FromBase64String(request.KyberPreKeySignature);
        }
        catch (FormatException)
        {
            return BadRequest("One or more key fields contain invalid Base64.");
        }

        // Wrap device creation and pre-key storage in a transaction for atomicity
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var device = new Device
            {
                Id = DecimalTools.GetNewId(),
                UserId = userId,
                DeviceName = request.DeviceName,
                IdentityPublicKeyClassical = Convert.FromBase64String(request.IdentityPublicKeyClassical),
                IdentityPublicKeyPostQuantum = Convert.FromBase64String(request.IdentityPublicKeyPostQuantum),
                SignedPreKeyPublic = Convert.FromBase64String(request.SignedPreKeyPublic),
                SignedPreKeySignature = Convert.FromBase64String(request.SignedPreKeySignature),
                SignedPreKeyId = request.SignedPreKeyId,
                KyberPreKeyPublic = Convert.FromBase64String(request.KyberPreKeyPublic),
                KyberPreKeySignature = Convert.FromBase64String(request.KyberPreKeySignature),
                CreatedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
                IsActive = true
            };

            _db.Devices.Add(device);
            await _db.SaveChangesAsync();

            if (request.OneTimePreKeys is { Count: > 0 })
            {
                await _preKeyService.StoreOneTimePreKeys(device.Id, request.OneTimePreKeys);
            }

            await transaction.CommitAsync();

            return Created(string.Empty, new { deviceId = device.Id });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// List all active devices for the requesting user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListDevices()
    {
        var userId = GetUserId();

        var devices = await _db.Devices
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(d => new DeviceInfoResponse(d.Id, d.DeviceName, d.CreatedAt, d.LastSeenAt))
            .ToListAsync();

        return Ok(devices);
    }

    /// <summary>
    /// Revoke/deactivate a device belonging to the requesting user.
    /// </summary>
    [HttpDelete("{deviceId}")]
    public async Task<IActionResult> RevokeDevice(decimal deviceId)
    {
        var userId = GetUserId();

        var device = await _db.Devices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId && d.IsActive);

        if (device is null)
            return NotFound("Device not found or does not belong to the current user.");

        device.IsActive = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Get remaining pre-key count for a device belonging to the requesting user.
    /// </summary>
    [HttpGet("{deviceId}/prekeys/count")]
    public async Task<IActionResult> GetPreKeyCount(decimal deviceId)
    {
        var userId = GetUserId();

        var deviceOwned = await _db.Devices
            .AnyAsync(d => d.Id == deviceId && d.UserId == userId && d.IsActive);

        if (!deviceOwned)
            return NotFound("Device not found or does not belong to the current user.");

        var count = await _preKeyService.CountRemainingPreKeys(deviceId);
        return Ok(new { count });
    }

    /// <summary>
    /// Replenish one-time pre-keys for a device belonging to the requesting user.
    /// </summary>
    [HttpPost("{deviceId}/prekeys")]
    public async Task<IActionResult> ReplenishPreKeys(
        decimal deviceId,
        [FromBody] List<OneTimePreKeyDto> preKeys)
    {
        var userId = GetUserId();

        var deviceOwned = await _db.Devices
            .AnyAsync(d => d.Id == deviceId && d.UserId == userId && d.IsActive);

        if (!deviceOwned)
            return NotFound("Device not found or does not belong to the current user.");

        await _preKeyService.StoreOneTimePreKeys(deviceId, preKeys);
        return NoContent();
    }

}
