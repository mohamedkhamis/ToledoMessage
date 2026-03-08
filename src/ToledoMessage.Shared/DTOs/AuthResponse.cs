namespace ToledoMessage.Shared.DTOs;

public sealed record AuthResponse(long UserId, string Username, string DisplayName, string Token, string? RefreshToken = null);
