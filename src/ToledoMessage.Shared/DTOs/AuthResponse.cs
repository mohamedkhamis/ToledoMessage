namespace ToledoMessage.Shared.DTOs;

public sealed record AuthResponse(decimal UserId, string Username, string DisplayName, string Token, string? RefreshToken = null);
