using System.Net.Http.Json;
using ToledoMessage.Crypto.Protocol;
using ToledoMessage.Shared.DTOs;
using ToledoMessage.Shared.Enums;

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
    private readonly HttpClient _http;

    /// <summary>
    /// Caches X3DH InitiationResults per device for embedding in PreKeyMessages.
    /// Consumed on first encrypt, then removed (subsequent messages are NormalMessages).
    /// </summary>
    private readonly Dictionary<decimal, X3dhInitiator.InitiationResult> _pendingInitiationResults = new();

    public CryptoService(
        SessionService sessionService,
        MessageEncryptionService messageEncryptionService,
        HttpClient http)
    {
        _sessionService = sessionService;
        _messageEncryptionService = messageEncryptionService;
        _http = http;
    }

    /// <summary>
    /// Ensures an encrypted session exists with the specified remote device.
    /// If no session exists, establishes one via the X3DH protocol and caches
    /// the InitiationResult for embedding in the first (PreKey) message.
    /// </summary>
    public async Task EstablishSessionAsync(decimal userId, decimal deviceId)
    {
        var hasSession = await _sessionService.HasSessionAsync(deviceId);
        if (!hasSession)
        {
            var (_, initiationResult) = await _sessionService.EstablishSessionAsync(userId, deviceId);
            _pendingInitiationResults[deviceId] = initiationResult;
        }
    }

    /// <summary>
    /// Encrypts a plaintext message for the specified remote device.
    /// If an X3DH InitiationResult is cached (first message), packs as PreKeyMessage;
    /// otherwise packs as NormalMessage.
    /// </summary>
    /// <returns>Tuple of (base64 ciphertext, MessageType).</returns>
    public async Task<(string ciphertextBase64, MessageType messageType)> EncryptMessageAsync(
        decimal recipientDeviceId, string plaintext)
    {
        var session = await _sessionService.LoadSessionAsync(recipientDeviceId)
            ?? throw new InvalidOperationException(
                $"No session exists for device {recipientDeviceId}. Call EstablishSessionAsync first.");

        byte[] ciphertextWithHeader;
        RatchetState updatedState;
        MessageType messageType;

        if (_pendingInitiationResults.Remove(recipientDeviceId, out var initiationResult))
        {
            // First message to this device — pack as PreKeyMessage with X3DH handshake data
            var preKeyHeader = new PreKeyHeaderInfo
            {
                EphemeralPublicKey = initiationResult.EphemeralPublicKey,
                KemCiphertext = initiationResult.KemCiphertext,
                UsedOneTimePreKeyId = initiationResult.UsedOneTimePreKeyId
            };

            (ciphertextWithHeader, updatedState) =
                _messageEncryptionService.EncryptPreKeyMessage(session, plaintext, preKeyHeader);
            messageType = MessageType.PreKeyMessage;
        }
        else
        {
            // Established session — pack as NormalMessage
            (ciphertextWithHeader, updatedState) =
                _messageEncryptionService.EncryptMessage(session, plaintext);
            messageType = MessageType.NormalMessage;
        }

        await _sessionService.SaveSessionAsync(recipientDeviceId, updatedState);

        return (Convert.ToBase64String(ciphertextWithHeader), messageType);
    }

    /// <summary>
    /// Decrypts a received ciphertext from the specified sender device.
    /// If messageType is PreKeyMessage and no session exists, establishes a responder
    /// session using the embedded X3DH handshake data, then decrypts.
    /// </summary>
    public async Task<string> DecryptMessageAsync(
        decimal senderDeviceId, string ciphertextBase64, MessageType messageType)
    {
        var ciphertextWithHeader = Convert.FromBase64String(ciphertextBase64);

        if (messageType == MessageType.PreKeyMessage)
        {
            // Extract X3DH params from the PreKeyMessage
            var preKeyHeader = MessageEncryptionService.ExtractPreKeyHeader(ciphertextWithHeader);

            // Establish responder session (or load existing if somehow already established)
            var session = await _sessionService.LoadSessionAsync(senderDeviceId);
            if (session is null)
            {
                session = await _sessionService.EstablishSessionAsResponderAsync(
                    preKeyHeader.EphemeralPublicKey,
                    preKeyHeader.KemCiphertext,
                    preKeyHeader.UsedOneTimePreKeyId,
                    senderDeviceId);
            }

            // Strip the PreKeyHeader and decrypt the ratchet portion
            var ratchetBlob = MessageEncryptionService.StripPreKeyHeader(ciphertextWithHeader);
            var (plaintext, updatedState) =
                _messageEncryptionService.DecryptMessage(session, ratchetBlob);

            await _sessionService.SaveSessionAsync(senderDeviceId, updatedState);
            return plaintext;
        }
        else
        {
            // NormalMessage — session must already exist
            var session = await _sessionService.LoadSessionAsync(senderDeviceId)
                ?? throw new InvalidOperationException(
                    $"No session exists for device {senderDeviceId}.");

            var (plaintext, updatedState) =
                _messageEncryptionService.DecryptMessage(session, ciphertextWithHeader);

            await _sessionService.SaveSessionAsync(senderDeviceId, updatedState);
            return plaintext;
        }
    }

    /// <summary>
    /// Encrypts a plaintext message for all active devices of a recipient user.
    /// Returns a list of (deviceId, ciphertextBase64, messageType) tuples.
    /// </summary>
    public async Task<List<(decimal deviceId, string ciphertextBase64, MessageType messageType)>> EncryptMessageForAllDevicesAsync(
        decimal recipientUserId, string plaintext)
    {
        var devices = await _http.GetFromJsonAsync<List<DeviceInfoResponse>>(
            $"/api/users/{recipientUserId}/devices")
            ?? throw new InvalidOperationException(
                $"Failed to fetch devices for user {recipientUserId}.");

        var results = new List<(decimal deviceId, string ciphertextBase64, MessageType messageType)>();

        foreach (var device in devices)
        {
            await EstablishSessionAsync(recipientUserId, device.DeviceId);
            var (ciphertextBase64, messageType) = await EncryptMessageAsync(device.DeviceId, plaintext);
            results.Add((device.DeviceId, ciphertextBase64, messageType));
        }

        return results;
    }

    /// <summary>
    /// Encrypts a plaintext message for all participants in a group conversation.
    /// </summary>
    public async Task<List<SendMessageRequest>> EncryptGroupMessageAsync(
        decimal conversationId, decimal selfUserId, string plaintext)
    {
        var participants = await _http.GetFromJsonAsync<List<ParticipantResponse>>(
            $"/api/conversations/{conversationId}/participants")
            ?? throw new InvalidOperationException(
                $"Failed to fetch participants for conversation {conversationId}.");

        var requests = new List<SendMessageRequest>();

        foreach (var participant in participants.Where(p => p.UserId != selfUserId))
        {
            var deviceCiphertexts = await EncryptMessageForAllDevicesAsync(participant.UserId, plaintext);

            foreach (var (deviceId, ciphertextBase64, messageType) in deviceCiphertexts)
            {
                requests.Add(new SendMessageRequest(
                    conversationId,
                    deviceId,
                    ciphertextBase64,
                    messageType,
                    ContentType.Text));
            }
        }

        return requests;
    }
}
