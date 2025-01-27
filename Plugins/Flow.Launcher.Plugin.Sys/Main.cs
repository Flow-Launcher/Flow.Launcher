using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedCommands;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Shutdown;
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;

namespace Flow.Launcher.Plugin.Sys
{
    public class Main : IPlugin, ISettingProvider, IPluginI18n
    {
        private PluginInitContext context;
        private Dictionary<string, string> KeywordTitleMappings = new Dictionary<string, string>();

        public Control CreateSettingPanel()
        {
            var results = Commands();
            return new SysSettings(results);
        }

        public List<Result> Query(Query query)
        {
            var commands = Commands();
            var results = new List<Result>();
            foreach (var c in commands)
            {
                c.Title = GetDynamicTitle(query, c);

                var titleMatch = StringMatcher.FuzzySearch(query.Search, c.Title);
                var subTitleMatch = StringMatcher.FuzzySearch(query.Search, c.SubTitle);

                var score = Math.Max(titleMatch.Score, subTitleMatch.Score);
                if (score > 0)
                {
                    c.Score = score;

                    if (score == titleMatch.Score)
                        c.TitleHighlightData = titleMatch.MatchData;

                    results.Add(c);
                }
            }

            return results;
        }

        private string GetDynamicTitle(Query query, Result result)
        {
            if (!KeywordTitleMappings.TryGetValue(result.Title, out var translationKey))
            {
                Log.Error("Flow.Launcher.Plugin.Sys.Main", $"Dynamic Title not found for: {result.Title}");
                return "Title Not Found";
            }

            var translatedTitle = context.API.GetTranslation(translationKey);

            if (result.Title == translatedTitle)
            {
                return result.Title;
            }

            var englishTitleMatch = StringMatcher.FuzzySearch(query.Search, result.Title);
            var translatedTitleMatch = StringMatcher.FuzzySearch(query.Search, translatedTitle);

            return englishTitleMatch.Score >= translatedTitleMatch.Score ? result.Title : translatedTitle;
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
            KeywordTitleMappings = new Dictionary<string, string>{
                {"Shutdown", "flowlauncher_plugin_sys_shutdown_computer_cmd"},
                {"Restart", "flowlauncher_plugin_sys_restart_computer_cmd"},
                {"Restart With Advanced Boot Options", "flowlauncher_plugin_sys_restart_advanced_cmd"},
                {"Log Off/Sign Out", "flowlauncher_plugin_sys_log_off_cmd"},
                {"Lock", "flowlauncher_plugin_sys_lock_cmd"},
                {"Sleep", "flowlauncher_plugin_sys_sleep_cmd"},
                {"Hibernate", "flowlauncher_plugin_sys_hibernate_cmd"},
                {"Index Option", "flowlauncher_plugin_sys_indexoption_cmd"},
                {"Empty Recycle Bin", "flowlauncher_plugin_sys_emptyrecyclebin_cmd"},
                {"Open Recycle Bin", "flowlauncher_plugin_sys_openrecyclebin_cmd"},
                {"Exit", "flowlauncher_plugin_sys_exit_cmd"},
                {"Save Settings", "flowlauncher_plugin_sys_save_all_settings_cmd"},
                {"Restart Flow Launcher", "flowlauncher_plugin_sys_restart_cmd"},
                {"Settings", "flowlauncher_plugin_sys_setting_cmd"},
                {"Reload Plugin Data", "flowlauncher_plugin_sys_reload_plugin_data_cmd"},
                {"Check For Update", "flowlauncher_plugin_sys_check_for_update_cmd"},
                {"Open Log Location", "flowlauncher_plugin_sys_open_log_location_cmd"},
                {"Flow Launcher Tips", "flowlauncher_plugin_sys_open_docs_tips_cmd"},
                {"Flow Launcher UserData Folder", "flowlauncher_plugin_sys_open_userdata_location_cmd"},
                {"Toggle Game Mode", "flowlauncher_plugin_sys_toggle_game_mode_cmd"}
            };
        }

