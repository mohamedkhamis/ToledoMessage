using System.Security.Claims;
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
public class DevicesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly PreKeyService _preKeyService;

    public DevicesController(ApplicationDbContext db, PreKeyService preKeyService)
    {
        _db = db;
        _preKeyService = preKeyService;
    }

    [HttpPost]
    public async Task<IActionResult> RegisterDevice([FromBody] DeviceRegistrationRequest request)
    {
        var userId = GetUserId();

        var activeDeviceCount = await _db.Devices.CountAsync(d => d.UserId == userId && d.IsActive);
        if (activeDeviceCount >= 10)
            return StatusCode(403, "Maximum number of devices (10) reached.");

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

        return Created(string.Empty, new { deviceId = device.Id });
    }

    private decimal GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return decimal.Parse(sub!);
    }
}
