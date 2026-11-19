using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace System.Windows.Media.Imaging;

/// <summary>
/// WPF-style bitmap that Avalonia can render directly. It implements <see cref="IImage"/>, so a view model
/// property typed as BitmapSource / BitmapImage binds straight to <c>Image.Source</c> in the Avalonia views.
/// </summary>
public abstract class BitmapSource : IImage
{
    public Bitmap Bitmap { get; protected set; }

    public int PixelWidth => Bitmap?.PixelSize.Width ?? 0;
    public int PixelHeight => Bitmap?.PixelSize.Height ?? 0;
    public double Width => Bitmap?.Size.Width ?? 0;
    public double Height => Bitmap?.Size.Height ?? 0;
    public double DpiX => Bitmap?.Dpi.X ?? 96;
    public double DpiY => Bitmap?.Dpi.Y ?? 96;

    /// <summary>
    /// Frames of an animated image (FModel's ImageAnimation). WPF attaches them to the bitmap as an attached
    /// property; Avalonia bindings can't read attached properties off a non-Avalonia object, so they live here.
    /// </summary>
    public object AnimationFrames { get; set; }

    /// <summary>Encodes the bitmap as PNG (used for the clipboard and image exports).</summary>
    public byte[] ToPng()
    {
        if (Bitmap == null) return [];
        using var stream = new MemoryStream();
        Bitmap.Save(stream);
        return stream.ToArray();
    }

    public bool IsFrozen { get; private set; }
    public void Freeze() => IsFrozen = true;

    Size IImage.Size => Bitmap?.Size ?? default;

    void IImage.Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
        if (Bitmap != null)
            context.DrawImage(Bitmap, sourceRect, destRect);
    }

    public static implicit operator Bitmap(BitmapSource source) => source?.Bitmap;
}

public enum BitmapCacheOption
{
    Default,
    OnDemand,
    OnLoad,
    None
}

public enum BitmapCreateOptions
{
    None,
    PreservePixelFormat,
    DelayCreation,
    IgnoreColorProfile,
    IgnoreImageCache
}

/// <summary>Supports both WPF construction patterns FModel uses: <c>new BitmapImage(uri)</c> and BeginInit/StreamSource/EndInit.</summary>
public class BitmapImage : BitmapSource
{
    public BitmapImage() { }

    public BitmapImage(Uri uriSource)
    {
        UriSource = uriSource;
        Load();
    }

    public Uri UriSource { get; set; }
    public Stream StreamSource { get; set; }
    public BitmapCacheOption CacheOption { get; set; }
    public BitmapCreateOptions CreateOptions { get; set; }
    public int DecodePixelWidth { get; set; }
    public int DecodePixelHeight { get; set; }

    public void BeginInit() { }

    public void EndInit() => Load();

    private void Load()
    {
        if (StreamSource != null)
        {
            Bitmap = Decode(StreamSource);
            return;
        }

        if (UriSource == null)
            return;

        if (UriSource.IsAbsoluteUri && UriSource.IsFile)
        {
            using var file = File.OpenRead(UriSource.LocalPath);
            Bitmap = Decode(file);
        }
        else
        {
            using var asset = Application.GetResourceStream(UriSource).Stream;
            Bitmap = Decode(asset);
        }
    }

    private Bitmap Decode(Stream stream)
    {
        if (DecodePixelWidth > 0) return Bitmap.DecodeToWidth(stream, DecodePixelWidth);
        if (DecodePixelHeight > 0) return Bitmap.DecodeToHeight(stream, DecodePixelHeight);
        return new Bitmap(stream);
    }
}

/// <summary>A BitmapSource wrapping an already decoded Avalonia bitmap.</summary>
public sealed class AvaloniaBitmapSource : BitmapSource
{
    public AvaloniaBitmapSource(Bitmap bitmap) => Bitmap = bitmap;
}
