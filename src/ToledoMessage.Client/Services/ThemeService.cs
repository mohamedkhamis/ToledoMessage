using Microsoft.JSInterop;

namespace ToledoMessage.Client.Services;

public sealed class ThemeService(IJSRuntime js)
{
    private string _cachedThemeId = "default";
    private ThemeLabelSet _cachedLabels = ThemeLabelSet.Default;

    // Event: fires when theme changes so components can re-render
    public event Action? OnThemeChanged;

    public ThemeLabelSet Labels => _cachedLabels;

    public async Task<string> GetThemeAsync()
    {
        _cachedThemeId = await js.InvokeAsync<string?>("toledoStorage.getTheme") ?? "default";
        _cachedLabels = ResolveLabels(_cachedThemeId);
        return _cachedThemeId;
    }

    public async Task SetThemeAsync(string themeName)
    {
        await js.InvokeVoidAsync("toledoStorage.setTheme", themeName);
        _cachedThemeId = themeName;
        _cachedLabels = ResolveLabels(themeName);
        OnThemeChanged?.Invoke();
    }

    private static ThemeLabelSet ResolveLabels(string themeId) => themeId switch
    {
        "whatsapp" or "whatsapp-dark" => ThemeLabelSet.WhatsApp,
        "telegram" or "telegram-dark" => ThemeLabelSet.Telegram,
        "signal" or "signal-dark" => ThemeLabelSet.Signal,
        _ => ThemeLabelSet.Default
    };

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
            new ThemeInfo("telegram-dark", "Telegram Dark", "#17212b"),
            new ThemeInfo("signal", "Signal", "#3a76f0"),
            new ThemeInfo("signal-dark", "Signal Dark", "#2c5fc7")
        ];
    }
}

public sealed record ThemeInfo(string Id, string DisplayName, string SwatchColor);
