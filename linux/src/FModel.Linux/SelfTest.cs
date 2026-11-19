using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using FModel.Services;
using FModel.Settings;
using FModel.ViewModels;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace FModel.Linux;

/// <summary>
/// Unattended export check for the Linux build: drives FModel's own commands (the same ones the
/// context menus use), waits for every export to finish, then writes a report and exits.
///
///   FMODEL_SELFTEST=1                              enable
///   FMODEL_SELFTEST_FOLDER=Game/Characters         folder to export (default: the whole first folder)
///   FMODEL_SELFTEST_ACTIONS=Save_Textures,...      context menu actions to run (default: all exports),
///                                                  or "list" to print the subfolders and their sizes
///
/// The exit code is the number of failed exports (capped at 125), so it can gate CI.
/// </summary>
public static class SelfTest
{
    private static readonly string[] _defaultActions =
        ["Save_Data", "Save_Properties", "Save_Textures", "Save_Models", "Save_Animations", "Save_Audio", "Save_EmbeddedFiles"];

    public static bool IsEnabled => Environment.GetEnvironmentVariable("FMODEL_SELFTEST") == "1";

    public static async Task RunAsync()
    {
        var report = new StringBuilder();
        var failures = 0;
        var errors = new ErrorCollector();
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Logger(Log.Logger).WriteTo.Sink(errors).CreateLogger();
        try
        {
            var app = ApplicationService.ApplicationView;
            await WaitIdle();

            await Dispatcher.UIThread.InvokeAsync(() => app.LoadingModes.LoadCommand.Execute(null));
            await Task.Delay(500);
            await WaitIdle();

            var folder = FindFolder(app, Environment.GetEnvironmentVariable("FMODEL_SELFTEST_FOLDER"));
            if (folder == null)
                throw new InvalidOperationException("Folder to export not found, check FMODEL_SELFTEST_FOLDER");

            if (Environment.GetEnvironmentVariable("FMODEL_SELFTEST_ACTIONS") == "list")
            {
                report.AppendLine($"Subfolders of {folder.PathAtThisPoint}:");
                foreach (var child in folder.Folders)
                    report.AppendLine($"  {child.Header,-40} {CountAssets(child),7} files");
                throw new OperationCanceledException("listing only");
            }

            var actions = Environment.GetEnvironmentVariable("FMODEL_SELFTEST_ACTIONS")?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? _defaultActions;

            report.AppendLine($"FModel {LinuxBuild.Version} (upstream {LinuxBuild.UpstreamCommit}) self-test");
            report.AppendLine($"Game: {UserSettings.Default.CurrentDir.GameName} ({UserSettings.Default.CurrentDir.UeVersion})");
            report.AppendLine($"Folder: {folder.PathAtThisPoint}");
            report.AppendLine();

            // Bulk exports swallow per-asset exceptions upstream, so extract one package directly first and
            // report what goes wrong, if anything.
            var probe = FindFirstPackage(folder);
            if (probe != null)
            {
                try
                {
                    await Task.Run(() => app.CUE4Parse.Extract(CancellationToken.None, probe, false, EBulkType.Properties | EBulkType.Auto));
                    report.AppendLine($"Probe: {probe.Path} extracted fine");
                }
                catch (Exception e)
                {
                    failures++;
                    report.AppendLine($"Probe: {probe.Path} FAILED");
                    report.AppendLine(e.ToString());
                }
                report.AppendLine();
            }

            report.AppendLine($"{"action",-20} {"files written",13} {"errors",8} {"time",9}");
            foreach (var action in actions)
            {
                errors.Clear();
                var (written, failed, elapsed) = await RunAction(app, action, folder, errors);
                var errorCount = failed + errors.Count;
                failures += errorCount;
                report.AppendLine($"{action,-20} {written,13} {errorCount,8} {elapsed.TotalSeconds,8:0.0}s");
                foreach (var error in errors.Messages.Take(10))
                    report.AppendLine($"    ! {error}");
            }
        }
        catch (OperationCanceledException) when (Environment.GetEnvironmentVariable("FMODEL_SELFTEST_ACTIONS") == "list")
        {
        }
        catch (Exception e)
        {
            failures = Math.Max(failures, 1);
            report.AppendLine($"Self-test aborted: {e}");
        }

        var text = report.ToString();
        var path = Path.Combine(UserSettings.Default.OutputDirectory, "Logs", $"SelfTest-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text);
        Console.WriteLine(text);
        Log.Information("Self-test report written to {Path}", path);

        // Exit directly: upstream's exit handler always reports success, the failure count is the point here
        await Log.CloseAndFlushAsync();
        Environment.Exit(Math.Min(failures, 125));
    }

    /// <summary>
    /// Runs one context menu action and measures what a user would get: files written to the export folders,
    /// errors logged while it ran, and failed items in the export session (models, animations, worlds).
    /// </summary>
    private static async Task<(int Written, int Failed, TimeSpan Elapsed)> RunAction(ApplicationViewModel app, string action, TreeItem folder, ErrorCollector errors)
    {
        var started = DateTime.UtcNow;
        var session = ExportSessionViewModel.Instance;
        await Dispatcher.UIThread.InvokeAsync(session.ClearQueue);

        await Dispatcher.UIThread.InvokeAsync(() => app.RightClickMenuCommand.Execute(new object[] { action, new object[] { folder } }));
        await Task.Delay(500);
        await WaitIdle();

        var failed = 0;
        if (session.Session.TotalQueued > 0)
        {
            var results = await session.ExportAsync() ?? [];
            foreach (var result in results.Where(r => !r.Success))
            {
                failed++;
                errors.Add($"{result.ObjectPath}: {result.Error?.GetType().Name} {result.Error?.Message}");
            }
        }

        return (CountWrittenSince(started), failed, DateTime.UtcNow - started);
    }

    private static int CountWrittenSince(DateTime since)
    {
        var settings = UserSettings.Default;
        var roots = new[] { settings.RawDataDirectory, settings.PropertiesDirectory, settings.TextureDirectory, settings.AudioDirectory, settings.ModelDirectory, settings.CodeDirectory }
            .Where(Directory.Exists).Select(Path.GetFullPath).Distinct();

        return roots.SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Distinct()
            .Count(file => File.GetLastWriteTimeUtc(file) >= since);
    }

    /// <summary>Collects error level log events (FModel and CUE4Parse log every failed export).</summary>
    private sealed class ErrorCollector : ILogEventSink
    {
        private readonly List<string> _messages = [];
        private readonly Lock _lock = new();

        public int Count { get { lock (_lock) return _messages.Count; } }
        public IReadOnlyList<string> Messages { get { lock (_lock) return _messages.ToList(); } }

        public void Emit(LogEvent logEvent)
        {
            if (logEvent.Level < LogEventLevel.Error) return;
            Add(logEvent.RenderMessage() + (logEvent.Exception is { } e ? $" ({e.GetType().Name}: {e.Message})" : ""));
        }

        public void Add(string message)
        {
            lock (_lock) _messages.Add(message.Length > 300 ? message[..300] + "..." : message);
        }

        public void Clear()
        {
            lock (_lock) _messages.Clear();
        }
    }

    private static int CountAssets(TreeItem folder) => folder.AssetsList.Assets.Count + folder.Folders.Sum(CountAssets);

    private static CUE4Parse.FileProvider.Objects.GameFile FindFirstPackage(TreeItem folder)
    {
        var asset = folder.AssetsList.Assets.Select(a => a.Asset).FirstOrDefault(a => a.Extension is "uasset" or "umap");
        return asset ?? folder.Folders.Select(FindFirstPackage).FirstOrDefault(a => a != null);
    }

    private static TreeItem FindFolder(ApplicationViewModel app, string path)
    {
        var roots = app.CUE4Parse.AssetsFolder.Folders;
        if (string.IsNullOrWhiteSpace(path))
            return roots.FirstOrDefault();

        IEnumerable<TreeItem> level = roots;
        TreeItem current = null;
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = level.FirstOrDefault(f => f.Header.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (current == null) return null;
            level = current.Folders;
        }

        return current;
    }

    private static async Task WaitIdle()
    {
        var deadline = DateTime.UtcNow.AddHours(2);
        while (DateTime.UtcNow < deadline)
        {
            var busy = ApplicationService.ThreadWorkerView.CanBeCanceled || !ApplicationService.ApplicationView.Status.IsReady;
            if (!busy) return;
            await Task.Delay(250);
        }

        throw new TimeoutException("FModel stayed busy for two hours");
    }
}
