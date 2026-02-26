using System.Collections.Concurrent;

namespace ToledoMessage.Services;

/// <summary>
/// Simple in-memory rate limiter that tracks request counts per key within sliding time windows.
/// Includes periodic cleanup of stale entries to prevent unbounded memory growth.
/// </summary>
public class RateLimitService
{
    private readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _clients = new();
    private DateTime _lastCleanup = DateTime.UtcNow;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

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

        // Periodically clean up stale entries to prevent unbounded memory growth
        if (now - _lastCleanup >= CleanupInterval)
        {
            CleanupStaleEntries(now);
            _lastCleanup = now;
        }

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

    /// <summary>
    /// Removes entries that have been inactive for more than 10 minutes.
    /// </summary>
    private void CleanupStaleEntries(DateTime now)
    {
        var staleThreshold = TimeSpan.FromMinutes(10);
        foreach (var kvp in _clients)
        {
            if (now - kvp.Value.WindowStart >= staleThreshold)
            {
                _clients.TryRemove(kvp.Key, out _);
            }
        }
    }
}
