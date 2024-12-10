using System.Linq;
using System.Text;
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
        var wallpaper = new StringBuilder(MAX_PATH);
        PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETDESKWALLPAPER, (uint)MAX_PATH, &wallpaper, 0);

        var str = wallpaper.ToString();
        if (string.IsNullOrEmpty(str))
            return null;

        return str;
    }

    public static Color GetWallpaperColor()
    {
        RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors", true);
        var result = key?.GetValue("Background", null);
        if (result is string strResult)
        {
            try
            {
                var parts = strResult.Trim().Split(new[] {' '}, 3).Select(byte.Parse).ToList();
                return Color.FromRgb(parts[0], parts[1], parts[2]);
            }
            catch
            {
            }
        }
        return Colors.Transparent;
    }
}
