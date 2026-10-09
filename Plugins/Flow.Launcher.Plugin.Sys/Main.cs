using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.Sys
{
    public partial class Main : IPlugin, ISettingProvider, IPluginI18n
    {
        private static readonly string ClassName = nameof(Main);

        private readonly Dictionary<string, string> KeywordTitleMappings = new()
        {
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
            {"Toggle Game Mode", "flowlauncher_plugin_sys_toggle_game_mode_cmd"},
            {"Set Flow Launcher Theme", "flowlauncher_plugin_sys_theme_selector_cmd"}
        };
        private readonly Dictionary<string, string> KeywordDescriptionMappings = [];

        private const string Documentation = "https://flowlauncher.com/docs/#/usage-tips";

        internal static PluginInitContext Context { get; private set; }
        private Settings _settings;
        private SettingsViewModel _viewModel;

        public List<Result> Query(Query query)
        {
            if (query.Search.StartsWith(ThemeSelector.Keyword))
            {
                return ThemeSelector.Query(query);
            }

            var commands = Commands(query);
            var results = new List<Result>();
            var isEmptyQuery = string.IsNullOrWhiteSpace(query.Search);
            foreach (var c in commands)
            {
                var command = _settings.Commands.First(x => x.Key == c.Title);
                c.Title = command.Name;
                c.SubTitle = command.Description;
                if (isEmptyQuery)
                {
                    results.Add(c);
                    continue;
                }

                // Match from localized title & localized subtitle & keyword
                var titleMatch = Context.API.FuzzySearch(query.Search, c.Title);
                var subTitleMatch = Context.API.FuzzySearch(query.Search, c.SubTitle);
                var keywordMatch = Context.API.FuzzySearch(query.Search, command.Keyword);

                // Get the largest score from them
                var score = Math.Max(titleMatch.Score, subTitleMatch.Score);
                var finalScore = Math.Max(score, keywordMatch.Score);
                if (finalScore > 0)
                {
                    c.Score = finalScore;

                    // If title match has the highest score, highlight title
                    if (finalScore == titleMatch.Score)
                    {
                        c.TitleHighlightData = titleMatch.MatchData;
                    }

                    results.Add(c);
                }
            }

            return results;
        }

        private string GetTitle(string key)
        {
            if (!KeywordTitleMappings.TryGetValue(key, out var translationKey))
            {
                Context.API.LogError(ClassName, $"Title not found for: {key}");
                return "Title Not Found";
            }

            return Context.API.GetTranslation(translationKey);
        }

        private string GetDescription(string key)
        {
            if (!KeywordDescriptionMappings.TryGetValue(key, out var translationKey))
            {
                Context.API.LogError(ClassName, $"Description not found for: {key}");
                return "Description Not Found";
            }

            return Context.API.GetTranslation(translationKey);
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
            _settings = context.API.LoadSettingJsonStorage<Settings>();
            _viewModel = new SettingsViewModel(_settings);
            foreach (string key in KeywordTitleMappings.Keys)
            {
                // Remove _cmd in the last of the strings
                KeywordDescriptionMappings[key] = KeywordTitleMappings[key][..^4];
            }
        }

        private void UpdateLocalizedNameDescription(bool force)
        {
            if (string.IsNullOrEmpty(_settings.Commands[0].Name) || force)
            {
                foreach (var c in _settings.Commands)
                {
                    c.Name = GetTitle(c.Key);
                    c.Description = GetDescription(c.Key);
                }
            }
        }

        /// <summary>
        /// Platform power, session and recycle bin commands, listed before the Flow Launcher commands.
        /// </summary>
        private partial List<Result> SystemCommands();

        private List<Result> Commands(Query query)
        {
            var results = SystemCommands();
            results.AddRange(
            [
                new Result
                {
                    Title = "Save Settings",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xea35"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        Context.API.SaveAppAllSettings();
                        Context.API.ShowMsg(Localize.flowlauncher_plugin_sys_dlgtitle_success(),
                            Localize.flowlauncher_plugin_sys_dlgtext_all_settings_saved());
                        return true;
                    }
                },
                new Result
                {
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe72c"),
                    Title = "Restart Flow Launcher",
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        Context.API.RestartApp();
                        return false;
                    }
                },
                new Result
                {
                    Title = "Settings",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xf210"),
                    IcoPath = "Images\\app.png",
                    Action = c =>
                    {
                        // Hide the window first then open setting dialog because main window can be topmost window which will still display on top of the setting dialog for a while
                        Context.API.HideMainWindow();
                        Context.API.OpenSettingDialog();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Reload Plugin Data",
                    IcoPath = "Images\\app.png",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe72c"),
                    Action = c =>
                    {
                        // Hide the window first then show msg after done because sometimes the reload could take a while, so not to make user think it's frozen. 
                        Context.API.HideMainWindow();
                        _ = Context.API.ReloadAllPluginData().ContinueWith(_ =>
                            Context.API.ShowMsg(
                                Localize.flowlauncher_plugin_sys_dlgtitle_success(),
                                Localize.flowlauncher_plugin_sys_dlgtext_all_applicableplugins_reloaded()),
                            TaskScheduler.Current);
                        return true;
                    }
                },
                new Result
                {
                    Title = "Check For Update",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xede4"),
                    IcoPath = "Images\\checkupdate.png",
                    Action = c =>
                    {
                        Context.API.HideMainWindow();
                        Context.API.CheckForNewUpdate();
                        return true;
                    }
                },
                new Result
                {
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xf12b"),
                    Title = "Open Log Location",
                    IcoPath = "Images\\app.png",
                    CopyText = Context.API.GetLogDirectory(),
                    AutoCompleteText = Context.API.GetLogDirectory(),
                    Action = c =>
                    {
                        Context.API.OpenDirectory(Context.API.GetLogDirectory());
                        return true;
                    }
                },
                new Result
                {
                    Title = "Flow Launcher Tips",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe897"),
                    IcoPath = "Images\\app.png",
                    CopyText = Documentation,
                    AutoCompleteText = Documentation,
                    Action = c =>
                    {
                        Context.API.OpenUrl(Documentation);
                        return true;
                    }
                },
                new Result
                {
                    Title = "Flow Launcher UserData Folder",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xf12b"),
                    IcoPath = "Images\\app.png",
                    CopyText = Context.API.GetDataDirectory(),
                    AutoCompleteText = Context.API.GetDataDirectory(),
                    Action = c =>
                    {
                        Context.API.OpenDirectory(Context.API.GetDataDirectory());
                        return true;
                    }
                },
                new Result
                {
                    Title = "Toggle Game Mode",
                    IcoPath = "Images\\app.png",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\ue7fc"),
                    Action = c =>
                    {
                        Context.API.ToggleGameMode();
                        return true;
                    }
                },
                new Result
                {
                    Title = "Set Flow Launcher Theme",
                    IcoPath = "Images\\app.png",
                    Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\ue790"),
                    Action = c =>
                    {
                        if (string.IsNullOrEmpty(query.ActionKeyword))
                        {
                            Context.API.ChangeQuery($"{ThemeSelector.Keyword}{Plugin.Query.ActionKeywordSeparator}");
                        }
                        else
                        {
                            Context.API.ChangeQuery($"{query.ActionKeyword}{Plugin.Query.ActionKeywordSeparator}{ThemeSelector.Keyword}{Plugin.Query.ActionKeywordSeparator}");
                        }
                        return false;
                    }
                }
            ]);

            return results;
        }

        public string GetTranslatedPluginTitle()
        {
            return Localize.flowlauncher_plugin_sys_plugin_name();
        }

        public string GetTranslatedPluginDescription()
        {
            return Localize.flowlauncher_plugin_sys_plugin_description();
        }

        public void OnCultureInfoChanged(CultureInfo _)
        {
            UpdateLocalizedNameDescription(true);
        }
    }
}
