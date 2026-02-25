using System.Collections.Concurrent;

namespace ToledoMessage.Services;

/// <summary>
/// Simple in-memory rate limiter that tracks request counts per key within sliding time windows.
/// </summary>
public class RateLimitService
{
    private readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _clients = new();

    /// <summary>
    /// Check if the given key has exceeded the allowed number of requests within the specified time window.
    /// If the window has expired, the counter resets. If within limit, the counter increments.
    /// </summary>
    /// <param name="key">A unique key identifying the client (e.g., IP address or user ID combined with route).</param>
    /// <param name="maxRequests">Maximum number of requests allowed within the time window.</param>
    /// <param name="window">The duration of the time window.</param>
    /// <returns>True if the request should be blocked (rate limit exceeded); false if the request is allowed.</returns>
    public bool IsRateLimited(string key, int maxRequests, TimeSpan window)
    {
        var now = DateTime.UtcNow;

        var entry = _clients.AddOrUpdate(
            key,
            // Factory for new key: start a fresh window with count 1
            _ => (1, now),
            // Update factory for existing key
            (_, existing) =>
            {
                // If the window has expired, reset the counter
                if (now - existing.WindowStart >= window)
                {
                    return (1, now);
                }

                // Window still active — increment the counter
                return (existing.Count + 1, existing.WindowStart);
            });

        return entry.Count > maxRequests;
    }
}
