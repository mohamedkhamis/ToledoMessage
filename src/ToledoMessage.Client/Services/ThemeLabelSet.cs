namespace ToledoMessage.Client.Services;

/// <summary>
/// Theme-specific display style for message delivery indicators.
/// Labels are handled by IStringLocalizer — this only controls visual style per theme.
/// </summary>
public sealed record ThemeStyleSet
{
    public DeliveryDisplayStyle DeliveryStyle { get; init; } = DeliveryDisplayStyle.Default;

    public static ThemeStyleSet Default { get; } = new();

    public static ThemeStyleSet WhatsApp { get; } = new()
    {
        DeliveryStyle = DeliveryDisplayStyle.DoubleTick
    };

    public static ThemeStyleSet Telegram { get; } = new()
    {
        DeliveryStyle = DeliveryDisplayStyle.SingleTick
    };

    public static ThemeStyleSet Signal { get; } = new()
    {
        DeliveryStyle = DeliveryDisplayStyle.Minimal
    };
}

public enum DeliveryDisplayStyle
{
    Default,
    DoubleTick,
    SingleTick,
    Minimal
}
