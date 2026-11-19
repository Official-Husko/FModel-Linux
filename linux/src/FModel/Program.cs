using System;
using System.IO;
using Avalonia;
using FModel.Linux;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace FModel;

/// <summary>Linux entry point. WPF generates this from App.xaml; Avalonia needs it spelled out.</summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        System.Windows.PackUriScheme.Register();
        NativeCompat.Install();
        LinuxEnvironment.PrepareOutputDirectory();
        ConfigureGlfw();

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnMainWindowClose);
    }

    // also used by the visual designer
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions
            {
                EnableIme = true,
                // FModel's file dialogs are synchronous (nested loop), which the XDG portal picker doesn't
                // reliably surface on every desktop. GTK's chooser (or Avalonia's own) does.
                // FMODEL_USE_PORTAL=1 switches back to the portal.
                UseDBusFilePicker = Environment.GetEnvironmentVariable("FMODEL_USE_PORTAL") == "1"
            })
            .LogToTrace();

    /// <summary>
    /// The 3D viewer (Snooper) opens a GLFW window. GLFW's native Wayland backend can't position windows,
    /// which the viewer relies on, so use XWayland like the rest of the UI. Must run before GLFW initializes.
    /// FMODEL_GLFW_WAYLAND=1 opts into native Wayland anyway.
    /// </summary>
    private static void ConfigureGlfw()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) ||
            Environment.GetEnvironmentVariable("FMODEL_GLFW_WAYLAND") == "1")
            return;

        try
        {
            GLFW.InitHint(InitHintPlatform.Platform, Platform.X11);
        }
        catch
        {
            // GLFW older than 3.4 has no platform hint and picks X11 on its own
        }
    }
}
