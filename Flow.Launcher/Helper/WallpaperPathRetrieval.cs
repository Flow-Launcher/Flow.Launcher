using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Flow.Launcher.Infrastructure;
using Microsoft.Win32;

namespace Flow.Launcher.Helper;

public static class WallpaperPathRetrieval
{
    private static readonly string ClassName = nameof(WallpaperPathRetrieval);

    private const int MaxCacheSize = 3;
    private static readonly Dictionary<(string, DateTime), ImageBrush> WallpaperCache = new();
    private static readonly Lock CacheLock = new();

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
                App.API.LogInfo(ClassName, $"Wallpaper path is invalid: {wallpaperPath}");
                var wallpaperColor = GetWallpaperColor();
                return new SolidColorBrush(wallpaperColor);
            }

            // Since the wallpaper file name can be the same (TranscodedWallpaper),
            // we need to add the last modified date to differentiate them
            var dateModified = File.GetLastWriteTime(wallpaperPath);
            lock (CacheLock)
            {
                WallpaperCache.TryGetValue((wallpaperPath, dateModified), out var cachedWallpaper);
                if (cachedWallpaper != null)
                {
                    return cachedWallpaper;
                }
            }

            int originalWidth, originalHeight;
            using (var fileStream = File.OpenRead(wallpaperPath))
            {
                var decoder = BitmapDecoder.Create(fileStream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                var frame = decoder.Frames[0];
                originalWidth = frame.PixelWidth;
                originalHeight = frame.PixelHeight;
            }

            if (originalWidth == 0 || originalHeight == 0)
            {
                App.API.LogError(ClassName, $"Failed to load bitmap: Width={originalWidth}, Height={originalHeight}");
                return new SolidColorBrush(Colors.Transparent);
            }

            // Calculate the scaling factor to fit the image within 800x600 while preserving aspect ratio
            var widthRatio = 800.0 / originalWidth;
            var heightRatio = 600.0 / originalHeight;
            var scaleFactor = Math.Min(widthRatio, heightRatio);
            var decodedPixelWidth = (int)(originalWidth * scaleFactor);
            var decodedPixelHeight = (int)(originalHeight * scaleFactor);

            // Set DecodePixelWidth and DecodePixelHeight to resize the image while preserving aspect ratio
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(wallpaperPath);
            bitmap.DecodePixelWidth = decodedPixelWidth;
            bitmap.DecodePixelHeight = decodedPixelHeight;
            bitmap.EndInit();
            bitmap.Freeze(); // Make the bitmap thread-safe
            var wallpaperBrush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
            wallpaperBrush.Freeze(); // Make the brush thread-safe

            // Manage cache size
            lock (CacheLock)
            {
                if (WallpaperCache.Count >= MaxCacheSize)
                {
                    // Remove the oldest wallpaper from the cache
                    var oldestCache = WallpaperCache.Keys.OrderBy(k => k.Item2).FirstOrDefault();
                    if (oldestCache != default)
                    {
                        WallpaperCache.Remove(oldestCache);
                    }
                }

                WallpaperCache.Add((wallpaperPath, dateModified), wallpaperBrush);
                return wallpaperBrush;
            }
        }
        catch (Exception ex)
        {
            App.API.LogException(ClassName, "Error retrieving wallpaper", ex);
            return new SolidColorBrush(Colors.Transparent);
        }
    }

    private static Color GetWallpaperColor()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors", false);
        var result = key?.GetValue("Background", null);
        if (result is string strResult)
        {
            try
            {
                var parts = strResult.Trim().Split([' '], 3).Select(byte.Parse).ToList();
                return Color.FromRgb(parts[0], parts[1], parts[2]);
            }
            catch (Exception ex)
            {
                App.API.LogException(ClassName, "Error parsing wallpaper color", ex);
            }
        }

        return Colors.Transparent;
    }
}
