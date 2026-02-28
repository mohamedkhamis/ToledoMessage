using ToledoMessage.Crypto.KeyManagement;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Client.Services;

/// <summary>
/// Generates all cryptographic key material for device registration
/// and persists private keys to local storage.
/// </summary>
public class KeyGenerationService(LocalStorageService storage)
{
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
            DeviceName: deviceName,
            IdentityPublicKeyClassical: Convert.ToBase64String(identity.ClassicalPublicKey),
            IdentityPublicKeyPostQuantum: Convert.ToBase64String(identity.PostQuantumPublicKey),
            SignedPreKeyPublic: Convert.ToBase64String(signedPreKey.PublicKey),
            SignedPreKeySignature: Convert.ToBase64String(signedPreKey.Signature),
            SignedPreKeyId: signedPreKey.KeyId,
            KyberPreKeyPublic: Convert.ToBase64String(kyberPreKey.PublicKey),
            KyberPreKeySignature: Convert.ToBase64String(kyberPreKey.Signature),
            OneTimePreKeys: oneTimePreKeys
                .Select(k => new OneTimePreKeyDto(k.KeyId, Convert.ToBase64String(k.PublicKey)))
                .ToList());
    }
}
