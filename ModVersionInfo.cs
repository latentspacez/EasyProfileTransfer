using System;
using System.Reflection;

namespace EasyProfileTransfer;

/// <summary>
/// EasyProfileTransfer mod version from <c>Directory.Build.props</c> assembly metadata.
/// </summary>
internal static class ModVersionInfo
{
    public static string GetCompiledAgainstVersions()
    {
        try
        {
            Assembly asm = typeof(ModVersionInfo).Assembly;
            foreach (AssemblyMetadataAttribute metadata in asm.GetCustomAttributes<AssemblyMetadataAttribute>())
            {
                if (string.Equals(metadata.Key, "CompiledAgainstSts2Versions", StringComparison.Ordinal))
                {
                    return string.IsNullOrWhiteSpace(metadata.Value) ? "unknown" : metadata.Value;
                }
            }
        }
        catch
        {
            // Ignore and fall through.
        }

        return "unknown";
    }

    public static string GetInformationalVersion()
    {
        try
        {
            Assembly asm = typeof(ModVersionInfo).Assembly;
            AssemblyInformationalVersionAttribute? info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (!string.IsNullOrWhiteSpace(info?.InformationalVersion))
            {
                return info.InformationalVersion;
            }

            return asm.GetName().Version?.ToString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
