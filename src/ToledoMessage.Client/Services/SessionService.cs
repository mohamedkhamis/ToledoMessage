using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ToledoMessage.Crypto.Protocol;
using ToledoMessage.Shared.DTOs;

// ReSharper disable RemoveRedundantBraces

namespace ToledoMessage.Client.Services;

/// <summary>
/// Orchestrates X3DH session establishment with a remote device.
/// Fetches the remote device's pre-key bundle, runs the X3DH initiator protocol,
/// initializes a Double Ratchet session, and persists the ratchet state.
/// </summary>
public class SessionService(HttpClient http, LocalStorageService storage)
{
    /// <summary>
    /// Establishes a new encrypted session with the specified remote device (initiator side).
    /// Fetches the pre-key bundle from the server, performs X3DH, and initializes
    /// the Double Ratchet. Persists the resulting session state locally.
    /// Returns the X3DH InitiationResult so the caller can embed it in a PreKeyMessage.
    /// </summary>
    /// <param name="userId">The remote user's ID (used for the API route).</param>
    /// <param name="deviceId">The remote device's ID.</param>
    /// <returns>A tuple of the ready-to-use <see cref="DoubleRatchet"/> session and the X3DH initiation result.</returns>
    public async Task<(DoubleRatchet session, X3dhInitiator.InitiationResult initiationResult)> EstablishSessionAsync(long userId, long deviceId)
    {
        // 1. Fetch pre-key bundle from server
        var bundleResponse = await http.GetFromJsonAsync<PreKeyBundleResponse>(
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
        await storage.StoreAsync($"remote.identity.{deviceId}", remoteIdentityKey);

        return (session, result);
    }

    /// <summary>
    /// Establishes a session as the responder (Bob) when receiving a PreKeyMessage.
    /// Loads local private keys, runs X3DH responder, initializes Double Ratchet as responder,
    /// and persists the resulting session state.
    /// </summary>
    /// <param name="ephemeralPublicKey">Alice's ephemeral X25519 public key from the PreKeyHeader.</param>
    /// <param name="kemCiphertext">Alice's ML-KEM ciphertext from the PreKeyHeader.</param>
    /// <param name="usedOneTimePreKeyId">The ID of the consumed one-time pre-key, or null.</param>
    /// <param name="senderDeviceId">The sender's device ID (used as the session key).</param>
    /// <returns>A ready-to-use <see cref="DoubleRatchet"/> session.</returns>
    public async Task<DoubleRatchet> EstablishSessionAsResponderAsync(
        byte[] ephemeralPublicKey,
        byte[] kemCiphertext,
        int? usedOneTimePreKeyId,
        long senderDeviceId)
    {
        // 1. Load private keys from local storage
        var signedPreKeyPrivate = await storage.GetAsync("signedPreKey.private")
                                  ?? throw new InvalidOperationException("Signed pre-key private key not found in local storage.");
        var signedPreKeyPublic = await storage.GetAsync("signedPreKey.public")
                                 ?? throw new InvalidOperationException("Signed pre-key public key not found in local storage.");
        var kyberPreKeyPrivate = await storage.GetAsync("kyberPreKey.private")
                                 ?? throw new InvalidOperationException("Kyber pre-key private key not found in local storage.");

        byte[]? oneTimePreKeyPrivate = null;
        if (usedOneTimePreKeyId.HasValue)
        {
            oneTimePreKeyPrivate = await storage.GetAsync($"otpk.{usedOneTimePreKeyId.Value}");
        }

        // 2. Run X3DH responder protocol
        // ReSharper disable once UnusedVariable
        var (rootKey, chainKey) = X3dhResponder.Respond(
            signedPreKeyPrivate,
            kyberPreKeyPrivate,
            oneTimePreKeyPrivate,
            ephemeralPublicKey,
            kemCiphertext);

        // 3. Initialize Double Ratchet as responder
        //    Bob's signed pre-key serves as the initial ratchet key pair.
        var session = DoubleRatchet.InitializeAsResponder(
            rootKey, signedPreKeyPrivate, signedPreKeyPublic);

        // 4. Persist session state
        await SaveSessionAsync(senderDeviceId, session.GetState());

        // 5. Delete consumed one-time pre-key
        if (usedOneTimePreKeyId.HasValue) await storage.DeleteAsync($"otpk.{usedOneTimePreKeyId.Value}");

        return session;
    }

    /// <summary>
    /// Loads an existing session for the specified remote device, if one exists.
    /// </summary>
    /// <returns>The restored <see cref="DoubleRatchet"/> session, or null if no session exists.</returns>
    public async Task<DoubleRatchet?> LoadSessionAsync(long deviceId)
    {
        var stateBytes = await storage.GetAsync(SessionKey(deviceId));
        if (stateBytes is null)
            return null;

        var stateJson = Encoding.UTF8.GetString(stateBytes);
        var state = JsonSerializer.Deserialize<RatchetState>(stateJson);
        return state is null ? null : DoubleRatchet.FromState(state);
    }

    /// <summary>
    /// Persists the current session state for the specified remote device.
    /// </summary>
    public async Task SaveSessionAsync(long deviceId, RatchetState state)
    {
        var stateJson = JsonSerializer.Serialize(state);
        var stateBytes = Encoding.UTF8.GetBytes(stateJson);
        await storage.StoreAsync(SessionKey(deviceId), stateBytes);
    }

    /// <summary>
    /// Checks whether a session already exists for the specified remote device.
    /// </summary>
    public Task<bool> HasSessionAsync(long deviceId)
    {
        return storage.ContainsKeyAsync(SessionKey(deviceId));
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

    private static string SessionKey(long deviceId)
    {
        return $"session.{deviceId}";
    }
}
