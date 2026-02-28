namespace ToledoMessage.Shared.DTOs;

public sealed record AuthResponse(decimal UserId, string DisplayName, string Token, string? RefreshToken = null);
