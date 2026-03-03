using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Toledo.SharedKernel.Helpers;
using ToledoMessage.Data;
using ToledoMessage.Models;
using ToledoMessage.Shared.DTOs;

// ReSharper disable  RemoveRedundantBraces

namespace ToledoMessage.Controllers;

[ApiController]
[Route("api/preferences")]
[Authorize]
public class PreferencesController(ApplicationDbContext db) : BaseApiController
{
    private static readonly HashSet<string> ValidThemes =
    [
        "default", "default-dark", "whatsapp", "whatsapp-dark",
        "telegram", "signal", "signal-dark"
    ];

    private static readonly HashSet<string> ValidFontSizes = ["small", "medium", "large"];

    [HttpGet]
    public async Task<IActionResult> GetPreferences()
    {
        var userId = GetUserId();
        var prefs = await db.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (prefs is null)
            return Ok(new UserPreferencesResponse("default", "medium", "en", true, true, true, true));

        return Ok(new UserPreferencesResponse(
            prefs.Theme, prefs.FontSize, prefs.Language,
            prefs.NotificationsEnabled, prefs.ReadReceiptsEnabled, prefs.TypingIndicatorsEnabled,
            prefs.SharedKeysEnabled));
    }

    [HttpPut]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request)
    {
        if (request.Theme is not null && !ValidThemes.Contains(request.Theme))
            return BadRequest("Invalid theme.");

        if (request.FontSize is not null && !ValidFontSizes.Contains(request.FontSize))
            return BadRequest("Invalid font size. Must be small, medium, or large.");

        var userId = GetUserId();
        var prefs = await db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);

        if (prefs is null)
        {
            prefs = new UserPreferences
            {
                Id = DecimalTools.GetNewId(),
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.UserPreferences.Add(prefs);
        }

        if (request.Theme is not null)
        {
            prefs.Theme = request.Theme;
        }

        if (request.FontSize is not null)
        {
            prefs.FontSize = request.FontSize;
        }

        if (request.Language is not null)
        {
            prefs.Language = request.Language;
        }

        if (request.NotificationsEnabled.HasValue)
        {
            prefs.NotificationsEnabled = request.NotificationsEnabled.Value;
        }

        if (request.ReadReceiptsEnabled.HasValue)
        {
            prefs.ReadReceiptsEnabled = request.ReadReceiptsEnabled.Value;
        }

        if (request.TypingIndicatorsEnabled.HasValue)
        {
            prefs.TypingIndicatorsEnabled = request.TypingIndicatorsEnabled.Value;
        }

        if (request.SharedKeysEnabled.HasValue)
        {
            prefs.SharedKeysEnabled = request.SharedKeysEnabled.Value;
        }

        prefs.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new UserPreferencesResponse(
            prefs.Theme, prefs.FontSize, prefs.Language,
            prefs.NotificationsEnabled, prefs.ReadReceiptsEnabled, prefs.TypingIndicatorsEnabled,
            prefs.SharedKeysEnabled));
    }
}
