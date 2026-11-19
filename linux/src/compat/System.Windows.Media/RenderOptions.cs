using Avalonia;
using Avalonia.Media.Imaging;

namespace System.Windows.Media;

public enum BitmapScalingMode
{
    Unspecified = 0,
    LowQuality = 1,
    HighQuality = 2,
    Linear = 1,
    Fant = 2,
    NearestNeighbor = 3
}

/// <summary>WPF's RenderOptions mapped onto Avalonia's bitmap interpolation setting.</summary>
public static class RenderOptions
{
    public static void SetBitmapScalingMode(Visual visual, BitmapInterpolationMode mode) =>
        Avalonia.Media.RenderOptions.SetBitmapInterpolationMode(visual, mode);

    public static void SetBitmapScalingMode(Visual visual, BitmapScalingMode mode) =>
        Avalonia.Media.RenderOptions.SetBitmapInterpolationMode(visual, mode switch
        {
            BitmapScalingMode.NearestNeighbor => BitmapInterpolationMode.None,
            BitmapScalingMode.LowQuality => BitmapInterpolationMode.LowQuality,
            _ => BitmapInterpolationMode.HighQuality
        });

    public static void SetBitmapScalingMode(Visual visual, object mode)
    {
        switch (mode)
        {
            case BitmapInterpolationMode interpolation:
                SetBitmapScalingMode(visual, interpolation);
                break;
            case BitmapScalingMode scaling:
                SetBitmapScalingMode(visual, scaling);
                break;
        }
    }
}
