namespace ToledoVault.Shared.DTOs;

public sealed record AdminChangePasswordRequest(string CurrentPassword, string NewPassword);
