using Microsoft.JSInterop;

namespace ToledoMessage.Client.Services;

/// <summary>
/// Wraps the Browser Notification API to show desktop notifications
/// when the user receives a message and the tab is not focused.
/// </summary>
public sealed class NotificationService(IJSRuntime js) : IAsyncDisposable
{
    private bool _permissionGranted;
    private bool _initialized;

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _permissionGranted = await js.InvokeAsync<bool>("toledoNotifications.initialize");
        _initialized = true;
    }

    public async Task RequestPermissionAsync()
    {
        _permissionGranted = await js.InvokeAsync<bool>("toledoNotifications.requestPermission");
    }

    public async Task ShowNotificationAsync(string title, string body)
    {
        if (!_permissionGranted) return;

        var isFocused = await js.InvokeAsync<bool>("toledoNotifications.isTabFocused");
        if (isFocused) return;

        await js.InvokeVoidAsync("toledoNotifications.show", title, body);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
