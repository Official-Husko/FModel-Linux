using System.Windows;
using System.Windows.Media.Imaging;

namespace FModel.Extensions;

/// <summary>
/// Linux version of upstream's ClipboardExtensions. The Windows one hand-builds DIB/PNG clipboard formats
/// through System.Drawing; Linux desktops just want image/png.
/// </summary>
public static class ClipboardExtensions
{
    public static void SetImage(BitmapSource image, string fileName = null)
    {
        if (image == null) return;
        Clipboard.SetImage(image.ToPng(), fileName);
    }

    public static void SetImage(byte[] pngBytes, string fileName = null) => Clipboard.SetImage(pngBytes, fileName);
}
