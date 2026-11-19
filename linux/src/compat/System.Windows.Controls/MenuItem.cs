using System;

namespace System.Windows.Controls;

/// <summary>
/// Avalonia MenuItem accepting the WPF-only layout properties upstream sets when it builds menus in code
/// (CustomDirectoriesViewModel). Avalonia's menu template already aligns headers the way they ask for.
/// </summary>
public class MenuItem : Avalonia.Controls.MenuItem
{
    protected override Type StyleKeyOverride => typeof(Avalonia.Controls.MenuItem);

    public HorizontalAlignment HorizontalContentAlignment { get; set; }
    public VerticalAlignment VerticalContentAlignment { get; set; }
}
