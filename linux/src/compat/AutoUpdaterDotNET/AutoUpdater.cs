using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FModel.Linux;
using Newtonsoft.Json.Linq;
using Serilog;

namespace AutoUpdaterDotNET;

/// <summary>
/// Linux replacement for AutoUpdater.NET, keeping the API upstream FModel calls.
///
/// Upstream asks api.fmodel.app which commit the latest official build was made from and compares it to
/// its own. On Linux that answer is only useful once a Linux build of that commit exists, so the remote
/// data is rewritten to point at the latest release of this fork (see <see cref="LinuxBuild"/>) before
/// upstream's handlers see it. Users get notified exactly when there is something they can install.
/// </summary>
public static class AutoUpdater
{
    public delegate void ParseUpdateInfoHandler(ParseUpdateInfoEventArgs args);
    public delegate void CheckForUpdateEventHandler(UpdateInfoEventArgs args);

    public static event ParseUpdateInfoHandler ParseUpdateInfoEvent;
    public static event CheckForUpdateEventHandler CheckForUpdateEvent;

    private static readonly HttpClient _http = CreateClient();

    public static void Start(string appCast) => _ = Task.Run(() => CheckAsync(appCast));

    private static async Task CheckAsync(string appCast)
    {
        UpdateInfoEventArgs result;
        try
        {
            var remote = JObject.Parse(await _http.GetStringAsync(appCast));
            var release = await LinuxRelease.GetLatestAsync(_http);
            if (release != null)
            {
                var version = remote.Value<string>("version") ?? "0.0.0.0";
                remote["version"] = $"{version.Split('-', '+')[0]}-linux+{release.UpstreamCommit}";
                remote["downloadUrl"] = release.DownloadUrl;
            }
            else
            {
                // no Linux release to offer yet: pretend we are current so nothing gets offered
                remote["version"] = $"{LinuxBuild.Version}-linux+{LinuxBuild.UpstreamCommit}";
            }

            var args = new ParseUpdateInfoEventArgs(remote.ToString());
            ParseUpdateInfoEvent?.Invoke(args);
            result = args.UpdateInfo ?? new UpdateInfoEventArgs();
            result.InstalledVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            if (result.CurrentVersion != null && Version.TryParse(result.CurrentVersion, out var current))
                result.IsUpdateAvailable = current > result.InstalledVersion;
        }
        catch (Exception e)
        {
            Log.Warning(e, "Update check failed");
            result = new UpdateInfoEventArgs { Error = e };
        }

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => CheckForUpdateEvent?.Invoke(result));
    }

    /// <summary>
    /// Installs an update. When FModel runs as an AppImage and the download is an AppImage, the running
    /// file is replaced in place and true is returned (upstream then closes the app). Anything else (for
    /// example a Windows build picked from the commit history) opens the Linux releases page instead.
    /// </summary>
    public static bool DownloadUpdate(UpdateInfoEventArgs args)
    {
        var url = args?.DownloadURL;
        var appImage = Environment.GetEnvironmentVariable("APPIMAGE");
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(appImage) || !url.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase))
        {
            OpenInBrowser(LinuxBuild.ReleasesPage);
            return false;
        }

        var temp = appImage + ".download";
        Task.Run(async () =>
        {
            await using var source = await _http.GetStreamAsync(url);
            await using var target = File.Create(temp);
            await source.CopyToAsync(target);
        }).GetAwaiter().GetResult();

        File.SetUnixFileMode(temp, File.GetUnixFileMode(appImage) | UnixFileMode.UserExecute);
        File.Move(temp, appImage, overwrite: true);
        Log.Information("Updated {AppImage} from {Url}", appImage, url);
        return true;
    }

    private static void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not open {Url}", url);
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"FModel-Linux/{LinuxBuild.Version}");
        return client;
    }
}

internal sealed record LinuxRelease(string UpstreamCommit, string DownloadUrl)
{
    private static readonly Regex _upstreamLine = new(@"Upstream commit:\s*`?([0-9a-f]{40})", RegexOptions.IgnoreCase);

    /// <summary>
    /// Newest release tagged "linux-*". The repository may also carry other releases (for example upstream's
    /// own QA workflow if it is enabled on the fork), so "releases/latest" can't be trusted to be ours.
    /// </summary>
    public static async Task<LinuxRelease> GetLatestAsync(HttpClient http)
    {
        try
        {
            var releases = JArray.Parse(await http.GetStringAsync(LinuxBuild.ReleasesApi));
            foreach (var release in releases.OfType<JObject>())
            {
                if (release.Value<bool>("draft") || !(release.Value<string>("tag_name") ?? "").StartsWith("linux-", StringComparison.Ordinal))
                    continue;

                var match = _upstreamLine.Match(release.Value<string>("body") ?? string.Empty);
                var asset = (release["assets"] as JArray)?
                    .Select(a => a.Value<string>("browser_download_url"))
                    .FirstOrDefault(u => u != null && u.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase));
                if (match.Success && asset != null)
                    return new LinuxRelease(match.Groups[1].Value, asset);
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not read the Linux releases");
        }

        return null;
    }
}

public class ParseUpdateInfoEventArgs(string remoteData) : EventArgs
{
    public string RemoteData { get; } = remoteData;
    public UpdateInfoEventArgs UpdateInfo { get; set; }
}

public class UpdateInfoEventArgs : EventArgs
{
    public bool IsUpdateAvailable { get; set; }
    public Exception Error { get; set; }
    public string DownloadURL { get; set; }
    public string ChangelogURL { get; set; }
    public string CurrentVersion { get; set; }
    public Version InstalledVersion { get; set; }
    public Mandatory Mandatory { get; set; }
    public string InstallerArgs { get; set; }
    public CheckSum CheckSum { get; set; }
}

public class Mandatory
{
    public bool Value { get; set; }
    public string MinimumVersion { get; set; }
    public Mode UpdateMode { get; set; }
}

public class CheckSum
{
    public string Value { get; set; }
    public string HashingAlgorithm { get; set; }
}

public enum Mode
{
    Normal,
    Forced,
    ForcedDownload
}
