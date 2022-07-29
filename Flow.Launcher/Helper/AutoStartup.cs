using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Microsoft.Win32;

namespace Flow.Launcher.Helper
{
    public class AutoStartup
    {
        private const string StartupPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

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
                    Log.Error("AutoStartup", $"Ignoring non-critical registry error (user permissions?): {e}");
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
                Log.Error("AutoStartup", $"Ignoring non-critical registry error (user permissions?): {e}");
            }
        }

        internal static void Enable()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
                key?.SetValue(Constant.FlowLauncher, Constant.ExecutablePath);
            }
            catch (Exception e)
            {
                Log.Error("AutoStartup", $"Ignoring non-critical registry error (user permissions?): {e}");
            }

        }
    }
}
