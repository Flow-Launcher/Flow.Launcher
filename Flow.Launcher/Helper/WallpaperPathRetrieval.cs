using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Helper;

public static class WallpaperPathRetrieval
{
    private static readonly int MAX_PATH = 260;

    private static readonly Dictionary<(string, DateTime), ImageBrush> wallpaperCache = new();

    public static Brush GetWallpaperBrush()
    {
        // Invoke the method on the UI thread
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            return Application.Current.Dispatcher.Invoke(GetWallpaperBrush);
        }

        var wallpaperPath = GetWallpaperPath();
        if (wallpaperPath is not null && File.Exists(wallpaperPath))
        {
            // Since the wallpaper file name can be the same (TranscodedWallpaper),
            // we need to add the last modified date to differentiate them
            var dateModified = File.GetLastWriteTime(wallpaperPath);
            wallpaperCache.TryGetValue((wallpaperPath, dateModified), out var cachedWallpaper);
            if (cachedWallpaper != null)
            {
                return cachedWallpaper;
            }

            // We should not dispose the memory stream since the bitmap is still in use
            var memStream = new MemoryStream(File.ReadAllBytes(wallpaperPath));
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = memStream;
            bitmap.DecodePixelWidth = 800;
            bitmap.DecodePixelHeight = 600;
            bitmap.EndInit();
            bitmap.Freeze(); // Make the bitmap thread-safe
            var wallpaperBrush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
            wallpaperBrush.Freeze(); // Make the brush thread-safe
            wallpaperCache.Add((wallpaperPath, dateModified), wallpaperBrush);
            return wallpaperBrush;
        }

        var wallpaperColor = GetWallpaperColor();
        return new SolidColorBrush(wallpaperColor);
    }

    private static unsafe string GetWallpaperPath()
    {
        var wallpaperPtr = stackalloc char[MAX_PATH];
        PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETDESKWALLPAPER, (uint)MAX_PATH,
            wallpaperPtr,
            0);
        var wallpaper = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(wallpaperPtr);
        
        return wallpaper.ToString();
    }

    private static Color GetWallpaperColor()
    {
        RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors", true);
        var result = key?.GetValue("Background", null);
        if (result is string strResult)
        {
            try
            {
                var parts = strResult.Trim().Split(new[] { ' ' }, 3).Select(byte.Parse).ToList();
                return Color.FromRgb(parts[0], parts[1], parts[2]);
            }
            catch
            {
            }
        }

        return Colors.Transparent;
    }
}
