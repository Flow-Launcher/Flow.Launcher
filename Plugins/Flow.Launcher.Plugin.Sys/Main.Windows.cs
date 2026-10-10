using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Shutdown;
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;

namespace Flow.Launcher.Plugin.Sys
{
#pragma warning disable FLAN0005 // The plugin context property is declared in Main.cs
    public partial class Main
#pragma warning restore FLAN0005
    {
        // SHTDN_REASON_MAJOR_OTHER indicates a generic shutdown reason that isn't categorized under hardware failure,
        // software updates, or other predefined reasons.
        // SHTDN_REASON_FLAG_PLANNED marks the shutdown as planned rather than an unexpected shutdown or failure
        private const SHUTDOWN_REASON REASON = SHUTDOWN_REASON.SHTDN_REASON_MAJOR_OTHER |
            SHUTDOWN_REASON.SHTDN_REASON_FLAG_PLANNED;

        public Control CreateSettingPanel()
        {
            UpdateLocalizedNameDescription(false);
            return new SysSettings(_viewModel);
        }

        private static unsafe bool EnableShutdownPrivilege()
        {
            try
            {
                if (!PInvoke.OpenProcessToken(Process.GetCurrentProcess().SafeHandle, TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | TOKEN_ACCESS_MASK.TOKEN_QUERY, out var tokenHandle))
                {
                    return false;
                }

                if (!PInvoke.LookupPrivilegeValue(null, PInvoke.SE_SHUTDOWN_NAME, out var luid))
                {
                    return false;
                }

                var privileges = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Privileges = new() { e0 = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = TOKEN_PRIVILEGES_ATTRIBUTES.SE_PRIVILEGE_ENABLED } }
                };

                if (!PInvoke.AdjustTokenPrivileges(tokenHandle, false, &privileges, null, out var _))
                {
                    return false;
                }

                if (Marshal.GetLastWin32Error() != (int)WIN32_ERROR.NO_ERROR)
                {
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private partial List<Result> SystemCommands()
        {
            var recycleBinFolder = "shell:RecycleBinFolder";
            return
            [
                new Result
                {
                    Title = "Shutdown",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe7e8"),
                    IcoPath = "Images\\shutdown.png",
                    Action = c =>
                    {
                        var result = _settings.SkipPowerActionConfirmation
                            ? MessageBoxResult.Yes
                            : Context.API.ShowMsgBox(
                                Localize.flowlauncher_plugin_sys_dlgtext_shutdown_computer(),
                                Localize.flowlauncher_plugin_sys_shutdown_computer(),
                                MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            Context.API.SaveAppAllSettings();
                            if (EnableShutdownPrivilege())
                                PInvoke.ExitWindowsEx(EXIT_WINDOWS_FLAGS.EWX_SHUTDOWN | EXIT_WINDOWS_FLAGS.EWX_POWEROFF, REASON);
                            else
                                Process.Start("shutdown", "/s /t 0");
                        }
                        return true;
                    }
                },
                new Result
                {
                    Title = "Restart",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe777"),
                    IcoPath = "Images\\restart.png",
                    Action = c =>
                    {
                        var result = _settings.SkipPowerActionConfirmation
                            ? MessageBoxResult.Yes
                            : Context.API.ShowMsgBox(
                                Localize.flowlauncher_plugin_sys_dlgtext_restart_computer(),
                                Localize.flowlauncher_plugin_sys_restart_computer(),
                                MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            Context.API.SaveAppAllSettings();
                            if (EnableShutdownPrivilege())
                                PInvoke.ExitWindowsEx(EXIT_WINDOWS_FLAGS.EWX_REBOOT, REASON);
                            else
                                Process.Start("shutdown", "/r /t 0");
                        }
                        return true;
                    }
                },
                new Result
                {
                    Title = "Restart With Advanced Boot Options",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xecc5"),
                    IcoPath = "Images\\restart_advanced.png",
                    Action = c =>
                    {
                        var result = _settings.SkipPowerActionConfirmation
                            ? MessageBoxResult.Yes
                            : Context.API.ShowMsgBox(
                                Localize.flowlauncher_plugin_sys_dlgtext_restart_computer_advanced(),
                                Localize.flowlauncher_plugin_sys_restart_computer(),
                                MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            // Save settings before advanced restart to avoid data loss
                            Context.API.SaveAppAllSettings();

                            if (EnableShutdownPrivilege())
                                PInvoke.ExitWindowsEx(EXIT_WINDOWS_FLAGS.EWX_REBOOT | EXIT_WINDOWS_FLAGS.EWX_BOOTOPTIONS, REASON);
                            else
                                Process.Start("shutdown", "/r /o /t 0");
                        }
                        return true;
                    }
                },
                new Result
                {
                    Title = "Log Off/Sign Out",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe77b"),
                    IcoPath = "Images\\logoff.png",
                    Action = c =>
                    {
                        var result = _settings.SkipPowerActionConfirmation
                            ? MessageBoxResult.Yes
                            : Context.API.ShowMsgBox(
                                Localize.flowlauncher_plugin_sys_dlgtext_logoff_computer(),
                                Localize.flowlauncher_plugin_sys_log_off(),
                                MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                            PInvoke.ExitWindowsEx(EXIT_WINDOWS_FLAGS.EWX_LOGOFF, REASON);
                        return true;
                    }
                },
                new Result
                {
                    Title = "Lock",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe72e"),
                    IcoPath = "Images\\lock.png",
                    Action = c =>
                    {
                        PInvoke.LockWorkStation();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Sleep",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xec46"),
                    IcoPath = "Images\\sleep.png",
                    Action = c =>
                    {
                        PInvoke.SetSuspendState(false, false, false);
                        return true;
                    }
                },
                new Result
                {
                    Title = "Hibernate",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe8be"),
                    IcoPath = "Images\\hibernate.png",
                    Action= c =>
                    {
                        PInvoke.SetSuspendState(true, false, false);
                        return true;
                    }
                },
                 new Result
                {
                    Title = "Index Option",
                    IcoPath = "Images\\indexoption.png",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe773"),
                    Action = c =>
                    {
                        Process.Start("control.exe", "srchadmin.dll");
                        return true;
                    }
                },
                new Result
                {
                    Title = "Empty Recycle Bin",
                    IcoPath = "Images\\recyclebin.png",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xea99"),
                    Action = c =>
                    {
                        // http://www.pinvoke.net/default.aspx/shell32/SHEmptyRecycleBin.html
                        // FYI, couldn't find documentation for this but if the recycle bin is already empty, it will return -2147418113 (0x8000FFFF (E_UNEXPECTED))
                        // 0 for nothing
                        var result = PInvoke.SHEmptyRecycleBin(new(), string.Empty, 0);
                        if (result != HRESULT.S_OK && result != HRESULT.E_UNEXPECTED)
                        {
                            Context.API.ShowMsgBox(
                                Localize.flowlauncher_plugin_sys_dlgtext_empty_recycle_bin_failed(Environment.NewLine),
                                Localize.flowlauncher_plugin_sys_dlgtitle_error(),
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        return true;
                    }
                },
                new Result
                {
                    Title = "Open Recycle Bin",
                    IcoPath = "Images\\openrecyclebin.png",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe74d"),
                    CopyText = recycleBinFolder,
                    Action = c =>
                    {
                        Process.Start("explorer", recycleBinFolder);
                        return true;
                    }
                },
                new Result
                {
                    Title = "Exit",
                    IcoPath = "Images\\app.png",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe89f"),
                    Action = c =>
                    {
                        Context.API.HideMainWindow();
                        Application.Current.MainWindow.Close();
                        return true;
                    }
                }
            ];
        }
    }
}
