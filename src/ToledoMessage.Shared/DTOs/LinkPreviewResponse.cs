namespace ToledoMessage.Shared.DTOs;

public sealed record LinkPreviewResponse(
    string? Title,
    string? Description,
    string? ImageUrl,
    string? Domain);
