using System.Text;
using System.Text.Json;
using ToledoMessage.Crypto.Protocol;

namespace ToledoMessage.Client.Services;

/// <summary>
/// Lower-level service handling Double Ratchet encrypt/decrypt operations.
/// Serializes the <see cref="MessageHeader"/> alongside the ciphertext into a
/// single byte blob using the format: [4-byte headerLength][headerJson][ciphertext].
/// </summary>
public class MessageEncryptionService
{
    /// <summary>
    /// Encrypts a plaintext message using the provided Double Ratchet session.
    /// Returns the combined header+ciphertext blob and the updated ratchet state.
    /// </summary>
    public (byte[] ciphertextWithHeader, RatchetState updatedState) EncryptMessage(
        DoubleRatchet session, string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var (ciphertext, header) = session.Encrypt(plaintextBytes);

        var ciphertextWithHeader = PackHeaderAndCiphertext(header, ciphertext);
        return (ciphertextWithHeader, session.GetState());
    }

    /// <summary>
    /// Decrypts a combined header+ciphertext blob using the provided Double Ratchet session.
    /// Returns the plaintext string and the updated ratchet state.
    /// </summary>
    public (string plaintext, RatchetState updatedState) DecryptMessage(
        DoubleRatchet session, byte[] ciphertextWithHeader)
    {
        var (header, ciphertext) = UnpackHeaderAndCiphertext(ciphertextWithHeader);
        var plaintextBytes = session.Decrypt(ciphertext, header);

        return (Encoding.UTF8.GetString(plaintextBytes), session.GetState());
    }

    /// <summary>
    /// Packs a <see cref="MessageHeader"/> and ciphertext into the wire format:
    /// [4-byte little-endian headerLength][headerJson UTF-8 bytes][ciphertext].
    /// </summary>
    private static byte[] PackHeaderAndCiphertext(MessageHeader header, byte[] ciphertext)
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
    /// Unpacks the wire format back into a <see cref="MessageHeader"/> and ciphertext.
    /// </summary>
    private static (MessageHeader header, byte[] ciphertext) UnpackHeaderAndCiphertext(
        byte[] ciphertextWithHeader)
    {
        if (ciphertextWithHeader.Length < 4)
            throw new InvalidOperationException("Invalid ciphertext blob: too short to contain header length.");

        var headerLength = BitConverter.ToInt32(ciphertextWithHeader, 0);

        if (ciphertextWithHeader.Length < 4 + headerLength)
            throw new InvalidOperationException("Invalid ciphertext blob: too short to contain header.");

        var headerJson = new ReadOnlySpan<byte>(ciphertextWithHeader, 4, headerLength);
        var headerDto = JsonSerializer.Deserialize<MessageHeaderDto>(headerJson)
            ?? throw new InvalidOperationException("Failed to deserialize message header.");

        var ciphertextLength = ciphertextWithHeader.Length - 4 - headerLength;
        var ciphertext = new byte[ciphertextLength];
        Buffer.BlockCopy(ciphertextWithHeader, 4 + headerLength, ciphertext, 0, ciphertextLength);

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
    /// Uses base64-encoded byte arrays for safe JSON transport.
    /// </summary>
    private sealed class MessageHeaderDto
    {
        public string RatchetPublicKey { get; set; } = "";
        public int PreviousChainLength { get; set; }
        public int MessageIndex { get; set; }
    }
}
