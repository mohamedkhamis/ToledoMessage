namespace ToledoVault.Shared.DTOs;

public sealed record RefreshTokenResponse(
    string Token,
    string RefreshToken);
