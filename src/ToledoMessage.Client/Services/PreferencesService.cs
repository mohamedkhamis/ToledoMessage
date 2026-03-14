using System.Net.Http.Json;
using ToledoMessage.Shared.DTOs;

namespace ToledoMessage.Client.Services;

public sealed class PreferencesService(HttpClient http)
{
    private UserPreferencesResponse? _cached;

    public async Task<UserPreferencesResponse> GetPreferencesAsync()
    {
        if (_cached is not null) return _cached;

        try
        {
            _cached = await http.GetFromJsonAsync<UserPreferencesResponse>("/api/preferences");
        }
        catch
        {
            // Fallback to defaults if API unreachable
        }

        return _cached ?? new UserPreferencesResponse("default", "15", "en", true, true, true, true);
    }

    public async Task<UserPreferencesResponse?> UpdatePreferencesAsync(UpdatePreferencesRequest request)
    {
        var response = await http.PutAsJsonAsync("/api/preferences", request);
        response.EnsureSuccessStatusCode();
        _cached = await response.Content.ReadFromJsonAsync<UserPreferencesResponse>();
        var userPreferencesResponse = _cached;
        return userPreferencesResponse;
    }
}
