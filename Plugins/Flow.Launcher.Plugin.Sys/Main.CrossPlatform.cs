using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Flow.Launcher.Plugin.Sys
{
#pragma warning disable FLAN0005 // The plugin context property is declared in Main.cs
    public partial class Main
#pragma warning restore FLAN0005
    {
        private const string OsaScript = "/usr/bin/osascript";
        private const string PmSet = "/usr/bin/pmset";
        private const string Open = "/usr/bin/open";
        // Removed in macOS 11; when present it locks immediately, unlike display sleep which only locks
        // if "require password after screen saver/display off" is set to immediately.
        private const string CGSession = "/System/Library/CoreServices/Menu Extras/User.menu/Contents/Resources/CGSession";

        // loginwindow shows the native macOS confirmation dialogs; System Events acts without asking.
        private const string ShutdownConfirmScript = "tell application \"loginwindow\" to «event aevtrsdn»";
        private const string ShutdownScript = "tell application \"System Events\" to shut down";
        private const string RestartConfirmScript = "tell application \"loginwindow\" to «event aevtrrst»";
        private const string RestartScript = "tell application \"System Events\" to restart";
        private const string LogOutConfirmScript = "tell application \"loginwindow\" to «event aevtlogo»";
        private const string LogOutScript = "tell application \"loginwindow\" to «event aevtrlgo»";
        private const string EmptyTrashScript = "tell application \"Finder\" to empty trash";

        private partial List<Result> SystemCommands()
        {
            if (OperatingSystem.IsLinux())
            {
                return LinuxSystemCommands();
            }

            // Power and session commands are only implemented for macOS and Linux on the cross-platform build
            if (!OperatingSystem.IsMacOS())
            {
                return [ExitCommand()];
            }

            var trashFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Trash");
            return
            [
                new Result
                {
                    Title = "Shutdown",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe7e8"),
                    IcoPath = "Images\\shutdown.png",
                    Action = c =>
                    {
                        Context.API.SaveAppAllSettings();
                        _ = RunCommandAsync(null, OsaScript, "-e",
                            _settings.SkipPowerActionConfirmation ? ShutdownScript : ShutdownConfirmScript);
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
                        Context.API.SaveAppAllSettings();
                        _ = RunCommandAsync(null, OsaScript, "-e",
                            _settings.SkipPowerActionConfirmation ? RestartScript : RestartConfirmScript);
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
                        _ = RunCommandAsync(null, OsaScript, "-e",
                            _settings.SkipPowerActionConfirmation ? LogOutScript : LogOutConfirmScript);
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
                        _ = File.Exists(CGSession)
                            ? RunCommandAsync(null, CGSession, "-suspend")
                            : RunCommandAsync(null, PmSet, "displaysleepnow");
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
                        _ = RunCommandAsync(null, PmSet, "sleepnow");
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
                        _ = RunCommandAsync(Localize.flowlauncher_plugin_sys_dlgtext_empty_recycle_bin_failed(Environment.NewLine),
                            OsaScript, "-e", EmptyTrashScript);
                        return true;
                    }
                },
                new Result
                {
                    Title = "Open Recycle Bin",
                    IcoPath = "Images\\openrecyclebin.png",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe74d"),
                    CopyText = trashFolder,
                    Action = c =>
                    {
                        _ = RunCommandAsync(null, Open, trashFolder);
                        return true;
                    }
                },
                ExitCommand()
            ];
        }

        private static Result ExitCommand() => new()
        {
            Title = "Exit",
            IcoPath = "Images\\app.png",
            Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe89f"),
            Action = c =>
            {
                Context.API.ExitApp();
                return true;
            }
        };

        /// <summary>
        /// Runs a command and reports a failure to the user, e.g. osascript fails when Flow Launcher has not
        /// been allowed to control the target app (macOS Automation privacy setting), or systemctl/loginctl
        /// fail when polkit denies the action on Linux.
        /// </summary>
        /// <param name="failureMessage">Message shown on failure; the command's error output when null.</param>
        private static async Task RunCommandAsync(string failureMessage, string fileName, params string[] arguments)
        {
            var commandLine = $"{fileName} {string.Join(' ', arguments)}";
            var startInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            string error;
            try
            {
                using var process = Process.Start(startInfo)!;
                error = (await process.StandardError.ReadToEndAsync()).Trim();
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    return;
                }

                Context.API.LogError(ClassName, $"<{commandLine}> exited with code {process.ExitCode}: {error}");
                if (string.IsNullOrEmpty(error))
                {
                    error = $"{commandLine} exited with code {process.ExitCode}";
                }
            }
            catch (Exception e)
            {
                Context.API.LogException(ClassName, $"Failed to run <{commandLine}>", e);
                error = e.Message;
            }

            Context.API.ShowMsgError(Localize.flowlauncher_plugin_sys_dlgtitle_error(), failureMessage ?? error);
        }

        #region Linux

        private const string LoginCtl = "loginctl";
        private const string SystemCtl = "systemctl";
        private const string XdgMime = "xdg-mime";
        private const string XdgOpen = "xdg-open";
        private const string TrashUri = "trash:///";

        private List<Result> LinuxSystemCommands()
        {
            // Freedesktop.org trash specification: the home trash lives in $XDG_DATA_HOME/Trash
            var trashFolder = Path.Combine(GetXdgDataHome(), "Trash");
            var trashFilesFolder = Path.Combine(trashFolder, "files");
            return
            [
                new Result
                {
                    Title = "Shutdown",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe7e8"),
                    IcoPath = "Images\\shutdown.png",
                    Action = c =>
                    {
                        if (ConfirmSessionAction(Localize.flowlauncher_plugin_sys_dlgtext_shutdown_computer(),
                            Localize.flowlauncher_plugin_sys_shutdown_computer()))
                        {
                            Context.API.SaveAppAllSettings();
                            _ = RunCommandAsync(null, GetPowerManager(), "poweroff");
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
                        if (ConfirmSessionAction(Localize.flowlauncher_plugin_sys_dlgtext_restart_computer(),
                            Localize.flowlauncher_plugin_sys_restart_computer()))
                        {
                            Context.API.SaveAppAllSettings();
                            _ = RunCommandAsync(null, GetPowerManager(), "reboot");
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
                        if (ConfirmSessionAction(Localize.flowlauncher_plugin_sys_dlgtext_logoff_computer(),
                            Localize.flowlauncher_plugin_sys_log_off()))
                        {
                            // Ending the session kills Flow Launcher with the rest of it
                            Context.API.SaveAppAllSettings();
                            var sessionId = Environment.GetEnvironmentVariable("XDG_SESSION_ID");
                            _ = string.IsNullOrEmpty(sessionId)
                                ? RunCommandAsync(null, LoginCtl, "terminate-user", GetUserName())
                                : RunCommandAsync(null, LoginCtl, "terminate-session", sessionId);
                        }
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
                        // logind forwards the request to the session's screen locker (GNOME, KDE, hyprlock via
                        // hypridle, swaylock via swayidle, ...). Without a known session, lock all of the user's sessions.
                        var sessionId = Environment.GetEnvironmentVariable("XDG_SESSION_ID");
                        _ = string.IsNullOrEmpty(sessionId)
                            ? RunCommandAsync(null, LoginCtl, "lock-sessions")
                            : RunCommandAsync(null, LoginCtl, "lock-session", sessionId);
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
                        _ = RunCommandAsync(null, GetPowerManager(), "suspend");
                        return true;
                    }
                },
                new Result
                {
                    Title = "Hibernate",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe8be"),
                    IcoPath = "Images\\hibernate.png",
                    Action = c =>
                    {
                        _ = RunCommandAsync(null, GetPowerManager(), "hibernate");
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
                        EmptyLinuxTrash(trashFolder);
                        return true;
                    }
                },
                new Result
                {
                    Title = "Open Recycle Bin",
                    IcoPath = "Images\\openrecyclebin.png",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe74d"),
                    CopyText = trashFilesFolder,
                    Action = c =>
                    {
                        _ = OpenLinuxTrashAsync(trashFilesFolder);
                        return true;
                    }
                },
                ExitCommand()
            ];
        }

        /// <summary>
        /// Linux has no system confirmation dialog for power actions (macOS uses loginwindow's),
        /// so ask through Flow Launcher like Windows does.
        /// </summary>
        private bool ConfirmSessionAction(string message, string caption) =>
            _settings.SkipPowerActionConfirmation
            || Context.API.ShowMsgBox(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

        /// <summary>
        /// systemctl on systemd distributions; loginctl from elogind (e.g. Void, Gentoo, Alpine) accepts the same verbs.
        /// </summary>
        private static string GetPowerManager() =>
            FindExecutable(SystemCtl) is not null || FindExecutable(LoginCtl) is null ? SystemCtl : LoginCtl;

        private static string GetUserName() =>
            Environment.GetEnvironmentVariable("USER") is { Length: > 0 } user ? user : Environment.UserName;

        private static string GetXdgDataHome()
        {
            // The base directory specification says relative values are invalid and must be ignored
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            return !string.IsNullOrEmpty(dataHome) && Path.IsPathRooted(dataHome)
                ? dataHome
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        /// <summary>
        /// Searches PATH like the shell does; distributions such as NixOS keep nothing in /usr/bin.
        /// </summary>
        private static string FindExecutable(string name)
        {
            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(directory, name);
                try
                {
                    if (File.Exists(candidate)
                        && (File.GetUnixFileMode(candidate) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0)
                    {
                        return candidate;
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // Unreadable PATH entries are skipped like the shell does
                }
            }

            return null;
        }

        /// <summary>
        /// Empties the home trash: the trashed items in Trash/files, their metadata in Trash/info and the
        /// directory size cache. Asks first, like the Windows recycle bin does.
        /// </summary>
        private static void EmptyLinuxTrash(string trashFolder)
        {
            var entries = new[] { "files", "info" }
                .Select(name => new DirectoryInfo(Path.Combine(trashFolder, name)))
                .Where(directory => directory.Exists)
                .SelectMany(directory => directory.EnumerateFileSystemInfos())
                .ToList();
            var directorySizes = new FileInfo(Path.Combine(trashFolder, "directorysizes"));
            if (entries.Count == 0)
            {
                return;
            }

            if (Context.API.ShowMsgBox(
                    Localize.flowlauncher_plugin_sys_dlgtext_empty_recycle_bin(),
                    Localize.flowlauncher_plugin_sys_emptyrecyclebin(),
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            _ = Task.Run(() =>
            {
                var failed = false;
                foreach (var entry in entries)
                {
                    try
                    {
                        DeleteTrashEntry(entry);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        Context.API.LogException(ClassName, $"Failed to delete <{entry.FullName}> from the trash", e);
                        failed = true;
                    }
                }

                try
                {
                    directorySizes.Delete();
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // Only a cache of the trashed directories' sizes; file managers rebuild it
                    Context.API.LogWarn(ClassName, $"Failed to delete <{directorySizes.FullName}>: {e.Message}");
                }

                if (failed)
                {
                    Context.API.ShowMsgError(Localize.flowlauncher_plugin_sys_dlgtitle_error(),
                        Localize.flowlauncher_plugin_sys_dlgtext_empty_recycle_bin_failed(Environment.NewLine));
                }
            });
        }

        private static void DeleteTrashEntry(FileSystemInfo entry)
        {
            // Symbolic links are removed themselves; never follow them out of the trash
            if (entry is not DirectoryInfo directory || entry.LinkTarget is not null)
            {
                entry.Delete();
                return;
            }

            try
            {
                directory.Delete(true);
            }
            catch (UnauthorizedAccessException)
            {
                // Trashed read-only directories (e.g. a Go module cache) cannot be emptied until they are writable again
                MakeDirectoriesWritable(directory);
                directory.Delete(true);
            }
        }

        private static void MakeDirectoriesWritable(DirectoryInfo directory)
        {
            directory.UnixFileMode |= UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            foreach (var child in directory.EnumerateDirectories())
            {
                if (child.LinkTarget is null)
                {
                    MakeDirectoriesWritable(child);
                }
            }
        }

        /// <summary>
        /// Opens trash:/// when a file manager is registered for it (GNOME Files, Dolphin, Nemo, ...), so restoring
        /// items works; otherwise shows the folder holding the trashed files in the configured file manager.
        /// </summary>
        private static async Task OpenLinuxTrashAsync(string trashFilesFolder)
        {
            if (await HasTrashUriHandlerAsync())
            {
                await RunCommandAsync(null, XdgOpen, TrashUri);
                return;
            }

            try
            {
                // Trash implementations create the folder on first use; it must be private to the user
                Directory.CreateDirectory(trashFilesFolder,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Context.API.LogException(ClassName, $"Failed to create <{trashFilesFolder}>", e);
            }

            Context.API.OpenDirectory(trashFilesFolder);
        }

        private static async Task<bool> HasTrashUriHandlerAsync()
        {
            if (FindExecutable(XdgOpen) is null || FindExecutable(XdgMime) is null)
            {
                return false;
            }

            var startInfo = new ProcessStartInfo(XdgMime)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("query");
            startInfo.ArgumentList.Add("default");
            startInfo.ArgumentList.Add("x-scheme-handler/trash");

            try
            {
                using var process = Process.Start(startInfo)!;
                var error = process.StandardError.ReadToEndAsync();
                var handler = (await process.StandardOutput.ReadToEndAsync()).Trim();
                await error;
                await process.WaitForExitAsync();
                return process.ExitCode == 0 && handler.EndsWith(".desktop", StringComparison.Ordinal);
            }
            catch (Exception e)
            {
                Context.API.LogException(ClassName, "Failed to query the trash:// handler", e);
                return false;
            }
        }

        #endregion
    }
}
