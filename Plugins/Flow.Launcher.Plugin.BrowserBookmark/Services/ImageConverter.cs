#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using SkiaSharp;
using Svg.Skia;

namespace Flow.Launcher.Plugin.BrowserBookmark.Services;

public class ImageConverter
{
    private readonly PluginInitContext _context;
    public const int TargetIconSize = 48;

    public ImageConverter(PluginInitContext context)
    {
        _context = context;
    }

    public async Task<(byte[]? PngData, int Size)> ToPngAsync(Stream stream, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, token);
        ms.Position = 0;

        // The returned 'Size' is the original width of the icon, used for scoring the best favicon.
        // It does not reflect the final dimensions of the resized PNG.

        var (pngData, size) = TryConvertSvgToPng(ms);
        if (pngData is not null) return (pngData, size);

        ms.Position = 0;
        (pngData, size) = TryConvertIcoToPng(ms);
        if (pngData is not null) return (pngData, size);

        ms.Position = 0;
        (pngData, size) = TryConvertBitmapToPng(ms);
        return (pngData, size);
    }

    private static (byte[]? PngData, int Size) TryConvertSvgToPng(Stream stream)
    {
        try
        {
            using var svg = new SKSvg();
            if (svg.Load(stream) is not null && svg.Picture is not null)
            {
                using var bitmap = new SKBitmap(TargetIconSize, TargetIconSize);
                using var canvas = new SKCanvas(bitmap);
                canvas.Clear(SKColors.Transparent);
                var scaleMatrix = SKMatrix.CreateScale((float)TargetIconSize / svg.Picture.CullRect.Width, (float)TargetIconSize / svg.Picture.CullRect.Height);
                canvas.DrawPicture(svg.Picture, in scaleMatrix);

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 80);
                return (data.ToArray(), TargetIconSize);
            }
        }
        catch { /* Not a valid SVG */ }

        return (null, 0);
    }

    private static (byte[]? PngData, int Size) TryConvertIcoToPng(Stream stream)
    {
        try
        {
            var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Any())
            {
                var largestFrame = decoder.Frames.OrderByDescending(f => f.Width * f.Height).First();

                using var pngStream = new MemoryStream();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(largestFrame));
                encoder.Save(pngStream);

                pngStream.Position = 0;
                using var original = SKBitmap.Decode(pngStream);
                if (original != null)
                {
                    var originalWidth = original.Width;
                    var info = new SKImageInfo(TargetIconSize, TargetIconSize, original.ColorType, original.AlphaType);
                    using var resized = original.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));
                    if (resized != null)
                    {
                        using var image = SKImage.FromBitmap(resized);
                        using var data = image.Encode(SKEncodedImageFormat.Png, 80);
                        return (data.ToArray(), originalWidth);
                    }
                }
            }
        }
        catch { /* Not a supported ICO format */ }

        return (null, 0);
    }

    private (byte[]? PngData, int Size) TryConvertBitmapToPng(Stream stream)
    {
        try
        {
            using var original = SKBitmap.Decode(stream);
            if (original != null)
            {
                var originalWidth = original.Width;
                var info = new SKImageInfo(TargetIconSize, TargetIconSize, original.ColorType, original.AlphaType);
                using var resized = original.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));
                if (resized != null)
                {
                    using var image = SKImage.FromBitmap(resized);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 80);
                    return (data.ToArray(), originalWidth);
                }
            }
        }
        catch (Exception ex)
        {
            _context.API.LogException(nameof(ImageConverter), "Failed to decode or convert bitmap with final fallback", ex);
        }

        return (null, 0);
    }
}
