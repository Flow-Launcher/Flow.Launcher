using System;
using System.IO;
using SkiaSharp;
using Svg.Skia;

namespace Flow.Launcher.Plugin.BrowserBookmark.Helper;

public static class FaviconHelper
{
    private static readonly string ClassName = nameof(FaviconHelper);

    public static void LoadFaviconsFromDb(string faviconCacheDir, string dbPath, Action<string> loadAction)
    {
        // Use a copy to avoid lock issues with the original file
        var tempDbPath = Path.Combine(faviconCacheDir, $"tempfavicons_{Guid.NewGuid()}.db");

        try
        {
            File.Copy(dbPath, tempDbPath, true);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempDbPath))
                {
                    File.Delete(tempDbPath);
                }
            }
            catch (Exception ex1)
            {
                Main.Context.API.LogException(ClassName, $"Failed to delete temporary favicon DB: {tempDbPath}", ex1);
            }
            Main.Context.API.LogException(ClassName, $"Failed to copy favicon DB: {dbPath}", ex);
            return;
        }

        try
        {
            loadAction(tempDbPath);
        }
        catch (Exception ex)
        {
            Main.Context.API.LogException(ClassName, $"Failed to connect to SQLite: {tempDbPath}", ex);
        }

        // Delete temporary file
        try
        {
            File.Delete(tempDbPath);
        }
        catch (Exception ex)
        {
            Main.Context.API.LogException(ClassName, $"Failed to delete temporary favicon DB: {tempDbPath}", ex);
        }
    }

    public static void SaveBitmapData(byte[] imageData, string outputPath)
    {
        try
        {
            File.WriteAllBytes(outputPath, imageData);
        }
        catch (Exception ex)
        {
            Main.Context.API.LogException(ClassName, $"Failed to save image: {outputPath}", ex);
        }
    }

    public static byte[] TryConvertToWebp(byte[] data)
    {
        if (data == null || data.Length == 0)
            return null;
        
        SKBitmap bitmap = null;

        try
        {
            using (var ms = new MemoryStream(data))
            {
                var svg = new SKSvg();
                if (svg.Load(ms) != null && svg.Picture != null)
                {
                    bitmap = new SKBitmap((int)svg.Picture.CullRect.Width, (int)svg.Picture.CullRect.Height);
                    using (var canvas = new SKCanvas(bitmap))
                    {
                        canvas.Clear(SKColors.Transparent);
                        canvas.DrawPicture(svg.Picture);
                        canvas.Flush();
                    }
                }
            }
        }
        catch { /* Not an SVG */ }

        if (bitmap == null)
        {
            try
            {
                bitmap = SKBitmap.Decode(data);
            }
            catch { /* Not a decodable bitmap */ }
        }

        if (bitmap != null)
        {
            try
            {
                using var image = SKImage.FromBitmap(bitmap);
                if (image is null)
                    return null;

                using var webp = image.Encode(SKEncodedImageFormat.Webp, 65);
                if (webp != null)
                    return webp.ToArray();
            }
            finally
            {
                bitmap.Dispose();
            }
        }

        return null;
    }
}
