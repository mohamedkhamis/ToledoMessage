namespace ToledoMessage.Shared.DTOs;

public sealed record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken);
