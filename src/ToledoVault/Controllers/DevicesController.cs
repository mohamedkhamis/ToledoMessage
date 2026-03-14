using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Toledo.SharedKernel.Helpers;
using ToledoVault.Data;
using ToledoVault.Models;
using ToledoVault.Services;
using ToledoVault.Shared.Constants;
using ToledoVault.Shared.DTOs;

// ReSharper disable All

namespace ToledoVault.Controllers;

[ApiController]
[Route("api/devices")]
[Authorize]
public class DevicesController(ApplicationDbContext db, PreKeyService preKeyService, MessageRelayService relayService) : BaseApiController
{
    /// <summary>
    /// Register a new device for the current user.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RegisterDevice([FromBody] DeviceRegistrationRequest request)
    {
        var userId = GetUserId();

        // Deactivate any existing device with the same name (same browser re-registering)
        var existingDevice = await db.Devices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceName == request.DeviceName && d.IsActive);
        if (existingDevice is not null)
        {
            existingDevice.IsActive = false;
            await db.SaveChangesAsync();
            // Clean up stale undelivered messages for the deactivated device
            await relayService.CleanupDeactivatedDeviceMessages(existingDevice.Id);
        }

        var activeDeviceCount = await db.Devices.CountAsync(d => d.UserId == userId && d.IsActive);
        if (activeDeviceCount >= ProtocolConstants.MaxDevicesPerUser)
            return StatusCode(403, $"Maximum number of devices ({ProtocolConstants.MaxDevicesPerUser}) reached.");

        // Validate DeviceName
        if (string.IsNullOrWhiteSpace(request.DeviceName) || request.DeviceName.Length > ProtocolConstants.MaxDeviceNameLength)
            return BadRequest($"Device name must be between 1 and {ProtocolConstants.MaxDeviceNameLength} characters.");

        // Decode and validate all Base64 key inputs
        byte[] classicalIdentityKey, pqIdentityKey, signedPreKeyPublic, signedPreKeySig, kyberPreKeyPublic, kyberPreKeySig;
        try
        {
            classicalIdentityKey = Convert.FromBase64String(request.IdentityPublicKeyClassical);
            pqIdentityKey = Convert.FromBase64String(request.IdentityPublicKeyPostQuantum);
            signedPreKeyPublic = Convert.FromBase64String(request.SignedPreKeyPublic);
            signedPreKeySig = Convert.FromBase64String(request.SignedPreKeySignature);
            kyberPreKeyPublic = Convert.FromBase64String(request.KyberPreKeyPublic);
            kyberPreKeySig = Convert.FromBase64String(request.KyberPreKeySignature);
        }
        catch (FormatException)
        {
            return BadRequest("One or more key fields contain invalid Base64.");
        }

        // Validate decoded key sizes against protocol constants
        if (classicalIdentityKey.Length != ProtocolConstants.Ed25519PublicKeySize)
            return BadRequest("Invalid identity public key (classical) size.");
        if (pqIdentityKey.Length != ProtocolConstants.MlDsa65PublicKeySize)
            return BadRequest("Invalid identity public key (post-quantum) size.");
        if (signedPreKeyPublic.Length != ProtocolConstants.X25519PublicKeySize)
            return BadRequest("Invalid signed pre-key public key size.");
        if (signedPreKeySig.Length != ProtocolConstants.HybridSignatureSize)
            return BadRequest("Invalid signed pre-key signature size.");
        if (kyberPreKeyPublic.Length != ProtocolConstants.MlKem768PublicKeySize)
            return BadRequest("Invalid Kyber pre-key public key size.");
        if (kyberPreKeySig.Length != ProtocolConstants.HybridSignatureSize)
            return BadRequest("Invalid Kyber pre-key signature size.");

        var device = new Device
        {
            Id = IdGenerator.GetNewId(),
            UserId = userId,
            DeviceName = request.DeviceName,
            IdentityPublicKeyClassical = classicalIdentityKey,
            IdentityPublicKeyPostQuantum = pqIdentityKey,
            SignedPreKeyPublic = signedPreKeyPublic,
            SignedPreKeySignature = signedPreKeySig,
            SignedPreKeyId = request.SignedPreKeyId,
            KyberPreKeyPublic = kyberPreKeyPublic,
            KyberPreKeySignature = kyberPreKeySig,
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        db.Devices.Add(device);
        await db.SaveChangesAsync();

        if (request.OneTimePreKeys is { Count: > 0 and <= ProtocolConstants.OneTimePreKeyBatchSize })
        {
            await preKeyService.StoreOneTimePreKeys(device.Id, request.OneTimePreKeys);
        }

        return Created(string.Empty, new { deviceId = device.Id });
    }

    /// <summary>
    /// List all active devices for the requesting user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListDevices()
    {
        var userId = GetUserId();

        var devices = await db.Devices
            .Where(d => d.UserId == userId && d.IsActive)
            .Select(d => new DeviceInfoResponse(d.Id, d.DeviceName, d.CreatedAt, d.LastSeenAt))
            .ToListAsync();

        return Ok(devices);
    }

    /// <summary>
    /// Revoke/deactivate a device belonging to the requesting user.
    /// </summary>
    [HttpDelete("{deviceId}")]
    public async Task<IActionResult> RevokeDevice(long deviceId)
    {
        var userId = GetUserId();

        var device = await db.Devices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId && d.IsActive);

        if (device is null)
            return NotFound("Device not found or does not belong to the current user.");

        device.IsActive = false;
        await db.SaveChangesAsync();

        // Clean up stale undelivered messages for the deactivated device
        await relayService.CleanupDeactivatedDeviceMessages(deviceId);

        return NoContent();
    }

    /// <summary>
    /// Get remaining pre-key count for a device belonging to the requesting user.
    /// </summary>
    [HttpGet("{deviceId}/prekeys/count")]
    public async Task<IActionResult> GetPreKeyCount(long deviceId)
    {
        var userId = GetUserId();

        var deviceOwned = await db.Devices
            .AnyAsync(d => d.Id == deviceId && d.UserId == userId && d.IsActive);

        if (!deviceOwned)
            return NotFound("Device not found or does not belong to the current user.");

        var count = await preKeyService.CountRemainingPreKeys(deviceId);
        return Ok(new { count });
    }

    /// <summary>
    /// Replenish one-time pre-keys for a device belonging to the requesting user.
    /// </summary>
    [HttpPost("{deviceId}/prekeys")]
    public async Task<IActionResult> ReplenishPreKeys(
        long deviceId,
        [FromBody] List<OneTimePreKeyDto> preKeys)
    {
        var userId = GetUserId();

        var deviceOwned = await db.Devices
            .AnyAsync(d => d.Id == deviceId && d.UserId == userId && d.IsActive);

        if (!deviceOwned)
            return NotFound("Device not found or does not belong to the current user.");

        if (preKeys.Count is 0 or > ProtocolConstants.OneTimePreKeyBatchSize)
            return BadRequest($"Pre-key batch must be between 1 and {ProtocolConstants.OneTimePreKeyBatchSize}.");

        await preKeyService.StoreOneTimePreKeys(deviceId, preKeys);
        return NoContent();
    }

}
