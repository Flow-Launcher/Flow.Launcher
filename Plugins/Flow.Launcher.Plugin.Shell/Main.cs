using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WindowsInput;
using WindowsInput.Native;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Plugin.SharedCommands;
using Control = System.Windows.Controls.Control;
using Keys = System.Windows.Forms.Keys;

namespace Flow.Launcher.Plugin.Shell
{
    public class Main : IPlugin, ISettingProvider, IPluginI18n, IContextMenu
    {
        private const string Image = "Images/shell.png";
        private PluginInitContext context;
        private bool _winRStroked;
        private readonly KeyboardSimulator _keyboardSimulator = new KeyboardSimulator(new InputSimulator());

        private Settings _settings;

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
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
                    if (Directory.Exists(excmd) && (cmd.EndsWith("/") || cmd.EndsWith(@"\")))
                    {
                        basedir = excmd;
                        dir = cmd;
                    }
                    else if (Directory.Exists(Path.GetDirectoryName(excmd) ?? string.Empty))
                    {
                        basedir = Path.GetDirectoryName(excmd);
                        var dirName = Path.GetDirectoryName(cmd);
                        dir = (dirName.EndsWith("/") || dirName.EndsWith(@"\")) ? dirName : cmd.Substring(0, dirName.Length + 1);
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
                    Log.Exception($"|Flow.Launcher.Plugin.Shell.Main.Query|Exception when query for <{query}>", e);
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
                        result.SubTitle = string.Format(context.API.GetTranslation("flowlauncher_plugin_cmd_cmd_has_been_executed_times"), m.Value);
                        return null;
                    }

                    var ret = new Result
                    {
                        Title = m.Key,
                        SubTitle = string.Format(context.API.GetTranslation("flowlauncher_plugin_cmd_cmd_has_been_executed_times"), m.Value),
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
                return history.Take(_settings.ShowOnlyMostUsedCMDsNumber).ToList();

            return history.ToList();
        }

        private Result GetCurrentCmd(string cmd)
        {
            Result result = new Result
            {
                Title = cmd,
                Score = 5000,
                SubTitle = context.API.GetTranslation("flowlauncher_plugin_cmd_execute_through_shell"),
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
                    SubTitle = string.Format(context.API.GetTranslation("flowlauncher_plugin_cmd_cmd_has_been_executed_times"), m.Value),
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
                return history.Take(_settings.ShowOnlyMostUsedCMDsNumber).ToList();

            return history.ToList();
        }

        private ProcessStartInfo PrepareProcessStartInfo(string command, bool runAsAdministrator = false)
        {
            command = command.Trim();
            command = Environment.ExpandEnvironmentVariables(command);
            var workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var runAsAdministratorArg = !runAsAdministrator && !_settings.RunAsAdministrator ? "" : "runas";

            ProcessStartInfo info = new()
            {
                Verb = runAsAdministratorArg, WorkingDirectory = workingDirectory,
            };
            switch (_settings.Shell)
            {
                case Shell.Cmd:
                {
                    info.FileName = "cmd.exe";
                    info.Arguments = $"{(_settings.LeaveShellOpen ? "/k" : "/c")} {command} {(_settings.CloseShellAfterPress ? $"&& echo {context.API.GetTranslation("flowlauncher_plugin_cmd_press_any_key_to_close")} && pause > nul /c" : "")}";

                    //// Use info.Arguments instead of info.ArgumentList to enable users better control over the arguments they are writing.
                    //// Previous code using ArgumentList, commands needed to be separated correctly:                      
                    //// Incorrect:
                    // info.ArgumentList.Add(_settings.LeaveShellOpen ? "/k" : "/c");
                    // info.ArgumentList.Add(command); //<== info.ArgumentList.Add("mkdir \"c:\\test new\"");

                    //// Correct version should be:
                    //info.ArgumentList.Add(_settings.LeaveShellOpen ? "/k" : "/c");
                    //info.ArgumentList.Add("mkdir");
                    //info.ArgumentList.Add(@"c:\test new");

                    //https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.argumentlist?view=net-6.0#remarks

                    break;
                }

                case Shell.Powershell:
                {
                    info.FileName = "powershell.exe";
                    if (_settings.LeaveShellOpen)
                    {
                        info.ArgumentList.Add("-NoExit");
                        info.ArgumentList.Add(command);
                    }
                    else
                    {
                        info.ArgumentList.Add("-Command");
                        info.ArgumentList.Add($"{command}; {(_settings.CloseShellAfterPress ? $"Write-Host '{context.API.GetTranslation("flowlauncher_plugin_cmd_press_any_key_to_close")}'; [System.Console]::ReadKey(); exit" : "")}");
                    }
                    break;
                }

                case Shell.Pwsh:
                {
                    info.FileName = "pwsh.exe";
                    if (_settings.LeaveShellOpen)
                    {
                        info.ArgumentList.Add("-NoExit");
                    }
                    info.ArgumentList.Add("-Command");
                    info.ArgumentList.Add($"{command}; {(_settings.CloseShellAfterPress ? $"Write-Host '{context.API.GetTranslation("flowlauncher_plugin_cmd_press_any_key_to_close")}'; [System.Console]::ReadKey(); exit" : "")}");

                    break;
                }

                case Shell.RunCommand:
                {
                    var parts = command.Split(new[]
                    {
                        ' '
                    }, 2);
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
                var name = "Plugin: Shell";
                var message = $"Command not found: {e.Message}";
                context.API.ShowMsg(name, message);
            }
            catch (Win32Exception e)
            {
                var name = "Plugin: Shell";
                var message = $"Error running the command: {e.Message}";
                context.API.ShowMsg(name, message);
            }
        }

        private bool ExistInPath(string filename)
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
            this.context = context;
            _settings = context.API.LoadSettingJsonStorage<Settings>();
            context.API.RegisterGlobalKeyboardCallback(API_GlobalKeyboardEvent);
        }

        bool API_GlobalKeyboardEvent(int keyevent, int vkcode, SpecialKeyState state)
        {
            if (!context.CurrentPluginMetadata.Disabled && _settings.ReplaceWinR)
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

        private void OnWinRPressed()
        {
            // show the main window and set focus to the query box
            Task.Run(() =>
            {
                context.API.ShowMainWindow();
                context.API.ChangeQuery($"{context.CurrentPluginMetadata.ActionKeywords[0]}{Plugin.Query.TermSeparator}");
            });

        }

        public Control CreateSettingPanel()
        {
            return new CMDSetting(_settings);
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("flowlauncher_plugin_cmd_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("flowlauncher_plugin_cmd_plugin_description");
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var results = new List<Result>
            {
                new()
                {
                    Title = context.API.GetTranslation("flowlauncher_plugin_cmd_run_as_different_user"),
                    AsyncAction = async c =>
                    {
                        Execute(ShellCommand.RunAsDifferentUser, PrepareProcessStartInfo(selectedResult.Title));
                        return true;
                    },
                    IcoPath = "Images/user.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe7ee")
                },
                new()
                {
                    Title = context.API.GetTranslation("flowlauncher_plugin_cmd_run_as_administrator"),
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
                    Title = context.API.GetTranslation("flowlauncher_plugin_cmd_copy"),
                    Action = c =>
                    {
                        context.API.CopyToClipboard(selectedResult.Title);
                        return true;
                    },
                    IcoPath = "Images/copy.png",
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe8c8")
                }
            };

            return results;
        }
    }
}
