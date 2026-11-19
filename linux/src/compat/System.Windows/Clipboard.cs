using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using FModel.Framework.Ui;
using Serilog;

namespace System.Windows;

/// <summary>
/// WPF's static Clipboard on top of Avalonia's async clipboard. Calls block until the clipboard took the
/// data, like they do on Windows.
/// </summary>
public static class Clipboard
{
    public static void SetText(string text)
    {
        if (text == null) return;
        Run(clipboard => clipboard.SetTextAsync(text));
    }

    public static string GetText()
    {
        return Dispatcher.UIThread.Invoke(() =>
            Current() is { } clipboard ? UiThread.WaitFor(() => clipboard.GetTextAsync()) ?? string.Empty : string.Empty);
    }

    public static void Clear() => Run(clipboard => clipboard.ClearAsync());

    /// <summary>WPF accepts any object; FModel only ever passes text.</summary>
    public static void SetDataObject(object data, bool copy = false) => SetText(data?.ToString());

    /// <summary>
    /// Puts a PNG on the clipboard. wl-copy / xclip are used when present because they keep serving the
    /// image after FModel exits and every desktop understands them; Avalonia's clipboard is the fallback.
    /// </summary>
    public static void SetImage(byte[] png, string fileName = null)
    {
        if (png is not { Length: > 0 }) return;
        if (TryExternal(png)) return;

        Run(clipboard =>
        {
            var data = new DataObject();
            data.Set("image/png", png);
            if (!string.IsNullOrEmpty(fileName))
                data.Set("text/html", $"<img src=\"{fileName}\"/>");
            return clipboard.SetDataObjectAsync(data);
        });
    }

    private static bool TryExternal(byte[] png)
    {
        var tool = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") != null && OnPath("wl-copy")
            ? ("wl-copy", "--type image/png")
            : OnPath("xclip") ? ("xclip", "-selection clipboard -t image/png -i") : default;
        if (tool == default) return false;

        try
        {
            using var process = Process.Start(new ProcessStartInfo(tool.Item1, tool.Item2)
            {
                RedirectStandardInput = true,
                UseShellExecute = false
            });
            if (process == null) return false;
            process.StandardInput.BaseStream.Write(png);
            process.StandardInput.Close();
            return true;
        }
        catch (Exception e)
        {
            Log.Warning(e, "{Tool} failed, falling back to the built-in clipboard", tool.Item1);
            return false;
        }
    }

    private static bool OnPath(string executable) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(':', StringSplitOptions.RemoveEmptyEntries)
        .Any(dir => File.Exists(Path.Combine(dir, executable)));

    private static void Run(Func<IClipboard, System.Threading.Tasks.Task> action)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (Current() is { } clipboard)
                UiThread.WaitFor(() => action(clipboard));
        });
    }

    private static IClipboard Current()
    {
        var app = Application.Current;
        TopLevel top = app?.Windows.FirstOrDefault(w => w.IsActive) ?? app?.MainWindow;
        return top?.Clipboard;
    }
}
