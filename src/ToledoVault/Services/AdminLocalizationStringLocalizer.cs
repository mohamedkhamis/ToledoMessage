using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using ToledoVault.Data;
using ToledoVault.Shared;

namespace ToledoVault.Services;

/// <summary>
/// Decorator around <see cref="IStringLocalizer{SharedResource}"/> that checks
/// DB overrides (LocalizationOverride table) first, then falls back to the
/// standard .resx-based localizer.  Results are cached in <see cref="IMemoryCache"/>
/// for 5 minutes keyed by culture + resource key.
/// </summary>
public class AdminLocalizationStringLocalizer(
    IStringLocalizer<SharedResource> inner,
    ApplicationDbContext db,
    IMemoryCache cache) : IStringLocalizer<SharedResource>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public LocalizedString this[string name]
    {
        get
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var cacheKey = $"loc:{culture}:{name}";

            if (cache.TryGetValue(cacheKey, out string? cachedValue) && cachedValue is not null)
                return new LocalizedString(name, cachedValue);

            // Query DB for an override
            var dbOverride = db.LocalizationOverrides
                .AsNoTracking()
                .FirstOrDefault(o => o.ResourceKey == name && o.LanguageCode == culture);

            if (dbOverride is not null)
            {
                cache.Set(cacheKey, dbOverride.Value, CacheDuration);
                return new LocalizedString(name, dbOverride.Value);
            }

            // Fallback to .resx
            var fallback = inner[name];
            if (!fallback.ResourceNotFound)
                cache.Set(cacheKey, fallback.Value, CacheDuration);

            return fallback;
        }
    }

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var cacheKey = $"loc:{culture}:{name}";

            if (cache.TryGetValue(cacheKey, out string? cachedValue) && cachedValue is not null)
                return new LocalizedString(name, string.Format(cachedValue, arguments));

            // Query DB for an override
            var dbOverride = db.LocalizationOverrides
                .AsNoTracking()
                .FirstOrDefault(o => o.ResourceKey == name && o.LanguageCode == culture);

            if (dbOverride is not null)
            {
                cache.Set(cacheKey, dbOverride.Value, CacheDuration);
                return new LocalizedString(name, string.Format(dbOverride.Value, arguments));
            }

            // Fallback to .resx
            var fallback = inner[name];
            if (!fallback.ResourceNotFound)
                cache.Set(cacheKey, fallback.Value, CacheDuration);

            return inner[name, arguments];
        }
    }

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        // Start with all strings from the .resx fallback
        var results = inner.GetAllStrings(includeParentCultures)
            .ToDictionary(s => s.Name, s => s);

        // Layer DB overrides on top
        var overrides = db.LocalizationOverrides
            .AsNoTracking()
            .Where(o => o.LanguageCode == culture)
            .ToList();

        foreach (var ov in overrides)
        {
            results[ov.ResourceKey] = new LocalizedString(ov.ResourceKey, ov.Value);
        }

        return results.Values;
    }
}
