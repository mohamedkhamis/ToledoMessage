using System.Net;
using System.Security.Claims;
using ToledoMessage.Services;

namespace ToledoMessage.Middleware;

/// <summary>
/// Middleware that applies per-route rate limits based on the client's IP (for anonymous endpoints)
/// or authenticated user ID (for protected endpoints).
/// Returns 429 Too Many Requests when a limit is exceeded.
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimitService _rateLimitService;

    // Rate limit rules: (path prefix, max requests, time window, use user ID as key)
    private static readonly (string Path, int MaxRequests, TimeSpan Window, bool ByUser)[] Rules =
    [
        ("/api/auth/register", 5, TimeSpan.FromMinutes(1), false),
        ("/api/auth/login", 10, TimeSpan.FromMinutes(1), false),
        ("/api/messages", 60, TimeSpan.FromMinutes(1), true),
        ("/api/users/search", 10, TimeSpan.FromMinutes(1), true),
    ];

    public RateLimitMiddleware(RequestDelegate next, RateLimitService rateLimitService)
    {
        _next = next;
        _rateLimitService = rateLimitService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (path is null)
        {
            await _next(context);
            return;
        }

        foreach (var rule in Rules)
        {
            if (!path.StartsWith(rule.Path, StringComparison.OrdinalIgnoreCase))
                continue;

            var key = BuildKey(context, rule.Path, rule.ByUser);
            if (key is null)
            {
                // If we need a user key but the user is not authenticated, let the
                // request through — the [Authorize] attribute will handle rejection.
                break;
            }

            if (_rateLimitService.IsRateLimited(key, rule.MaxRequests, rule.Window))
            {
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"Rate limit exceeded. Please try again later.\"}");
                return;
            }

            // First matching rule wins — stop checking further rules
            break;
        }

        await _next(context);
    }

    private static string? BuildKey(HttpContext context, string path, bool byUser)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (byUser)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? context.User.FindFirstValue("sub");

            // User not authenticated yet — cannot rate-limit by user
            if (string.IsNullOrEmpty(userId))
                return null;

            // Combine IP + UserId to rate-limit per user-IP pair
            return $"user:{userId}:ip:{ip}:{path}";
        }

        // Rate-limit by IP address for anonymous endpoints
        return $"ip:{ip}:{path}";
    }
}
