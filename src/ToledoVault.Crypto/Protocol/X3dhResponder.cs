using System.Diagnostics.CodeAnalysis;
using ToledoVault.Crypto.Classical;
using ToledoVault.Crypto.Hybrid;
using ToledoVault.Crypto.PostQuantum;

namespace ToledoVault.Crypto.Protocol;

/// <summary>
/// Performs the responder (Bob) side of the X3DH key agreement with post-quantum extension.
/// </summary>
[SuppressMessage("ReSharper", "RemoveRedundantBraces")]
public static class X3dhResponder
{
    private static readonly byte[] HkdfInfo = "ToledoVault-X3DH-v1"u8.ToArray();

    /// <summary>
    /// Completes the X3DH handshake from Bob's side, deriving the same session keys as Alice.
    /// </summary>
    /// <param name="signedPreKeyPrivate">Bob's X25519 signed pre-key private key.</param>
    /// <param name="kyberPreKeyPrivate">Bob's ML-KEM-768 private key.</param>
    /// <param name="oneTimePreKeyPrivate">Bob's consumed one-time X25519 pre-key private key, or null if not used.</param>
    /// <param name="aliceEphemeralPublicKey">Alice's ephemeral X25519 public key from the initiation message.</param>
    /// <param name="kemCiphertext">Alice's ML-KEM ciphertext from the initiation message.</param>
    /// <returns>A tuple of (rootKey, chainKey) matching Alice's derived keys.</returns>
    public static (byte[] rootKey, byte[] chainKey) Respond(
        byte[] signedPreKeyPrivate,
        byte[] kyberPreKeyPrivate,
        byte[]? oneTimePreKeyPrivate,
        byte[] aliceEphemeralPublicKey,
        byte[] kemCiphertext)
    {
        // 1. DH1 = X25519(signedPreKeyPrivate, aliceEphemeralPublicKey)
        var dh1 = X25519KeyExchange.ComputeSharedSecret(signedPreKeyPrivate, aliceEphemeralPublicKey);

        // 2. DH2 = X25519(oneTimePreKeyPrivate, aliceEphemeralPublicKey) if used
        byte[]? dh2 = null;
        if (oneTimePreKeyPrivate is not null)
        {
            dh2 = X25519KeyExchange.ComputeSharedSecret(oneTimePreKeyPrivate, aliceEphemeralPublicKey);
        }

        // 3. KEM decapsulation
        var kemSharedSecret = MlKemKeyExchange.Decapsulate(kyberPreKeyPrivate, kemCiphertext);

        // 4. Combine: ikm = DH1 || [DH2] || kemSharedSecret
        byte[] ikm;
        if (dh2 is not null)
        {
            ikm = new byte[dh1.Length + dh2.Length + kemSharedSecret.Length];
            Buffer.BlockCopy(dh1, 0, ikm, 0, dh1.Length);
            Buffer.BlockCopy(dh2, 0, ikm, dh1.Length, dh2.Length);
            Buffer.BlockCopy(kemSharedSecret, 0, ikm, dh1.Length + dh2.Length, kemSharedSecret.Length);
        }
        else
        {
            ikm = new byte[dh1.Length + kemSharedSecret.Length];
            Buffer.BlockCopy(dh1, 0, ikm, 0, dh1.Length);
            Buffer.BlockCopy(kemSharedSecret, 0, ikm, dh1.Length, kemSharedSecret.Length);
        }

        // 5. SK = HKDF(ikm, info: "ToledoVault-X3DH-v1", outputLength: 64)
        var sk = HybridKeyDerivation.DeriveKey(ikm, HkdfInfo, 64);

        // 6. rootKey = SK[0..32], chainKey = SK[32..64]
        var rootKey = new byte[32];
        var chainKey = new byte[32];
        Buffer.BlockCopy(sk, 0, rootKey, 0, 32);
        Buffer.BlockCopy(sk, 32, chainKey, 0, 32);

        return (rootKey, chainKey);
    }
}
