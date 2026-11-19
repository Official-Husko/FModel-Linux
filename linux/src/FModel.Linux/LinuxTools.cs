using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using FModel.Framework;
using FModel.Views.Resources.Controls;
using FModel.Services;
using FModel.Settings;
using Serilog;

namespace FModel.Linux;

/// <summary>
/// Linux builds of the external tools upstream downloads for Windows.
/// </summary>
public static class LinuxTools
{
    // vgmstream's tagged releases only carry Windows binaries; Linux builds are published as nightlies
    private const string VgmStreamUrl = "https://github.com/vgmstream/vgmstream-releases/releases/download/nightly/vgmstream-linux-cli.tar.gz";

    public static string VgmStreamPath => Path.Combine(UserSettings.Default.OutputDirectory, ".data", "vgmstream-cli");

    /// <summary>
    /// Linux counterpart of ApplicationViewModel.InitVgmStream: fetches vgmstream-cli (used to convert
    /// Wwise, ADX, HCA, ... audio) and refreshes it every four months like upstream does.
    /// </summary>
    public static async Task InitVgmStream()
    {
        var archive = new FileInfo(Path.Combine(UserSettings.Default.OutputDirectory, ".data", "vgmstream-linux-cli.tar.gz"));
        if (archive.Exists && archive.Length > 0 && File.Exists(VgmStreamPath) && archive.LastWriteTimeUtc >= DateTime.UtcNow.AddMonths(-4))
            return;

        await ApplicationService.ApiEndpointView.DownloadFileAsync(VgmStreamUrl, archive.FullName);
        archive.Refresh();
        if (!archive.Exists || archive.Length == 0)
        {
            FLogger.Append(ELog.Error, () => FLogger.Text("Could not download vgmstream", Constants.WHITE, true));
            return;
        }

        try
        {
            await using (var file = archive.OpenRead())
            await using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            {
                await TarFile.ExtractToDirectoryAsync(gzip, archive.DirectoryName!, overwriteFiles: true);
            }

            if (File.Exists(VgmStreamPath))
            {
                File.SetUnixFileMode(VgmStreamPath, File.GetUnixFileMode(VgmStreamPath) |
                    UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Could not extract vgmstream");
            FLogger.Append(ELog.Error, () => FLogger.Text("Could not extract vgmstream", Constants.WHITE, true));
        }
    }
}
