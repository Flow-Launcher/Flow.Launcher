using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.Infrastructure;
using Microsoft.Win32;

namespace Flow.Launcher.Helper;

public static class WallpaperPathRetrieval
{
    private static readonly int MAX_CACHE_SIZE = 3;
    private static readonly Dictionary<(string, DateTime), ImageBrush> wallpaperCache = new();

    public static Brush GetWallpaperBrush()
    {
        // Invoke the method on the UI thread
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            return Application.Current.Dispatcher.Invoke(GetWallpaperBrush);
        }

        try
        {
            var wallpaperPath = Win32Helper.GetWallpaperPath();
            if (string.IsNullOrEmpty(wallpaperPath) || !File.Exists(wallpaperPath))
            {
                App.API.LogInfo(nameof(WallpaperPathRetrieval), $"Wallpaper path is invalid: {wallpaperPath}");
                var wallpaperColor = GetWallpaperColor();
                return new SolidColorBrush(wallpaperColor);
            }

            // Since the wallpaper file name can be the same (TranscodedWallpaper),
            // we need to add the last modified date to differentiate them
            var dateModified = File.GetLastWriteTime(wallpaperPath);
            wallpaperCache.TryGetValue((wallpaperPath, dateModified), out var cachedWallpaper);
            if (cachedWallpaper != null)
            {
                App.API.LogInfo(nameof(WallpaperPathRetrieval), "Using cached wallpaper");
                return cachedWallpaper;
            }

            // We should not dispose the memory stream since the bitmap is still in use
            var memStream = new MemoryStream(File.ReadAllBytes(wallpaperPath));
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = memStream;
            bitmap.EndInit();

            if (bitmap.PixelWidth == 0 || bitmap.PixelHeight == 0)
            {
                App.API.LogInfo(nameof(WallpaperPathRetrieval), $"Failed to load bitmap: Width={bitmap.PixelWidth}, Height={bitmap.PixelHeight}");
                return new SolidColorBrush(Colors.Transparent);
            }

            var originalWidth = bitmap.PixelWidth;
            var originalHeight = bitmap.PixelHeight;

            // Calculate the scaling factor to fit the image within 800x600 while preserving aspect ratio
            double widthRatio = 800.0 / originalWidth;
            double heightRatio = 600.0 / originalHeight;
            double scaleFactor = Math.Min(widthRatio, heightRatio);

            int decodedPixelWidth = (int)(originalWidth * scaleFactor);
            int decodedPixelHeight = (int)(originalHeight * scaleFactor);

            // Set DecodePixelWidth and DecodePixelHeight to resize the image while preserving aspect ratio
            bitmap = new BitmapImage();
            bitmap.BeginInit();
            memStream.Seek(0, SeekOrigin.Begin); // Reset stream position
            bitmap.StreamSource = memStream;
            bitmap.DecodePixelWidth = decodedPixelWidth;
            bitmap.DecodePixelHeight = decodedPixelHeight;
            bitmap.EndInit();
            bitmap.Freeze(); // Make the bitmap thread-safe
            var wallpaperBrush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
            wallpaperBrush.Freeze(); // Make the brush thread-safe

            // Manage cache size
            if (wallpaperCache.Count >= MAX_CACHE_SIZE)
            {
                // Remove the oldest wallpaper from the cache
                var oldestCache = wallpaperCache.Keys.OrderBy(k => k.Item2).FirstOrDefault();
                if (oldestCache != default)
                {
                    wallpaperCache.Remove(oldestCache);
                }
            }

            wallpaperCache.Add((wallpaperPath, dateModified), wallpaperBrush);
            return wallpaperBrush;

        }
        catch (Exception ex)
        {
            App.API.LogException(nameof(WallpaperPathRetrieval), "Error retrieving wallpaper", ex);
            return new SolidColorBrush(Colors.Transparent);
        }
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
            catch (Exception ex)
            {
                 App.API.LogException(nameof(WallpaperPathRetrieval), "Error parsing wallpaper color", ex);
            }
        }

        return Colors.Transparent;
    }
}
