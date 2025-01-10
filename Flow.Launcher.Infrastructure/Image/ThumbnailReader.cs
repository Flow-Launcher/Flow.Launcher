using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.Graphics.Gdi;

namespace Flow.Launcher.Infrastructure.Image
{
    /// <summary>
    /// Subclass of <see cref="Windows.Win32.UI.Shell.SIIGBF"/>
    /// </summary>
    [Flags]
    public enum ThumbnailOptions
    {
        None = 0x00,
        BiggerSizeOk = 0x01,
        InMemoryOnly = 0x02,
        IconOnly = 0x04,
        ThumbnailOnly = 0x08,
        InCacheOnly = 0x10,
    }

    public class WindowsThumbnailProvider
    {
        // Based on https://stackoverflow.com/questions/21751747/extract-thumbnail-for-any-file-in-windows

        private static readonly Guid GUID_IShellItem = typeof(IShellItem).GUID;

        private static readonly HRESULT S_ExtractionFailed = (HRESULT)0x8004B200;

        public static BitmapSource GetThumbnail(string fileName, int width, int height, ThumbnailOptions options)
        {
            HBITMAP hBitmap = GetHBitmap(Path.GetFullPath(fileName), width, height, options);

            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                // delete HBitmap to avoid memory leaks
                PInvoke.DeleteObject(hBitmap);
            }
        }

        private static unsafe HBITMAP GetHBitmap(string fileName, int width, int height, ThumbnailOptions options)
        {
            var retCode = PInvoke.SHCreateItemFromParsingName(
                fileName,
                null,
                GUID_IShellItem,
                out var nativeShellItem);

            if (retCode != HRESULT.S_OK)
                throw Marshal.GetExceptionForHR(retCode);

            if (nativeShellItem is not IShellItemImageFactory imageFactory)
            {
                Marshal.ReleaseComObject(nativeShellItem);
                nativeShellItem = null;
                throw new InvalidOperationException("Failed to get IShellItemImageFactory");
            }

            SIZE size = new SIZE
            {
                cx = width,
                cy = height
            };

            HBITMAP hBitmap = default;
            try
            {
                try
                {
                    imageFactory.GetImage(size, (SIIGBF)options, &hBitmap);
                }
                catch (COMException ex) when (ex.HResult == S_ExtractionFailed && options == ThumbnailOptions.ThumbnailOnly)
                {
                    // Fallback to IconOnly if ThumbnailOnly fails
                    imageFactory.GetImage(size, (SIIGBF)ThumbnailOptions.IconOnly, &hBitmap);
                }
            }
            finally
            {
                if (nativeShellItem != null)
                {
                    Marshal.ReleaseComObject(nativeShellItem);
                }
            }

            return hBitmap;
        }
    }
}
