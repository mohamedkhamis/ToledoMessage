using System.Text;
using System.Text.Json;
using ToledoMessage.Crypto.Protocol;

namespace ToledoMessage.Client.Services;

/// <summary>
/// Lower-level service handling Double Ratchet encrypt/decrypt operations.
/// Supports two wire formats:
/// - NormalMessage: [4-byte ratchetHeaderLen][RatchetHeader JSON][ciphertext]
/// - PreKeyMessage: [4-byte preKeyHeaderLen][PreKeyHeader JSON][4-byte ratchetHeaderLen][RatchetHeader JSON][ciphertext]
/// </summary>
public class MessageEncryptionService
{
    /// <summary>
    /// Encrypts a plaintext message as a NormalMessage (subsequent messages in an established session).
    /// </summary>
    public (byte[] ciphertextWithHeader, RatchetState updatedState) EncryptMessage(
        DoubleRatchet session, string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        return EncryptBytes(session, plaintextBytes);
    }

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
    /// Encrypts a plaintext message as a PreKeyMessage (first message to a device, embeds X3DH handshake data).
    /// </summary>
    public (byte[] ciphertextWithHeader, RatchetState updatedState) EncryptPreKeyMessage(
        DoubleRatchet session, string plaintext, PreKeyHeaderInfo preKeyHeader)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        return EncryptPreKeyMessageBytes(session, plaintextBytes, preKeyHeader);
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
    /// Decrypts a NormalMessage blob using the provided Double Ratchet session.
    /// </summary>
    public (string plaintext, RatchetState updatedState) DecryptMessage(
        DoubleRatchet session, byte[] ciphertextWithHeader)
    {
        var (bytes, updatedState) = DecryptToBytes(session, ciphertextWithHeader);
        return (Encoding.UTF8.GetString(bytes), updatedState);
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
    /// Unpacks a PreKeyMessage blob, extracting the PreKeyHeader and returning it along with the
    /// ratchet-encrypted portion (which can be decrypted after session establishment).
    /// </summary>
    public (PreKeyHeaderInfo preKeyHeader, string plaintext, RatchetState updatedState) DecryptPreKeyMessage(
        DoubleRatchet session, byte[] ciphertextWithHeader)
    {
        var (preKeyHeader, ratchetHeader, ciphertext) = UnpackPreKeyMessage(ciphertextWithHeader);
        var plaintextBytes = session.Decrypt(ciphertext, ratchetHeader);

        return (preKeyHeader, Encoding.UTF8.GetString(plaintextBytes), session.GetState());
    }

    /// <summary>
    /// Extracts the PreKeyHeader from a PreKeyMessage blob without decrypting the message.
    /// Used to get X3DH params before establishing the responder session.
    /// </summary>
    public static PreKeyHeaderInfo ExtractPreKeyHeader(byte[] ciphertextWithHeader)
    {
        if (ciphertextWithHeader.Length < 4)
            throw new InvalidOperationException("Invalid PreKeyMessage blob: too short.");

        var preKeyHeaderLength = BitConverter.ToInt32(ciphertextWithHeader, 0);
        if (ciphertextWithHeader.Length < 4 + preKeyHeaderLength)
            throw new InvalidOperationException("Invalid PreKeyMessage blob: too short for PreKeyHeader.");

        var preKeyHeaderJson = new ReadOnlySpan<byte>(ciphertextWithHeader, 4, preKeyHeaderLength);
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
    /// This is then in NormalMessage format and can be decrypted with <see cref="DecryptMessage"/>.
    /// </summary>
    public static byte[] StripPreKeyHeader(byte[] ciphertextWithHeader)
    {
        if (ciphertextWithHeader.Length < 4)
            throw new InvalidOperationException("Invalid PreKeyMessage blob: too short.");

        var preKeyHeaderLength = BitConverter.ToInt32(ciphertextWithHeader, 0);
        var remainingStart = 4 + preKeyHeaderLength;

        if (ciphertextWithHeader.Length < remainingStart)
            throw new InvalidOperationException("Invalid PreKeyMessage blob: too short for PreKeyHeader.");

        var remaining = new byte[ciphertextWithHeader.Length - remainingStart];
        Buffer.BlockCopy(ciphertextWithHeader, remainingStart, remaining, 0, remaining.Length);
        return remaining;
    }

    /// <summary>
    /// Packs NormalMessage format: [4-byte headerLen][headerJson][ciphertext].
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

        var result = new byte[4 + headerJson.Length + ciphertext.Length];
        Buffer.BlockCopy(headerLength, 0, result, 0, 4);
        Buffer.BlockCopy(headerJson, 0, result, 4, headerJson.Length);
        Buffer.BlockCopy(ciphertext, 0, result, 4 + headerJson.Length, ciphertext.Length);

        return result;
    }

    /// <summary>
    /// Packs PreKeyMessage format: [4-byte preKeyHeaderLen][PreKeyHeader JSON][4-byte ratchetHeaderLen][RatchetHeader JSON][ciphertext].
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

        var totalLength = 4 + preKeyHeaderJson.Length + 4 + ratchetHeaderJson.Length + ciphertext.Length;
        var result = new byte[totalLength];
        var offset = 0;

        Buffer.BlockCopy(preKeyHeaderLength, 0, result, offset, 4); offset += 4;
        Buffer.BlockCopy(preKeyHeaderJson, 0, result, offset, preKeyHeaderJson.Length); offset += preKeyHeaderJson.Length;
        Buffer.BlockCopy(ratchetHeaderLength, 0, result, offset, 4); offset += 4;
        Buffer.BlockCopy(ratchetHeaderJson, 0, result, offset, ratchetHeaderJson.Length); offset += ratchetHeaderJson.Length;
        Buffer.BlockCopy(ciphertext, 0, result, offset, ciphertext.Length);

        return result;
    }

