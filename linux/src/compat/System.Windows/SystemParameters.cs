using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform;

namespace System.Windows
{
    /// <summary>Screen metrics in device independent pixels, like WPF's SystemParameters.</summary>
    public static class SystemParameters
    {
        public static double PrimaryScreenWidth => ScreenQuery.Primary is { } s ? s.Bounds.Width / s.Scaling : 1920;
        public static double PrimaryScreenHeight => ScreenQuery.Primary is { } s ? s.Bounds.Height / s.Scaling : 1080;
        public static double MaximizedPrimaryScreenWidth => ScreenQuery.Primary is { } s ? s.WorkingArea.Width / s.Scaling : 1920;
        public static double MaximizedPrimaryScreenHeight => ScreenQuery.Primary is { } s ? s.WorkingArea.Height / s.Scaling : 1040;
        public static double WorkAreaWidth => MaximizedPrimaryScreenWidth;
        public static double WorkAreaHeight => MaximizedPrimaryScreenHeight;
    }

    internal static class ScreenQuery
    {
        public static Avalonia.Platform.Screen Primary
        {
            get
            {
                var screens = AnyTopLevel()?.Screens;
                return screens?.Primary ?? screens?.All.FirstOrDefault();
            }
        }

        private static TopLevel AnyTopLevel()
        {
            var app = Application.Current;
            return app?.MainWindow ?? app?.Windows.FirstOrDefault();
        }
    }
}

namespace System.Windows.Forms
{
    /// <summary>The bit of WinForms' Screen FModel uses: the primary screen bounds in physical pixels.</summary>
    public sealed class Screen
    {
        public System.Drawing.Rectangle Bounds { get; private init; }
        public System.Drawing.Rectangle WorkingArea { get; private init; }
        public bool Primary { get; private init; }

        public static Screen PrimaryScreen
        {
            get
            {
                if (ScreenQuery.Primary is not { } s) return null;
                return new Screen
                {
                    Bounds = new System.Drawing.Rectangle(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height),
                    WorkingArea = new System.Drawing.Rectangle(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height),
                    Primary = true
                };
            }
        }
    }

    public enum ScreenOrientation
    {
        Angle0 = 0,
        Angle90 = 1,
        Angle180 = 2,
        Angle270 = 3
    }
}
