// WPF and AvalonEdit types that have a 1:1 Avalonia / AvaloniaEdit counterpart.
//
// Upstream FModel refers to these by their simple name after `using System.Windows...;`. A global alias
// takes precedence over namespace imports, so the shared sources bind straight to the Avalonia type
// without any edit. Only add a type here when the members FModel uses behave the same on both sides;
// anything with different semantics belongs in a real stand-in class next to this file.

global using Control = Avalonia.Controls.Control;
global using GridLength = Avalonia.Controls.GridLength;
global using GridUnitType = Avalonia.Controls.GridUnitType;
global using Image = Avalonia.Controls.Image;
global using Separator = Avalonia.Controls.Separator;
global using WindowState = Avalonia.Controls.WindowState;
global using HorizontalAlignment = Avalonia.Layout.HorizontalAlignment;
global using VerticalAlignment = Avalonia.Layout.VerticalAlignment;
global using Thickness = Avalonia.Thickness;
global using ImageSource = Avalonia.Media.IImage;
global using Geometry = Avalonia.Media.Geometry;
// WPF brushes are classes, Avalonia binds brush properties as IBrush. Upstream compares binding target types
// with typeof(Brush), so Brush has to mean the interface for those checks to match.
global using Brush = Avalonia.Media.IBrush;
global using DispatcherTimer = Avalonia.Threading.DispatcherTimer;
global using DispatcherPriority = Avalonia.Threading.DispatcherPriority;
global using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

// Avalonia's Key enum mirrors WPF's member for member (same names, same values), so hotkeys saved by
// FModel keep working. ModifierKeys is a real enum (System.Windows.Input) because WPF has a Windows flag.
global using Key = Avalonia.Input.Key;

// AvalonEdit -> AvaloniaEdit (same project, ported to Avalonia, same public API)
global using TextDocument = AvaloniaEdit.Document.TextDocument;
global using IHighlightingDefinition = AvaloniaEdit.Highlighting.IHighlightingDefinition;
global using HighlightingManager = AvaloniaEdit.Highlighting.HighlightingManager;

// Value converters: Avalonia's interface has the exact same Convert/ConvertBack signatures as WPF's.
global using IValueConverter = Avalonia.Data.Converters.IValueConverter;
global using MarkupExtension = Avalonia.Markup.Xaml.MarkupExtension;
