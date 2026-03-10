using System.Diagnostics.CodeAnalysis;

namespace ToledoMessage.Client.Services;

[SuppressMessage("ReSharper", "InvertIf")]
public sealed class ToastService
{
    // ReSharper disable once CollectionNeverUpdated.Local
    private readonly List<ToastItem> _toasts = [];

    public event Action? OnChange;

    public IReadOnlyList<ToastItem> Toasts => _toasts;

    public void Dismiss(int id)
    {
        var toast = _toasts.FirstOrDefault(t => t.Id == id);
        if (toast is not null)
        {
            toast.Exiting = true;
            OnChange?.Invoke();
            _ = RemoveAfterDelay(id, 300);
        }
    }

    private async Task RemoveAfterDelay(int id, int delayMs)
    {
        await Task.Delay(delayMs);
        var toast = _toasts.FirstOrDefault(t => t.Id == id);
        if (toast is not null && !toast.Exiting)
        {
            toast.Exiting = true;
            OnChange?.Invoke();
            await Task.Delay(300); // Wait for exit animation
        }

        _toasts.RemoveAll(t => t.Id == id);
        OnChange?.Invoke();
    }
}

public sealed class ToastItem
{
    public int Id { get; set; }
    public string Message { get; set; } = "";
    public string Type { get; set; } = "info";
    public bool Exiting { get; set; }
}
