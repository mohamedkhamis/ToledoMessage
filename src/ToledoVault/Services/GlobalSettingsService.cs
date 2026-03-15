using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ToledoVault.Data;
using ToledoVault.Models;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Services;

public class GlobalSettingsService(
    ApplicationDbContext db,
    IMemoryCache cache,
    ILogger<GlobalSettingsService> logger)
{
    private const string AllSettingsCacheKey = "admin:settings:all";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<List<SettingCategoryResponse>> GetAllGroupedAsync()
    {
        if (cache.TryGetValue(AllSettingsCacheKey, out List<SettingCategoryResponse>? cached) && cached is not null)
            return cached;

        var settings = await db.GlobalSettings
            .OrderBy(s => s.Category)
            .ThenBy(s => s.SortOrder)
            .ToListAsync();

        var grouped = settings
            .GroupBy(s => s.Category)
            .Select(g => new SettingCategoryResponse(
                g.Key,
                g.Select(s => MapToResponse(s)).ToList()))
            .ToList();

        cache.Set(AllSettingsCacheKey, grouped, CacheDuration);
        return grouped;
    }

    public async Task<GlobalSetting?> GetByKeyAsync(string key)
    {
        return await db.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key);
    }

    public async Task<(bool success, string? error)> UpdateValueAsync(string key, string newValue)
    {
        var setting = await db.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting is null)
            return (false, "Setting not found.");

        var validationError = ValidateValue(setting, newValue);
        if (validationError is not null)
            return (false, validationError);

        var oldValue = setting.CurrentValue;
        setting.CurrentValue = newValue;
        setting.LastModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        InvalidateCache(key);
        logger.LogInformation("Setting {Key} updated from {OldValue} to {NewValue}", key, oldValue, newValue);

        return (true, null);
    }

    public async Task<bool> ResetToDefaultAsync(string key)
    {
        var setting = await db.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting is null)
            return false;

        setting.CurrentValue = setting.DefaultValue;
        setting.LastModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        InvalidateCache(key);
        logger.LogInformation("Setting {Key} reset to default value {DefaultValue}", key, setting.DefaultValue);

        return true;
    }

    public async Task<string?> GetValueAsync(string key)
    {
        var cacheKey = $"admin:setting:{key}";
        if (cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        var setting = await db.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting is null)
            return null;

        cache.Set(cacheKey, setting.CurrentValue, CacheDuration);
        return setting.CurrentValue;
    }

    private void InvalidateCache(string key)
    {
        cache.Remove(AllSettingsCacheKey);
        cache.Remove($"admin:setting:{key}");
    }

    private static string? ValidateValue(GlobalSetting setting, string newValue)
    {
        if (string.IsNullOrEmpty(setting.ValidationRules))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(setting.ValidationRules);
            var root = doc.RootElement;

            return setting.ValueType switch
            {
                "integer" => ValidateInteger(root, newValue),
                "selection" => ValidateSelection(root, newValue),
                "boolean" => ValidateBoolean(newValue),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null; // If validation rules are malformed, allow the value
        }
    }

    private static string? ValidateInteger(JsonElement rules, string value)
    {
        if (!int.TryParse(value, out var intValue))
            return "Value must be a valid integer.";

        if (rules.TryGetProperty("min", out var minEl) && intValue < minEl.GetInt32())
            return $"Value must be at least {minEl.GetInt32()}.";

        if (rules.TryGetProperty("max", out var maxEl) && intValue > maxEl.GetInt32())
            return $"Value must be at most {maxEl.GetInt32()}.";

        return null;
    }

    private static string? ValidateSelection(JsonElement rules, string value)
    {
        if (!rules.TryGetProperty("options", out var optionsEl))
            return null;

        var options = optionsEl.EnumerateArray().Select(o => o.GetString()).ToList();
        if (!options.Contains(value))
            return $"Value must be one of: {string.Join(", ", options)}.";

        return null;
    }

    private static string? ValidateBoolean(string value)
    {
        if (value is not ("true" or "false"))
            return "Value must be 'true' or 'false'.";
        return null;
    }

    private static GlobalSettingResponse MapToResponse(GlobalSetting s)
    {
        object? validationRules = null;
        if (!string.IsNullOrEmpty(s.ValidationRules))
        {
            try
            {
                validationRules = JsonSerializer.Deserialize<JsonElement>(s.ValidationRules);
            }
            catch (JsonException) { }
        }

        return new GlobalSettingResponse(
            s.Id.ToString(),
            s.Key,
            s.DisplayName,
            s.Description,
            s.Category,
            s.ValueType,
            s.CurrentValue,
            s.DefaultValue,
            validationRules,
            s.LastModifiedAt);
    }
}
