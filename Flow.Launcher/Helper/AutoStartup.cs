using System;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Microsoft.Win32;

namespace Flow.Launcher.Helper;

public class AutoStartup
{
    private const string StartupPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
                var path = key?.GetValue(Constant.FlowLauncher) as string;
                return path == Constant.ExecutablePath;
            }
            catch (Exception e)
            {
                Log.Error("AutoStartup", $"Ignoring non-critical registry error (querying if enabled): {e}");
            }

            return false;
        }
    }

    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
            key?.DeleteValue(Constant.FlowLauncher, false);
        }
        catch (Exception e)
        {
            Log.Error("AutoStartup", $"Failed to disable auto-startup: {e}");
            throw;
        }
    }

    internal static void Enable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
            key?.SetValue(Constant.FlowLauncher, $"\"{Constant.ExecutablePath}\"");
        }
        catch (Exception e)
        {
            Log.Error("AutoStartup", $"Failed to enable auto-startup: {e}");
            throw;
        }
    }
}
