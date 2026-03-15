namespace ToledoVault.Shared.DTOs;

public sealed record LogQueryRequest(
    string? Level = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50);
