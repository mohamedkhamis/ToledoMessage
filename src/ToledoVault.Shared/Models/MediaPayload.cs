using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ToledoVault.Shared.Models;

/// <summary>
/// Represents the payload structure for media messages (images, videos, documents, audio).
/// This payload is serialized to JSON, UTF-8 encoded, and then encrypted before transmission.
/// </summary>
public sealed record MediaPayload
{
    [JsonPropertyName("fn")]
    public string? FileName { get; init; }

    [JsonPropertyName("mt")]
    public string MimeType { get; init; } = "application/octet-stream";

    [JsonPropertyName("c")]
    public string? Caption { get; init; }

    [JsonPropertyName("t")]
    public string? Thumbnail { get; init; }

    [JsonPropertyName("d")]
    public string Data { get; init; } = "";

    [JsonPropertyName("w")]
    public int[]? Waveform { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// Serializes the MediaPayload to a UTF-8 byte array.
    /// </summary>
    public static byte[] Serialize(MediaPayload payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>
    /// Deserializes a UTF-8 byte array to a MediaPayload.
    /// </summary>
    public static MediaPayload Deserialize(byte[] bytes)
    {
        var json = Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<MediaPayload>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize MediaPayload");
    }

    /// <summary>
    /// Sanitizes a filename by removing dangerous characters and truncating to 255 characters.
    /// </summary>
    public static string? SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        // Remove path separators and null bytes
        var sanitized = fileName
            .Replace('/', '_')
            .Replace('\\', '_')
            .Replace('\0', '_')
            .Trim();

        // Remove control characters
        sanitized = new string(sanitized.Where(static c => !char.IsControl(c)).ToArray());
        // ReSharper disable  RemoveRedundantBraces

        // Truncate to 255 characters
        if (sanitized.Length > 255)
        {
            sanitized = sanitized[..255];
        }

        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    /// <summary>
    /// Gets the default MIME type when the provided type is null or unknown.
    /// </summary>
    public static string GetDefaultMimeType(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return "application/octet-stream";

        // Basic validation - return as-is if it looks like a valid MIME type
        if (mimeType.Contains('/') && !mimeType.Contains(' '))
            return mimeType;

        return "application/octet-stream";
    }

    /// <summary>
    /// Validates that the data size is within the allowed limit (16 MB, matching WhatsApp).
    /// </summary>
    public static bool IsValidSize(string? base64Data, int maxSizeBytes = Constants.ProtocolConstants.MaxMediaFileSizeBytes)
    {
        if (string.IsNullOrEmpty(base64Data))
            return false;

        try
        {
            // Calculate decoded size (base64 is ~75% efficient)
            var encodedSize = base64Data.Length;
            var decodedSize = (int)(encodedSize * 0.75);
            return decodedSize <= maxSizeBytes;
        }
        catch
        {
            return false;
        }
    }
}
