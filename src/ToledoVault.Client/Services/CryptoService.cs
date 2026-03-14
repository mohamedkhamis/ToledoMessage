using System.Net.Http.Json;
using ToledoVault.Crypto.Protocol;
using ToledoVault.Shared.DTOs;
using ToledoVault.Shared.Enums;

// ReSharper disable RemoveRedundantBraces

namespace ToledoVault.Client.Services;

/// <summary>
/// High-level facade that orchestrates encrypted session management,
/// message encryption, and message decryption.
/// Delegates session establishment to <see cref="SessionService"/> and
/// encrypt/decrypt operations to <see cref="MessageEncryptionService"/>.
/// </summary>
public class CryptoService(
    SessionService sessionService,
    MessageEncryptionService messageEncryptionService,
    HttpClient http)
{
    /// <summary>
    /// Caches X3DH InitiationResults per device for embedding in PreKeyMessages.
    /// Consumed on first encrypt, then removed (subsequent messages are NormalMessages).
    /// </summary>
    private readonly Dictionary<long, X3dhInitiator.InitiationResult> _pendingInitiationResults = new();

    /// <summary>
    /// Ensures an encrypted session exists with the specified remote device.
    /// If no session exists, establishes one via the X3DH protocol and caches
    /// the InitiationResult for embedding in the first (PreKey) message.
    /// </summary>
    public async Task EstablishSessionAsync(long userId, long deviceId)
    {
        var hasSession = await sessionService.HasSessionAsync(deviceId);
        if (!hasSession)
        {
            var (_, initiationResult) = await sessionService.EstablishSessionAsync(userId, deviceId);
            _pendingInitiationResults[deviceId] = initiationResult;
        }
    }

    /// <summary>
    /// Encrypts raw bytes for the specified remote device.
    /// If an X3DH InitiationResult is cached (first message), packs as PreKeyMessage;
    /// otherwise packs as NormalMessage.
    /// </summary>
    /// <returns>Tuple of (base64 ciphertext, MessageType).</returns>
    public async Task<(string ciphertextBase64, MessageType messageType)> EncryptBytesAsync(
        long recipientDeviceId, byte[] data)
    {
        var session = await sessionService.LoadSessionAsync(recipientDeviceId)
                      ?? throw new InvalidOperationException(
                          $"No session exists for device {recipientDeviceId}. Call EstablishSessionAsync first.");

        byte[] ciphertextWithHeader;
        RatchetState updatedState;
        MessageType messageType;

        if (_pendingInitiationResults.Remove(recipientDeviceId, out var initiationResult))
        {
            var preKeyHeader = new PreKeyHeaderInfo
            {
                EphemeralPublicKey = initiationResult.EphemeralPublicKey,
                KemCiphertext = initiationResult.KemCiphertext,
                UsedOneTimePreKeyId = initiationResult.UsedOneTimePreKeyId
            };

            (ciphertextWithHeader, updatedState) =
                messageEncryptionService.EncryptPreKeyMessageBytes(session, data, preKeyHeader);
            messageType = MessageType.PreKeyMessage;
        }
        else
        {
            (ciphertextWithHeader, updatedState) =
                messageEncryptionService.EncryptBytes(session, data);
            messageType = MessageType.NormalMessage;
        }

        await sessionService.SaveSessionAsync(recipientDeviceId, updatedState);

        return (Convert.ToBase64String(ciphertextWithHeader), messageType);
    }

    /// <summary>
    /// Decrypts a received ciphertext to raw bytes from the specified sender device.
    /// If messageType is PreKeyMessage and no session exists, establishes a responder
    /// session using the embedded X3DH handshake data, then decrypts.
    /// </summary>
    public async Task<byte[]> DecryptToBytesAsync(
        long senderDeviceId, string ciphertextBase64, MessageType messageType)
    {
        var ciphertextWithHeader = Convert.FromBase64String(ciphertextBase64);

        if (messageType == MessageType.PreKeyMessage)
        {
            var preKeyHeader = MessageEncryptionService.ExtractPreKeyHeader(ciphertextWithHeader);

            var session = await sessionService.LoadSessionAsync(senderDeviceId) ?? await sessionService.EstablishSessionAsResponderAsync(
                preKeyHeader.EphemeralPublicKey,
                preKeyHeader.KemCiphertext,
                preKeyHeader.UsedOneTimePreKeyId,
                senderDeviceId);

            var ratchetBlob = MessageEncryptionService.StripPreKeyHeader(ciphertextWithHeader);
            var (data, updatedState) =
                messageEncryptionService.DecryptToBytes(session, ratchetBlob);

            await sessionService.SaveSessionAsync(senderDeviceId, updatedState);
            return data;
        }
        else
        {
            var session = await sessionService.LoadSessionAsync(senderDeviceId)
                          ?? throw new InvalidOperationException(
                              $"No session exists for device {senderDeviceId}.");

            var (data, updatedState) =
                messageEncryptionService.DecryptToBytes(session, ciphertextWithHeader);

            await sessionService.SaveSessionAsync(senderDeviceId, updatedState);
            return data;
        }
    }

    /// <summary>
    /// Encrypts a plaintext message for all active devices of a recipient user.
    /// Returns a list of (deviceId, ciphertextBase64, messageType) tuples.
    /// </summary>
    public async Task<List<(long deviceId, string ciphertextBase64, MessageType messageType)>> EncryptMessageForAllDevicesAsync(
        long recipientUserId, string plaintext)
    {
        var data = System.Text.Encoding.UTF8.GetBytes(plaintext);
        return await EncryptBytesForAllDevicesAsync(recipientUserId, data);
    }

    /// <summary>
    /// Encrypts raw bytes for all active devices of a recipient user (for media).
    /// Continues to remaining devices if one device's session establishment fails.
    /// </summary>
    public async Task<List<(long deviceId, string ciphertextBase64, MessageType messageType)>> EncryptBytesForAllDevicesAsync(
        long recipientUserId, byte[] data)
    {
        var devices = await http.GetFromJsonAsync<List<DeviceInfoResponse>>(
                          $"/api/users/{recipientUserId}/devices")
                      ?? throw new InvalidOperationException(
                          $"Failed to fetch devices for user {recipientUserId}.");

        var results = new List<(long deviceId, string ciphertextBase64, MessageType messageType)>();

        foreach (var device in devices)
        {
            try
            {
                await EstablishSessionAsync(recipientUserId, device.DeviceId);
                var (ciphertextBase64, messageType) = await EncryptBytesAsync(device.DeviceId, data);
                results.Add((device.DeviceId, ciphertextBase64, messageType));
            }
            catch (Exception ex)
            {
                // Skip this device but continue sending to others.
                // Common cause: PreKey bundle fetch failed for an inactive/stale device.
                Console.WriteLine($@"Failed to encrypt for device {device.DeviceId}: {ex.Message}");
            }
        }

        if (results.Count == 0)
            throw new InvalidOperationException(
                $"Failed to encrypt message for any device of user {recipientUserId}.");

        return results;
    }

    /// <summary>
    /// Encrypts a plaintext message for all participants in a group conversation.
    /// </summary>
    public async Task<List<SendMessageRequest>> EncryptGroupMessageAsync(
        long conversationId, long selfUserId, long senderDeviceId, string plaintext)
    {
        return await EncryptGroupBytesAsync(
            conversationId, selfUserId, senderDeviceId,
            System.Text.Encoding.UTF8.GetBytes(plaintext), ContentType.Text, null, null);
    }

    /// <summary>
    /// Encrypts raw bytes (media) for all participants in a group conversation.
    /// </summary>
    public async Task<List<SendMessageRequest>> EncryptGroupBytesAsync(
        long conversationId, long selfUserId, long senderDeviceId,
        byte[] data, ContentType contentType, string? fileName, string? mimeType)
    {
        var participants = await http.GetFromJsonAsync<List<ParticipantResponse>>(
                               $"/api/conversations/{conversationId}/participants")
                           ?? throw new InvalidOperationException(
                               $"Failed to fetch participants for conversation {conversationId}.");

        var requests = new List<SendMessageRequest>();

        foreach (var participant in participants.Where(p => p.UserId != selfUserId))
        {
            var deviceCiphertexts = await EncryptBytesForAllDevicesAsync(participant.UserId, data);

            foreach (var (deviceId, ciphertextBase64, messageType) in deviceCiphertexts)
                requests.Add(new SendMessageRequest
                {
                    ConversationId = conversationId,
                    SenderDeviceId = senderDeviceId,
                    RecipientDeviceId = deviceId,
                    Ciphertext = ciphertextBase64,
                    MessageType = messageType,
                    ContentType = contentType,
                    FileName = fileName,
                    MimeType = mimeType
                });
        }

        return requests;
    }
}
