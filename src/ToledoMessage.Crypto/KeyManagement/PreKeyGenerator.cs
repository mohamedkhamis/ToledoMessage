using ToledoMessage.Crypto.Classical;
using ToledoMessage.Crypto.Hybrid;
using ToledoMessage.Crypto.PostQuantum;

namespace ToledoMessage.Crypto.KeyManagement;

/// <summary>
/// Generates pre-keys required for X3DH session establishment:
/// signed pre-keys (X25519), Kyber pre-keys (ML-KEM-768), and one-time pre-keys (X25519).
/// </summary>
public static class PreKeyGenerator
{
    /// <summary>
    /// X25519 signed pre-key with a hybrid signature from the identity key.
    /// </summary>
    public sealed class SignedPreKey
    {
        /// <summary>Sequential key identifier.</summary>
        public required int KeyId { get; init; }

        /// <summary>X25519 public key (32 bytes).</summary>
        public required byte[] PublicKey { get; init; }

        /// <summary>X25519 private key (32 bytes).</summary>
        public required byte[] PrivateKey { get; init; }

        /// <summary>Hybrid signature over the public key.</summary>
        public required byte[] Signature { get; init; }
    }

    /// <summary>
    /// ML-KEM-768 pre-key with a hybrid signature from the identity key.
    /// </summary>
    public sealed class KyberPreKey
    {
        /// <summary>ML-KEM-768 public key.</summary>
        public required byte[] PublicKey { get; init; }

        /// <summary>ML-KEM-768 private key.</summary>
        public required byte[] PrivateKey { get; init; }

        /// <summary>Hybrid signature over the public key.</summary>
        public required byte[] Signature { get; init; }
    }

    /// <summary>
    /// Unsigned one-time X25519 pre-key for optional X3DH forward secrecy.
    /// </summary>
    public sealed class OneTimePreKey
    {
        /// <summary>Sequential key identifier.</summary>
        public required int KeyId { get; init; }

        /// <summary>X25519 public key (32 bytes).</summary>
        public required byte[] PublicKey { get; init; }

        /// <summary>X25519 private key (32 bytes).</summary>
        public required byte[] PrivateKey { get; init; }
    }

    /// <summary>
    /// Generates a signed X25519 pre-key signed with the identity key's hybrid signing keys.
    /// </summary>
    public static SignedPreKey GenerateSignedPreKey(
        int keyId,
        byte[] identityClassicalPrivate,
        byte[] identityPqPrivate)
    {
        var (publicKey, privateKey) = X25519KeyExchange.GenerateKeyPair();
        var signature = HybridSigner.Sign(identityClassicalPrivate, identityPqPrivate, publicKey);

        return new SignedPreKey
        {
            KeyId = keyId,
            PublicKey = publicKey,
            PrivateKey = privateKey,
            Signature = signature
        };
    }

    /// <summary>
    /// Generates an ML-KEM-768 pre-key signed with the identity key's hybrid signing keys.
    /// </summary>
    public static KyberPreKey GenerateKyberPreKey(
        byte[] identityClassicalPrivate,
        byte[] identityPqPrivate)
    {
        var (publicKey, privateKey) = MlKemKeyExchange.GenerateKeyPair();
        var signature = HybridSigner.Sign(identityClassicalPrivate, identityPqPrivate, publicKey);

        return new KyberPreKey
        {
            PublicKey = publicKey,
            PrivateKey = privateKey,
            Signature = signature
        };
    }

    /// <summary>
    /// Generates a batch of one-time X25519 pre-keys with sequential key IDs.
    /// </summary>
    public static List<OneTimePreKey> GenerateOneTimePreKeys(int startKeyId, int count)
    {
        var keys = new List<OneTimePreKey>(count);

        for (var i = 0; i < count; i++)
        {
            var (publicKey, privateKey) = X25519KeyExchange.GenerateKeyPair();

            keys.Add(new OneTimePreKey
            {
                KeyId = startKeyId + i,
                PublicKey = publicKey,
                PrivateKey = privateKey
            });
        }

        return keys;
    }
}
