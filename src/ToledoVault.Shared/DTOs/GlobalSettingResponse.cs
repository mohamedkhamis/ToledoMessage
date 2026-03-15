namespace ToledoVault.Shared.DTOs;

public sealed record GlobalSettingResponse(
    string Id,
    string Key,
    string DisplayName,
    string? Description,
    string Category,
    string ValueType,
    string CurrentValue,
    string DefaultValue,
    object? ValidationRules,
    DateTimeOffset LastModifiedAt);
