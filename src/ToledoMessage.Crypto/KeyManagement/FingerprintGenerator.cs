using System.Text;
using Org.BouncyCastle.Crypto.Digests;

namespace ToledoMessage.Crypto.KeyManagement;

/// <summary>
/// Generates human-readable safety numbers from two identity keys for out-of-band verification,
/// similar to Signal's safety number mechanism.
/// </summary>
public static class FingerprintGenerator
{
    /// <summary>
    /// Generates a safety number from two identity public keys (local + remote).
    /// Keys are sorted lexicographically so both parties produce the same result.
    /// </summary>
    /// <param name="localIdentityKey">Local party's identity public key.</param>
    /// <param name="remoteIdentityKey">Remote party's identity public key.</param>
    /// <returns>A 30-digit string formatted as 6 groups of 5 digits separated by spaces.</returns>
    public static string GenerateFingerprint(byte[] localIdentityKey, byte[] remoteIdentityKey)
    {
        // 1. Sort keys lexicographically to ensure both sides produce the same fingerprint
        byte[] first, second;
        if (CompareByteArrays(localIdentityKey, remoteIdentityKey) <= 0)
        {
            first = localIdentityKey;
            second = remoteIdentityKey;
        }
        else
        {
            first = remoteIdentityKey;
            second = localIdentityKey;
        }

        // 2. Combine keys
        var combined = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, combined, 0, first.Length);
        Buffer.BlockCopy(second, 0, combined, first.Length, second.Length);

        // 3. Hash with SHA-256 iteratively (5200 rounds like Signal) for a strong fingerprint
        var digest = new Sha256Digest();
        var hash = new byte[digest.GetDigestSize()];

        // Initial hash
        digest.BlockUpdate(combined, 0, combined.Length);
        digest.DoFinal(hash, 0);

        // Iterate to strengthen
        for (int i = 0; i < 5199; i++)
        {
            digest.Reset();
            digest.BlockUpdate(hash, 0, hash.Length);
            digest.BlockUpdate(combined, 0, combined.Length);
            digest.DoFinal(hash, 0);
        }

        // 4. Convert hash bytes to 30 digits in groups of 5
        var sb = new StringBuilder(35); // 30 digits + 5 spaces
        for (int i = 0; i < 6; i++)
        {
            if (i > 0)
                sb.Append(' ');

            // Take 5 bytes (starting at offset i*5), interpret as a number, mod 100000
            int offset = i * 5;
            long value = 0;
            for (int j = 0; j < 5; j++)
            {
                value = (value << 8) | hash[offset + j];
            }
            value = value % 100000;
            sb.Append(value.ToString("D5"));
        }

        return sb.ToString();
    }

    private static int CompareByteArrays(byte[] a, byte[] b)
    {
        int minLength = Math.Min(a.Length, b.Length);
        for (int i = 0; i < minLength; i++)
        {
            int cmp = a[i].CompareTo(b[i]);
            if (cmp != 0)
                return cmp;
        }
        return a.Length.CompareTo(b.Length);
    }
}
