using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.Plugin.Shell.Views;
using WindowsInput;
using WindowsInput.Native;
using Control = System.Windows.Controls.Control;
using Keys = System.Windows.Forms.Keys;

namespace Flow.Launcher.Plugin.Shell
{
    public class Main : IPlugin, ISettingProvider, IPluginI18n, IContextMenu, IDisposable
    {
        private static readonly string ClassName = nameof(Main);

        internal static PluginInitContext Context { get; private set; }

        private const string Image = "Images/shell.png";
        private bool _winRStroked;
        private readonly KeyboardSimulator _keyboardSimulator = new(new InputSimulator());

        private Settings _settings;

        public List<Result> Query(Query query)
        {
            List<Result> results = [];
            string cmd = query.Search;
            if (string.IsNullOrEmpty(cmd))
            {
                return ResultsFromHistory();
            }
            else
            {
                var queryCmd = GetCurrentCmd(cmd);
                results.Add(queryCmd);
                var history = GetHistoryCmds(cmd, queryCmd);
                results.AddRange(history);

                try
                {
                    string basedir = null;
                    string dir = null;
                    string excmd = Environment.ExpandEnvironmentVariables(cmd);
                    if (Directory.Exists(excmd) && (cmd.EndsWith('/') || cmd.EndsWith('\\')))
                    {
                        basedir = excmd;
                        dir = cmd;
                    }
                    else if (Directory.Exists(Path.GetDirectoryName(excmd) ?? string.Empty))
                    {
                        basedir = Path.GetDirectoryName(excmd);
                        var dirName = Path.GetDirectoryName(cmd);
                        dir = (dirName.EndsWith('/') || dirName.EndsWith('\\')) ? dirName : cmd[..(dirName.Length + 1)];
                    }

                    if (basedir != null)
                    {
                        var autocomplete =
                            Directory.GetFileSystemEntries(basedir)
                                .Select(o => dir + Path.GetFileName(o))
                                .Where(o => o.StartsWith(cmd, StringComparison.OrdinalIgnoreCase) &&
                                            !results.Any(p => o.Equals(p.Title, StringComparison.OrdinalIgnoreCase)) &&
                                            !results.Any(p => o.Equals(p.Title, StringComparison.OrdinalIgnoreCase))).ToList();

                        autocomplete.Sort();

                        results.AddRange(autocomplete.ConvertAll(m => new Result
                        {
                            Title = m,
                            IcoPath = Image,
                            Action = c =>
                            {
                                var runAsAdministrator =
                                    c.SpecialKeyState.CtrlPressed &&
                                    c.SpecialKeyState.ShiftPressed &&
                                    !c.SpecialKeyState.AltPressed &&
                                    !c.SpecialKeyState.WinPressed;

                                Execute(Process.Start, PrepareProcessStartInfo(m, runAsAdministrator));
                                return true;
                            },
                            CopyText = m
                        }));
                    }
                }
                catch (Exception e)
                {
                    Context.API.LogException(ClassName, $"Exception when query for <{query}>", e);
                }
                return results;
            }
        }

        private List<Result> GetHistoryCmds(string cmd, Result result)
        {
            IEnumerable<Result> history = _settings.CommandHistory.Where(o => o.Key.Contains(cmd))
                .OrderByDescending(o => o.Value)
                .Select(m =>
                {
                    if (m.Key == cmd)
                    {
                        result.SubTitle = Localize.flowlauncher_plugin_cmd_cmd_has_been_executed_times(m.Value);
                        return null;
                    }

                    var ret = new Result
                    {
                        Title = m.Key,
                        SubTitle = Localize.flowlauncher_plugin_cmd_cmd_has_been_executed_times(m.Value),
                        IcoPath = Image,
                        Action = c =>
                        {
                            var runAsAdministrator =
                                c.SpecialKeyState.CtrlPressed &&
                                c.SpecialKeyState.ShiftPressed &&
                                !c.SpecialKeyState.AltPressed &&
                                !c.SpecialKeyState.WinPressed;

                            Execute(Process.Start, PrepareProcessStartInfo(m.Key, runAsAdministrator));
                            return true;
                        },
                        CopyText = m.Key
                    };
                    return ret;
                }).Where(o => o != null);

            if (_settings.ShowOnlyMostUsedCMDs)
                return [.. history.Take(_settings.ShowOnlyMostUsedCMDsNumber)];

            return [.. history];
        }

