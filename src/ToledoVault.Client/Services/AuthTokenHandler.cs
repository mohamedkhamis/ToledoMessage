using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Client.Services;

/// <summary>
/// HTTP delegating handler that attaches JWT Bearer tokens to outgoing requests
/// and automatically refreshes expired tokens via the /api/auth/refresh endpoint.
/// </summary>
public class AuthTokenHandler(LocalStorageService storage) : DelegatingHandler
{
    private static readonly SemaphoreSlim RefreshSemaphore = new(1, 1);
    private static bool _lastRefreshSucceeded;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Skip auth header for auth endpoints themselves to avoid loops
        var path = request.RequestUri?.PathAndQuery ?? "";
        var isAuthEndpoint = path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase);

        if (!isAuthEndpoint) await AttachTokenAsync(request);

        var response = await base.SendAsync(request, cancellationToken);

        // If we get a 401 and this isn't an auth endpoint, try refreshing
        if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthEndpoint)
        {
            if (await RefreshSemaphore.WaitAsync(0, cancellationToken))
            {
                try
                {
                    _lastRefreshSucceeded = await TryRefreshTokenAsync(cancellationToken);
                    if (_lastRefreshSucceeded)
                    {
                        var retryRequest = await CloneRequestAsync(request);
                        await AttachTokenAsync(retryRequest);
                        response.Dispose();
                        response = await base.SendAsync(retryRequest, cancellationToken);
                    }
                }
                finally
                {
                    RefreshSemaphore.Release();
                }
            }
            else
            {
                // Another refresh is in progress — wait for it to finish
                await RefreshSemaphore.WaitAsync(cancellationToken);
                RefreshSemaphore.Release();

                // Only retry if the other thread's refresh succeeded
                if (_lastRefreshSucceeded)
                {
                    var retryRequest = await CloneRequestAsync(request);
                    await AttachTokenAsync(retryRequest);
                    response.Dispose();
                    response = await base.SendAsync(retryRequest, cancellationToken);
                }
            }
        }

        return response;
    }

    private async Task AttachTokenAsync(HttpRequestMessage request)
    {
        var tokenBytes = await storage.GetAsync("auth.token");
        if (tokenBytes is not null)
        {
            var token = Encoding.UTF8.GetString(tokenBytes);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task<bool> TryRefreshTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var accessTokenBytes = await storage.GetAsync("auth.token");
            var refreshTokenBytes = await storage.GetAsync("auth.refreshToken");

            if (accessTokenBytes is null || refreshTokenBytes is null)
                return false;

            var accessToken = Encoding.UTF8.GetString(accessTokenBytes);
            var refreshToken = Encoding.UTF8.GetString(refreshTokenBytes);

            // Include device ID for device-bound token validation
            long? deviceId = null;
            var deviceIdBytes = await storage.GetAsync("local.deviceId");
            if (deviceIdBytes is not null && long.TryParse(Encoding.UTF8.GetString(deviceIdBytes), out var parsed))
                deviceId = parsed;

            var refreshRequest = new RefreshTokenRequest(accessToken, refreshToken, deviceId);

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
            await storage.StoreAsync("auth.token", Encoding.UTF8.GetBytes(result.Token));
            await storage.StoreAsync("auth.refreshToken", Encoding.UTF8.GetBytes(result.RefreshToken));

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        if (original.Content is not null)
        {
            var contentBytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in original.Content.Headers) clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in original.Headers) clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}
