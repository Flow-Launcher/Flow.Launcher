using System;
using Microsoft.Win32;

namespace Flow.Launcher.Helper;

internal static class WindowsMediaPlayerHelper
{
    private static readonly string ClassName = nameof(WindowsMediaPlayerHelper);

    internal static bool IsWindowsMediaPlayerInstalled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\MediaPlayer");
            return key?.GetValue("Installation Directory") != null;
        }
        catch (Exception e)
        {
            App.API.LogException(ClassName, "Failed to check if Windows Media Player is installed", e);
            return false;
        }
    }
}
