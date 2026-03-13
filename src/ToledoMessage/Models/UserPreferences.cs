namespace ToledoMessage.Models;

public class UserPreferences
{
    // ReSharper disable  NullableWarningSuppressionIsUsed
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Theme { get; set; } = "default";
    public string FontSize { get; set; } = "15";
    public string Language { get; set; } = "en";
    public bool NotificationsEnabled { get; set; } = true;
    public bool ReadReceiptsEnabled { get; set; } = true;
    public bool TypingIndicatorsEnabled { get; set; } = true;
    public bool SharedKeysEnabled { get; set; } = true;
    public bool SendPhotoHd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
