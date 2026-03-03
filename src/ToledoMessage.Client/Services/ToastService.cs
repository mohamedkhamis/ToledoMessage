namespace ToledoMessage.Client.Services;

public sealed class ToastService
{
    private readonly List<ToastItem> _toasts = [];
    private int _nextId;

    public event Action? OnChange;

    public IReadOnlyList<ToastItem> Toasts => _toasts;

    public void ShowSuccess(string message)
    {
        Show(message, "success");
    }

    public void ShowError(string message)
    {
        Show(message, "error");
    }

    public void ShowInfo(string message)
    {
        Show(message, "info");
    }

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

    private void Show(string message, string type, int durationMs = 4000)
    {
        var toast = new ToastItem
        {
            Id = ++_nextId,
            Message = message,
            Type = type
        };
        _toasts.Add(toast);
        OnChange?.Invoke();
        _ = RemoveAfterDelay(toast.Id, durationMs);
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
