namespace ToledoMessage.Shared.DTOs;

public sealed record UpdatePreferencesRequest(
    string? Theme = null,
    string? FontSize = null,
    string? Language = null,
    bool? NotificationsEnabled = null,
    bool? ReadReceiptsEnabled = null,
    bool? TypingIndicatorsEnabled = null);
