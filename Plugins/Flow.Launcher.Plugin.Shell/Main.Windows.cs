using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Plugin.Shell.Views;
using WindowsInput;
using WindowsInput.Native;
using Control = System.Windows.Controls.Control;
using Keys = System.Windows.Forms.Keys;

namespace Flow.Launcher.Plugin.Shell
{
#pragma warning disable FLAN0005 // The plugin context property is declared in Main.cs
    public partial class Main
#pragma warning restore FLAN0005
    {
        private bool _winRStroked;
        private readonly KeyboardSimulator _keyboardSimulator = new(new InputSimulator());

        private static partial void ConfigureShellProcessStartInfo(
            ProcessStartInfo info,
            string command,
            Shell shell,
            bool leaveShellOpen,
            bool closeShellAfterPress,
            bool useWindowsTerminal,
            string closePrompt)
        {
            switch (shell)
            {
                case Shell.Cmd:
                    ConfigureCmdProcessStartInfo(
                        info,
                        command,
                        leaveShellOpen,
                        closeShellAfterPress,
                        useWindowsTerminal,
                        closePrompt);
                    break;

                case Shell.Powershell:
                    ConfigurePowershellProcessStartInfo(
                        info,
                        command,
                        leaveShellOpen,
                        closeShellAfterPress,
                        useWindowsTerminal,
                        closePrompt);
                    break;

                case Shell.Pwsh:
                    ConfigurePwshProcessStartInfo(
                        info,
                        command,
                        leaveShellOpen,
                        closeShellAfterPress,
                        useWindowsTerminal,
                        closePrompt);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        private static void ConfigureCmdProcessStartInfo(
            ProcessStartInfo info,
            string command,
            bool leaveShellOpen,
            bool closeShellAfterPress,
            bool useWindowsTerminal,
            string closePrompt)
        {
            var shellSwitch = leaveShellOpen ? "/k" : "/c";
            var commandToRun = $"{command}{(closeShellAfterPress ? $" && echo {closePrompt} && pause > nul" : "")}";

            if (useWindowsTerminal)
            {
                // Windows Terminal takes individual arguments via ArgumentList.
                info.FileName = "wt.exe";
                info.ArgumentList.Add("cmd");
                info.ArgumentList.Add(shellSwitch);
                info.ArgumentList.Add(commandToRun);
            }
            else
            {
                // Must use Arguments (not ArgumentList) 
                // so that quoted commands are passed to cmd.exe /c without backslash-escaping
                info.FileName = "cmd.exe";
                info.Arguments = $"{shellSwitch} {commandToRun}";
            }
        }

        private static void ConfigurePowershellProcessStartInfo(
            ProcessStartInfo info,
            string command,
            bool leaveShellOpen,
            bool closeShellAfterPress,
            bool useWindowsTerminal,
            string closePrompt)
        {
            // Using just a ; doesn't work with wt, as it's used to create a new tab for the terminal window.
            // \\ must be escaped for it to work properly, or breaking it into multiple arguments.
            var escape = useWindowsTerminal ? "\\" : "";

            if (useWindowsTerminal)
            {
                info.FileName = "wt.exe";
                info.ArgumentList.Add("powershell");
            }
            else
            {
                info.FileName = "powershell.exe";
            }

            if (leaveShellOpen)
            {
                info.ArgumentList.Add("-NoExit");
                info.ArgumentList.Add(command);
            }
            else
            {
                info.ArgumentList.Add("-Command");
                var commandStr = $"{command}{escape};";
                if (closeShellAfterPress)
                {
                    commandStr += $" Write-Host '{closePrompt}'{escape}; [System.Console]::ReadKey(){escape}; exit";
                }
                info.ArgumentList.Add(commandStr);
            }
        }

        private static void ConfigurePwshProcessStartInfo(
            ProcessStartInfo info,
            string command,
            bool leaveShellOpen,
            bool closeShellAfterPress,
            bool useWindowsTerminal,
            string closePrompt)
        {
            // Using just a ; doesn't work with wt, as it's used to create a new tab for the terminal window.
            // \\ must be escaped for it to work properly, or breaking it into multiple arguments.
            var escape = useWindowsTerminal ? "\\" : "";

            if (useWindowsTerminal)
            {
                info.FileName = "wt.exe";
                info.ArgumentList.Add("pwsh");
            }
            else
            {
                info.FileName = "pwsh.exe";
            }

            if (leaveShellOpen)
            {
                info.ArgumentList.Add("-NoExit");
            }

            info.ArgumentList.Add("-Command");
            var commandStr = $"{command}{escape};";
            if (closeShellAfterPress)
            {
                commandStr += $" Write-Host '{closePrompt}'{escape}; [System.Console]::ReadKey(){escape}; exit";
            }
            info.ArgumentList.Add(commandStr);
        }

        partial void RegisterReplaceWinR()
        {
            Context.API.RegisterGlobalKeyboardCallback(API_GlobalKeyboardEvent);
        }

        partial void UnregisterReplaceWinR()
        {
            Context.API.RemoveGlobalKeyboardCallback(API_GlobalKeyboardEvent);
        }

        private bool API_GlobalKeyboardEvent(int keyevent, int vkcode, SpecialKeyState state)
        {
            if (!Context.CurrentPluginMetadata.Disabled && _settings.ReplaceWinR)
            {
                if (keyevent == (int)KeyEvent.WM_KEYDOWN && vkcode == (int)Keys.R && state.WinPressed)
                {
                    _winRStroked = true;
                    OnWinRPressed();
                    return false;
                }
                if (keyevent == (int)KeyEvent.WM_KEYUP && _winRStroked && vkcode == (int)Keys.LWin)
                {
                    _winRStroked = false;
                    _keyboardSimulator.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.CONTROL);
                    return false;
                }
            }
            return true;
        }

        private static void OnWinRPressed()
        {
            Context.API.ShowMainWindow();
            // show the main window and set focus to the query box
            _ = Task.Run(async () =>
            {
                Context.API.ChangeQuery($"{Context.CurrentPluginMetadata.ActionKeywords[0]}{Plugin.Query.TermSeparator}");

                // Win+R is a system-reserved shortcut, and though the plugin intercepts the keyboard event and
                // shows the main window, Windows continues to process the Win key and briefly reclaims focus.
                // So we need to wait until the keyboard event processing is completed and then set focus
                await Task.Delay(50);
                Context.API.FocusQueryTextBox();
            });
        }

        public Control CreateSettingPanel()
        {
            return new CMDSetting(_settings);
        }

        partial void AddRunAsContextMenus(List<Result> results, Result selectedResult)
        {
            results.Add(new()
            {
                Title = Localize.flowlauncher_plugin_cmd_run_as_different_user(),
                Action = c =>
                {
                    Execute(ShellCommand.RunAsDifferentUser, PrepareProcessStartInfo(selectedResult.Title));
                    return true;
                },
                IcoPath = "Images/user.png",
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe7ee")
            });
            results.Add(new()
            {
                Title = Localize.flowlauncher_plugin_cmd_run_as_administrator(),
                Action = c =>
                {
                    Execute(Process.Start, PrepareProcessStartInfo(selectedResult.Title, true));
                    return true;
                },
                IcoPath = "Images/admin.png",
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe7ef")
            });
        }
    }
}
