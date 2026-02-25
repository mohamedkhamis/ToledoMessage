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

    /// <summary>
    /// Encrypts a plaintext message for all active devices of a recipient user.
    /// Fetches the user's active devices, establishes sessions as needed, and
    /// encrypts the message independently for each device (fan-out encryption).
    /// </summary>
    /// <param name="recipientUserId">The recipient user's ID.</param>
    /// <param name="plaintext">The plaintext message to encrypt.</param>
    /// <returns>A list of (deviceId, ciphertextBase64) tuples, one per active device.</returns>
    public async Task<List<(decimal deviceId, string ciphertextBase64)>> EncryptMessageForAllDevicesAsync(
        decimal recipientUserId, string plaintext)
    {
        // 1. Fetch all active devices for the recipient user
        var devices = await _http.GetFromJsonAsync<List<DeviceInfoResponse>>(
            $"/api/users/{recipientUserId}/devices")
            ?? throw new InvalidOperationException(
                $"Failed to fetch devices for user {recipientUserId}.");

        var results = new List<(decimal deviceId, string ciphertextBase64)>();

        // 2. For each device, ensure session exists and encrypt
        foreach (var device in devices)
        {
            await EstablishSessionAsync(recipientUserId, device.DeviceId);
            var ciphertextBase64 = await EncryptMessageAsync(device.DeviceId, plaintext);
            results.Add((device.DeviceId, ciphertextBase64));
        }

        return results;
    }

    /// <summary>
    /// Encrypts a plaintext message for all participants in a group conversation.
    /// Fetches the participant list, then for each participant (except self) encrypts
    /// the message for all their devices (fan-out). Returns a list of SendMessageRequests
    /// ready to be sent via SignalR.
    /// </summary>
    /// <param name="conversationId">The group conversation ID.</param>
    /// <param name="selfUserId">The current (sending) user's ID, to exclude from recipients.</param>
    /// <param name="plaintext">The plaintext message to encrypt.</param>
    /// <returns>A list of <see cref="SendMessageRequest"/> objects, one per recipient device.</returns>
    public async Task<List<SendMessageRequest>> EncryptGroupMessageAsync(
        decimal conversationId, decimal selfUserId, string plaintext)
    {
        // 1. Get all participants in the conversation
        var participants = await _http.GetFromJsonAsync<List<ParticipantResponse>>(
            $"/api/conversations/{conversationId}/participants")
            ?? throw new InvalidOperationException(
                $"Failed to fetch participants for conversation {conversationId}.");

        var requests = new List<SendMessageRequest>();

        // 2. For each participant (except self), encrypt for all their devices
        foreach (var participant in participants.Where(p => p.UserId != selfUserId))
        {
            var deviceCiphertexts = await EncryptMessageForAllDevicesAsync(participant.UserId, plaintext);

            foreach (var (deviceId, ciphertextBase64) in deviceCiphertexts)
            {
                requests.Add(new SendMessageRequest(
                    conversationId,
                    deviceId,
                    ciphertextBase64,
                    MessageType.NormalMessage,
                    ContentType.Text));
            }
        }

        return requests;
    }
}
