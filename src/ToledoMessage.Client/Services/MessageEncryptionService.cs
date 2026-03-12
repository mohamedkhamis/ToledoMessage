using System.Text.Json;
using ToledoMessage.Crypto.Protocol;

namespace ToledoMessage.Client.Services;

/// <summary>
/// Lower-level service handling Double Ratchet encrypt/decrypt operations.
/// Supports two wire formats (v0 legacy and v1 versioned):
/// - v0 NormalMessage: [4-byte ratchetHeaderLen][RatchetHeader JSON][ciphertext]
/// - v0 PreKeyMessage: [4-byte preKeyHeaderLen][PreKeyHeader JSON][4-byte ratchetHeaderLen][RatchetHeader JSON][ciphertext]
/// - v1 NormalMessage: [0x01][4-byte ratchetHeaderLen][RatchetHeader JSON][ciphertext]
/// - v1 PreKeyMessage: [0x01][4-byte preKeyHeaderLen][PreKeyHeader JSON][4-byte ratchetHeaderLen][RatchetHeader JSON][ciphertext]
/// </summary>
public class MessageEncryptionService
{
    private const byte ProtocolVersion = 0x01;


    /// <summary>
    /// Encrypts raw bytes as a NormalMessage (for media payloads).
    /// </summary>
    public (byte[] ciphertextWithHeader, RatchetState updatedState) EncryptBytes(
        DoubleRatchet session, byte[] data)
    {
        var (ciphertext, header) = session.Encrypt(data);

        var ciphertextWithHeader = PackNormalMessage(header, ciphertext);
        return (ciphertextWithHeader, session.GetState());
    }


    /// <summary>
    /// Encrypts raw bytes as a PreKeyMessage (for media payloads on first message to a device).
    /// </summary>
    public (byte[] ciphertextWithHeader, RatchetState updatedState) EncryptPreKeyMessageBytes(
        DoubleRatchet session, byte[] data, PreKeyHeaderInfo preKeyHeader)
    {
        var (ciphertext, header) = session.Encrypt(data);

        var ciphertextWithHeader = PackPreKeyMessage(preKeyHeader, header, ciphertext);
        return (ciphertextWithHeader, session.GetState());
    }

    /// <summary>
    /// Decrypts a NormalMessage blob returning raw bytes (for media payloads).
    /// </summary>
    public (byte[] data, RatchetState updatedState) DecryptToBytes(
        DoubleRatchet session, byte[] ciphertextWithHeader)
    {
        var (header, ciphertext) = UnpackNormalMessage(ciphertextWithHeader);
        var plaintextBytes = session.Decrypt(ciphertext, header);

        return (plaintextBytes, session.GetState());
    }


    /// <summary>
    /// Extracts the PreKeyHeader from a PreKeyMessage blob without decrypting the message.
    /// Used to get X3DH params before establishing the responder session.
    /// </summary>
    public static PreKeyHeaderInfo ExtractPreKeyHeader(byte[] ciphertextWithHeader)
    {
        if (ciphertextWithHeader.Length < 4)
            throw new InvalidOperationException("Invalid PreKeyMessage blob: too short.");

        var (_, dataOffset) = DetectVersion(ciphertextWithHeader);

        var preKeyHeaderLength = BitConverter.ToInt32(ciphertextWithHeader, dataOffset);
        var headerStart = dataOffset + 4;
        if (ciphertextWithHeader.Length < headerStart + preKeyHeaderLength)
            throw new InvalidOperationException("Invalid PreKeyMessage blob: too short for PreKeyHeader.");

        var preKeyHeaderJson = new ReadOnlySpan<byte>(ciphertextWithHeader, headerStart, preKeyHeaderLength);
        var preKeyHeaderDto = JsonSerializer.Deserialize<PreKeyHeaderDto>(preKeyHeaderJson)
                              ?? throw new InvalidOperationException("Failed to deserialize PreKeyHeader.");

        return new PreKeyHeaderInfo
        {
            EphemeralPublicKey = Convert.FromBase64String(preKeyHeaderDto.EphemeralPublicKey),
            KemCiphertext = Convert.FromBase64String(preKeyHeaderDto.KemCiphertext),
            UsedOneTimePreKeyId = preKeyHeaderDto.UsedOneTimePreKeyId
        };
    }

