namespace System.Windows;

/// <summary>
/// WPF's Visibility. Avalonia binds visibility as a bool (IsVisible); upstream converters that check
/// <c>targetType == typeof(Visibility)</c> therefore fall back to returning a bool, which is what Avalonia wants.
/// </summary>
public enum Visibility
{
    Visible,
    Hidden,
    Collapsed
}
