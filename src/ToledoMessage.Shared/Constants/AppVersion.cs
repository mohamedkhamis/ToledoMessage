using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ToledoMessage.Shared.Constants;

public static class AppVersion
{
    public static string Current { get; } = ResolveVersion();

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Fallback to AssemblyVersion if attribute is trimmed")]
    private static string ResolveVersion()
    {
        // Primary: InformationalVersion (set by Directory.Build.props, e.g. "1.0.93")
        var infoVersion = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrEmpty(infoVersion))
            return infoVersion.Split('+')[0];

        // Fallback: AssemblyVersion (always in the manifest, survives trimming)
        var asmVersion = typeof(AppVersion).Assembly.GetName().Version;
        return asmVersion is not null ? $"{asmVersion.Major}.{asmVersion.Minor}.{asmVersion.Build}" : "0.0.0";
    }
}