    /// <summary>
    /// Returns the ratchet-encrypted portion of a PreKeyMessage (strips the PreKeyHeader prefix).
    /// This is then in NormalMessage format and can be decrypted with <see cref="DecryptToBytes"/>.
    /// </summary>
    public static byte[] StripPreKeyHeader(byte[] ciphertextWithHeader)
    {
        if (ciphertextWithHeader.Length < 4)
            throw new InvalidOperationException("Invalid PreKeyMessage blob: too short.");

        var (_, dataOffset) = DetectVersion(ciphertextWithHeader);

        var preKeyHeaderLength = BitConverter.ToInt32(ciphertextWithHeader, dataOffset);
        var remainingStart = dataOffset + 4 + preKeyHeaderLength;

        if (ciphertextWithHeader.Length < remainingStart)
            throw new InvalidOperationException("Invalid PreKeyMessage blob: too short for PreKeyHeader.");

        var remaining = new byte[ciphertextWithHeader.Length - remainingStart];
        Buffer.BlockCopy(ciphertextWithHeader, remainingStart, remaining, 0, remaining.Length);
        return remaining;
    }

    /// <summary>
    /// Packs NormalMessage v1 format: [version][4-byte headerLen][headerJson][ciphertext].
    /// </summary>
    private static byte[] PackNormalMessage(MessageHeader header, byte[] ciphertext)
    {
        var headerDto = new MessageHeaderDto
        {
            RatchetPublicKey = Convert.ToBase64String(header.RatchetPublicKey),
            PreviousChainLength = header.PreviousChainLength,
            MessageIndex = header.MessageIndex
        };

        var headerJson = JsonSerializer.SerializeToUtf8Bytes(headerDto);
        var headerLength = BitConverter.GetBytes(headerJson.Length);

        var result = new byte[1 + 4 + headerJson.Length + ciphertext.Length];
        result[0] = ProtocolVersion;
        Buffer.BlockCopy(headerLength, 0, result, 1, 4);
        Buffer.BlockCopy(headerJson, 0, result, 5, headerJson.Length);
        Buffer.BlockCopy(ciphertext, 0, result, 5 + headerJson.Length, ciphertext.Length);

        return result;
    }

    /// <summary>
    /// Packs PreKeyMessage v1 format: [version][4-byte preKeyHeaderLen][PreKeyHeader JSON][4-byte ratchetHeaderLen][RatchetHeader JSON][ciphertext].
    /// </summary>
    private static byte[] PackPreKeyMessage(PreKeyHeaderInfo preKeyHeader, MessageHeader ratchetHeader, byte[] ciphertext)
    {
        var preKeyHeaderDto = new PreKeyHeaderDto
        {
            EphemeralPublicKey = Convert.ToBase64String(preKeyHeader.EphemeralPublicKey),
            KemCiphertext = Convert.ToBase64String(preKeyHeader.KemCiphertext),
            UsedOneTimePreKeyId = preKeyHeader.UsedOneTimePreKeyId
        };

        var preKeyHeaderJson = JsonSerializer.SerializeToUtf8Bytes(preKeyHeaderDto);
        var preKeyHeaderLength = BitConverter.GetBytes(preKeyHeaderJson.Length);

        var ratchetHeaderDto = new MessageHeaderDto
        {
            RatchetPublicKey = Convert.ToBase64String(ratchetHeader.RatchetPublicKey),
            PreviousChainLength = ratchetHeader.PreviousChainLength,
            MessageIndex = ratchetHeader.MessageIndex
        };

        var ratchetHeaderJson = JsonSerializer.SerializeToUtf8Bytes(ratchetHeaderDto);
        var ratchetHeaderLength = BitConverter.GetBytes(ratchetHeaderJson.Length);

        var totalLength = 1 + 4 + preKeyHeaderJson.Length + 4 + ratchetHeaderJson.Length + ciphertext.Length;
        var result = new byte[totalLength];
        var offset = 0;

        result[offset] = ProtocolVersion;
        offset += 1;
        Buffer.BlockCopy(preKeyHeaderLength, 0, result, offset, 4);
        offset += 4;
        Buffer.BlockCopy(preKeyHeaderJson, 0, result, offset, preKeyHeaderJson.Length);
        offset += preKeyHeaderJson.Length;
        Buffer.BlockCopy(ratchetHeaderLength, 0, result, offset, 4);
        offset += 4;
        Buffer.BlockCopy(ratchetHeaderJson, 0, result, offset, ratchetHeaderJson.Length);
        offset += ratchetHeaderJson.Length;
        Buffer.BlockCopy(ciphertext, 0, result, offset, ciphertext.Length);

        return result;
    }

