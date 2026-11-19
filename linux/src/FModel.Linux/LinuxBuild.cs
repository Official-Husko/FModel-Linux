using System.Linq;
using System.Reflection;

namespace FModel.Linux;

/// <summary>
/// Facts about this Linux build, stamped by linux/FModel.Linux.csproj at build time.
/// </summary>
public static class LinuxBuild
{
    /// <summary>GitHub repository that publishes the Linux builds (owner/name).</summary>
    public static string Repository => Metadata("LinuxRepository") ?? "Official-Husko/FModel-Linux";

    /// <summary>Commit of the official 4sval/FModel repository this build was made from.</summary>
    public static string UpstreamCommit => Metadata("UpstreamCommit") ?? string.Empty;

    public static string Version => Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0.0";

    public static string ReleasesPage => $"https://github.com/{Repository}/releases";
    public static string ReleasesApi => $"https://api.github.com/repos/{Repository}/releases?per_page=20";

    private static string Metadata(string key)
    {
        var value = typeof(LinuxBuild).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
