using Microsoft.JSInterop;

namespace ToledoMessage.Client.Services;

public sealed class ThemeService(IJSRuntime js)
{
    public async Task<string> GetThemeAsync()
    {
        var theme = await js.InvokeAsync<string?>("toledoStorage.getTheme");
        // ReSharper disable once InvertIf
        if (theme is null or "default")
        {
            var prefersDark = await js.InvokeAsync<bool>("toledoStorage.prefersDarkMode");
            if (prefersDark) return "default-dark";
        }

        return theme ?? "default";
    }

    public async Task SetThemeAsync(string themeName)
    {
        await js.InvokeVoidAsync("toledoStorage.setTheme", themeName);
    }

    public async Task<string> GetFontSizeAsync()
    {
        return await js.InvokeAsync<string?>("toledoStorage.getFontSize") ?? "medium";
    }

    public async Task SetFontSizeAsync(string fontSize)
    {
        await js.InvokeVoidAsync("toledoStorage.setFontSize", fontSize);
    }

    public static IReadOnlyList<ThemeInfo> GetAvailableThemes()
    {
        return
        [
            new ThemeInfo("default", "Default", "#1976d2"),
            new ThemeInfo("default-dark", "Default Dark", "#1565c0"),
            new ThemeInfo("whatsapp", "WhatsApp", "#25d366"),
            new ThemeInfo("whatsapp-dark", "WhatsApp Dark", "#005c4b"),
            new ThemeInfo("telegram", "Telegram", "#2aabee"),
            new ThemeInfo("signal", "Signal", "#3a76f0"),
            new ThemeInfo("signal-dark", "Signal Dark", "#2c5fc7"),
        ];
    }
}

public sealed record ThemeInfo(string Id, string DisplayName, string SwatchColor);