        private Result GetCurrentCmd(string cmd)
        {
            Result result = new Result
            {
                Title = cmd,
                Score = 5000,
                SubTitle = Localize.flowlauncher_plugin_cmd_execute_through_shell(),
                IcoPath = Image,
                Action = c =>
                {
                    var runAsAdministrator =
                        c.SpecialKeyState.CtrlPressed &&
                        c.SpecialKeyState.ShiftPressed &&
                        !c.SpecialKeyState.AltPressed &&
                        !c.SpecialKeyState.WinPressed;

                    Execute(Process.Start, PrepareProcessStartInfo(cmd, runAsAdministrator));
                    return true;
                },
                CopyText = cmd
            };

            return result;
        }

        private List<Result> ResultsFromHistory()
        {
            IEnumerable<Result> history = _settings.CommandHistory.OrderByDescending(o => o.Value)
                .Select(m => new Result
                {
                    Title = m.Key,
                    SubTitle = Localize.flowlauncher_plugin_cmd_cmd_has_been_executed_times(m.Value),
                    IcoPath = Image,
                    Action = c =>
                    {
                        var runAsAdministrator =
                            c.SpecialKeyState.CtrlPressed &&
                            c.SpecialKeyState.ShiftPressed &&
                            !c.SpecialKeyState.AltPressed &&
                            !c.SpecialKeyState.WinPressed;

                        Execute(Process.Start, PrepareProcessStartInfo(m.Key, runAsAdministrator));
                        return true;
                    },
                    CopyText = m.Key
                });

            if (_settings.ShowOnlyMostUsedCMDs)
                return [.. history.Take(_settings.ShowOnlyMostUsedCMDsNumber)];

            return [.. history];
        }

        private ProcessStartInfo PrepareProcessStartInfo(string command, bool runAsAdministrator = false)
        {
            command = command.Trim();
            command = Environment.ExpandEnvironmentVariables(command);
            var workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var runAsAdministratorArg = !runAsAdministrator && !_settings.RunAsAdministrator ? "" : "runas";

            var info = new ProcessStartInfo()
            {
                Verb = runAsAdministratorArg,
                WorkingDirectory = workingDirectory,
            };
            var notifyStr = Localize.flowlauncher_plugin_cmd_press_any_key_to_close();
            var addedCharacter = _settings.UseWindowsTerminal ? "\\" : "";
            switch (_settings.Shell)
            {
                case Shell.Cmd:
                    {
                        if (_settings.UseWindowsTerminal)
                        {
                            info.FileName = "wt.exe";
                            info.ArgumentList.Add("cmd");
                        }
                        else
                        {
                            info.FileName = "cmd.exe";
                        }
                        if (_settings.LeaveShellOpen)
                        {
                            info.ArgumentList.Add("/k");
                        }
                        else
                        {
                            info.ArgumentList.Add("/c");
                        }
                        info.ArgumentList.Add(
                            $"{command}" +
                            $"{(_settings.CloseShellAfterPress ?
                                $" && echo {notifyStr} && pause > nul /c" :
                                "")}");
                        break;
                    }

                case Shell.Powershell:
                    {
                        // Using just a ; doesn't work with wt, as it's used to create a new tab for the terminal window
                        // \\ must be escaped for it to work properly, or breaking it into multiple arguments
                        if (_settings.UseWindowsTerminal)
                        {
                            info.FileName = "wt.exe";
                            info.ArgumentList.Add("powershell");
                        }
                        else
                        {
                            info.FileName = "powershell.exe";
                        }
                        if (_settings.LeaveShellOpen)
                        {
                            info.ArgumentList.Add("-NoExit");
                            info.ArgumentList.Add(command);
                        }
                        else
                        {
                            info.ArgumentList.Add("-Command");
                            info.ArgumentList.Add(
                                $"{command}{addedCharacter};" +
                                $"{(_settings.CloseShellAfterPress ?
                                    $" Write-Host '{notifyStr}'{addedCharacter}; [System.Console]::ReadKey(){addedCharacter}; exit" :
                                    "")}");
                        }
                        break;
                    }

