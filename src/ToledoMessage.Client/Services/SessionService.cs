using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ToledoMessage.Crypto.Protocol;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Client.Services;

/// <summary>
/// Orchestrates X3DH session establishment with a remote device.
/// Fetches the remote device's pre-key bundle, runs the X3DH initiator protocol,
/// initializes a Double Ratchet session, and persists the ratchet state.
/// </summary>
public class SessionService
{
    private readonly HttpClient _http;
    private readonly LocalStorageService _storage;

    public SessionService(HttpClient http, LocalStorageService storage)
    {
        _http = http;
        _storage = storage;
    }

    /// <summary>
    /// Establishes a new encrypted session with the specified remote device.
    /// Fetches the pre-key bundle from the server, performs X3DH, and initializes
    /// the Double Ratchet. Persists the resulting session state locally.
    /// </summary>
    /// <param name="userId">The remote user's ID (used for the API route).</param>
    /// <param name="deviceId">The remote device's ID.</param>
    /// <returns>A ready-to-use <see cref="DoubleRatchet"/> session.</returns>
    public async Task<DoubleRatchet> EstablishSessionAsync(decimal userId, decimal deviceId)
    {
        // 1. Fetch pre-key bundle from server
        var bundleResponse = await _http.GetFromJsonAsync<PreKeyBundleResponse>(
            $"/api/users/{userId}/prekey-bundle?deviceId={deviceId}")
            ?? throw new InvalidOperationException("Failed to fetch pre-key bundle from server.");

        // 2. Convert PreKeyBundleResponse (base64 strings) → crypto PreKeyBundle (byte arrays)
        var cryptoBundle = ConvertToCryptoBundle(bundleResponse);

        // 3. Run X3DH initiator protocol
        var result = X3dhInitiator.Initiate(cryptoBundle);

        // 4. Initialize Double Ratchet as initiator
        //    The remote signed pre-key serves as the initial ratchet public key for Alice.
        var session = DoubleRatchet.InitializeAsInitiator(
            result.RootKey, cryptoBundle.SignedPreKeyPublic);

        // 5. Persist session state
        await SaveSessionAsync(deviceId, session.GetState());

        // 6. Store the remote device's identity public key for safety number verification
        var remoteIdentityKey = Convert.FromBase64String(bundleResponse.IdentityPublicKeyClassical);
        await _storage.StoreAsync($"remote.identity.{deviceId}", remoteIdentityKey);

        return session;
    }

    /// <summary>
    /// Loads an existing session for the specified remote device, if one exists.
    /// </summary>
    /// <returns>The restored <see cref="DoubleRatchet"/> session, or null if no session exists.</returns>
    public async Task<DoubleRatchet?> LoadSessionAsync(decimal deviceId)
    {
        var stateBytes = await _storage.GetAsync(SessionKey(deviceId));
        if (stateBytes is null)
            return null;

        var stateJson = Encoding.UTF8.GetString(stateBytes);
        var state = JsonSerializer.Deserialize<RatchetState>(stateJson);
        if (state is null)
            return null;

        return DoubleRatchet.FromState(state);
    }

    /// <summary>
    /// Persists the current session state for the specified remote device.
    /// </summary>
    public async Task SaveSessionAsync(decimal deviceId, RatchetState state)
    {
        var stateJson = JsonSerializer.Serialize(state);
        var stateBytes = Encoding.UTF8.GetBytes(stateJson);
        await _storage.StoreAsync(SessionKey(deviceId), stateBytes);
    }

    /// <summary>
    /// Checks whether a session already exists for the specified remote device.
    /// </summary>
    public Task<bool> HasSessionAsync(decimal deviceId)
    {
        return _storage.ContainsKeyAsync(SessionKey(deviceId));
    }

    /// <summary>
    /// Converts the server DTO (base64-encoded strings) into the crypto domain model (byte arrays).
    /// </summary>
    private static PreKeyBundle ConvertToCryptoBundle(PreKeyBundleResponse response)
    {
        return new PreKeyBundle
        {
            IdentityKeyClassical = Convert.FromBase64String(response.IdentityPublicKeyClassical),
            IdentityKeyPostQuantum = Convert.FromBase64String(response.IdentityPublicKeyPostQuantum),
            SignedPreKeyPublic = Convert.FromBase64String(response.SignedPreKeyPublic),
            SignedPreKeySignature = Convert.FromBase64String(response.SignedPreKeySignature),
            SignedPreKeyId = response.SignedPreKeyId,
            KyberPreKeyPublic = Convert.FromBase64String(response.KyberPreKeyPublic),
            KyberPreKeySignature = Convert.FromBase64String(response.KyberPreKeySignature),
            OneTimePreKeyPublic = response.OneTimePreKey is not null
                ? Convert.FromBase64String(response.OneTimePreKey.PublicKey)
                : null,
            OneTimePreKeyId = response.OneTimePreKey?.KeyId
        };
    }

    private static string SessionKey(decimal deviceId) => $"session.{deviceId}";
}
