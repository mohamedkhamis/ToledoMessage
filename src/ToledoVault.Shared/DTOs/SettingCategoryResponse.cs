namespace ToledoVault.Shared.DTOs;

public sealed record SettingCategoryResponse(string Category, List<GlobalSettingResponse> Settings);
