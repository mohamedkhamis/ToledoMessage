using System.Diagnostics.CodeAnalysis;
using ToledoMessage.Crypto.Classical;
using ToledoMessage.Crypto.Hybrid;

namespace ToledoMessage.Crypto.Protocol;

/// <summary>
/// Message header sent alongside each encrypted message.
/// </summary>
public sealed class MessageHeader
{
    /// <summary>Sender's current DH ratchet public key (X25519, 32 bytes).</summary>
    public required byte[] RatchetPublicKey { get; init; }

    /// <summary>Number of messages in previous sending chain.</summary>
    public required int PreviousChainLength { get; init; }

    /// <summary>Message number in current sending chain.</summary>
    public required int MessageIndex { get; init; }
}

/// <summary>
/// Implements the Double Ratchet algorithm providing per-message forward secrecy
/// via symmetric ratchet (chain key stepping) combined with asymmetric ratchet (DH ratchet with X25519).
/// </summary>
[SuppressMessage("ReSharper", "RemoveRedundantBraces")]
public class DoubleRatchet
{
    private readonly RatchetState _state;
    private const int MaxSkippedKeys = 100;
    private static readonly byte[] RatchetInfo = "ToledoMessage-Ratchet-v1"u8.ToArray();

    private DoubleRatchet(RatchetState state)
    {
        _state = state;
    }

    /// <summary>
    /// Initialize as the session initiator (Alice).
    /// Called after X3DH completes. Alice sends first, so she needs Bob's ratchet public key.
    /// </summary>
    public static DoubleRatchet InitializeAsInitiator(byte[] sharedSecret, byte[] remoteRatchetPublicKey)
    {
        // 1. Generate local X25519 ratchet key pair
        var (localPub, localPriv) = X25519KeyExchange.GenerateKeyPair();

        // 2. DH ratchet step: dhOutput = X25519(localPriv, remoteRatchetPublicKey)
        var dhOutput = X25519KeyExchange.ComputeSharedSecret(localPriv, remoteRatchetPublicKey);

        // 3. Derive new rootKey + sendChainKey from HKDF(dhOutput, salt=sharedSecret, info, 64)
        var derived = HybridKeyDerivation.DeriveKey(dhOutput, sharedSecret, RatchetInfo, 64);

        var rootKey = new byte[32];
        var sendChainKey = new byte[32];
        Buffer.BlockCopy(derived, 0, rootKey, 0, 32);
        Buffer.BlockCopy(derived, 32, sendChainKey, 0, 32);

        // 4. Initialize state
        var state = new RatchetState
        {
            RootKey = rootKey,
            SendChainKey = sendChainKey,
            SendMessageIndex = 0,
            ReceiveChainKey = [],
            ReceiveMessageIndex = 0,
            LocalRatchetPrivateKey = localPriv,
            LocalRatchetPublicKey = localPub,
            RemoteRatchetPublicKey = remoteRatchetPublicKey,
            PreviousChainLength = 0
        };

        return new DoubleRatchet(state);
    }

    /// <summary>
    /// Initialize as the session responder (Bob).
    /// Bob receives first. His signed pre-key serves as initial ratchet key.
    /// </summary>
    public static DoubleRatchet InitializeAsResponder(
        byte[] sharedSecret,
        byte[] localRatchetPrivateKey,
        byte[] localRatchetPublicKey)
    {
        // Bob's state: he has the shared secret and his SPK as the initial ratchet key.
        // He waits for Alice's first message (which contains her ratchet public key) to derive receive chain.
        var state = new RatchetState
        {
            RootKey = sharedSecret,
            SendChainKey = [],
            SendMessageIndex = 0,
            ReceiveChainKey = [],
            ReceiveMessageIndex = 0,
            LocalRatchetPrivateKey = localRatchetPrivateKey,
            LocalRatchetPublicKey = localRatchetPublicKey,
            RemoteRatchetPublicKey = [],
            PreviousChainLength = 0
        };

        return new DoubleRatchet(state);
    }

    /// <summary>
    /// Encrypt a message. Returns (ciphertext, messageHeader).
    /// </summary>
    public (byte[] ciphertext, MessageHeader header) Encrypt(byte[] plaintext)
    {
        // 1. Derive message key from sendChainKey
        var (messageKey, nextChainKey) = MessageKeys.DeriveKeys(_state.SendChainKey);

        // 2. Advance sendChainKey
        _state.SendChainKey = nextChainKey;

        // 3. Create header
        var header = new MessageHeader
        {
            RatchetPublicKey = _state.LocalRatchetPublicKey,
            PreviousChainLength = _state.PreviousChainLength,
            MessageIndex = _state.SendMessageIndex
        };

        // 4. Encrypt plaintext with AesGcmCipher using messageKey
        var nonce = CreateNonce(_state.SendMessageIndex);
        var ciphertext = AesGcmCipher.Encrypt(messageKey, nonce, plaintext);

        // 5. Increment SendMessageIndex
        _state.SendMessageIndex++;

        return (ciphertext, header);
    }

