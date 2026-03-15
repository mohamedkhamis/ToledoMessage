namespace ToledoVault.Shared.DTOs;

public sealed record PaginatedResponse<T>(List<T> Items, int TotalCount, int Page, int PageSize, int TotalPages);