    /// <summary>
    /// Unpacks NormalMessage format back into MessageHeader and ciphertext.
    /// </summary>
    private static (MessageHeader header, byte[] ciphertext) UnpackNormalMessage(byte[] blob)
    {
        if (blob.Length < 4)
            throw new InvalidOperationException("Invalid ciphertext blob: too short to contain header length.");

        var headerLength = BitConverter.ToInt32(blob, 0);

        if (blob.Length < 4 + headerLength)
            throw new InvalidOperationException("Invalid ciphertext blob: too short to contain header.");

        var headerJson = new ReadOnlySpan<byte>(blob, 4, headerLength);
        var headerDto = JsonSerializer.Deserialize<MessageHeaderDto>(headerJson)
            ?? throw new InvalidOperationException("Failed to deserialize message header.");

        var ciphertextLength = blob.Length - 4 - headerLength;
        var ciphertext = new byte[ciphertextLength];
        Buffer.BlockCopy(blob, 4 + headerLength, ciphertext, 0, ciphertextLength);

        var header = new MessageHeader
        {
            RatchetPublicKey = Convert.FromBase64String(headerDto.RatchetPublicKey),
            PreviousChainLength = headerDto.PreviousChainLength,
            MessageIndex = headerDto.MessageIndex
        };

        return (header, ciphertext);
    }

    /// <summary>
    /// Unpacks PreKeyMessage format into PreKeyHeader, RatchetHeader, and ciphertext.
    /// </summary>
    private static (PreKeyHeaderInfo preKeyHeader, MessageHeader ratchetHeader, byte[] ciphertext) UnpackPreKeyMessage(byte[] blob)
    {
        if (blob.Length < 4)
            throw new InvalidOperationException("Invalid PreKeyMessage blob: too short.");

        var offset = 0;

        // Read PreKeyHeader
        var preKeyHeaderLength = BitConverter.ToInt32(blob, offset); offset += 4;
        if (blob.Length < offset + preKeyHeaderLength)
            throw new InvalidOperationException("Invalid PreKeyMessage blob: too short for PreKeyHeader.");

        var preKeyHeaderJson = new ReadOnlySpan<byte>(blob, offset, preKeyHeaderLength); offset += preKeyHeaderLength;
        var preKeyHeaderDto = JsonSerializer.Deserialize<PreKeyHeaderDto>(preKeyHeaderJson)
            ?? throw new InvalidOperationException("Failed to deserialize PreKeyHeader.");

        // Read RatchetHeader
        if (blob.Length < offset + 4)
            throw new InvalidOperationException("Invalid PreKeyMessage blob: too short for ratchet header length.");

        var ratchetHeaderLength = BitConverter.ToInt32(blob, offset); offset += 4;
        if (blob.Length < offset + ratchetHeaderLength)
            throw new InvalidOperationException("Invalid PreKeyMessage blob: too short for ratchet header.");

        var ratchetHeaderJson = new ReadOnlySpan<byte>(blob, offset, ratchetHeaderLength); offset += ratchetHeaderLength;
        var ratchetHeaderDto = JsonSerializer.Deserialize<MessageHeaderDto>(ratchetHeaderJson)
            ?? throw new InvalidOperationException("Failed to deserialize ratchet header.");

        // Read ciphertext
        var ciphertextLength = blob.Length - offset;
        var ciphertext = new byte[ciphertextLength];
        Buffer.BlockCopy(blob, offset, ciphertext, 0, ciphertextLength);

        var preKeyHeader = new PreKeyHeaderInfo
        {
            EphemeralPublicKey = Convert.FromBase64String(preKeyHeaderDto.EphemeralPublicKey),
            KemCiphertext = Convert.FromBase64String(preKeyHeaderDto.KemCiphertext),
            UsedOneTimePreKeyId = preKeyHeaderDto.UsedOneTimePreKeyId
        };

        var ratchetHeader = new MessageHeader
        {
            RatchetPublicKey = Convert.FromBase64String(ratchetHeaderDto.RatchetPublicKey),
            PreviousChainLength = ratchetHeaderDto.PreviousChainLength,
            MessageIndex = ratchetHeaderDto.MessageIndex
        };

        return (preKeyHeader, ratchetHeader, ciphertext);
    }

    /// <summary>
    /// Internal DTO for JSON serialization of <see cref="MessageHeader"/>.
    /// </summary>
    private sealed class MessageHeaderDto
    {
        public string RatchetPublicKey { get; set; } = "";
        public int PreviousChainLength { get; set; }
        public int MessageIndex { get; set; }
    }

    /// <summary>
    /// Internal DTO for JSON serialization of PreKeyHeader.
    /// </summary>
    private sealed class PreKeyHeaderDto
    {
        public string EphemeralPublicKey { get; set; } = "";
        public string KemCiphertext { get; set; } = "";
        public int? UsedOneTimePreKeyId { get; set; }
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
