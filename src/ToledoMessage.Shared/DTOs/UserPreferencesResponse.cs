namespace ToledoMessage.Shared.DTOs;

public sealed record UserPreferencesResponse(
    string Theme,
    string FontSize,
    string Language,
    bool NotificationsEnabled,
    bool ReadReceiptsEnabled,
    bool TypingIndicatorsEnabled,
    bool SharedKeysEnabled,
    bool SendPhotoHd = false);
