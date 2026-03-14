using System.Net;
using System.Net.Http.Json;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Client.Services;

public class KeyBackupService(
    HttpClient http,
    LocalStorageService storage,
    KeyBackupCryptoService crypto)
{
    public async Task UploadBackupAsync(string password)
    {
        var payload = new KeyBackupPayload
        {
            ClassicalPrivateKey = await storage.GetAsync("identity.classical.private") ?? [],
            ClassicalPublicKey = await storage.GetAsync("identity.classical.public") ?? [],
            PostQuantumPrivateKey = await storage.GetAsync("identity.pq.private") ?? [],
            PostQuantumPublicKey = await storage.GetAsync("identity.pq.public") ?? []
        };

        var (encryptedBlob, salt, nonce) = crypto.Encrypt(payload, password);

        var request = new UploadKeyBackupRequest(
            Convert.ToBase64String(encryptedBlob),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(nonce));

        var response = await http.PostAsJsonAsync("/api/keys/backup", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<KeyBackupPayload?> TryRestoreBackupAsync(string password)
    {
        var response = await http.GetAsync("/api/keys/backup");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var backup = await response.Content.ReadFromJsonAsync<KeyBackupResponse>();
        if (backup is null)
            return null;

        var encryptedBlob = Convert.FromBase64String(backup.EncryptedBlob);
        var salt = Convert.FromBase64String(backup.Salt);
        var nonce = Convert.FromBase64String(backup.Nonce);

        var payload = crypto.Decrypt(encryptedBlob, salt, nonce, password);

        // Store restored identity keys to localStorage
        await storage.StoreAsync("identity.classical.private", payload.ClassicalPrivateKey);
        await storage.StoreAsync("identity.classical.public", payload.ClassicalPublicKey);
        await storage.StoreAsync("identity.pq.private", payload.PostQuantumPrivateKey);
        await storage.StoreAsync("identity.pq.public", payload.PostQuantumPublicKey);

        return payload;
    }

    public async Task<DateTimeOffset?> GetBackupTimestampAsync()
    {
        var response = await http.GetAsync("/api/keys/backup");
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        var backup = await response.Content.ReadFromJsonAsync<KeyBackupResponse>();
        return backup?.UpdatedAt;
    }

    public async Task DeleteBackupAsync()
    {
        var response = await http.DeleteAsync("/api/keys/backup");
        response.EnsureSuccessStatusCode();
    }
}
