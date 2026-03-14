namespace ToledoVault.Shared.DTOs;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