                case Shell.Pwsh:
                    {
                        // Using just a ; doesn't work with wt, as it's used to create a new tab for the terminal window
                        // \\ must be escaped for it to work properly, or breaking it into multiple arguments
                        if (_settings.UseWindowsTerminal)
                        {
                            info.FileName = "wt.exe";
                            info.ArgumentList.Add("pwsh");
                        }
                        else
                        {
                            info.FileName = "pwsh.exe";
                        }
                        if (_settings.LeaveShellOpen)
                        {
                            info.ArgumentList.Add("-NoExit");
                        }
                        info.ArgumentList.Add("-Command");
                        info.ArgumentList.Add(
                            $"{command}{addedCharacter};" +
                            $"{(_settings.CloseShellAfterPress ?
                                $" Write-Host '{notifyStr}'{addedCharacter}; [System.Console]::ReadKey(){addedCharacter}; exit" :
                                "")}");
                        break;
                    }

                case Shell.RunCommand:
                    {
                        var parts = command.Split(
                        [
                            ' '
                        ], 2);
                        if (parts.Length == 2)
                        {
                            var filename = parts[0];
                            if (ExistInPath(filename))
                            {
                                var arguments = parts[1];
                                info.FileName = filename;
                                info.ArgumentList.Add(arguments);
                            }
                            else
                            {
                                info.FileName = command;
                            }
                        }
                        else
                        {
                            info.FileName = command;
                        }

                        info.UseShellExecute = true;

                        break;
                    }

                default:
                    throw new NotImplementedException();
            }

            info.UseShellExecute = true;

            _settings.AddCmdHistory(command);

            return info;
        }

        private void Execute(Func<ProcessStartInfo, Process> startProcess, ProcessStartInfo info)
        {
            try
            {
                ShellCommand.Execute(startProcess, info);
            }
            catch (FileNotFoundException e)
            {
                Context.API.ShowMsgError(GetTranslatedPluginTitle(),
                    Localize.flowlauncher_plugin_cmd_command_not_found(e.Message));
            }
            catch (Win32Exception e)
            {
                Context.API.ShowMsgError(GetTranslatedPluginTitle(),
                    Localize.flowlauncher_plugin_cmd_error_running_command(e.Message));
            }
            catch (Exception e)
            {
                Context.API.LogException(ClassName, $"Error executing command: {info.FileName} {string.Join(" ", info.ArgumentList)}", e);
            }
        }

        private static bool ExistInPath(string filename)
        {
            if (File.Exists(filename))
            {
                return true;
            }
            else
            {
                var values = Environment.GetEnvironmentVariable("PATH");
                if (values != null)
                {
                    foreach (var path in values.Split(';'))
                    {
                        var path1 = Path.Combine(path, filename);
                        var path2 = Path.Combine(path, filename + ".exe");
                        if (File.Exists(path1) || File.Exists(path2))
                        {
                            return true;
                        }
                    }
                    return false;
                }
                else
                {
                    return false;
                }
            }
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
            _settings = context.API.LoadSettingJsonStorage<Settings>();
            context.API.RegisterGlobalKeyboardCallback(API_GlobalKeyboardEvent);
            // Since the old Settings class set default value of ShowOnlyMostUsedCMDsNumber to 0 which is a wrong value,
            // we need to fix it here to make sure the default value is 5
            // todo: remove this code block after release v2.2.0
            if (_settings.ShowOnlyMostUsedCMDsNumber == 0)
            {
                _settings.ShowOnlyMostUsedCMDsNumber = 5;
            }
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

        public string GetTranslatedPluginTitle()
        {
            return Localize.flowlauncher_plugin_cmd_plugin_name();
        }

        public string GetTranslatedPluginDescription()
        {
            return Localize.flowlauncher_plugin_cmd_plugin_description();
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var results = new List<Result>
            {
                new()
                {
                    Title = Localize.flowlauncher_plugin_cmd_run_as_different_user(),
                    Action = c =>
                    {
                        Execute(ShellCommand.RunAsDifferentUser, PrepareProcessStartInfo(selectedResult.Title));
                        return true;
                    },
                    IcoPath = "Images/user.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe7ee")
                },
                new()
                {
                    Title = Localize.flowlauncher_plugin_cmd_run_as_administrator(),
                    Action = c =>
                    {
                        Execute(Process.Start, PrepareProcessStartInfo(selectedResult.Title, true));
                        return true;
                    },
                    IcoPath = "Images/admin.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe7ef")
                },
                new()
                {
                    Title = Localize.flowlauncher_plugin_cmd_copy(),
                    Action = c =>
                    {
                        Context.API.CopyToClipboard(selectedResult.Title);
                        return true;
                    },
                    IcoPath = "Images/copy.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe8c8")
                }
            };

            return results;
        }

        public void Dispose()
        {
            Context.API.RemoveGlobalKeyboardCallback(API_GlobalKeyboardEvent);
        }
    }
}
