using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using ToledoVault.Shared.DTOs;

// ReSharper disable RemoveRedundantBraces
// ReSharper disable InvertIf

namespace ToledoVault.Services;

public partial class LinkPreviewService(IHttpClientFactory httpClientFactory, IMemoryCache cache)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private const int MaxResponseBytes = 1_048_576; // 1 MB

    public async Task<LinkPreviewResponse?> GetPreviewAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // FR-009: Validate URL length (max 2048 characters)
        if (url.Length > 2048)
            return null;

        if (cache.TryGetValue($"linkpreview:{url}", out LinkPreviewResponse? cached))
            return cached;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        // Only allow HTTP/HTTPS schemes
        if (uri.Scheme is not ("http" or "https"))
            return null;

        // Block private/local hostnames
        if (uri.Host is "localhost" || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            return null;

        // S-02 Fix: Resolve DNS once and pin the IP to prevent DNS rebinding
        IPAddress resolvedIp;
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.Host);
            if (addresses.Length == 0)
                return null;
            resolvedIp = addresses[0];
        }
        catch
        {
            return null;
        }

        // Validate the resolved IP (not the hostname) against private ranges
        if (IsPrivateIp(resolvedIp))
            return null;

        try
        {
            var client = httpClientFactory.CreateClient("LinkPreview");

            // Connect to the pinned IP directly to prevent DNS rebinding
            var pinnedUri = new UriBuilder(uri) { Host = resolvedIp.ToString() }.Uri;
            using var request = new HttpRequestMessage(HttpMethod.Get, pinnedUri);
            request.Headers.Host = uri.Host; // Preserve original Host header
            request.Headers.Add("User-Agent", "ToledoVault-LinkPreview/1.0");

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

            var result = await ParseOpenGraphAsync(html, uri);

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

    private static async Task<LinkPreviewResponse?> ParseOpenGraphAsync(string html, Uri baseUri)
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

        // S-03 Fix: Re-validate the resolved og:image URL against private IPs (including DNS rebinding)
        if (!string.IsNullOrEmpty(imageUrl) && Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri))
        {
            if (imageUri.Host is "localhost" || imageUri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            {
                imageUrl = null;
            }
            else if (IPAddress.TryParse(imageUri.Host, out var imageIp))
            {
                if (IsPrivateIp(imageIp))
                    imageUrl = null;
            }
            else
            {
                // Hostname — resolve DNS and check the IP
                try
                {
                    var imageAddresses = await Dns.GetHostAddressesAsync(imageUri.Host);
                    if (imageAddresses.Length == 0 || IsPrivateIp(imageAddresses[0]))
                        imageUrl = null;
                }
                catch
                {
                    imageUrl = null;
                }
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

    private static bool IsPrivateIp(IPAddress ip)
    {
        // Map IPv6-mapped IPv4 (::ffff:x.x.x.x) to IPv4 for consistent checks
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

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
