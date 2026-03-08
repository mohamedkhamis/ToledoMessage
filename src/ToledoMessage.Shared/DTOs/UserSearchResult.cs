namespace ToledoMessage.Shared.DTOs;

public sealed record UserSearchResponse(List<UserSearchResult> Users);

public sealed record UserSearchResult(long UserId, string Username, string DisplayName, int DeviceCount);
