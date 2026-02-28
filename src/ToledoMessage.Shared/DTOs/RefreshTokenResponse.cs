namespace ToledoMessage.Shared.DTOs;

public sealed record RefreshTokenResponse(
    string Token,
    string RefreshToken);
