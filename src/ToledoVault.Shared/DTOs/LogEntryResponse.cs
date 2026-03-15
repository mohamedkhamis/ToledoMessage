namespace ToledoVault.Shared.DTOs;

public sealed record LogEntryResponse(int Id, DateTimeOffset Timestamp, string Level, string? Message, string? Source, string? Exception);
