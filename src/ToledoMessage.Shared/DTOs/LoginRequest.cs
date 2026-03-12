namespace ToledoMessage.Shared.DTOs;

public sealed record LoginRequest(string Username, string Password, bool RememberMe = true);
