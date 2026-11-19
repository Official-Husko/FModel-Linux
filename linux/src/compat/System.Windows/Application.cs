using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;

namespace System.Windows;

/// <summary>
/// WPF-shaped application object. FModel's <c>App</c> derives from this, so upstream code calling
/// <c>Application.Current.Dispatcher.Invoke(...)</c>, <c>Application.Current.Windows</c> or
/// <c>Application.Current.Shutdown()</c> runs unchanged on Avalonia.
/// </summary>
public class Application : Avalonia.Application
{
    public static new Application Current => Avalonia.Application.Current as Application;

    /// <summary>Raised when the app is shutting down (App.xaml's Exit="..." in the WPF build).</summary>
    public event EventHandler<ExitEventArgs> Exit;

    /// <summary>Raised for exceptions escaping a UI thread callback (App.xaml's DispatcherUnhandledException="...").</summary>
    public event EventHandler<System.Windows.Threading.DispatcherUnhandledExceptionEventArgs> DispatcherUnhandledException;

    /// <summary>Equivalent of WPF's StartupUri: the window shown once <see cref="OnStartup"/> returned.</summary>
    protected virtual Avalonia.Controls.Window CreateMainWindow() => null;

    protected virtual void OnStartup(StartupEventArgs e) { }

    public override void OnFrameworkInitializationCompleted()
    {
        if (Desktop is { } desktop)
        {
            Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, args) =>
            {
                var wpfArgs = new System.Windows.Threading.DispatcherUnhandledExceptionEventArgs(args.Exception);
                DispatcherUnhandledException?.Invoke(this, wpfArgs);
                args.Handled = wpfArgs.Handled;
            };
            desktop.Exit += (_, args) => Exit?.Invoke(this, new ExitEventArgs(args.ApplicationExitCode));

            OnStartup(new StartupEventArgs(desktop.Args ?? []));
            desktop.MainWindow ??= CreateMainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public Avalonia.Threading.Dispatcher Dispatcher => Avalonia.Threading.Dispatcher.UIThread;

    /// <summary>Looks a resource up in the application resources and styles (null when missing).</summary>
    public object FindResource(object resourceKey) => TryFindResource(resourceKey);

    public object TryFindResource(object resourceKey) =>
        resourceKey != null && TryGetResource(resourceKey, ActualThemeVariant, out var value) ? value : null;

    private IClassicDesktopStyleApplicationLifetime Desktop => ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

    /// <summary>All open windows, like WPF's Application.Windows.</summary>
    public IEnumerable<Avalonia.Controls.Window> Windows => Desktop?.Windows ?? Enumerable.Empty<Avalonia.Controls.Window>();

    public Avalonia.Controls.Window MainWindow
    {
        get => Desktop?.MainWindow;
        set
        {
            if (Desktop is { } desktop) desktop.MainWindow = value;
        }
    }

    public void Shutdown(int exitCode = 0)
    {
        if (Desktop is { } desktop) desktop.Shutdown(exitCode);
        else Environment.Exit(exitCode);
    }

    /// <summary>
    /// Opens a resource bundled with the application. Accepts the WPF pack syntax used across FModel
    /// ("pack://application:,,,/Resources/x.png", "/FModel;component/Resources/x.png") and maps it onto
    /// Avalonia's avares:// scheme, where linux/FModel.Linux.csproj publishes the same files.
    /// </summary>
    public static StreamResourceInfo GetResourceStream(Uri uri) => new(AssetLoader.Open(ToAvaloniaUri(uri)));

    internal static Uri ToAvaloniaUri(Uri uri) => ToAvaloniaUri(uri.OriginalString);

    internal static Uri ToAvaloniaUri(string path)
    {
        if (path.StartsWith("avares://", StringComparison.Ordinal))
            return new Uri(path);

        const string pack = "pack://application:,,,";
        if (path.StartsWith(pack, StringComparison.OrdinalIgnoreCase))
            path = path[pack.Length..];

        path = path.TrimStart('/');
        const string component = "FModel;component/";
        if (path.StartsWith(component, StringComparison.OrdinalIgnoreCase))
            path = path[component.Length..];

        return new Uri($"avares://FModel/{path}");
    }
}

public class StartupEventArgs(string[] args) : EventArgs
{
    public string[] Args { get; } = args;
}

public class ExitEventArgs(int exitCode) : EventArgs
{
    public int ApplicationExitCode { get; set; } = exitCode;
}

public sealed class StreamResourceInfo(Stream stream)
{
    public Stream Stream { get; } = stream;
}
