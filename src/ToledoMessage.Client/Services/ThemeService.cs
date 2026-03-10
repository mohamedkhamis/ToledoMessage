using Microsoft.JSInterop;

namespace ToledoMessage.Client.Services;

public sealed class ThemeService(IJSRuntime js)
{
    private string _cachedThemeId = "default";

    // Event: fires when theme changes so components can re-render
    public event Action? OnThemeChanged;

    public ThemeStyleSet Styles { get; private set; } = ThemeStyleSet.Default;

    public async Task<string> GetThemeAsync()
    {
        _cachedThemeId = await js.InvokeAsync<string?>("toledoStorage.getTheme") ?? "default";
        Styles = ResolveStyles(_cachedThemeId);
        return _cachedThemeId;
    }

    public async Task SetThemeAsync(string themeName)
    {
        await js.InvokeVoidAsync("toledoStorage.setTheme", themeName);
        _cachedThemeId = themeName;
        Styles = ResolveStyles(themeName);
        OnThemeChanged?.Invoke();
    }

    private static ThemeStyleSet ResolveStyles(string themeId)
    {
        return themeId switch
        {
            "whatsapp" or "whatsapp-dark" => ThemeStyleSet.WhatsApp,
            "telegram" or "telegram-dark" => ThemeStyleSet.Telegram,
            "signal" or "signal-dark" => ThemeStyleSet.Signal,
            _ => ThemeStyleSet.Default
        };
    }

    public async Task<int> GetFontSizeAsync()
    {
        var stored = await js.InvokeAsync<string?>("toledoStorage.getFontSize") ?? "15";
        return int.TryParse(stored, out var px) ? px : 15;
    }

    public async Task SetFontSizeAsync(int fontSizePx)
    {
        await js.InvokeVoidAsync("toledoStorage.setFontSize", fontSizePx.ToString());
    }

    public async Task<string> GetWallpaperAsync()
    {
        return await js.InvokeAsync<string?>("toledoStorage.getWallpaper") ?? "default";
    }

    public async Task SetWallpaperAsync(string wallpaperId)
    {
        await js.InvokeVoidAsync("toledoStorage.setWallpaper", wallpaperId);
        OnThemeChanged?.Invoke();
    }

