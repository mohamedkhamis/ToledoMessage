using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using ToledoMessage.Shared.DTOs;

// ReSharper disable RemoveRedundantBraces
// ReSharper disable InvertIf

namespace ToledoMessage.Services;

public partial class LinkPreviewService(IHttpClientFactory httpClientFactory, IMemoryCache cache)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private const int MaxResponseBytes = 1_048_576; // 1 MB

    public async Task<LinkPreviewResponse?> GetPreviewAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (cache.TryGetValue($"linkpreview:{url}", out LinkPreviewResponse? cached))
            return cached;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        // Block private/local IPs (SSRF protection)
        if (IsPrivateHost(uri))
            return null;

        try
        {
            var client = httpClientFactory.CreateClient("LinkPreview");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("User-Agent", "ToledoMessage-LinkPreview/1.0");

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                return null;

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                return null;

            // Read limited body
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            var buffer = new char[MaxResponseBytes];
            var read = await reader.ReadBlockAsync(buffer, 0, MaxResponseBytes);
            var html = new string(buffer, 0, read);

            var result = ParseOpenGraph(html, uri);

            if (result is not null)
            {
                cache.Set($"linkpreview:{url}", result, CacheDuration);
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private static LinkPreviewResponse? ParseOpenGraph(string html, Uri baseUri)
    {
        var title = ExtractMeta(html, "og:title") ?? ExtractTitle(html);
        var description = ExtractMeta(html, "og:description") ?? ExtractMeta(html, "description");
        var imageUrl = ExtractMeta(html, "og:image");

        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description))
            return null;

        // Resolve relative image URL
        if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(baseUri, imageUrl, out var absoluteImage))
            {
                imageUrl = absoluteImage.ToString();
            }
        }

        return new LinkPreviewResponse(
            title?.Trim(),
            description?.Trim(),
            imageUrl,
            baseUri.Host);
    }

    private static string? ExtractMeta(string html, string property)
    {
        // Try property="og:*"
        var match = Regex.Match(html,
            $"""<meta[^>]*(?:property|name)=["']{Regex.Escape(property)}["'][^>]*content=["']([^"']*)["']""",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match.Success)
            return WebUtility.HtmlDecode(match.Groups[1].Value);

        // Try content before property
        match = Regex.Match(html,
            $"""<meta[^>]*content=["']([^"']*)["'][^>]*(?:property|name)=["']{Regex.Escape(property)}["']""",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    private static string? ExtractTitle(string html)
    {
        var match = TitleRegex().Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    private static bool IsPrivateHost(Uri uri)
    {
        var host = uri.Host;
        if (host is "localhost" || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!IPAddress.TryParse(host, out var ip))
            return false;

        // Map IPv6-mapped IPv4 (::ffff:x.x.x.x) to IPv4 for consistent checks
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        // Block IPv6 loopback (::1) and link-local (fe80::/10) and unique-local (fc00::/7)
        if (IPAddress.IsLoopback(ip))
            return true;

        var bytes = ip.GetAddressBytes();
        if (bytes.Length == 16)
        {
            // fc00::/7 (unique local) or fe80::/10 (link local)
            return (bytes[0] & 0xFE) == 0xFC || (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80);
        }

        // IPv4: 10.x, 172.16-31.x, 192.168.x, 127.x, 169.254.x (link-local), 0.x.x.x
        return bytes[0] == 10
               || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
               || (bytes[0] == 192 && bytes[1] == 168)
               || bytes[0] == 127
               || (bytes[0] == 169 && bytes[1] == 254)
               || bytes[0] == 0;
    }

    // ReSharper disable once RedundantVerbatimStringPrefix
    [GeneratedRegex(@"<title[^>]*>([^<]*)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();
}
