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
    protected long GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        if (sub == null || !long.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException("Invalid user identity claim");

        return userId;
    }

    /// <summary>
    /// Tries to extract the authenticated user's ID from the JWT claims.
    /// Returns false if no valid claim is present.
    /// </summary>
    protected bool TryGetUserId(out long userId)
    {
        userId = 0;
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return sub != null && long.TryParse(sub, out userId);
    }
}
