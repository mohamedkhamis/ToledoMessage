using System.Text;
using Org.BouncyCastle.Crypto.Digests;
using ToledoMessage.Crypto.Classical;
using ToledoMessage.Crypto.Hybrid;

namespace ToledoMessage.Client.Services;

/// <summary>
/// In-memory store for client-side crypto state.
/// Will be replaced with IndexedDB JS interop in a future iteration.
/// Private keys stored here must never leave the browser.
/// Supports optional encryption-at-rest: when a password is provided via
/// <see cref="InitializeEncryption"/>, all values are AES-256-GCM encrypted
/// before storage and decrypted on retrieval.
/// </summary>
public class LocalStorageService
{
    private readonly Dictionary<string, byte[]> _store = new();
    private byte[]? _encryptionKey;

    /// <summary>
    /// Derives a 32-byte AES key from the given password and enables
    /// encryption-at-rest for all subsequent store/get operations.
    /// Uses SHA-256 of the password bytes as IKM, then HKDF-SHA256 to derive the final key.
    /// </summary>
    public void InitializeEncryption(string password)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        // Hash the password to get a fixed-length IKM for HKDF
        var sha256 = new Sha256Digest();
        var ikm = new byte[sha256.GetDigestSize()];
        sha256.BlockUpdate(passwordBytes, 0, passwordBytes.Length);
        sha256.DoFinal(ikm, 0);

        var info = Encoding.UTF8.GetBytes("ToledoMessage-StorageEncryption-v1");

        _encryptionKey = HybridKeyDerivation.DeriveKey(ikm, info, 32);
    }

    public Task StoreAsync(string key, byte[] value)
    {
        if (_encryptionKey is not null)
        {
            var nonce = DeriveNonceFromKey(key);
            _store[key] = AesGcmCipher.Encrypt(_encryptionKey, nonce, value);
        }
        else
        {
            _store[key] = value;
        }

        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(string key)
    {
        if (!_store.TryGetValue(key, out var value))
            return Task.FromResult<byte[]?>(null);

        if (_encryptionKey is not null)
        {
            var nonce = DeriveNonceFromKey(key);
            return Task.FromResult<byte[]?>(AesGcmCipher.Decrypt(_encryptionKey, nonce, value));
        }

        return Task.FromResult<byte[]?>(value);
    }

    public Task DeleteAsync(string key)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ContainsKeyAsync(string key)
    {
        return Task.FromResult(_store.ContainsKey(key));
    }

    /// <summary>
    /// Derives a 12-byte AES-GCM nonce from the storage key name.
    /// Uses SHA-256 of the key name, truncated to 12 bytes.
    /// </summary>
    private static byte[] DeriveNonceFromKey(string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var sha256 = new Sha256Digest();
        var hash = new byte[sha256.GetDigestSize()];
        sha256.BlockUpdate(keyBytes, 0, keyBytes.Length);
        sha256.DoFinal(hash, 0);

        var nonce = new byte[12];
        Buffer.BlockCopy(hash, 0, nonce, 0, 12);
        return nonce;
    }
}
