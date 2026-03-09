using System.Security.Cryptography;

namespace Toledo.SharedKernel.Helpers;

/// <summary>
/// Provides utility methods for generating unique long IDs.
/// Uses cryptographically secure random bytes for unpredictable IDs.
/// Thread-safe implementation using lock for concurrent access.
/// </summary>
public static class IdGenerator
{
    private static long _lastId;
    private static readonly Lock Lock = new();

    /// <summary>
    /// Generates a new unique long ID using cryptographically secure randomness.
    /// Full 63-bit range — positive values only.
    /// </summary>
    /// <returns>A unique, unpredictable long identifier</returns>
    public static long GetNewId()
    {
        lock (Lock)
        {
            while (true)
            {
                // Generate 8 cryptographically random bytes → 64-bit unsigned integer
                // ReSharper disable once StackAllocInsideLoop
                Span<byte> bytes = stackalloc byte[8];
                RandomNumberGenerator.Fill(bytes);
                var value = (long)(BitConverter.ToUInt64(bytes) & 0x7FFFFFFFFFFFFFFF);

                if (value == 0 || value == _lastId)
                    continue;

                _lastId = value;
                return value;
            }
        }
    }
}
