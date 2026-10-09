using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

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
            // System commands are only implemented for macOS on the cross-platform build
            if (!OperatingSystem.IsMacOS())
            {
                return [];
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
                }
            ];
        }

        /// <summary>
        /// Runs a macOS command and reports a failure to the user, e.g. osascript fails when
        /// Flow Launcher has not been allowed to control the target app (Automation privacy setting).
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
    }
}
