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
public class LocalStorageService
{
    private readonly IJSRuntime _js;
    private readonly Dictionary<string, byte[]> _cache = new();
    private byte[]? _encryptionKey;

    public LocalStorageService(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>
    /// Derives a 32-byte AES key from the given password and enables
    /// encryption-at-rest for all subsequent store/get operations.
    /// </summary>
    public void InitializeEncryption(string password)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        var sha256 = new Sha256Digest();
        var ikm = new byte[sha256.GetDigestSize()];
        sha256.BlockUpdate(passwordBytes, 0, passwordBytes.Length);
        sha256.DoFinal(ikm, 0);

        var info = Encoding.UTF8.GetBytes("ToledoMessage-StorageEncryption-v1");

        _encryptionKey = HybridKeyDerivation.DeriveKey(ikm, info, 32);
    }

    public async Task StoreAsync(string key, byte[] value)
    {
        byte[] toStore;
        if (_encryptionKey is not null)
        {
            var nonce = DeriveNonceFromKey(key);
            toStore = AesGcmCipher.Encrypt(_encryptionKey, nonce, value);
        }
        else
        {
            toStore = value;
        }

        _cache[key] = toStore;
        var base64 = Convert.ToBase64String(toStore);
        await _js.InvokeVoidAsync("toledoStorage.setItem", key, base64);
    }

    public async Task<byte[]?> GetAsync(string key)
    {
        // Check in-memory cache first
        if (_cache.TryGetValue(key, out var cached))
        {
            if (_encryptionKey is not null)
            {
                var nonce = DeriveNonceFromKey(key);
                return AesGcmCipher.Decrypt(_encryptionKey, nonce, cached);
            }
            return cached;
        }

        // Fall back to browser localStorage
        var base64 = await _js.InvokeAsync<string?>("toledoStorage.getItem", key);
        if (base64 is null)
            return null;

        var stored = Convert.FromBase64String(base64);
        _cache[key] = stored;

        if (_encryptionKey is not null)
        {
            var nonce = DeriveNonceFromKey(key);
            return AesGcmCipher.Decrypt(_encryptionKey, nonce, stored);
        }

        return stored;
    }

    public async Task DeleteAsync(string key)
    {
        _cache.Remove(key);
        await _js.InvokeVoidAsync("toledoStorage.removeItem", key);
    }

    public async Task<bool> ContainsKeyAsync(string key)
    {
        if (_cache.ContainsKey(key))
            return true;

        return await _js.InvokeAsync<bool>("toledoStorage.containsKey", key);
    }

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
