using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace ToledoMessage.Controllers;

/// <inheritdoc />
/// <summary>
/// Base controller providing shared utility methods for all API controllers.
/// </summary>
[ApiController]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Extracts the authenticated user's ID from the JWT claims.
    /// Throws if no valid claim is present (should only be called from [Authorize] endpoints).
    /// </summary>
    protected decimal GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        // BUG-CR-005 FIX: Throw if claim missing or invalid
        if (sub == null || !decimal.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException("Invalid user identity claim");

        return userId;
    }

    /// <summary>
    /// Tries to extract the authenticated user's ID from the JWT claims.
    /// Returns false if no valid claim is present.
    /// </summary>
    protected bool TryGetUserId(out decimal userId)
    {
        userId = 0;
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return sub != null && decimal.TryParse(sub, out userId);
    }
}
