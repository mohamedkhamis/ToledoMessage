using Microsoft.JSInterop;

namespace ToledoMessage.Client.Services;

public sealed class ThemeService
{
    private readonly IJSRuntime _js;
    private const string StorageKey = "app.theme";

    public ThemeService(IJSRuntime js) => _js = js;

    public async Task<string> GetThemeAsync()
    {
        var theme = await _js.InvokeAsync<string?>("toledoStorage.getTheme");
        return theme ?? "default";
    }

    public async Task SetThemeAsync(string themeName)
    {
        await _js.InvokeVoidAsync("toledoStorage.setTheme", themeName);
    }

    public static IReadOnlyList<ThemeInfo> GetAvailableThemes() =>
    [
        new("default",       "Default",        "#1976d2"),
        new("default-dark",  "Default Dark",   "#1565c0"),
        new("whatsapp",      "WhatsApp",       "#25d366"),
        new("whatsapp-dark", "WhatsApp Dark",  "#005c4b"),
        new("telegram",      "Telegram",       "#2aabee"),
        new("signal",        "Signal",         "#3a76f0"),
        new("signal-dark",   "Signal Dark",    "#2c5fc7"),
    ];
}

public sealed record ThemeInfo(string Id, string DisplayName, string SwatchColor);
