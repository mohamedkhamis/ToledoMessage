namespace ToledoVault.Crypto.Protocol;

/// <summary>
/// Pre-key bundle published by a device for X3DH session establishment.
/// Contains both classical (X25519/Ed25519) and post-quantum (ML-KEM-768/ML-DSA-65) public keys.
/// </summary>
public sealed class PreKeyBundle
{
    /// <summary>Ed25519 identity public key (32 bytes).</summary>
    public required byte[] IdentityKeyClassical { get; init; }

    /// <summary>ML-DSA-65 identity public key (~1952 bytes).</summary>
    public required byte[] IdentityKeyPostQuantum { get; init; }

    /// <summary>X25519 signed pre-key public (32 bytes).</summary>
    public required byte[] SignedPreKeyPublic { get; init; }

    /// <summary>Hybrid signature over the signed pre-key.</summary>
    public required byte[] SignedPreKeySignature { get; init; }

    /// <summary>Signed pre-key identifier.</summary>
    public required int SignedPreKeyId { get; init; }

    /// <summary>ML-KEM-768 pre-key public (1184 bytes).</summary>
    public required byte[] KyberPreKeyPublic { get; init; }

    /// <summary>Hybrid signature over the Kyber pre-key.</summary>
    public required byte[] KyberPreKeySignature { get; init; }

    /// <summary>Optional one-time X25519 pre-key public (32 bytes). Null if exhausted.</summary>
    public byte[]? OneTimePreKeyPublic { get; init; }

    /// <summary>One-time pre-key identifier. Null if no one-time pre-key is included.</summary>
    public int? OneTimePreKeyId { get; init; }
}
