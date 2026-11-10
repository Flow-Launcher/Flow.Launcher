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

        private const uint WmSysCommand = 0x0112;
        private const int ScMonitorPower = 0xF170;
        private static readonly IntPtr HwndBroadcast = new(0xFFFF);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SendNotifyMessage(
            IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public Control CreateSettingPanel()
        {
            UpdateLocalizedNameDescription(false);
            return new SysSettings(_viewModel);
        }

        private static bool EnableShutdownPrivilege() => EnableShutdownPrivilege(out _);

        /// <param name="previousState">What to pass to <see cref="RestorePrivileges"/> to undo the change.</param>
        private static unsafe bool EnableShutdownPrivilege(out TOKEN_PRIVILEGES previousState)
        {
            previousState = default;
            try
            {
                var tokenOpened = PInvoke.OpenProcessToken(Process.GetCurrentProcess().SafeHandle, TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | TOKEN_ACCESS_MASK.TOKEN_QUERY, out var tokenHandle);
                using var token = tokenHandle;
                if (!tokenOpened)
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

                Span<byte> previous = stackalloc byte[sizeof(TOKEN_PRIVILEGES)];
                if (!PInvoke.AdjustTokenPrivileges(token, false, &privileges, previous, out var _))
                {
                    return false;
                }

                if (Marshal.GetLastWin32Error() != (int)WIN32_ERROR.NO_ERROR)
                {
                    return false;
                }

                previousState = MemoryMarshal.Read<TOKEN_PRIVILEGES>(previous);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Puts back the privileges <see cref="EnableShutdownPrivilege(out TOKEN_PRIVILEGES)"/> changed.
        /// An empty state (the privilege was already enabled) changes nothing.
        /// </summary>
        private static unsafe void RestorePrivileges(TOKEN_PRIVILEGES previousState)
        {
            if (previousState.PrivilegeCount == 0)
            {
                return;
            }

            try
            {
                var tokenOpened = PInvoke.OpenProcessToken(Process.GetCurrentProcess().SafeHandle, TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | TOKEN_ACCESS_MASK.TOKEN_QUERY, out var tokenHandle);
                using var token = tokenHandle;
                if (!tokenOpened)
                {
                    Context.API.LogWarn(ClassName, "Failed to reopen the process token while restoring the shutdown privilege");
                    return;
                }

                Marshal.SetLastPInvokeError((int)WIN32_ERROR.NO_ERROR);
                if (!PInvoke.AdjustTokenPrivileges(token, false, &previousState, null, out var _) ||
                    Marshal.GetLastPInvokeError() == (int)WIN32_ERROR.ERROR_NOT_ALL_ASSIGNED)
                {
                    Context.API.LogWarn(ClassName, "Failed to restore the shutdown privilege");
                }
            }
            catch (Exception)
            {
                Context.API.LogWarn(ClassName, "Failed to restore the shutdown privilege");
            }
        }

        private static void Suspend(bool hibernate)
        {
            if (!hibernate)
            {
                if (!PInvoke.GetPwrCapabilities(out var capabilities))
                {
                    Context.API.LogWarn(ClassName, $"Failed to read system power capabilities: {Marshal.GetLastPInvokeError()}");
                    ShowSuspendFailed();
                    return;
                }

                if (capabilities.AoAc)
                {
                    // On S0 systems SetSuspendState may hibernate instead of entering Modern Standby.
                    // Turning off the display can start a Modern Standby session, but Windows controls
                    // whether and when the system reaches the low-power sleep phase.
                    // SendNotifyMessage does not wait for other windows to process the broadcast.
                    Marshal.SetLastPInvokeError(0);
                    if (SendNotifyMessage(HwndBroadcast, WmSysCommand, new IntPtr(ScMonitorPower),
                        new IntPtr(2)))
                    {
                        return;
                    }

                    Context.API.LogWarn(ClassName, $"Failed to request display off for Modern Standby: {Marshal.GetLastPInvokeError()}");
                    ShowSuspendFailed();
                    return;
                }
            }

            // SetSuspendState requires the SE_SHUTDOWN_NAME privilege. Flow keeps running
            // afterwards, so the privilege is disabled again once the call returns.
            if (!EnableShutdownPrivilege(out var previousState))
            {
                Context.API.LogWarn(ClassName, "Failed to enable the shutdown privilege");
            }
            else
            {
                bool suspended;
                try
                {
                    suspended = PInvoke.SetSuspendState(hibernate, false, false);
                }
                finally
                {
                    RestorePrivileges(previousState);
                }

                if (suspended)
                {
                    return;
                }
            }

            ShowSuspendFailed();
        }

        private static void ShowSuspendFailed()
        {
            Context.API.ShowMsgBox(
                Localize.flowlauncher_plugin_sys_dlgtext_suspend_failed(),
                Localize.flowlauncher_plugin_sys_dlgtitle_error(),
                MessageBoxButton.OK, MessageBoxImage.Error);
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
                        Suspend(hibernate: false);
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
                        Suspend(hibernate: true);
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
