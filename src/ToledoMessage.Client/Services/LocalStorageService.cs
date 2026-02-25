namespace ToledoMessage.Client.Services;

/// <summary>
/// In-memory store for client-side crypto state.
/// Will be replaced with IndexedDB JS interop in a future iteration.
/// Private keys stored here must never leave the browser.
/// </summary>
public class LocalStorageService
{
    private readonly Dictionary<string, byte[]> _store = new();

    public Task StoreAsync(string key, byte[] value)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetAsync(string key)
    {
        _store.TryGetValue(key, out var value);
        return Task.FromResult(value);
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
}
