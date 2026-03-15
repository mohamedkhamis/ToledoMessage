using ToledoVault.Crypto.Hybrid;

namespace ToledoVault.Crypto.KeyManagement;

/// <summary>
/// Generates hybrid identity key pairs (Ed25519 + ML-DSA-65) for long-term device identity.
/// </summary>
public static class IdentityKeyGenerator
{
    /// <summary>
    /// Holds a hybrid identity key pair consisting of both classical (Ed25519) and
    /// post-quantum (ML-DSA-65) signing key pairs.
    /// </summary>
    public sealed class IdentityKeyPair
    {
        /// <summary>Ed25519 public key (32 bytes).</summary>
        public required byte[] ClassicalPublicKey { get; init; }

        /// <summary>Ed25519 private key (32 bytes).</summary>
        public required byte[] ClassicalPrivateKey { get; init; }

        /// <summary>ML-DSA-65 public key.</summary>
        public required byte[] PostQuantumPublicKey { get; init; }

        /// <summary>ML-DSA-65 private key.</summary>
        public required byte[] PostQuantumPrivateKey { get; init; }
    }

    /// <summary>
    /// Generates a new hybrid identity key pair using Ed25519 and ML-DSA-65.
    /// </summary>
    public static IdentityKeyPair Generate()
    {
        var (classicalPublic, classicalPrivate, pqPublic, pqPrivate) = HybridSigner.GenerateKeyPair();

        return new IdentityKeyPair
        {
            ClassicalPublicKey = classicalPublic,
            ClassicalPrivateKey = classicalPrivate,
            PostQuantumPublicKey = pqPublic,
            PostQuantumPrivateKey = pqPrivate
        };
    }
}
