using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Avalonia.Helper;

/// <summary>
/// Windows icons: thumbnails from the Shell API (IShellItemImageFactory) for exe, dll, ico, lnk, folders, etc.
/// </summary>
public static partial class ImageLoader
{
    private static partial IImage? LoadPlatformIcon(string path) => LoadShellThumbnail(path);

    private static partial Task<IImage?> LoadPlatformIconAsync(string path) => Task.FromResult(LoadShellThumbnail(path));

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
