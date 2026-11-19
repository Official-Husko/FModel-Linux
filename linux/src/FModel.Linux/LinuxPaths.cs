using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FModel.Linux;

public static partial class LinuxPaths
{
    /// <summary>
    /// Normalizes a path typed or pasted by the user: trims quotes and spaces, expands "~" and $VARIABLES the
    /// way a shell would, and makes it absolute. Windows users paste "C:\..." paths that need none of this.
    /// </summary>
    public static string Expand(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        path = path.Trim().Trim('"', '\'').Trim();
        if (path == "~" || path.StartsWith("~/"))
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];

        path = ShellVariable().Replace(path, m => Environment.GetEnvironmentVariable(m.Groups[1].Value) ?? m.Value);

        try
        {
            path = Path.GetFullPath(path);
        }
        catch
        {
            // keep it as is, the caller validates it
        }

        return path.Length > 1 ? path.TrimEnd('/', '\\') : path;
    }

    [GeneratedRegex(@"\$\{?([A-Za-z_][A-Za-z0-9_]*)\}?")]
    private static partial Regex ShellVariable();
}
