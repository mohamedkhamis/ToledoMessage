using System.Security.Cryptography;
using System.Text;
using Microsoft.JSInterop;
using Org.BouncyCastle.Crypto.Digests;
using ToledoMessage.Crypto.Classical;
using ToledoMessage.Crypto.Hybrid;

namespace ToledoMessage.Client.Services;

/// <summary>
/// Persistent browser localStorage backed service with optional encryption-at-rest.
/// Uses JS interop to store base64-encoded byte arrays in the browser's localStorage,
/// ensuring data survives page navigations and refreshes.
/// An in-memory cache avoids repeated JS interop calls for frequently accessed keys.
/// </summary>
public class LocalStorageService(IJSRuntime js)
{
    private readonly Dictionary<string, byte[]> _cache = new();
    private byte[]? _encryptionKey;

    /// <summary>
    /// Derives a 32-byte AES key from the given password using iterated SHA-256 + HKDF.
    /// Enables encryption-at-rest for all subsequent store/get operations.
    /// </summary>
    public void InitializeEncryption(string password)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var salt = "ToledoMessage-StorageEncryption-Salt-v2"u8.ToArray();

        // Iterated hashing (100k rounds) to slow down brute-force attacks
        var sha256 = new Sha256Digest();
        var ikm = new byte[sha256.GetDigestSize()];

        // First round: hash(password || salt)
        sha256.BlockUpdate(passwordBytes, 0, passwordBytes.Length);
        sha256.BlockUpdate(salt, 0, salt.Length);
        sha256.DoFinal(ikm, 0);

        // Subsequent rounds
        for (var i = 1; i < 100_000; i++)
        {
            sha256.Reset();
            sha256.BlockUpdate(ikm, 0, ikm.Length);
            sha256.DoFinal(ikm, 0);
        }

        var info = "ToledoMessage-StorageEncryption-v2"u8.ToArray();
        _encryptionKey = HybridKeyDerivation.DeriveKey(ikm, info, 32);
    }

    public async Task StoreAsync(string key, byte[] value)
    {
        byte[] toStore;
        if (_encryptionKey is not null)
        {
            var nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);
            var ciphertext = AesGcmCipher.Encrypt(_encryptionKey, nonce, value);
            // Prepend nonce to ciphertext: [12-byte nonce][ciphertext]
            toStore = new byte[12 + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, toStore, 0, 12);
            Buffer.BlockCopy(ciphertext, 0, toStore, 12, ciphertext.Length);
        }
        else
        {
            toStore = value;
        }

        _cache[key] = toStore;
        var base64 = Convert.ToBase64String(toStore);
        await js.InvokeVoidAsync("toledoStorage.setItem", key, base64);
    }

    public async Task<byte[]?> GetAsync(string key)
    {
        // Check in-memory cache first
        if (_cache.TryGetValue(key, out var cached))
        {
            if (_encryptionKey is null) return cached;

            var nonce = cached.AsSpan(0, 12).ToArray();
            var ciphertext = cached.AsSpan(12).ToArray();
            return AesGcmCipher.Decrypt(_encryptionKey, nonce, ciphertext);
        }

        // Fall back to browser localStorage
        var base64 = await js.InvokeAsync<string?>("toledoStorage.getItem", key);
        if (base64 is null)
            return null;

        var stored = Convert.FromBase64String(base64);
        _cache[key] = stored;

        // ReSharper disable once InvertIf
        if (_encryptionKey is not null)
        {
            var nonce = stored.AsSpan(0, 12).ToArray();
            var ciphertext = stored.AsSpan(12).ToArray();
            return AesGcmCipher.Decrypt(_encryptionKey, nonce, ciphertext);
        }

        return stored;
    }

    public async Task DeleteAsync(string key)
    {
        _cache.Remove(key);
        await js.InvokeVoidAsync("toledoStorage.removeItem", key);
    }

    public async Task<bool> ContainsKeyAsync(string key)
    {
        if (_cache.ContainsKey(key))
            return true;

        return await js.InvokeAsync<bool>("toledoStorage.containsKey", key);
    }
}
