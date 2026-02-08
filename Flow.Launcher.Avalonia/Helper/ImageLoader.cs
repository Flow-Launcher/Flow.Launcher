using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
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
/// Uses Windows Shell API for exe/ico thumbnails.
/// </summary>
public static class ImageLoader
{
    private static readonly string ClassName = nameof(ImageLoader);

    // Thread-safe cache
    private static readonly ConcurrentDictionary<string, IImage?> _cache = new();
    private static readonly HttpClient _httpClient = new();

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
    /// Use with Avalonia's ^ binding operator: {Binding Image^}
    /// </summary>
    public static Task<IImage?> LoadAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(DefaultImage);

        // Check cache first - return immediately without Task.Run overhead
        if (_cache.TryGetValue(path, out var cached))
            return Task.FromResult(cached);

        // Load on background thread to avoid blocking UI
        return Task.Run(() => LoadCore(path));
    }

    /// <summary>
    /// Core loading logic - runs on thread pool when not cached.
    /// </summary>
    private static async Task<IImage?> LoadCore(string path)
    {
        // Double-check cache (another thread may have loaded it)
        if (_cache.TryGetValue(path, out var cached))
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
                image = LoadFromFile(path);
            }
            else if (Directory.Exists(path))
            {
                // Folder - get shell icon
                image = LoadShellThumbnail(path);
            }

            // Cache the result (even if null, to avoid repeated attempts)
            image ??= DefaultImage;
            _cache.TryAdd(path, image);

            return image;
        }
        catch (Exception ex)
        {
            Log.Debug(ClassName, $"Failed to load image: {path}, Error: {ex.Message}");
            _cache.TryAdd(path, DefaultImage);
            return DefaultImage;
        }
    }

    private static IImage? LoadFromFile(string path)
    {
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();

            // For standard image formats, load directly
            if (Array.Exists(DirectLoadExtensions, e => e == ext))
            {
                using var stream = File.OpenRead(path);
                return new Bitmap(stream);
            }

            // For exe, dll, ico, lnk, and other files - use Windows Shell API
            return LoadShellThumbnail(path);
        }
        catch (Exception ex)
        {
            Log.Debug(ClassName, $"Failed to load file: {path}, Error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Load thumbnail/icon using Windows Shell API (IShellItemImageFactory).
    /// Works for exe, dll, ico, folders, and any file type.
    /// </summary>
    private static IImage? LoadShellThumbnail(string path, int size = 64)
    {
        try
        {
            var hr = SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var shellItem);
            if (hr != 0 || shellItem == null)
            {
                Log.Debug(ClassName, $"SHCreateItemFromParsingName failed for {path}, hr={hr}");
                return null;
            }

            try
            {
                var imageFactory = (IShellItemImageFactory)shellItem;
                var sz = new SIZE { cx = size, cy = size };

                // Try to get thumbnail, fall back to icon
                hr = imageFactory.GetImage(sz, SIIGBF.SIIGBF_BIGGERSIZEOK, out var hBitmap);
                if (hr != 0 || hBitmap == IntPtr.Zero)
                {
                    // Fallback to icon only
                    hr = imageFactory.GetImage(sz, SIIGBF.SIIGBF_ICONONLY, out hBitmap);
                }

                if (hr != 0 || hBitmap == IntPtr.Zero)
                {
                    Log.Debug(ClassName, $"GetImage failed for {path}, hr={hr}");
                    return null;
                }

                try
                {
                    return ConvertHBitmapToAvaloniaBitmap(hBitmap);
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(shellItem);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ClassName, $"Failed to load shell thumbnail: {path}, Error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Convert Windows HBITMAP to Avalonia Bitmap.
    /// </summary>
    private static Bitmap? ConvertHBitmapToAvaloniaBitmap(IntPtr hBitmap)
    {
        // Get bitmap info
        var bmp = new BITMAP();
        if (GetObject(hBitmap, Marshal.SizeOf<BITMAP>(), ref bmp) == 0)
            return null;

        var width = bmp.bmWidth;
        var height = bmp.bmHeight;

        // Create BITMAPINFO for DIB (top-down, 32-bit BGRA)
        var bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // Negative = top-down DIB
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0 // BI_RGB
            }
        };

        // Allocate buffer for pixel data
        var stride = width * 4;
        var bufferSize = stride * height;
        var buffer = new byte[bufferSize];

        // Get the device context and extract DIB bits
        var hdc = CreateCompatibleDC(IntPtr.Zero);
        try
        {
            if (GetDIBits(hdc, hBitmap, 0, (uint)height, buffer, ref bmi, 0) == 0)
                return null;

            // Analyze alpha channel to determine if image has transparency
            bool hasTransparent = false;
            bool hasOpaque = false;
            bool hasPartialAlpha = false;
            
            for (int i = 3; i < bufferSize; i += 4)
            {
                byte a = buffer[i];
                if (a == 0) hasTransparent = true;
                else if (a == 255) hasOpaque = true;
                else hasPartialAlpha = true;
                
                // Early exit once we know it has alpha
                if (hasPartialAlpha || (hasTransparent && hasOpaque))
                    break;
            }

            bool hasAlpha = hasPartialAlpha || (hasTransparent && hasOpaque);

            // If no alpha channel data, set all alpha to 255 (fully opaque)
            if (!hasAlpha)
            {
                for (int i = 3; i < bufferSize; i += 4)
                {
                    buffer[i] = 255;
                }
            }

            // Create Avalonia bitmap from pixel data
            // Use Unpremul - this correctly renders transparent icons without white borders
            var bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                global::Avalonia.Platform.PixelFormat.Bgra8888,
                global::Avalonia.Platform.AlphaFormat.Unpremul);

            using (var fb = bitmap.Lock())
            {
                Marshal.Copy(buffer, 0, fb.Address, bufferSize);
            }

            return bitmap;
        }
        finally
        {
            DeleteDC(hdc);
        }
    }

    private static async Task<IImage?> LoadFromUrlAsync(string url)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
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

            // Try ICO via shell
            defaultIconPath = Path.Combine(appDir, "Images", "app.ico");
            if (File.Exists(defaultIconPath))
            {
                return LoadShellThumbnail(defaultIconPath);
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
        if (!string.IsNullOrWhiteSpace(path) && _cache.TryGetValue(path, out image))
            return true;
        image = null;
        return false;
    }

    /// <summary>
    /// Clear the image cache.
    /// </summary>
    public static void ClearCache() => _cache.Clear();

    #region Windows Shell API P/Invoke

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [Flags]
    private enum SIIGBF
    {
        SIIGBF_RESIZETOFIT = 0x00,
        SIIGBF_BIGGERSIZEOK = 0x01,
        SIIGBF_MEMORYONLY = 0x02,
        SIIGBF_ICONONLY = 0x04,
        SIIGBF_THUMBNAILONLY = 0x08,
        SIIGBF_INCACHEONLY = 0x10
    }

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, ref BITMAP lpvObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        [Out] byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public uint[] bmiColors;
    }

    #endregion
}
