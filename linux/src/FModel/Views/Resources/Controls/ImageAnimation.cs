using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media.Imaging;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CUE4Parse.UE4.Assets.Exports.Paper2D;
using CUE4Parse_Conversion.Options;
using CUE4Parse_Conversion.Textures;
using CUE4Parse_Conversion.Textures.Paper2D;

namespace FModel.Views.Resources.Controls;

/// <summary>
/// Avalonia port of upstream's flipbook player. Same public surface: view models build frames with
/// <see cref="CreateFrames"/> / <see cref="CreateThumbnail"/>, views attach them to an Image with the
/// Frames attached property. In the WPF build the frames ride along on the BitmapSource as an attached
/// property; here they are stored on the BitmapSource stand-in (<see cref="BitmapSource.AnimationFrames"/>).
/// </summary>
public class ImageAnimation : AvaloniaObject
{
    public sealed record Frame(BitmapSource Image, TimeSpan Duration);

    public static readonly AttachedProperty<IReadOnlyList<Frame>> FramesProperty =
        AvaloniaProperty.RegisterAttached<ImageAnimation, Image, IReadOnlyList<Frame>>("Frames");

    private static readonly AttachedProperty<Playback> _playbackProperty =
        AvaloniaProperty.RegisterAttached<ImageAnimation, Image, Playback>("Playback");

    static ImageAnimation()
    {
        FramesProperty.Changed.AddClassHandler<Image>(OnFramesChanged);
    }

    public static IReadOnlyList<Frame> GetFrames(Image image) => image.GetValue(FramesProperty);
    public static void SetFrames(Image image, IReadOnlyList<Frame> frames) => image.SetValue(FramesProperty, frames);

    public static BitmapSource CreateSource(IReadOnlyList<Frame> frames)
    {
        if (frames is not { Count: > 0 })
            return null;
        if (frames.Count == 1)
            return frames[0].Image;

        var source = new AvaloniaBitmapSource(frames[0].Image.Bitmap) { AnimationFrames = frames };
        source.Freeze();
        return source;
    }

    internal static Frame[] CreateFrames(CFlipbook flipbook, int maxSize = 0, CancellationToken cancellationToken = default)
    {
        using var animation = flipbook.ToImage(new ExportOptions(), maxSize: maxSize, cancellationToken: cancellationToken);
        var frames = new Frame[animation.Frames.Count];
        var minimumDuration = maxSize > 0 ? 1.0 / 15 : 0.001;
        for (var i = 0; i < frames.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = animation.Frames[i];
            var pixels = new byte[frame.Width * frame.Height * 4];
            frame.CopyPixelDataTo(pixels);

            // ImageSharp hands out RGBA, which Avalonia takes as is (WPF needed BGRA)
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            Bitmap bitmap;
            try
            {
                bitmap = new Bitmap(PixelFormats.Rgba8888, AlphaFormat.Unpremul, handle.AddrOfPinnedObject(),
                    new PixelSize(frame.Width, frame.Height), new Vector(96, 96), frame.Width * 4);
            }
            finally
            {
                handle.Free();
            }

            var image = new AvaloniaBitmapSource(bitmap);
            image.Freeze();

            var seconds = flipbook.FramesPerSecond > 0 ? flipbook.Frames[i].FrameRun / (double) flipbook.FramesPerSecond : 0;
            frames[i] = new Frame(image, TimeSpan.FromSeconds(Math.Max(minimumDuration, seconds)));
        }

        return frames;
    }

    // Only light animations, otherwise use the first frame as a thumbnail
    internal static BitmapSource CreateThumbnail(UPaperFlipbook flipbook, ExportOptions options, int maxSize)
    {
        var cache = new Dictionary<string, CTexture>();
        if (flipbook.FramesPerSecond > 0 && flipbook.KeyFrames.Length is > 1 and <= 64)
        {
            var pixels = flipbook.KeyFrames.Where(frame => frame.FrameRun > 0).Select(frame => frame.LoadSprite()).OfType<UPaperSprite>().Distinct()
                .Sum(sprite => (double) sprite.BakedSourceDimension.X * sprite.BakedSourceDimension.Y);
            if (pixels <= 16 * 1024 * 1024 && flipbook.Decode(options, cache) is { } decoded)
            {
                var minimumRun = Math.Max(1, (int) Math.Ceiling(decoded.FramesPerSecond / 15));
                var frames = new List<CFlipbookFrame>();
                foreach (var frame in decoded.Frames)
                {
                    if (frames.Count > 0 && frames[^1].FrameRun < minimumRun)
                    {
                        var previous = frames[^1];
                        frames[^1] = new CFlipbookFrame(previous.Texture, previous.FrameRun + frame.FrameRun, previous.Offset);
                    }
                    else
                    {
                        frames.Add(frame);
                    }
                }

                return CreateSource(CreateFrames(new CFlipbook(frames, decoded.FramesPerSecond), maxSize));
            }
        }

        var first = flipbook.KeyFrames.Where(frame => frame.FrameRun > 0).Select(frame => frame.LoadSprite()?.Decode(options, cache)).FirstOrDefault(texture => texture != null);
        return first == null ? null : CreateSource(CreateFrames(new CFlipbook([new CFlipbookFrame(first)], 1), maxSize));
    }

    private static void OnFramesChanged(Image image, AvaloniaPropertyChangedEventArgs args)
    {
        image.GetValue(_playbackProperty)?.Dispose();
        image.ClearValue(_playbackProperty);
        if (args.NewValue is not IReadOnlyList<Frame> { Count: > 0 } frames)
            return;

        image.SetCurrentValue(Image.SourceProperty, frames[0].Image);
        if (frames.Count > 1)
        {
            image.SetValue(_playbackProperty, new Playback(image, frames));
        }
    }

    private sealed class Playback : IDisposable
    {
        private readonly Image _image;
        private readonly IReadOnlyList<Frame> _frames;
        private readonly DispatcherTimer _timer;
        private int _index;

        public Playback(Image image, IReadOnlyList<Frame> frames)
        {
            _image = image;
            _frames = frames;
            _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = frames[0].Duration };
            _timer.Tick += (_, _) =>
            {
                _index = (_index + 1) % _frames.Count;
                _image.SetCurrentValue(Image.SourceProperty, _frames[_index].Image);
                _timer.Interval = _frames[_index].Duration;
            };
            image.AttachedToVisualTree += OnAttached;
            image.DetachedFromVisualTree += OnDetached;
            image.PropertyChanged += OnPropertyChanged;
            UpdatePlayback();
        }

        private void UpdatePlayback()
        {
            if (_image.IsEffectivelyVisible && TopLevel.GetTopLevel(_image) != null)
                _timer.Start();
            else
                _timer.Stop();
        }

        private void OnAttached(object sender, VisualTreeAttachmentEventArgs args) => UpdatePlayback();
        private void OnDetached(object sender, VisualTreeAttachmentEventArgs args) => _timer.Stop();

        private void OnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Property == Visual.IsVisibleProperty)
                UpdatePlayback();
        }

        public void Dispose()
        {
            _timer.Stop();
            _image.AttachedToVisualTree -= OnAttached;
            _image.DetachedFromVisualTree -= OnDetached;
            _image.PropertyChanged -= OnPropertyChanged;
        }
    }
}
