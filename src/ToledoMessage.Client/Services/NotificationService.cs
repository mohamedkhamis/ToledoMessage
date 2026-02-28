using Microsoft.JSInterop;

namespace ToledoMessage.Client.Services;

/// <summary>
/// Wraps the Browser Notification API to show desktop notifications
/// when the user receives a message and the tab is not focused.
/// </summary>
public sealed class NotificationService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private bool _permissionGranted;
    private bool _initialized;

    public NotificationService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _permissionGranted = await _js.InvokeAsync<bool>("toledoNotifications.initialize");
        _initialized = true;
    }

    public async Task RequestPermissionAsync()
    {
        _permissionGranted = await _js.InvokeAsync<bool>("toledoNotifications.requestPermission");
    }

    public async Task ShowNotificationAsync(string title, string body)
    {
        if (!_permissionGranted) return;

        var isFocused = await _js.InvokeAsync<bool>("toledoNotifications.isTabFocused");
        if (isFocused) return;

        await _js.InvokeVoidAsync("toledoNotifications.show", title, body);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
