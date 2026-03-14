using System.Collections.Concurrent;

namespace ToledoVault.Services;

/// <summary>
/// In-memory blacklist for revoked JWT access tokens (S-11 fix).
/// Tokens are tracked by JTI (JWT ID) until they expire naturally.
/// </summary>
public class TokenBlacklistService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _blacklist = new();
    private int _cleanupCounter;
    private const int CleanupInterval = 50; // Run cleanup every N additions

    public void RevokeToken(string jti, DateTimeOffset expiresAt)
    {
        _blacklist[jti] = expiresAt;

        if (Interlocked.Increment(ref _cleanupCounter) % CleanupInterval == 0)
        {
            CleanupExpired();
        }
    }

    public bool IsRevoked(string jti)
    {
        if (!_blacklist.TryGetValue(jti, out var expiresAt))
            return false;

        // Auto-remove if expired
        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            _blacklist.TryRemove(jti, out _);
            return false;
        }

        return true;
    }

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _blacklist)
        {
            if (kvp.Value <= now)
                _blacklist.TryRemove(kvp.Key, out _);
        }
    }
}
