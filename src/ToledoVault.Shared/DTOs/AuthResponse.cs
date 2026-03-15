namespace ToledoVault.Shared.DTOs;

public sealed record AuthResponse(long UserId, string Username, string DisplayName, string Token, string? RefreshToken = null, string? DisplayNameSecondary = null);

/// <summary>
/// Response from the combined register-with-device endpoint.
/// </summary>
public sealed record RegisterWithDeviceResponse(long UserId, string Username, string DisplayName, string Token, string? RefreshToken, long DeviceId, string? DisplayNameSecondary = null);
