using System;
using System.IO;
using FModel.Settings;
using Newtonsoft.Json.Linq;

namespace FModel.Linux;

/// <summary>Adjustments made before upstream's startup code runs.</summary>
public static class LinuxEnvironment
{
    /// <summary>
    /// $XDG_DATA_HOME/FModel (usually ~/.local/share/FModel).
    /// </summary>
    public static string DataHome
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            var root = string.IsNullOrEmpty(xdg)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
                : xdg;
            return Path.Combine(root, "FModel");
        }
    }

    /// <summary>
    /// Upstream creates its Output folder next to the executable, the "portable" layout Windows users know.
    /// That works for an extracted tarball in your home folder, but not for an AppImage (read-only mount)
    /// or a system install under /opt or /usr. In those cases, and only when no valid output folder is
    /// configured yet, point the settings at $XDG_DATA_HOME/FModel/Output before upstream reads them.
    /// </summary>
    public static void PrepareOutputDirectory()
    {
        try
        {
            var settingsPath = UserSettings.FilePath;
            var settings = File.Exists(settingsPath) ? TryParse(File.ReadAllText(settingsPath)) : null;
            var configured = settings?.Value<string>(nameof(UserSettings.OutputDirectory));
            if (!string.IsNullOrEmpty(configured) && Directory.Exists(configured))
                return;

            if (!IsRunningAsAppImage && IsWritable(AppContext.BaseDirectory))
                return;

            var output = Directory.CreateDirectory(Path.Combine(DataHome, "Output")).FullName;
            settings ??= new JObject();
            settings[nameof(UserSettings.OutputDirectory)] = output;
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, settings.ToString());
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[FModel] could not prepare the output directory: {e.Message}");
        }
    }

    public static bool IsRunningAsAppImage => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPIMAGE"));

    private static JObject TryParse(string json)
    {
        try
        {
            return JObject.Parse(json);
        }
        catch
        {
            return null; // upstream resets broken settings itself
        }
    }

    private static bool IsWritable(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, Path.GetRandomFileName());
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
