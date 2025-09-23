using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using IniParser;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;

namespace Flow.Launcher.Infrastructure.Image
{
    /// <summary>
    /// Subclass of <see cref="SIIGBF"/>
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

        private static readonly HRESULT S_EXTRACTIONFAILED = (HRESULT)0x8004B200;

        private static readonly HRESULT S_PATHNOTFOUND = (HRESULT)0x8004B205;

        private const string UrlExtension = ".url";

        public static BitmapSource GetThumbnail(string fileName, int width, int height, ThumbnailOptions options)
        {
            HBITMAP hBitmap;

            var extension = Path.GetExtension(fileName);
            if (string.Equals(extension, UrlExtension, StringComparison.OrdinalIgnoreCase))
            {
                hBitmap = GetHBitmapForUrlFile(fileName, width, height, options);
            }
            else
            {
                hBitmap = GetHBitmap(Path.GetFullPath(fileName), width, height, options);
            }

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
                catch (COMException ex) when (options == ThumbnailOptions.ThumbnailOnly &&
                    (ex.HResult == S_PATHNOTFOUND || ex.HResult == S_EXTRACTIONFAILED))
                {
                    // Fallback to IconOnly if extraction fails or files cannot be found
                    imageFactory.GetImage(size, (SIIGBF)ThumbnailOptions.IconOnly, &hBitmap);
                }
                catch (FileNotFoundException) when (options == ThumbnailOptions.ThumbnailOnly)
                {
                    // Fallback to IconOnly if files cannot be found
                    imageFactory.GetImage(size, (SIIGBF)ThumbnailOptions.IconOnly, &hBitmap);
                }
                catch (System.Exception ex)
                {
                    // Handle other exceptions
                    throw new InvalidOperationException("Failed to get thumbnail", ex);
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

        private static unsafe HBITMAP GetHBitmapForUrlFile(string fileName, int width, int height, ThumbnailOptions options)
        {
            HBITMAP hBitmap;

            try
            {
                var parser = new FileIniDataParser();
                var data = parser.ReadFile(fileName);
                var urlSection = data["InternetShortcut"];

                var iconPath = urlSection?["IconFile"];
                if (string.IsNullOrEmpty(iconPath))
                {
                    throw new FileNotFoundException("Icon file not specified in Internet shortcut (.url) file.");
                }
                hBitmap = GetHBitmap(Path.GetFullPath(iconPath), width, height, options);
            }
            catch
            {
                try
                {
                    hBitmap = GetHBitmap(Path.GetFullPath(fileName), width, height, options);
                }
                catch (System.Exception ex)
                {
                    // Handle other exceptions
                    throw new InvalidOperationException("Failed to get thumbnail", ex);
                }
            }

            return hBitmap;
        }
    }
}