        private List<Result> Commands()
        {
            var results = new List<Result>();
            var logPath = Path.Combine(DataLocation.DataDirectory(), "Logs", Constant.Version);
            var userDataPath = DataLocation.DataDirectory();
            var recycleBinFolder = "shell:RecycleBinFolder";
            results.AddRange(new[]
            {
                new Result
                {
                    Title = "Shutdown",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_shutdown_computer"),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe7e8"),
                    IcoPath = "Images\\shutdown.png",
                    Action = c =>
                    {
                        var result = context.API.ShowMsgBox(
                            context.API.GetTranslation("flowlauncher_plugin_sys_dlgtext_shutdown_computer"),
                            context.API.GetTranslation("flowlauncher_plugin_sys_shutdown_computer"),
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.Yes)
                        {
                            Process.Start("shutdown", "/s /t 0");
                        }

                        return true;
                    }
                },
                new Result
                {
                    Title = "Restart",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_restart_computer"),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe777"),
                    IcoPath = "Images\\restart.png",
                    Action = c =>
                    {
                        var result = context.API.ShowMsgBox(
                            context.API.GetTranslation("flowlauncher_plugin_sys_dlgtext_restart_computer"),
                            context.API.GetTranslation("flowlauncher_plugin_sys_restart_computer"),
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.Yes)
                        {
                            Process.Start("shutdown", "/r /t 0");
                        }

                        return true;
                    }
                },
                new Result
                {
                    Title = "Restart With Advanced Boot Options",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_restart_advanced"),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xecc5"),
                    IcoPath = "Images\\restart_advanced.png",
                    Action = c =>
                    {
                        var result = context.API.ShowMsgBox(
                            context.API.GetTranslation("flowlauncher_plugin_sys_dlgtext_restart_computer_advanced"),
                            context.API.GetTranslation("flowlauncher_plugin_sys_restart_computer"),
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                            Process.Start("shutdown", "/r /o /t 0");

                        return true;
                    }
                },
                new Result
                {
                    Title = "Log Off/Sign Out",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_log_off"),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe77b"),
                    IcoPath = "Images\\logoff.png",
                    Action = c =>
                    {
                        var result = context.API.ShowMsgBox(
                            context.API.GetTranslation("flowlauncher_plugin_sys_dlgtext_logoff_computer"),
                            context.API.GetTranslation("flowlauncher_plugin_sys_log_off"),
                            MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                            PInvoke.ExitWindowsEx(EXIT_WINDOWS_FLAGS.EWX_LOGOFF, 0);

                        return true;
                    }
                },
                new Result
                {
                    Title = "Lock",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_lock"),
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
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_sleep"),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xec46"),
                    IcoPath = "Images\\sleep.png",
                    Action = c => PInvoke.SetSuspendState(false, false, false)
                },
                new Result
                {
                    Title = "Hibernate",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_hibernate"),
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe945"),
                    IcoPath = "Images\\hibernate.png",
                    Action= c =>
                    {
                        var info = ShellCommand.SetProcessStartInfo("shutdown", arguments:"/h");
                        info.WindowStyle = ProcessWindowStyle.Hidden;
                        info.UseShellExecute = true;

                        ShellCommand.Execute(info);

                        return true;
                    }
                },
                 new Result
                {
                    Title = "Index Option",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_indexoption"),
                    IcoPath = "Images\\indexoption.png",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe773"),
                    Action = c =>
                    {
                        {
                            System.Diagnostics.Process.Start("control.exe", "srchadmin.dll");
                        }

                        return true;
                    }
                },
                new Result
                {
                    Title = "Empty Recycle Bin",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_emptyrecyclebin"),
                    IcoPath = "Images\\recyclebin.png",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe74d"),
                    Action = c =>
                    {
                        // http://www.pinvoke.net/default.aspx/shell32/SHEmptyRecycleBin.html
                        // FYI, couldn't find documentation for this but if the recycle bin is already empty, it will return -2147418113 (0x8000FFFF (E_UNEXPECTED))
                        // 0 for nothing
                        var result = PInvoke.SHEmptyRecycleBin(new(), string.Empty, 0);
                        if (result != HRESULT.S_OK && result != HRESULT.E_UNEXPECTED)
                        {
                            context.API.ShowMsgBox("Failed to empty the recycle bin. This might happen if:\n" +
                                            "- A file in the recycle bin is in use\n" +
                                            "- You don't have permission to delete some items\n" +
                                            "Please close any applications that might be using these files and try again.",
                                "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                        return true;
                    }
                },
                new Result
                {
                    Title = "Open Recycle Bin",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_openrecyclebin"),
                    IcoPath = "Images\\openrecyclebin.png",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe74d"),
                    CopyText = recycleBinFolder,
                    Action = c =>
                    {
                        {
                                   System.Diagnostics.Process.Start("explorer", recycleBinFolder);
                        }

                        return true;
                    }
                },
                new Result
                {
                    Title = "Exit",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_exit"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        Application.Current.MainWindow.Close();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Save Settings",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_save_all_settings"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        context.API.SaveAppAllSettings();
                        context.API.ShowMsg(context.API.GetTranslation("flowlauncher_plugin_sys_dlgtitle_success"),
                            context.API.GetTranslation("flowlauncher_plugin_sys_dlgtext_all_settings_saved"));
                        return true;
                    }
                },
                new Result
                {
                    Title = "Restart Flow Launcher",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_restart"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        context.API.RestartApp();
                        return false;
                    }
                },
                new Result
                {
                    Title = "Settings",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_setting"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        context.API.OpenSettingDialog();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Reload Plugin Data",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_reload_plugin_data"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        // Hide the window first then show msg after done because sometimes the reload could take a while, so not to make user think it's frozen. 
                        Application.Current.MainWindow.Hide();

                        _ = context.API.ReloadAllPluginData().ContinueWith(_ =>
                            context.API.ShowMsg(
                                context.API.GetTranslation("flowlauncher_plugin_sys_dlgtitle_success"),
                                context.API.GetTranslation(
                                    "flowlauncher_plugin_sys_dlgtext_all_applicableplugins_reloaded")),
                            System.Threading.Tasks.TaskScheduler.Current);

                        return true;
                    }
                },
                new Result
                {
                    Title = "Check For Update",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_check_for_update"),
                    IcoPath = "Images\\checkupdate.png",
                    Action = c =>
                    {
                        Application.Current.MainWindow.Hide();
                        context.API.CheckForNewUpdate();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Open Log Location",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_open_log_location"),
                    IcoPath = "Images\\app.png",
                    CopyText = logPath,
                    AutoCompleteText = logPath,
                    Action = c =>
                    {
                        context.API.OpenDirectory(logPath);
                        return true;
                    }
                },
                new Result
                {
                    Title = "Flow Launcher Tips",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_open_docs_tips"),
                    IcoPath = "Images\\app.png",
                    CopyText = Constant.Documentation,
                    AutoCompleteText = Constant.Documentation,
                    Action = c =>
                    {
                        context.API.OpenUrl(Constant.Documentation);
                        return true;
                    }
                },
                new Result
                {
                    Title = "Flow Launcher UserData Folder",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_open_userdata_location"),
                    IcoPath = "Images\\app.png",
                    CopyText = userDataPath,
                    AutoCompleteText = userDataPath,
                    Action = c =>
                    {
                        context.API.OpenDirectory(userDataPath);
                        return true;
                    }
                },
                new Result
                {
                    Title = "Toggle Game Mode",
                    SubTitle = context.API.GetTranslation("flowlauncher_plugin_sys_toggle_game_mode"),
                    IcoPath = "Images\\app.png",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\ue7fc"),
                    Action = c =>
                    {
                        context.API.ToggleGameMode();
                        return true;
                    }
                }
            });

            return results;
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("flowlauncher_plugin_sys_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("flowlauncher_plugin_sys_plugin_description");
        }
    }
}
