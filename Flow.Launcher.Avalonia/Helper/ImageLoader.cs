using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Avalonia-compatible image loader with caching support.
/// Loads images from file paths, URLs, and data URIs.
/// Icons for other files and folders come from the platform partial (Windows Shell API / macOS .icns).
/// </summary>
public static partial class ImageLoader
{
    private static readonly string ClassName = nameof(ImageLoader);

    // Completed images keyed by (path, decode width); 0 width = full size.
    private static readonly ConcurrentDictionary<(string Path, int Width), IImage?> _cache = new();

    // Default image (lazy loaded)
    private static IImage? _defaultImage;

    // Image file extensions that Avalonia can load directly
    private static readonly string[] DirectLoadExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"];

    /// <summary>
    /// Default image shown when no icon is available.
    /// </summary>
    public static IImage? DefaultImage => _defaultImage ??= LoadDefaultImage();

    /// <summary>
    /// Load an image from the given path asynchronously.
    /// Supports local files, HTTP/HTTPS URLs, and data URIs.
    /// <paramref name="decodeWidth"/> &gt; 0 caps the pixel width of directly decoded raster images.
    /// </summary>
    public static Task<IImage?> LoadAsync(string? path, int decodeWidth = 0)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(DefaultImage);

        var key = (path, decodeWidth);

        // Check cache first - return immediately without Task.Run overhead
        if (_cache.TryGetValue(key, out var cached))
            return Task.FromResult(cached);

        // Load on background thread to avoid blocking UI
        return Task.Run(() => LoadCore(path, decodeWidth));
    }

    /// <summary>
    /// Core loading logic - runs on thread pool when not cached.
    /// </summary>
    private static async Task<IImage?> LoadCore(string path, int decodeWidth)
    {
        // Double-check cache (another thread may have loaded it)
        if (_cache.TryGetValue((path, decodeWidth), out var cached))
            return cached;

        try
        {
            IImage? image = null;

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                image = await LoadFromUrlAsync(path);
            }
            else if (path.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                image = LoadFromDataUri(path);
            }
            else if (File.Exists(path))
            {
                image = await LoadFromFileAsync(path, decodeWidth);
            }
            else if (Directory.Exists(path))
            {
                // Folder - get platform icon
                image = await LoadPlatformIconAsync(path);
            }

            // Cache the result (even if null, to avoid repeated attempts)
            image ??= DefaultImage;
            _cache.TryAdd((path, decodeWidth), image);

            return image;
        }
        catch (Exception ex)
        {
            Log.Debug(ClassName, $"Failed to load image: {path}, Error: {ex.Message}");
            _cache.TryAdd((path, decodeWidth), DefaultImage);
            return DefaultImage;
        }
    }

    private static async Task<IImage?> LoadFromFileAsync(string path, int decodeWidth)
    {
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();

            // For standard image formats, load directly
            if (Array.Exists(DirectLoadExtensions, e => e == ext))
            {
                using var stream = File.OpenRead(path);
                return Downscale(new Bitmap(stream), decodeWidth);
            }

            // For exe, dll, ico, lnk, icns and other files - use the platform icon loader
            return await LoadPlatformIconAsync(path);
        }
        catch (Exception ex)
        {
            Log.Debug(ClassName, $"Failed to load file: {path}, Error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Shrinks bitmaps wider than <paramref name="decodeWidth"/> so small icon slots don't keep and filter
    /// full-resolution images. Never upscales; 0 keeps the original.
    /// </summary>
    private static Bitmap Downscale(Bitmap bitmap, int decodeWidth)
    {
        var size = bitmap.PixelSize;
        if (decodeWidth <= 0 || size.Width <= decodeWidth)
            return bitmap;

        var height = Math.Max(1, (int)Math.Round(size.Height * (double)decodeWidth / size.Width));
        var scaled = bitmap.CreateScaledBitmap(new PixelSize(decodeWidth, height), BitmapInterpolationMode.HighQuality);
        bitmap.Dispose();
        return scaled;
    }

    /// <summary>
    /// Synchronous icon load for files Avalonia can't decode directly (used for the bundled app.ico fallback).
    /// </summary>
    private static partial IImage? LoadPlatformIcon(string path);

    /// <summary>
    /// Icon/thumbnail for a file or folder that isn't a directly loadable image. Runs on the thread pool.
    /// </summary>
    private static partial Task<IImage?> LoadPlatformIconAsync(string path);

    private static async Task<IImage?> LoadFromUrlAsync(string url)
    {
        try
        {
            await using var stream = await Flow.Launcher.Infrastructure.Http.Http.GetStreamAsync(url);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            return new Bitmap(memoryStream);
        }
        catch (Exception ex)
        {
            Log.Debug(ClassName, $"Failed to load URL: {url}, Error: {ex.Message}");
            return null;
        }
    }

    private static IImage? LoadFromDataUri(string dataUri)
    {
        try
        {
            // Parse data URI: data:image/png;base64,xxxxx
            var commaIndex = dataUri.IndexOf(',');
            if (commaIndex < 0)
                return null;

            var base64Data = dataUri.Substring(commaIndex + 1);
            var imageData = Convert.FromBase64String(base64Data);

            using var stream = new MemoryStream(imageData);
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            Log.Debug(ClassName, $"Failed to parse data URI: {ex.Message}");
            return null;
        }
    }

    private static IImage? LoadDefaultImage()
    {
        try
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;

            // Try PNG first
            var defaultIconPath = Path.Combine(appDir, "Images", "app.png");
            if (File.Exists(defaultIconPath))
            {
                using var stream = File.OpenRead(defaultIconPath);
                return new Bitmap(stream);
            }

            // Try ICO via the platform loader
            defaultIconPath = Path.Combine(appDir, "Images", "app.ico");
            if (File.Exists(defaultIconPath))
            {
                return LoadPlatformIcon(defaultIconPath);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ClassName, $"Failed to load default image: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Try to get a cached image without loading.
    /// </summary>
    public static bool TryGetCached(string? path, out IImage? image)
    {
        if (!string.IsNullOrWhiteSpace(path) && _cache.TryGetValue((path, 0), out image))
            return true;
        image = null;
        return false;
    }

    /// <summary>
    /// Clear the image cache.
    /// </summary>
    public static void ClearCache() => _cache.Clear();
}
