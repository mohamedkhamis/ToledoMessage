using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToledoVault.Services;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Controllers.Admin;

[ApiController]
[Route("api/admin/settings")]
[Authorize(Roles = "admin")]
public class AdminSettingsController(
    GlobalSettingsService settingsService,
    ILogger<AdminSettingsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var result = await settingsService.GetAllGroupedAsync();
        return Ok(result);
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] UpdateSettingRequest request)
    {
        var setting = await settingsService.GetByKeyAsync(key);
        if (setting is null)
            return NotFound();

        var oldValue = setting.CurrentValue;
        var (success, error) = await settingsService.UpdateValueAsync(key, request.Value);
        if (!success)
            return BadRequest(error);

        var adminUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
        logger.LogInformation("Admin setting updated: {Key} changed from {OldValue} to {NewValue} by {AdminUsername}",
            key, oldValue, request.Value, adminUsername);

        return NoContent();
    }

    [HttpPost("reset/{key}")]
    public async Task<IActionResult> ResetSetting(string key)
    {
        var success = await settingsService.ResetToDefaultAsync(key);
        if (!success)
            return NotFound();

        var adminUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
        logger.LogInformation("Admin setting reset to default: {Key} by {AdminUsername}", key, adminUsername);

        return NoContent();
    }
}
