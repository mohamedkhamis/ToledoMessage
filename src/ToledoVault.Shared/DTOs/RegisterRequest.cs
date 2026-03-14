namespace ToledoVault.Shared.DTOs;

public sealed record RegisterRequest(string Username, string DisplayName, string Password, string? DisplayNameSecondary = null, bool RememberMe = true);

/// <summary>
/// Combined registration request that creates user + device in a single atomic transaction.
/// </summary>
public sealed record RegisterWithDeviceRequest(
    string Username,
    string DisplayName,
    string Password,
    DeviceRegistrationRequest Device,
    string? DisplayNameSecondary = null,
    bool RememberMe = true);