    /// <summary>
    /// Decrypt a received message.
    /// </summary>
    public byte[] Decrypt(byte[] ciphertext, MessageHeader header)
    {
        // 1. Check if message key is in skipped keys (out-of-order)
        var skippedKey = TryGetSkippedMessageKey(header);
        if (skippedKey is not null)
        {
            var skippedNonce = CreateNonce(header.MessageIndex);
            return AesGcmCipher.Decrypt(skippedKey, skippedNonce, ciphertext);
        }

        // 2. If header.RatchetPublicKey != state.RemoteRatchetPublicKey: perform DH ratchet step
        if (!ByteArraysEqual(_state.RemoteRatchetPublicKey, header.RatchetPublicKey))
        {
            SkipMessages(header.PreviousChainLength);
            PerformDhRatchetStep(header.RatchetPublicKey);
        }

        // 3. Skip messages up to header.MessageIndex in receive chain
        SkipMessages(header.MessageIndex);

        // 4. Derive message key from receiveChainKey
        var (messageKey, nextChainKey) = MessageKeys.DeriveKeys(_state.ReceiveChainKey);

        // 5. Advance receiveChainKey
        _state.ReceiveChainKey = nextChainKey;
        _state.ReceiveMessageIndex++;

        // 6. Decrypt with AesGcmCipher
        var nonce = CreateNonce(header.MessageIndex);
        return AesGcmCipher.Decrypt(messageKey, nonce, ciphertext);
    }

    /// <summary>
    /// Restores a DoubleRatchet session from a previously serialized <see cref="RatchetState"/>.
    /// </summary>
    public static DoubleRatchet FromState(RatchetState state)
    {
        return new DoubleRatchet(state);
    }

    /// <summary>
    /// Returns the current ratchet state for serialization.
    /// </summary>
    public RatchetState GetState()
    {
        return _state;
    }

    private void PerformDhRatchetStep(byte[] newRemoteRatchetPublicKey)
    {
        // Store previous chain length
        _state.PreviousChainLength = _state.SendMessageIndex;

        // Update remote ratchet key
        _state.RemoteRatchetPublicKey = newRemoteRatchetPublicKey;

        // Reset message indices
        _state.SendMessageIndex = 0;
        _state.ReceiveMessageIndex = 0;

        // DH ratchet for receiving: derive new rootKey + receiveChainKey
        var dhOutput = X25519KeyExchange.ComputeSharedSecret(
            _state.LocalRatchetPrivateKey, _state.RemoteRatchetPublicKey);
        var derived = HybridKeyDerivation.DeriveKey(dhOutput, _state.RootKey, RatchetInfo, 64);

        _state.RootKey = new byte[32];
        _state.ReceiveChainKey = new byte[32];
        Buffer.BlockCopy(derived, 0, _state.RootKey, 0, 32);
        Buffer.BlockCopy(derived, 32, _state.ReceiveChainKey, 0, 32);

        // Generate new local ratchet key pair
        var (localPub, localPriv) = X25519KeyExchange.GenerateKeyPair();
        _state.LocalRatchetPrivateKey = localPriv;
        _state.LocalRatchetPublicKey = localPub;

        // DH ratchet for sending: derive new rootKey + sendChainKey
        dhOutput = X25519KeyExchange.ComputeSharedSecret(
            _state.LocalRatchetPrivateKey, _state.RemoteRatchetPublicKey);
        derived = HybridKeyDerivation.DeriveKey(dhOutput, _state.RootKey, RatchetInfo, 64);

        _state.RootKey = new byte[32];
        _state.SendChainKey = new byte[32];
        Buffer.BlockCopy(derived, 0, _state.RootKey, 0, 32);
        Buffer.BlockCopy(derived, 32, _state.SendChainKey, 0, 32);
    }

    private void SkipMessages(int untilIndex)
    {
        if (_state.ReceiveChainKey.Length == 0)
            return;

        while (_state.ReceiveMessageIndex < untilIndex)
        {
            if (_state.SkippedMessageKeys.Count >= MaxSkippedKeys)
                break;

            var (messageKey, nextChainKey) = MessageKeys.DeriveKeys(_state.ReceiveChainKey);
            _state.ReceiveChainKey = nextChainKey;

            var keyId = BuildSkippedKeyId(_state.RemoteRatchetPublicKey, _state.ReceiveMessageIndex);
            _state.SkippedMessageKeys[keyId] = messageKey;

            _state.ReceiveMessageIndex++;
        }
    }

    private byte[]? TryGetSkippedMessageKey(MessageHeader header)
    {
        var keyId = BuildSkippedKeyId(header.RatchetPublicKey, header.MessageIndex);
        // ReSharper disable once InvertIf
        // ReSharper disable once CanSimplifyDictionaryRemovingWithSingleCall
        if (_state.SkippedMessageKeys.TryGetValue(keyId, out var key))
        {
            _state.SkippedMessageKeys.Remove(keyId);
            return key;
        }

        return null;
    }

    private static string BuildSkippedKeyId(byte[] ratchetPublicKey, int messageIndex)
    {
        return $"{Convert.ToBase64String(ratchetPublicKey)}:{messageIndex}";
    }

    private static byte[] CreateNonce(int messageIndex)
    {
        var nonce = new byte[12];
        var indexBytes = BitConverter.GetBytes(messageIndex);
        Buffer.BlockCopy(indexBytes, 0, nonce, 0, indexBytes.Length);
        return nonce;
    }

    private static bool ByteArraysEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;

        // Constant-time comparison to prevent timing side-channel attacks
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
    }
}
