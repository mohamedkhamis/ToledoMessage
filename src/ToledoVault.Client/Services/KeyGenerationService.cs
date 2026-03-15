using ToledoVault.Crypto.KeyManagement;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Client.Services;

/// <summary>
/// Generates all cryptographic key material for device registration
/// and persists private keys to local storage.
/// </summary>
public class KeyGenerationService(LocalStorageService storage)
{
    /// <summary>
    /// Restores identity keys from a backup payload, generates fresh pre-keys,
    /// and returns a <see cref="DeviceRegistrationRequest"/> ready to send to the server.
    /// </summary>
    public async Task<DeviceRegistrationRequest> RestoreKeysAndBuildRequest(KeyBackupPayload payload, string deviceName)
    {
        // Identity keys are already stored in localStorage by KeyBackupService.TryRestoreBackupAsync
        // Generate fresh signed pre-key (X25519, keyId = 1)
        var signedPreKey = PreKeyGenerator.GenerateSignedPreKey(
            1, payload.ClassicalPrivateKey, payload.PostQuantumPrivateKey);

        // Generate fresh Kyber pre-key (ML-KEM-768)
        var kyberPreKey = PreKeyGenerator.GenerateKyberPreKey(
            payload.ClassicalPrivateKey, payload.PostQuantumPrivateKey);

        // Generate fresh batch of one-time pre-keys
        var oneTimePreKeys = PreKeyGenerator.GenerateOneTimePreKeys(0, 100);

        // Store pre-key private keys in local storage
        await storage.StoreAsync("signedPreKey.private", signedPreKey.PrivateKey);
        await storage.StoreAsync("signedPreKey.public", signedPreKey.PublicKey);
        await storage.StoreAsync("kyberPreKey.private", kyberPreKey.PrivateKey);

        foreach (var otpk in oneTimePreKeys) await storage.StoreAsync($"otpk.{otpk.KeyId}", otpk.PrivateKey);
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

        // Build registration request with restored identity public keys
        return new DeviceRegistrationRequest(
            deviceName,
            Convert.ToBase64String(payload.ClassicalPublicKey),
            Convert.ToBase64String(payload.PostQuantumPublicKey),
            Convert.ToBase64String(signedPreKey.PublicKey),
            Convert.ToBase64String(signedPreKey.Signature),
            signedPreKey.KeyId,
            Convert.ToBase64String(kyberPreKey.PublicKey),
            Convert.ToBase64String(kyberPreKey.Signature),
            oneTimePreKeys
                .Select(static k => new OneTimePreKeyDto(k.KeyId, Convert.ToBase64String(k.PublicKey)))
                ?.ToList());
    }

    /// <summary>
    /// Generates all key material for device registration and stores private keys locally.
    /// Returns a <see cref="DeviceRegistrationRequest"/> ready to send to the server.
    /// </summary>
    public async Task<DeviceRegistrationRequest> GenerateAndStoreKeys(string deviceName)
    {
        // 1. Generate identity key pair (Ed25519 + ML-DSA-65)
        var identity = IdentityKeyGenerator.Generate();

        // 2. Generate signed pre-key (X25519, keyId = 1 for first key)
        var signedPreKey = PreKeyGenerator.GenerateSignedPreKey(
            1, identity.ClassicalPrivateKey, identity.PostQuantumPrivateKey);

        // 3. Generate Kyber pre-key (ML-KEM-768)
        var kyberPreKey = PreKeyGenerator.GenerateKyberPreKey(
            identity.ClassicalPrivateKey, identity.PostQuantumPrivateKey);

        // 4. Generate batch of one-time pre-keys (100 keys starting from ID 0)
        var oneTimePreKeys = PreKeyGenerator.GenerateOneTimePreKeys(0, 100);

        // 5. Store all private keys in local storage
        await storage.StoreAsync("identity.classical.private", identity.ClassicalPrivateKey);
        await storage.StoreAsync("identity.classical.public", identity.ClassicalPublicKey);
        await storage.StoreAsync("identity.pq.private", identity.PostQuantumPrivateKey);
        await storage.StoreAsync("identity.pq.public", identity.PostQuantumPublicKey);
        await storage.StoreAsync("signedPreKey.private", signedPreKey.PrivateKey);
        await storage.StoreAsync("signedPreKey.public", signedPreKey.PublicKey);
        await storage.StoreAsync("kyberPreKey.private", kyberPreKey.PrivateKey);

        foreach (var otpk in oneTimePreKeys) await storage.StoreAsync($"otpk.{otpk.KeyId}", otpk.PrivateKey);

        // 6. Build DeviceRegistrationRequest with base64-encoded public keys
        return new DeviceRegistrationRequest(
            deviceName,
            Convert.ToBase64String(identity.ClassicalPublicKey),
            Convert.ToBase64String(identity.PostQuantumPublicKey),
            Convert.ToBase64String(signedPreKey.PublicKey),
            Convert.ToBase64String(signedPreKey.Signature),
            signedPreKey.KeyId,
            Convert.ToBase64String(kyberPreKey.PublicKey),
            Convert.ToBase64String(kyberPreKey.Signature),
            [.. oneTimePreKeys.Select(static k => new OneTimePreKeyDto(k.KeyId, Convert.ToBase64String(k.PublicKey)))]);
    }
}
