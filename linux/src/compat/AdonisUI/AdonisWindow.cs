namespace AdonisUI.Controls;

/// <summary>Base class of every FModel window (AdonisUI's themed window in the WPF build).</summary>
public class AdonisWindow : System.Windows.Window
{
    /// <summary>AdonisUI lets content extend into the title bar; Avalonia draws a regular decorated window.</summary>
    public double TitleBarHeight { get; set; }
    public bool IconVisibility { get; set; } = true;
}
