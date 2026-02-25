using ToledoMessage.Crypto.Protocol;

namespace ToledoMessage.Client.Services;

/// <summary>
/// High-level facade that orchestrates encrypted session management,
/// message encryption, and message decryption.
/// Delegates session establishment to <see cref="SessionService"/> and
/// encrypt/decrypt operations to <see cref="MessageEncryptionService"/>.
/// </summary>
public class CryptoService
{
    private readonly SessionService _sessionService;
    private readonly MessageEncryptionService _messageEncryptionService;

    public CryptoService(SessionService sessionService, MessageEncryptionService messageEncryptionService)
    {
        _sessionService = sessionService;
        _messageEncryptionService = messageEncryptionService;
    }

    /// <summary>
    /// Ensures an encrypted session exists with the specified remote device.
    /// If no session exists, establishes one via the X3DH protocol.
    /// </summary>
    /// <param name="userId">The remote user's ID.</param>
    /// <param name="deviceId">The remote device's ID.</param>
    public async Task EstablishSessionAsync(decimal userId, decimal deviceId)
    {
        var hasSession = await _sessionService.HasSessionAsync(deviceId);
        if (!hasSession)
        {
            await _sessionService.EstablishSessionAsync(userId, deviceId);
        }
    }

    /// <summary>
    /// Encrypts a plaintext message for the specified remote device.
    /// Loads the existing session, encrypts the message, persists the updated
    /// ratchet state, and returns the ciphertext as a base64-encoded string.
    /// </summary>
    /// <param name="recipientDeviceId">The recipient device's ID.</param>
    /// <param name="plaintext">The plaintext message to encrypt.</param>
    /// <returns>Base64-encoded ciphertext (includes embedded <see cref="MessageHeader"/>).</returns>
    /// <exception cref="InvalidOperationException">No session exists for the device.</exception>
    public async Task<string> EncryptMessageAsync(decimal recipientDeviceId, string plaintext)
    {
        var session = await _sessionService.LoadSessionAsync(recipientDeviceId)
            ?? throw new InvalidOperationException(
                $"No session exists for device {recipientDeviceId}. Call EstablishSessionAsync first.");

        var (ciphertextWithHeader, updatedState) =
            _messageEncryptionService.EncryptMessage(session, plaintext);

        await _sessionService.SaveSessionAsync(recipientDeviceId, updatedState);

        return Convert.ToBase64String(ciphertextWithHeader);
    }

    /// <summary>
    /// Decrypts a received ciphertext from the specified sender device.
    /// Loads the existing session, decrypts the message, persists the updated
    /// ratchet state, and returns the plaintext string.
    /// </summary>
    /// <param name="senderDeviceId">The sender device's ID.</param>
    /// <param name="ciphertextBase64">Base64-encoded ciphertext (includes embedded header).</param>
    /// <returns>The decrypted plaintext string.</returns>
    /// <exception cref="InvalidOperationException">No session exists for the device.</exception>
    public async Task<string> DecryptMessageAsync(decimal senderDeviceId, string ciphertextBase64)
    {
        var session = await _sessionService.LoadSessionAsync(senderDeviceId)
            ?? throw new InvalidOperationException(
                $"No session exists for device {senderDeviceId}.");

        var ciphertextWithHeader = Convert.FromBase64String(ciphertextBase64);

        var (plaintext, updatedState) =
            _messageEncryptionService.DecryptMessage(session, ciphertextWithHeader);

        await _sessionService.SaveSessionAsync(senderDeviceId, updatedState);

        return plaintext;
    }
}
