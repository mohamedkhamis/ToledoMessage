namespace ToledoVault.Shared.DTOs;

public sealed record CreateLocalizationKeyRequest(string ResourceKey, Dictionary<string, string> Values);
