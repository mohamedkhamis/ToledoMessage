namespace ToledoMessage.Models;

public class UserPreferences
{
    // ReSharper disable  NullableWarningSuppressionIsUsed
    public decimal Id { get; set; }
    public decimal UserId { get; set; }
    public string Theme { get; set; } = "default";
    public string FontSize { get; set; } = "medium";
    public string Language { get; set; } = "en";
    public bool NotificationsEnabled { get; set; } = true;
    public bool ReadReceiptsEnabled { get; set; } = true;
    public bool TypingIndicatorsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
