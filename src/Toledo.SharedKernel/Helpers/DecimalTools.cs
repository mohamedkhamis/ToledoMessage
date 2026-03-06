using System.Security.Cryptography;

namespace Toledo.SharedKernel.Helpers;

/// <summary>
/// Provides utility methods for generating unique decimal IDs.
/// Uses cryptographically secure random bytes for unpredictable IDs.
/// Thread-safe implementation using lock for concurrent access.
/// </summary>
public static class DecimalTools
{
    private static decimal _lastId;
    private static readonly Lock Lock = new();

    /// <summary>
    /// Generates a new unique decimal ID using cryptographically secure randomness.
    /// Produces a 19-digit positive integer (fits in decimal(28,8) with no fractional part).
    /// ~63 bits of entropy — computationally infeasible to guess.
    /// </summary>
    /// <returns>A unique, unpredictable decimal identifier</returns>
    public static decimal GetNewId()
    {
        lock (Lock)
        {
            while (true)
            {
                // Generate 8 cryptographically random bytes → 64-bit unsigned integer
                // ReSharper disable once StackAllocInsideLoop
                Span<byte> bytes = stackalloc byte[8];
                RandomNumberGenerator.Fill(bytes);
                var value = BitConverter.ToUInt64(bytes) & 0x7FFFFFFFFFFFFFFF; // Ensure positive (clear sign bit)

                // ReSharper disable  RemoveRedundantBraces
                // Clamp to 19 digits max (fits decimal(28,8) integer part which supports 20 digits)
                value %= 10_000_000_000_000_000_000UL; // Max 19 digits
                if (value < 1_000_000_000_000_000_000UL)
                {
                    value += 1_000_000_000_000_000_000UL; // Ensure 19 digits (no leading zeros)
                }

                var num = (decimal)value;
                if (num == _lastId)
                    continue;

                _lastId = num;
                return num;
            }
        }
    }
}
