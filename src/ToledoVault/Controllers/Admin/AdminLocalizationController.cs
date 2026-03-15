using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToledoVault.Services;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Controllers.Admin;

[ApiController]
[Route("api/admin/localization")]
[Authorize(Roles = "admin")]
public class AdminLocalizationController(
    LocalizationOverrideService localizationService,
    ILogger<AdminLocalizationController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLocalization(
        [FromQuery] string? language = null,
        [FromQuery] string? search = null,
        [FromQuery] bool missingOnly = false)
    {
        var result = await localizationService.GetAllMergedAsync(language, search, missingOnly);
        return Ok(result);
    }

    [HttpPut("{resourceKey}")]
    public async Task<IActionResult> UpdateLocalization(string resourceKey, [FromBody] UpdateLocalizationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LanguageCode) || string.IsNullOrWhiteSpace(request.Value))
            return BadRequest("Language code and value are required.");

        var success = await localizationService.UpdateOverrideAsync(resourceKey, request.LanguageCode, request.Value);
        if (!success)
            return BadRequest("Failed to update localization.");

        var adminUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
        logger.LogInformation("Localization override saved: {ResourceKey}/{LanguageCode} by {AdminUsername}",
            resourceKey, request.LanguageCode, adminUsername);

        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> CreateLocalizationKey([FromBody] CreateLocalizationKeyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ResourceKey) || request.Values is null || request.Values.Count == 0)
            return BadRequest("Resource key and at least one value are required.");

        var (success, error) = await localizationService.CreateNewKeyAsync(request.ResourceKey, request.Values);
        if (!success)
            return Conflict(error);

        var adminUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
        logger.LogInformation("Localization key created: {ResourceKey} by {AdminUsername}",
            request.ResourceKey, adminUsername);

        return Created($"/api/admin/localization/{request.ResourceKey}", null);
    }

    [HttpDelete("{resourceKey}/{languageCode}")]
    public async Task<IActionResult> DeleteLocalizationOverride(string resourceKey, string languageCode)
    {
        var success = await localizationService.DeleteOverrideAsync(resourceKey, languageCode);
        if (!success)
            return NotFound();

        var adminUsername = User.FindFirstValue(ClaimTypes.NameIdentifier);
        logger.LogInformation("Localization override deleted: {ResourceKey}/{LanguageCode} by {AdminUsername}",
            resourceKey, languageCode, adminUsername);

        return NoContent();
    }
}
