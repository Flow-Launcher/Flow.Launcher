using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Plugin.Shell
{
    public partial class Main : IPlugin, ISettingProvider, IPluginI18n, IContextMenu, IDisposable
    {
        private static readonly string ClassName = nameof(Main);

        internal static PluginInitContext Context { get; private set; }

        private const string Image = "Images/shell.png";

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
            var runAsAdmin = runAsAdministrator || _settings.RunAsAdministrator;
            var closePrompt = Localize.flowlauncher_plugin_cmd_press_any_key_to_close();
            var info = CreateProcessStartInfo(
                command,
                _settings.Shell,
                _settings.LeaveShellOpen,
                _settings.CloseShellAfterPress,
                _settings.UseWindowsTerminal,
                runAsAdmin,
                closePrompt,
                _settings.CustomTemplateShellConfig);

            _settings.AddCmdHistory(command);
            return info;
        }

        internal static ProcessStartInfo CreateProcessStartInfo(
            string command,
            Shell shell,
            bool leaveShellOpen,
            bool closeShellAfterPress,
            bool useWindowsTerminal,
            bool runAsAdmin,
            string closePrompt,
            CustomTemplateShellConfig customTemplateShellConfig = null)
        {
            command = command.Trim();
            command = Environment.ExpandEnvironmentVariables(command);

            var workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var runAsAdministratorArg = runAsAdmin ? "runas" : "";

            var info = new ProcessStartInfo()
            {
                Verb = runAsAdministratorArg,
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
            };

            switch (shell)
            {
                case Shell.RunCommand:
                    ConfigureRunCommandStartInfo(
                        info,
                        command);
                    break;

                case Shell.CustomTemplate:
                    ConfigureCustomTemplateShellStartInfo(
                        info,
                        command,
                        customTemplateShellConfig);
                    break;

                default:
                    ConfigureShellProcessStartInfo(
                        info,
                        command,
                        shell,
                        leaveShellOpen,
                        closeShellAfterPress,
                        useWindowsTerminal,
                        closePrompt);
                    break;
            }

            RemoveRunAsVerb(info);

            return info;
        }

        private static partial void ConfigureShellProcessStartInfo(
            ProcessStartInfo info,
            string command,
            Shell shell,
            bool leaveShellOpen,
            bool closeShellAfterPress,
            bool useWindowsTerminal,
            string closePrompt);

        static partial void RemoveRunAsVerb(ProcessStartInfo info);

        private static void ConfigureRunCommandStartInfo(
            ProcessStartInfo info,
            string command)
        {
            string filename = null;
            string arguments = null;

            bool isQuotedPath = command.StartsWith("\"");

            if (isQuotedPath)
            {
                // Quoted paths ("C:\Program Files\app.exe") can have spaces in the path. 
                
                // We know the command starts with a quote,
                // So strip it off and split to see if theres a second one
                var parts = command[1..].Split("\"", 2);
                bool hasMatchingQuotes = parts.Length > 1;

                if (hasMatchingQuotes)
                {
                    filename = parts[0];
                    arguments = parts[1];
                }
                // If there is no closing quote then we leave both as null
            }
            else
            {
                // Without a quoted path,
                // we split on the first space to get the filename and args
                var parts = command.Split(' ', 2);
                filename = parts[0];
                arguments = parts.Length > 1 ? parts[1] : null;
            }

            if (filename != null && ExistInPath(filename))
            {
                info.FileName = filename;
                
                // catch unnecessary space or arguments that are just whitespace
                var trimmedArgs = arguments?.TrimStart();
                if (!string.IsNullOrEmpty(trimmedArgs))
                    info.Arguments = trimmedArgs;
            }
            else
            {
                // Could not parse a valid filename or it was not found.
                // Pass the whole command anyways so the OS produces a meaningful error.
                info.FileName = command;
            }
        }

        private static void ConfigureCustomTemplateShellStartInfo(
            ProcessStartInfo info,
            string command,
            CustomTemplateShellConfig config)
        {
            if (config == null)
                return;

            if (!string.IsNullOrWhiteSpace(config.ExecutablePath))
                info.FileName = Environment.ExpandEnvironmentVariables(config.ExecutablePath).Trim().Trim('"');

            if (!string.IsNullOrWhiteSpace(config.ArgumentsTemplate))
            {
                var template = Environment.ExpandEnvironmentVariables(config.ArgumentsTemplate).Trim();
                info.Arguments = template.Replace("{command}", command);
            }
        }

        private void Execute(Func<ProcessStartInfo, Process> startProcess, ProcessStartInfo info)
        {
            if (string.IsNullOrEmpty(info.FileName))
            {
                Context.API.ShowMsgError(GetTranslatedPluginTitle(),
                    Localize.flowlauncher_plugin_cmd_error_no_exe_path_set());
                return;
            }

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
                // ArgumentList and Arguments are mutually exclusive (https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.argumentlist?view=net-10.0#remarks).
                var arguments = info.ArgumentList.Count > 0
                    ? string.Join(" ", info.ArgumentList)
                    : info.Arguments;
                Context.API.LogException(ClassName, $"Error executing command: {info.FileName} {arguments}", e);
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
                    foreach (var path in values.Split(Path.PathSeparator))
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
            RegisterReplaceWinR();
            // Since the old Settings class set default value of ShowOnlyMostUsedCMDsNumber to 0 which is a wrong value,
            // we need to fix it here to make sure the default value is 5
            // todo: remove this code block after release v2.2.0
            if (_settings.ShowOnlyMostUsedCMDsNumber == 0)
            {
                _settings.ShowOnlyMostUsedCMDsNumber = 5;
            }
        }

        partial void RegisterReplaceWinR();

        partial void UnregisterReplaceWinR();

        public object CreateSettingPanelAvalonia()
        {
            return new Flow.Launcher.Plugin.Shell.Views.Avalonia.ShellSetting(_settings);
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
            var results = new List<Result>();
            AddRunAsContextMenus(results, selectedResult);
            results.Add(new()
            {
                Title = Localize.flowlauncher_plugin_cmd_copy(),
                Action = c =>
                {
                    Context.API.CopyToClipboard(selectedResult.Title);
                    return true;
                },
                IcoPath = "Images/copy.png",
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe8c8")
            });

            return results;
        }

        partial void AddRunAsContextMenus(List<Result> results, Result selectedResult);

        public void Dispose()
        {
            UnregisterReplaceWinR();
        }
    }
}
