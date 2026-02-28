using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Client.Services;

/// <summary>
/// HTTP delegating handler that attaches JWT Bearer tokens to outgoing requests
/// and automatically refreshes expired tokens via the /api/auth/refresh endpoint.
/// </summary>
public class AuthTokenHandler : DelegatingHandler
{
    private readonly LocalStorageService _storage;
    private static bool _isRefreshing;

    public AuthTokenHandler(LocalStorageService storage)
    {
        _storage = storage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Skip auth header for auth endpoints themselves to avoid loops
        var path = request.RequestUri?.PathAndQuery ?? "";
        var isAuthEndpoint = path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase);

        if (!isAuthEndpoint)
        {
            await AttachTokenAsync(request);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // If we get a 401 and this isn't an auth endpoint, try refreshing
        if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthEndpoint && !_isRefreshing)
        {
            var refreshed = await TryRefreshTokenAsync(cancellationToken);
            if (refreshed)
            {
                // Clone the request (original is disposed) and retry
                var retryRequest = await CloneRequestAsync(request);
                await AttachTokenAsync(retryRequest);
                response.Dispose();
                response = await base.SendAsync(retryRequest, cancellationToken);
            }
        }

        return response;
    }

    private async Task AttachTokenAsync(HttpRequestMessage request)
    {
        var tokenBytes = await _storage.GetAsync("auth.token");
        if (tokenBytes is not null)
        {
            var token = Encoding.UTF8.GetString(tokenBytes);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task<bool> TryRefreshTokenAsync(CancellationToken cancellationToken)
    {
        _isRefreshing = true;
        try
        {
            var accessTokenBytes = await _storage.GetAsync("auth.token");
            var refreshTokenBytes = await _storage.GetAsync("auth.refreshToken");

            if (accessTokenBytes is null || refreshTokenBytes is null)
                return false;

            var accessToken = Encoding.UTF8.GetString(accessTokenBytes);
            var refreshToken = Encoding.UTF8.GetString(refreshTokenBytes);

            var refreshRequest = new RefreshTokenRequest(accessToken, refreshToken);

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
            {
                Content = JsonContent.Create(refreshRequest)
            };

            var response = await base.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>(cancellationToken);
            if (result is null)
                return false;

            // Store the new tokens
            await _storage.StoreAsync("auth.token", Encoding.UTF8.GetBytes(result.Token));
            await _storage.StoreAsync("auth.refreshToken", Encoding.UTF8.GetBytes(result.RefreshToken));

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        if (original.Content is not null)
        {
            var contentBytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