    /// <summary>
    /// Detects if blob starts with a version byte (v1+) or is legacy v0.
    /// Legacy v0 blobs start with a 4-byte int32 header length (JSON headers are typically 50+ bytes,
    /// so the first byte will be >= 0x10). Version bytes are small (0x01-0x0F).
    /// </summary>
    // ReSharper disable once UnusedTupleComponentInReturnValue
    private static (byte version, int dataOffset) DetectVersion(byte[] blob)
    {
        if (blob.Length < 5) return (0, 0); // Too short for v1, try as v0

        // ReSharper disable  InvertIf
        if (blob[0] >= 0x01 && blob[0] <= 0x0F)
        {
            // Validate: the next 4 bytes should be a reasonable header length
            var headerLen = BitConverter.ToInt32(blob, 1);
            if (headerLen > 0 && headerLen < blob.Length)
                return (blob[0], 1); // v1: skip version byte
        }

        return (0, 0); // Legacy v0: no version prefix
    }

    /// <summary>
    /// Unpacks NormalMessage format back into MessageHeader and ciphertext.
    /// Supports both v0 (legacy) and v1 (versioned) wire formats.
    /// </summary>
    private static (MessageHeader header, byte[] ciphertext) UnpackNormalMessage(byte[] blob)
    {
        if (blob.Length < 4)
            throw new InvalidOperationException("Invalid ciphertext blob: too short to contain header length.");

        var (_, dataOffset) = DetectVersion(blob);

        var headerLength = BitConverter.ToInt32(blob, dataOffset);
        var headerStart = dataOffset + 4;

        if (blob.Length < headerStart + headerLength)
            throw new InvalidOperationException("Invalid ciphertext blob: too short to contain header.");

        var headerJson = new ReadOnlySpan<byte>(blob, headerStart, headerLength);
        var headerDto = JsonSerializer.Deserialize<MessageHeaderDto>(headerJson)
                        ?? throw new InvalidOperationException("Failed to deserialize message header.");

        var ciphertextStart = headerStart + headerLength;
        var ciphertextLength = blob.Length - ciphertextStart;
        var ciphertext = new byte[ciphertextLength];
        Buffer.BlockCopy(blob, ciphertextStart, ciphertext, 0, ciphertextLength);

        var header = new MessageHeader
        {
            RatchetPublicKey = Convert.FromBase64String(headerDto.RatchetPublicKey),
            PreviousChainLength = headerDto.PreviousChainLength,
            MessageIndex = headerDto.MessageIndex
        };

        return (header, ciphertext);
    }

    /// <summary>
    /// Internal DTO for JSON serialization of <see cref="MessageHeader"/>.
    /// </summary>
    private sealed class MessageHeaderDto
    {
        public string RatchetPublicKey { get; init; } = string.Empty;
        public int PreviousChainLength { get; init; }
        public int MessageIndex { get; init; }
    }

    /// <summary>
    /// Internal DTO for JSON serialization of PreKeyHeader.
    /// </summary>
    private sealed class PreKeyHeaderDto
    {
        public string EphemeralPublicKey { get; init; } = string.Empty;
        public string KemCiphertext { get; init; } = string.Empty;
        public int? UsedOneTimePreKeyId { get; init; }
    }
}

/// <summary>
/// X3DH handshake data embedded in PreKeyMessages.
/// </summary>
public sealed class PreKeyHeaderInfo
{
    public required byte[] EphemeralPublicKey { get; init; }
    public required byte[] KemCiphertext { get; init; }
    public int? UsedOneTimePreKeyId { get; init; }
}
