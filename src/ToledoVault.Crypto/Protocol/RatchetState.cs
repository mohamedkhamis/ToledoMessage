namespace ToledoVault.Crypto.Protocol;

/// <summary>
/// Serializable Double Ratchet session state.
/// Stored client-side in IndexedDB; never sent to the server.
/// </summary>
public sealed class RatchetState
{
    /// <summary>Current root key (32 bytes). Used for DH ratchet derivation.</summary>
    public byte[] RootKey { get; set; } = [];

    /// <summary>Current sending chain key.</summary>
    public byte[] SendChainKey { get; set; } = [];

    /// <summary>Next send message number (increments with each sent message).</summary>
    public int SendMessageIndex { get; set; }

    /// <summary>Current receiving chain key.</summary>
    public byte[] ReceiveChainKey { get; set; } = [];

    /// <summary>Next expected receive message number.</summary>
    public int ReceiveMessageIndex { get; set; }

    /// <summary>Our current X25519 DH ratchet private key.</summary>
    public byte[] LocalRatchetPrivateKey { get; set; } = [];

    /// <summary>Our current X25519 DH ratchet public key.</summary>
    public byte[] LocalRatchetPublicKey { get; set; } = [];

    /// <summary>Remote party's current X25519 DH ratchet public key.</summary>
    public byte[] RemoteRatchetPublicKey { get; set; } = [];

    /// <summary>
    /// Skipped message keys for out-of-order message handling.
    /// Key format: "{ratchetPublicKeyBase64}:{messageIndex}"
    /// Value: the 32-byte message key.
    /// </summary>
    public Dictionary<string, byte[]> SkippedMessageKeys { get; set; } = new();

    /// <summary>Number of messages in the previous sending chain (for header).</summary>
    public int PreviousChainLength { get; set; }
}
