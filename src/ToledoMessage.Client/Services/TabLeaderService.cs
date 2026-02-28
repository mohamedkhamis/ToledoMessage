using Microsoft.JSInterop;

namespace ToledoMessage.Client.Services;

/// <summary>
/// Uses the BroadcastChannel API to elect a single browser tab as the "leader"
/// responsible for maintaining the SignalR connection. Other tabs receive messages
/// via cross-tab broadcasting from the leader tab.
/// </summary>
public sealed class TabLeaderService(IJSRuntime js) : IAsyncDisposable
{
    private readonly string _tabId = Guid.NewGuid().ToString("N")[..8];
    private DotNetObjectReference<TabLeaderService>? _dotNetRef;
    private bool _isLeader;
    private bool _initialized;

    public event Action<bool>? OnLeadershipChanged;
    public bool IsLeader => _isLeader;

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _dotNetRef = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("toledoTabLeader.initialize", _tabId, _dotNetRef);
        _initialized = true;
    }

    [JSInvokable]
    public void OnLeaderElected(bool isLeader)
    {
        if (_isLeader == isLeader) return;
        _isLeader = isLeader;
        OnLeadershipChanged?.Invoke(isLeader);
    }

    public async ValueTask DisposeAsync()
    {
        if (_initialized)
            try
            {
                await js.InvokeVoidAsync("toledoTabLeader.dispose");
            }
            catch (JSDisconnectedException)
            {
                // Circuit already disconnected
            }

        _dotNetRef?.Dispose();
    }
}
