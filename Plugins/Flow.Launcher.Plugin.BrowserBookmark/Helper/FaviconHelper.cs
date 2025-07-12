using System;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using SkiaSharp;
using Svg.Skia;

namespace Flow.Launcher.Plugin.BrowserBookmark.Helper;

public static class FaviconHelper
{
    private static readonly string ClassName = nameof(FaviconHelper);

    public static void ExecuteWithTempDb(string faviconCacheDir, string dbPath, Action<string> action)
    {
        var tempDbPath = Path.Combine(faviconCacheDir, $"tempfavicons_{Guid.NewGuid()}.db");
        try
        {
            File.Copy(dbPath, tempDbPath, true);
            using (var connection = new SqliteConnection($"Data Source={tempDbPath};Mode=ReadWrite"))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "CREATE INDEX IF NOT EXISTS idx_moz_pages_w_icons_page_url ON moz_pages_w_icons(page_url);";
                try { command.ExecuteNonQuery(); } catch (SqliteException) { /* ignore */ }
                command.CommandText = "CREATE INDEX IF NOT EXISTS idx_icon_mapping_page_url ON icon_mapping(page_url);";
                try { command.ExecuteNonQuery(); } catch (SqliteException) { /* ignore */ }
            }
            action(tempDbPath);
        }
        catch (Exception ex)
        {
            Main._context.API.LogException(ClassName, $"Failed to process or index SQLite DB: {dbPath}", ex);
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch (Exception ex) { Main._context.API.LogException(ClassName, $"Failed to delete temp favicon DB: {tempDbPath}", ex); }
            }
        }
    }

    private static bool IsSvg(byte[] imageData)
    {
        var text = Encoding.UTF8.GetString(imageData, 0, Math.Min(imageData.Length, 100)).Trim();
        return text.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
               (text.StartsWith("<?xml") && text.Contains("<svg"));
    }

    private static bool ConvertSvgToPng(byte[] svgData, string outputPath)
    {
        try
        {
            using var stream = new MemoryStream(svgData);
            using var svg = new SKSvg();

            svg.Load(stream);

            if (svg.Picture == null)
            {
                Main._context.API.LogWarn(ClassName, $"Failed to load SVG picture from stream for {Path.GetFileName(outputPath)}.");
                return false;
            }

            var info = new SKImageInfo(64, 64);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            var pictureRect = svg.Picture.CullRect;

            canvas.Save();
            if (pictureRect.Width > 0 && pictureRect.Height > 0)
            {
                // Manually calculate the scaling factors to fill the destination canvas.
                float scaleX = info.Width / pictureRect.Width;
                float scaleY = info.Height / pictureRect.Height;

                // Apply the scaling transformation directly to the canvas.
                canvas.Scale(scaleX, scaleY);
            }

            // Draw the picture onto the now-transformed canvas.
            canvas.DrawPicture(svg.Picture);
            canvas.Restore();

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var fileStream = File.OpenWrite(outputPath);
            data.SaveTo(fileStream);
            return true;
        }
        catch (Exception ex)
        {
            Main._context.API.LogException(ClassName, $"Failed to convert SVG to PNG for {Path.GetFileName(outputPath)}.", ex);
            return false;
        }
    }

    public static bool SaveBitmapData(byte[] imageData, string outputPath)
    {
        if (IsSvg(imageData))
        {
            return ConvertSvgToPng(imageData, outputPath);
        }

        try
        {
            // Attempt to load the image data. This will handle all formats ImageSharp
            // supports, including common raster formats.
            using var image = Image.Load(imageData);

            // Resize the image to a maximum of 64x64.
            var options = new ResizeOptions
            {
                Size = new Size(64, 64),
                Mode = ResizeMode.Max
            };
            image.Mutate(x => x.Resize(options));

            // Always save as PNG for maximum compatibility with the UI renderer.
            image.SaveAsPng(outputPath, new PngEncoder { CompressionLevel = PngCompressionLevel.DefaultCompression });
            return true;
        }
        catch (Exception ex)
        {
            // This will now catch errors from loading malformed images,
            // preventing them from being saved and crashing the UI.
            Main._context.API.LogException(ClassName, $"Failed to load/resize/save image to {outputPath}. It may be a malformed image.", ex);
            return false;
        }
    }
}