    public static bool IsDarkTheme(string themeId)
    {
        return themeId.EndsWith("-dark", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<WallpaperInfo> GetAvailableWallpapers()
    {
        return
        [
            new WallpaperInfo("default", "Default", null, null),
            new WallpaperInfo("none", "None", null, null),
            new WallpaperInfo("doodle-chat", "Chat Doodle",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='80' height='80' viewBox='0 0 80 80'%3E%3Cg fill='%23000' fill-opacity='0.05'%3E%3Cpath d='M10 10h8v2h-8zM30 6l4 4-4 4-4-4zM58 12a4 4 0 110-8 4 4 0 010 8zM10 38l6-3v6zM50 36h8v2h2v4h-2v2h-8v-2h-2v-4h2zM30 52a6 6 0 110-12 6 6 0 010 12zM58 58l4 4h-8zM12 62h4v4h-4zM62 36l-3 6h6z'/%3E%3C/g%3E%3C/svg%3E\")",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='80' height='80' viewBox='0 0 80 80'%3E%3Cg fill='%23fff' fill-opacity='0.04'%3E%3Cpath d='M10 10h8v2h-8zM30 6l4 4-4 4-4-4zM58 12a4 4 0 110-8 4 4 0 010 8zM10 38l6-3v6zM50 36h8v2h2v4h-2v2h-8v-2h-2v-4h2zM30 52a6 6 0 110-12 6 6 0 010 12zM58 58l4 4h-8zM12 62h4v4h-4zM62 36l-3 6h6z'/%3E%3C/g%3E%3C/svg%3E\")"),
            new WallpaperInfo("geometric", "Geometric",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='60' height='60' viewBox='0 0 60 60'%3E%3Cg fill='%23000' fill-opacity='0.04'%3E%3Cpath d='M30 0l30 30-30 30L0 30zM30 10l20 20-20 20L10 30z'/%3E%3C/g%3E%3C/svg%3E\")",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='60' height='60' viewBox='0 0 60 60'%3E%3Cg fill='%23fff' fill-opacity='0.03'%3E%3Cpath d='M30 0l30 30-30 30L0 30zM30 10l20 20-20 20L10 30z'/%3E%3C/g%3E%3C/svg%3E\")"),
            new WallpaperInfo("dots", "Polka Dots",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='40' height='40' viewBox='0 0 40 40'%3E%3Cg fill='%23000' fill-opacity='0.05'%3E%3Ccircle cx='10' cy='10' r='3'/%3E%3Ccircle cx='30' cy='30' r='3'/%3E%3C/g%3E%3C/svg%3E\")",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='40' height='40' viewBox='0 0 40 40'%3E%3Cg fill='%23fff' fill-opacity='0.04'%3E%3Ccircle cx='10' cy='10' r='3'/%3E%3Ccircle cx='30' cy='30' r='3'/%3E%3C/g%3E%3C/svg%3E\")"),
            new WallpaperInfo("waves", "Waves",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='100' height='20' viewBox='0 0 100 20'%3E%3Cpath d='M0 10c25 0 25-8 50-8s25 8 50 8' fill='none' stroke='%23000' stroke-opacity='0.04' stroke-width='1.5'/%3E%3C/svg%3E\")",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='100' height='20' viewBox='0 0 100 20'%3E%3Cpath d='M0 10c25 0 25-8 50-8s25 8 50 8' fill='none' stroke='%23fff' stroke-opacity='0.03' stroke-width='1.5'/%3E%3C/svg%3E\")"),
            new WallpaperInfo("crosses", "Crosses",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='50' height='50' viewBox='0 0 50 50'%3E%3Cg fill='%23000' fill-opacity='0.04'%3E%3Cpath d='M23 0h4v50h-4zM0 23h50v4H0z'/%3E%3C/g%3E%3C/svg%3E\")",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='50' height='50' viewBox='0 0 50 50'%3E%3Cg fill='%23fff' fill-opacity='0.03'%3E%3Cpath d='M23 0h4v50h-4zM0 23h50v4H0z'/%3E%3C/g%3E%3C/svg%3E\")"),
            new WallpaperInfo("hexagons", "Hexagons",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='56' height='100' viewBox='0 0 56 100'%3E%3Cpath d='M28 66L0 50V16l28-16 28 16v34L28 66zM28 0l28 16v34L28 66 0 50V16L28 0z' fill='none' stroke='%23000' stroke-opacity='0.04' stroke-width='1'/%3E%3C/svg%3E\")",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='56' height='100' viewBox='0 0 56 100'%3E%3Cpath d='M28 66L0 50V16l28-16 28 16v34L28 66zM28 0l28 16v34L28 66 0 50V16L28 0z' fill='none' stroke='%23fff' stroke-opacity='0.03' stroke-width='1'/%3E%3C/svg%3E\")"),
            new WallpaperInfo("zigzag", "Zigzag",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='40' height='12' viewBox='0 0 40 12'%3E%3Cpath d='M0 6l10-6 10 6 10-6 10 6' fill='none' stroke='%23000' stroke-opacity='0.04' stroke-width='1.5'/%3E%3C/svg%3E\")",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='40' height='12' viewBox='0 0 40 12'%3E%3Cpath d='M0 6l10-6 10 6 10-6 10 6' fill='none' stroke='%23fff' stroke-opacity='0.03' stroke-width='1.5'/%3E%3C/svg%3E\")"),
            new WallpaperInfo("bubbles", "Bubbles",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='80' height='80' viewBox='0 0 80 80'%3E%3Cg fill='none' stroke='%23000' stroke-opacity='0.04' stroke-width='1'%3E%3Ccircle cx='20' cy='20' r='12'/%3E%3Ccircle cx='60' cy='55' r='8'/%3E%3Ccircle cx='45' cy='15' r='5'/%3E%3Ccircle cx='12' cy='60' r='6'/%3E%3Ccircle cx='65' cy='25' r='4'/%3E%3C/g%3E%3C/svg%3E\")",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='80' height='80' viewBox='0 0 80 80'%3E%3Cg fill='none' stroke='%23fff' stroke-opacity='0.03' stroke-width='1'%3E%3Ccircle cx='20' cy='20' r='12'/%3E%3Ccircle cx='60' cy='55' r='8'/%3E%3Ccircle cx='45' cy='15' r='5'/%3E%3Ccircle cx='12' cy='60' r='6'/%3E%3Ccircle cx='65' cy='25' r='4'/%3E%3C/g%3E%3C/svg%3E\")"),
            new WallpaperInfo("stars", "Starfield",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='70' height='70' viewBox='0 0 70 70'%3E%3Cg fill='%23000' fill-opacity='0.04'%3E%3Cpath d='M15 5l1.5 3 3.5.5-2.5 2.5.5 3.5L15 13l-3 1.5.5-3.5L10 8.5l3.5-.5zM50 40l1 2 2.5.3-1.8 1.8.3 2.5L50 45.3l-2 1.3.3-2.5-1.8-1.8 2.5-.3zM35 60l.8 1.5 1.7.2-1.2 1.2.2 1.7-1.5-.8-1.5.8.2-1.7-1.2-1.2 1.7-.2z'/%3E%3C/g%3E%3C/svg%3E\")",
                "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='70' height='70' viewBox='0 0 70 70'%3E%3Cg fill='%23fff' fill-opacity='0.04'%3E%3Cpath d='M15 5l1.5 3 3.5.5-2.5 2.5.5 3.5L15 13l-3 1.5.5-3.5L10 8.5l3.5-.5zM50 40l1 2 2.5.3-1.8 1.8.3 2.5L50 45.3l-2 1.3.3-2.5-1.8-1.8 2.5-.3zM35 60l.8 1.5 1.7.2-1.2 1.2.2 1.7-1.5-.8-1.5.8.2-1.7-1.2-1.2 1.7-.2z'/%3E%3C/g%3E%3C/svg%3E\")")
        ];
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

public sealed record WallpaperInfo(string Id, string DisplayName, string? LightPattern, string? DarkPattern);
