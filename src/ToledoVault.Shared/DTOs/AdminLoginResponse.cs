namespace ToledoVault.Shared.DTOs;

public sealed record AdminLoginResponse(string Token, bool MustChangePassword);
