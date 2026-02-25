namespace ToledoMessage.Shared.DTOs;

public sealed record UserSearchResponse(List<UserSearchResult> Users);

public sealed record UserSearchResult(decimal UserId, string DisplayName, int DeviceCount);
