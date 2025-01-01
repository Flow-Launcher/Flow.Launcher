using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Flow.Launcher.Helper;

public static class WallpaperPathRetrieval
{
    private static readonly int MAX_PATH = 260;

    public static unsafe string GetWallpaperPath()
    {
        var wallpaperPtr = stackalloc char[MAX_PATH];
        PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETDESKWALLPAPER, (uint)MAX_PATH,
            wallpaperPtr,
            0);
        var wallpaper = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(wallpaperPtr);
        
        return wallpaper.ToString();
    }

    public static Color GetWallpaperColor()
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
