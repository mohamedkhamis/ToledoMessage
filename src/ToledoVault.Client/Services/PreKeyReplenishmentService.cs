using System.Net.Http.Json;
using ToledoVault.Crypto.KeyManagement;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Client.Services;

/// <inheritdoc />
/// <summary>
/// Monitors the <see cref="E:ToledoVault.Client.Services.SignalRService.OnPreKeyCountLow">SignalRService.OnPreKeyCountLow</see> event and automatically
/// generates and uploads fresh one-time pre-keys when the server signals that the
/// device's pre-key supply is running low.
/// </summary>
public class PreKeyReplenishmentService : IDisposable
{
    private readonly SignalRService _signalR;
    private readonly LocalStorageService _storage;
    private readonly HttpClient _http;

    /// <summary>
    /// The number of new one-time pre-keys to generate during each replenishment cycle.
    /// </summary>
    private const int ReplenishBatchSize = 50;

    /// <summary>
    /// The threshold below which replenishment is triggered.
    /// </summary>
    private const int LowThreshold = 10;

    public PreKeyReplenishmentService(
        SignalRService signalR,
        LocalStorageService storage,
        HttpClient http)
    {
        _signalR = signalR;
        _storage = storage;
        _http = http;

        _signalR.OnPreKeyCountLow += HandlePreKeyCountLow;
    }

    // ReSharper disable once AsyncVoidMethod
    private async void HandlePreKeyCountLow(long deviceId, int remainingCount)
    {
        if (remainingCount >= LowThreshold)
            return;

        try
        {
            // Determine the starting key ID for the new batch.
            // Use a counter stored in local storage to avoid key ID collisions.
            var counterBytes = await _storage.GetAsync("otpk.nextKeyId");
            var nextKeyId = counterBytes is not null
                ? BitConverter.ToInt32(counterBytes)
                : 100; // Start after the initial 100 keys (0-99)

            // Generate new one-time pre-keys
            var newKeys = PreKeyGenerator.GenerateOneTimePreKeys(nextKeyId, ReplenishBatchSize);

            // Store private keys locally
            foreach (var key in newKeys) await _storage.StoreAsync($"otpk.{key.KeyId}", key.PrivateKey);

            // Update the next key ID counter
            await _storage.StoreAsync("otpk.nextKeyId",
                BitConverter.GetBytes(nextKeyId + ReplenishBatchSize));

            // Upload public keys to the server
            var dtos = newKeys
                .Select(static k => new OneTimePreKeyDto(k.KeyId, Convert.ToBase64String(k.PublicKey)))
                .ToList();

            await _http.PostAsJsonAsync($"/api/devices/{deviceId}/prekeys", dtos);
        }
        catch
        {
            // Pre-key replenishment failures are non-critical.
            // The server will continue to function without one-time pre-keys,
            // just without the additional forward secrecy they provide.
        }
    }

    public void Dispose()
    {
        _signalR.OnPreKeyCountLow -= HandlePreKeyCountLow;
    }
}
