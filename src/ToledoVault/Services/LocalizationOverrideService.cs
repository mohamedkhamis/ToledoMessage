using System.Globalization;
using System.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Toledo.SharedKernel.Helpers;
using ToledoVault.Data;
using ToledoVault.Models;
using ToledoVault.Shared.DTOs;

namespace ToledoVault.Services;

public class LocalizationOverrideService(
    ApplicationDbContext db,
    IMemoryCache cache,
    ILogger<LocalizationOverrideService> logger)
{
    private static readonly string[] SupportedLanguages = ["en", "ar"];
    private const string CacheKey = "admin:localization:all";

    public async Task<LocalizationListResponse> GetAllMergedAsync(string? languageFilter, string? searchFilter, bool missingOnly)
    {
        // Load baseline from .resx via ResourceManager
        var baseline = LoadResxBaseline();

        // Load DB overrides
        var overrides = await db.LocalizationOverrides.ToListAsync();

        // Merge
        var merged = new Dictionary<string, LocalizationEntryResponse>();

        // Add baseline entries
        foreach (var (key, values) in baseline)
        {
            var valueInfos = new Dictionary<string, LocalizationValueInfo>();
            foreach (var (lang, val) in values)
                valueInfos[lang] = new LocalizationValueInfo(val, "resx");

            merged[key] = new LocalizationEntryResponse(key, valueInfos, false, null);
        }

        // Apply overrides
        foreach (var ov in overrides)
        {
            if (!merged.TryGetValue(ov.ResourceKey, out var entry))
            {
                // New key from DB
                entry = new LocalizationEntryResponse(ov.ResourceKey, new Dictionary<string, LocalizationValueInfo>(), true, ov.LastModifiedAt);
                merged[ov.ResourceKey] = entry;
            }

            entry.Values[ov.LanguageCode] = new LocalizationValueInfo(ov.Value, "override");

            // Update LastModifiedAt to latest override
            if (entry.LastModifiedAt is null || ov.LastModifiedAt > entry.LastModifiedAt)
                merged[ov.ResourceKey] = entry with { LastModifiedAt = ov.LastModifiedAt };
        }

        var entries = merged.Values.AsEnumerable();

        // Apply filters
        if (!string.IsNullOrEmpty(languageFilter))
        {
            entries = entries.Where(e => e.Values.ContainsKey(languageFilter));
        }

        if (!string.IsNullOrEmpty(searchFilter))
        {
            var search = searchFilter.ToLowerInvariant();
            entries = entries.Where(e =>
                e.ResourceKey.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Values.Any(v => v.Value.Value.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        if (missingOnly)
        {
            entries = entries.Where(e =>
                SupportedLanguages.Any(lang => !e.Values.ContainsKey(lang)));
        }

        var result = entries.OrderBy(e => e.ResourceKey).ToList();
        return new LocalizationListResponse(result, result.Count, SupportedLanguages.ToList());
    }

    public async Task<bool> UpdateOverrideAsync(string resourceKey, string languageCode, string value)
    {
        var existing = await db.LocalizationOverrides
            .FirstOrDefaultAsync(o => o.ResourceKey == resourceKey && o.LanguageCode == languageCode);

        if (existing is not null)
        {
            existing.Value = value;
            existing.LastModifiedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            db.LocalizationOverrides.Add(new LocalizationOverride
            {
                Id = IdGenerator.GetNewId(),
                ResourceKey = resourceKey,
                LanguageCode = languageCode,
                Value = value,
                IsNewKey = false,
                LastModifiedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();
        cache.Remove(CacheKey);
        logger.LogInformation("Localization override saved: {Key}/{Lang}", resourceKey, languageCode);
        return true;
    }

    public async Task<(bool success, string? error)> CreateNewKeyAsync(string resourceKey, Dictionary<string, string> values)
    {
        // Check if key exists in .resx baseline
        var baseline = LoadResxBaseline();
        if (baseline.ContainsKey(resourceKey))
            return (false, "Key already exists in .resx baseline.");

        // Check if key exists in DB
        var dbExists = await db.LocalizationOverrides.AnyAsync(o => o.ResourceKey == resourceKey);
        if (dbExists)
            return (false, "Key already exists in database.");

        foreach (var (lang, value) in values)
        {
            db.LocalizationOverrides.Add(new LocalizationOverride
            {
                Id = IdGenerator.GetNewId(),
                ResourceKey = resourceKey,
                LanguageCode = lang,
                Value = value,
                IsNewKey = true,
                LastModifiedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();
        cache.Remove(CacheKey);
        logger.LogInformation("New localization key created: {Key}", resourceKey);
        return (true, null);
    }

    public async Task<bool> DeleteOverrideAsync(string resourceKey, string languageCode)
    {
        var existing = await db.LocalizationOverrides
            .FirstOrDefaultAsync(o => o.ResourceKey == resourceKey && o.LanguageCode == languageCode);

        if (existing is null)
            return false;

        db.LocalizationOverrides.Remove(existing);
        await db.SaveChangesAsync();
        cache.Remove(CacheKey);
        logger.LogInformation("Localization override deleted: {Key}/{Lang}", resourceKey, languageCode);
        return true;
    }

    private static Dictionary<string, Dictionary<string, string>> LoadResxBaseline()
    {
        var result = new Dictionary<string, Dictionary<string, string>>();

        try
        {
            var assembly = typeof(ToledoVault.Shared.SharedResource).Assembly;
            var resourceManager = new ResourceManager(typeof(ToledoVault.Shared.SharedResource));

            foreach (var lang in SupportedLanguages)
            {
                var culture = new CultureInfo(lang);
                try
                {
                    var resourceSet = resourceManager.GetResourceSet(culture, true, lang == "en");
                    if (resourceSet is null) continue;

                    foreach (System.Collections.DictionaryEntry entry in resourceSet)
                    {
                        var key = entry.Key?.ToString();
                        var value = entry.Value?.ToString();
                        if (key is null || value is null) continue;

                        if (!result.ContainsKey(key))
                            result[key] = new Dictionary<string, string>();

                        result[key][lang] = value;
                    }
                }
                catch { /* Culture not available */ }
            }
        }
        catch { /* Resource not available */ }

        return result;
    }
}
