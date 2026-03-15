using System.Net.Http.Json;
using System.Net.Http.Headers;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Admin.Services;

public class AdminApiService(HttpClient http, AdminAuthService auth)
{
    private void SetAuthHeader()
    {
        if (auth.Token is not null)
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
    }

    // --- Settings (US1) ---

    public async Task<List<SettingCategoryResponse>> GetSettingsAsync()
    {
        SetAuthHeader();
        var result = await http.GetFromJsonAsync<List<SettingCategoryResponse>>("/api/admin/settings");
        return result ?? [];
    }

    public async Task<HttpResponseMessage> UpdateSettingAsync(string key, string value)
    {
        SetAuthHeader();
        return await http.PutAsJsonAsync($"/api/admin/settings/{key}", new UpdateSettingRequest(value));
    }

    public async Task<HttpResponseMessage> ResetSettingAsync(string key)
    {
        SetAuthHeader();
        return await http.PostAsync($"/api/admin/settings/reset/{key}", null);
    }

    // --- Logs (US2) ---

    public async Task<PaginatedResponse<LogEntryResponse>> GetLogsAsync(LogQueryRequest query)
    {
        SetAuthHeader();
        var queryString = BuildLogQueryString(query);
        var result = await http.GetFromJsonAsync<PaginatedResponse<LogEntryResponse>>($"/api/admin/logs{queryString}");
        return result ?? new PaginatedResponse<LogEntryResponse>([], 0, 1, 50, 0);
    }

    public async Task<LogDeleteResponse> DeleteLogsAsync(DateTimeOffset olderThan)
    {
        SetAuthHeader();
        var response = await http.DeleteAsync($"/api/admin/logs?olderThan={olderThan:O}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LogDeleteResponse>() ?? new LogDeleteResponse(0);
    }

    // --- Localization (US3) ---

    public async Task<LocalizationListResponse> GetLocalizationAsync(string? language, string? search, bool missingOnly)
    {
        SetAuthHeader();
        var parts = new List<string>();
        if (language is not null) parts.Add($"language={Uri.EscapeDataString(language)}");
        if (search is not null) parts.Add($"search={Uri.EscapeDataString(search)}");
        if (missingOnly) parts.Add("missingOnly=true");
        var qs = parts.Count > 0 ? "?" + string.Join("&", parts) : "";
        var result = await http.GetFromJsonAsync<LocalizationListResponse>($"/api/admin/localization{qs}");
        return result ?? new LocalizationListResponse([], 0, []);
    }

    public async Task<HttpResponseMessage> UpdateLocalizationAsync(string resourceKey, string languageCode, string value)
    {
        SetAuthHeader();
        return await http.PutAsJsonAsync($"/api/admin/localization/{Uri.EscapeDataString(resourceKey)}",
            new UpdateLocalizationRequest(languageCode, value));
    }

    public async Task<HttpResponseMessage> CreateLocalizationKeyAsync(string resourceKey, Dictionary<string, string> values)
    {
        SetAuthHeader();
        return await http.PostAsJsonAsync("/api/admin/localization",
            new CreateLocalizationKeyRequest(resourceKey, values));
    }

    public async Task<HttpResponseMessage> DeleteLocalizationOverrideAsync(string resourceKey, string languageCode)
    {
        SetAuthHeader();
        return await http.DeleteAsync($"/api/admin/localization/{Uri.EscapeDataString(resourceKey)}/{Uri.EscapeDataString(languageCode)}");
    }

    private static string BuildLogQueryString(LogQueryRequest query)
    {
        var parts = new List<string>();
        if (query.Level is not null) parts.Add($"level={Uri.EscapeDataString(query.Level)}");
        if (query.From is not null) parts.Add($"from={query.From.Value:O}");
        if (query.To is not null) parts.Add($"to={query.To.Value:O}");
        if (query.Search is not null) parts.Add($"search={Uri.EscapeDataString(query.Search)}");
        parts.Add($"page={query.Page}");
        parts.Add($"pageSize={query.PageSize}");
        return parts.Count > 0 ? "?" + string.Join("&", parts) : "";
    }
}
