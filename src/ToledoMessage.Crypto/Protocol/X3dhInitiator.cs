using ToledoMessage.Crypto.Classical;
using ToledoMessage.Crypto.Hybrid;
using ToledoMessage.Crypto.PostQuantum;

namespace ToledoMessage.Crypto.Protocol;

// ReSharper disable  RemoveRedundantBraces
/// <summary>
/// Performs the initiator (Alice) side of the X3DH key agreement with post-quantum extension.
/// </summary>
public static class X3dhInitiator
{
    /// <summary>
    /// Result of X3DH initiation containing session keys and data Bob needs to complete the handshake.
    /// </summary>
    public sealed class InitiationResult
    {
        /// <summary>Root key for the Double Ratchet (32 bytes).</summary>
        public required byte[] RootKey { get; init; }

        /// <summary>Chain key for the Double Ratchet (32 bytes).</summary>
        public required byte[] ChainKey { get; init; }

        /// <summary>Alice's ephemeral X25519 public key (32 bytes), sent to Bob.</summary>
        public required byte[] EphemeralPublicKey { get; init; }

        /// <summary>ML-KEM ciphertext, sent to Bob for KEM decapsulation.</summary>
        public required byte[] KemCiphertext { get; init; }

        /// <summary>The ID of the one-time pre-key consumed, or null if none was available.</summary>
        public int? UsedOneTimePreKeyId { get; init; }
    }

    private static readonly byte[] HkdfInfo = "ToledoMessage-X3DH-v1"u8.ToArray();

    /// <summary>
    /// Performs X3DH initiation against Bob's pre-key bundle.
    /// </summary>
    /// <param name="bobBundle">Bob's published pre-key bundle.</param>
    /// <returns>Initiation result containing derived keys and data to send to Bob.</returns>
    /// <exception cref="InvalidOperationException">Thrown if any signature in the bundle fails verification.</exception>
    public static InitiationResult Initiate(PreKeyBundle bobBundle)
    {
        // 1. Verify Bob's signed pre-key signature
        var spkValid = HybridSigner.Verify(
            bobBundle.IdentityKeyClassical,
            bobBundle.IdentityKeyPostQuantum,
            bobBundle.SignedPreKeyPublic,
            bobBundle.SignedPreKeySignature);

        if (!spkValid)
            throw new InvalidOperationException("Signed pre-key signature verification failed.");

        // 2. Verify Bob's Kyber pre-key signature
        var kyberValid = HybridSigner.Verify(
            bobBundle.IdentityKeyClassical,
            bobBundle.IdentityKeyPostQuantum,
            bobBundle.KyberPreKeyPublic,
            bobBundle.KyberPreKeySignature);

        if (!kyberValid)
            throw new InvalidOperationException("Kyber pre-key signature verification failed.");

        // 3. Generate ephemeral X25519 key pair
        var (ephemeralPublic, ephemeralPrivate) = X25519KeyExchange.GenerateKeyPair();

        // 4. DH1 = X25519(ek_A_private, spk_B)
        var dh1 = X25519KeyExchange.ComputeSharedSecret(ephemeralPrivate, bobBundle.SignedPreKeyPublic);

        // 5. DH2 = X25519(ek_A_private, opk_B) if available
        byte[]? dh2 = null;
        if (bobBundle.OneTimePreKeyPublic is not null)
        {
            dh2 = X25519KeyExchange.ComputeSharedSecret(ephemeralPrivate, bobBundle.OneTimePreKeyPublic);
        }

        // 6. KEM encapsulation
        var (kemCiphertext, kemSharedSecret) = MlKemKeyExchange.Encapsulate(bobBundle.KyberPreKeyPublic);

        // 7. Combine: ikm = DH1 || [DH2] || kemSharedSecret
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

        // 8. SK = HKDF(ikm, info: "ToledoMessage-X3DH-v1", outputLength: 64)
        var sk = HybridKeyDerivation.DeriveKey(ikm, HkdfInfo, 64);

        // 9. rootKey = SK[0..32], chainKey = SK[32..64]
        var rootKey = new byte[32];
        var chainKey = new byte[32];
        Buffer.BlockCopy(sk, 0, rootKey, 0, 32);
        Buffer.BlockCopy(sk, 32, chainKey, 0, 32);

        // 10. Return result
        return new InitiationResult
        {
            RootKey = rootKey,
            ChainKey = chainKey,
            EphemeralPublicKey = ephemeralPublic,
            KemCiphertext = kemCiphertext,
            UsedOneTimePreKeyId = bobBundle.OneTimePreKeyPublic is not null ? bobBundle.OneTimePreKeyId : null
        };
    }
}
