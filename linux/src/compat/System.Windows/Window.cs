using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace System.Windows;

/// <summary>
/// Avalonia window with the WPF behaviors FModel relies on: a blocking <see cref="ShowDialog()"/>,
/// a settable <see cref="DialogResult"/> that closes the dialog, and a settable <see cref="Owner"/>.
/// Every FModel window on Linux derives from this (through AdonisUI.Controls.AdonisWindow).
/// </summary>
public class Window : Avalonia.Controls.Window
{
    // Use the regular Window theme for every subclass.
    protected override Type StyleKeyOverride => typeof(Avalonia.Controls.Window);

    private static WindowIcon _appIcon;
    private static readonly HashSet<MenuBase> _openMenus = [];

    private bool _isDialog;
    private bool? _dialogResult;

    static Window()
    {
        MenuBase.OpenedEvent.AddClassHandler<MenuBase>((menu, _) => _openMenus.Add(menu), handledEventsToo: true);
        MenuBase.ClosedEvent.AddClassHandler<MenuBase>((menu, _) => _openMenus.Remove(menu), handledEventsToo: true);
    }

    public Window()
    {
        try
        {
            _appIcon ??= new WindowIcon(Application.GetResourceStream(new Uri("/FModel;component/Resources/FModel.ico", UriKind.Relative)).Stream);
            Icon = _appIcon;
        }
        catch
        {
            // a missing icon is not worth failing a window over
        }
    }

    /// <summary>Same as WPF: setting it while the window is shown as a dialog closes it.</summary>
    public bool? DialogResult
    {
        get => _dialogResult;
        set
        {
            _dialogResult = value;
            if (_isDialog) Close(value);
        }
    }

    public ResizeMode ResizeMode
    {
        get => CanResize ? ResizeMode.CanResize : ResizeMode.NoResize;
        set => CanResize = value is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
    }

    /// <summary>Owner used by the next <see cref="Show"/> / <see cref="ShowDialog()"/> call.</summary>
    public new Avalonia.Controls.Window Owner { get; set; }

    /// <summary>
    /// Modal and blocking, like WPF. Avalonia only has an async ShowDialog, so a nested dispatcher loop
    /// is pumped until the window closes.
    /// </summary>
    public bool? ShowDialog()
    {
        CloseOpenMenus();
        var owner = Owner ?? ActiveWindow();
        _isDialog = true;
        try
        {
            if (owner == null || ReferenceEquals(owner, this) || !owner.IsVisible)
            {
                FModel.Framework.Ui.UiThread.WaitFor(() =>
                {
                    var tcs = new TaskCompletionSource();
                    Closed += (_, _) => tcs.TrySetResult();
                    base.Show();
                    return tcs.Task;
                });
            }
            else
            {
                FModel.Framework.Ui.UiThread.WaitFor(() => ShowDialog<object>(owner));
            }
        }
        finally
        {
            _isDialog = false;
        }

        return _dialogResult ?? false;
    }

    public new void Show()
    {
        if (Owner is { IsVisible: true } owner) base.Show(owner);
        else base.Show();
    }

    private static Avalonia.Controls.Window ActiveWindow()
    {
        var windows = Application.Current?.Windows.ToList() ?? [];
        return windows.FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow ?? windows.FirstOrDefault();
    }

    /// <summary>
    /// A blocking dialog started from a menu item runs its nested loop before the menu gets a chance to
    /// close itself, so close open menus first (WPF does the same).
    /// </summary>
    private static void CloseOpenMenus()
    {
        foreach (var menu in _openMenus.ToArray())
        {
            try
            {
                menu.Close();
            }
            catch
            {
                // ignored
            }
        }

        _openMenus.Clear();
    }
}

public enum ResizeMode
{
    NoResize,
    CanMinimize,
    CanResize,
    CanResizeWithGrip
}
