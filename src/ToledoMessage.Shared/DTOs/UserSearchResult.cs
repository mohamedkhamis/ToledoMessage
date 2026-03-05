namespace ToledoMessage.Shared.DTOs;

public sealed record UserSearchResponse(List<UserSearchResult> Users);

public sealed record UserSearchResult(decimal UserId, string Username, string DisplayName, int DeviceCount);
