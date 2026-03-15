namespace ToledoVault.Shared.DTOs;

public sealed record LocalizationValueInfo(string Value, string Source);
public sealed record LocalizationEntryResponse(string ResourceKey, Dictionary<string, LocalizationValueInfo> Values, bool IsNewKey, DateTimeOffset? LastModifiedAt);
public sealed record LocalizationListResponse(List<LocalizationEntryResponse> Entries, int TotalKeys, List<string> Languages);
