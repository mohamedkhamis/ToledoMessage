using System.Globalization;

namespace ToledoVault.Shared.Helpers;

public static class DisplayNameHelper
{
    /// <summary>
    /// Returns the appropriate display name based on the current UI culture.
    /// When the culture is not the default (en), returns the secondary name if available.
    /// </summary>
    public static string Resolve(string displayName, string? displayNameSecondary)
    {
        if (string.IsNullOrEmpty(displayNameSecondary))
            return displayName;

        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return culture != "en" ? displayNameSecondary : displayName;
    }
}
