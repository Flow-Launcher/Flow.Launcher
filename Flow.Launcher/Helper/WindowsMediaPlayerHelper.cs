using Microsoft.Win32;

namespace Flow.Launcher.Helper;

internal static class WindowsMediaPlayerHelper
{
    internal static bool IsWindowsMediaPlayerInstalled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\MediaPlayer");
            return key?.GetValue("Installation Directory") != null;
        }
        catch
        {
            return false;
        }
    }
}
