using Avalonia.Markup.Xaml;
using FModel.Framework.Ui;
using System.Windows.Input;

namespace FModel;

/// <summary>
/// Avalonia half of the App class. Startup, logging, settings and crash handling come from upstream's
/// App.xaml.cs, which linux/FModel.Linux.csproj compiles unchanged; this file only provides what App.xaml
/// declares in the WPF build (resources, StartupUri, event wiring).
/// </summary>
public partial class App : System.Windows.Application
{
    public App()
    {
        Exit += AppExit;
        DispatcherUnhandledException += OnUnhandledException;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    protected override System.Windows.Window CreateMainWindow()
    {
        Keyboard.Initialize();
        return new MainWindow();
    }
}
