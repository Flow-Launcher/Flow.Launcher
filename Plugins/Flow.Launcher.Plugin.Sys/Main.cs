using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Shutdown;
using Application = System.Windows.Application;
using Control = System.Windows.Controls.Control;

namespace Flow.Launcher.Plugin.Sys;

public class Main : IPlugin, ISettingProvider, IPluginI18n
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

    // SHTDN_REASON_MAJOR_OTHER indicates a generic shutdown reason that isn't categorized under hardware failure,
    // software updates, or other predefined reasons.
    // SHTDN_REASON_FLAG_PLANNED marks the shutdown as planned rather than an unexpected shutdown or failure
    private const SHUTDOWN_REASON REASON = SHUTDOWN_REASON.SHTDN_REASON_MAJOR_OTHER |
        SHUTDOWN_REASON.SHTDN_REASON_FLAG_PLANNED;

    private const string Documentation = "https://flowlauncher.com/docs/#/usage-tips";

    internal static PluginInitContext Context { get; private set; }
    private Settings _settings;
    private SettingsViewModel _viewModel;

    public Control CreateSettingPanel()
    {
        UpdateLocalizedNameDescription(false);
        return new SysSettings(_viewModel);
    }

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

            if (!PInvoke.AdjustTokenPrivileges(tokenHandle, false, &privileges, 0, null, null))
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

    private static List<Result> Commands(Query query)
    {
        var results = new List<Result>();
        var recycleBinFolder = "shell:RecycleBinFolder";
        results.AddRange(
        [
            new Result
            {
                Title = "Shutdown",
                Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xe7e8"),
                IcoPath = "Images\\shutdown.png",
                Action = c =>
                {
                    var result = Context.API.ShowMsgBox(
                        Context.API.GetTranslation("flowlauncher_plugin_sys_dlgtext_shutdown_computer"),
                        Context.API.GetTranslation("flowlauncher_plugin_sys_shutdown_computer"),
                        MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                        if (EnableShutdownPrivilege())
                            PInvoke.ExitWindowsEx(EXIT_WINDOWS_FLAGS.EWX_SHUTDOWN | EXIT_WINDOWS_FLAGS.EWX_POWEROFF, REASON);
                        else
                            Process.Start("shutdown", "/s /t 0");

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
                    var result = Context.API.ShowMsgBox(
                        Context.API.GetTranslation("flowlauncher_plugin_sys_dlgtext_restart_computer"),
                        Context.API.GetTranslation("flowlauncher_plugin_sys_restart_computer"),
                        MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                        if (EnableShutdownPrivilege())
                            PInvoke.ExitWindowsEx(EXIT_WINDOWS_FLAGS.EWX_REBOOT, REASON);
                        else
                            Process.Start("shutdown", "/r /t 0");

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
                    var result = Context.API.ShowMsgBox(
                        Context.API.GetTranslation("flowlauncher_plugin_sys_dlgtext_restart_computer_advanced"),
                        Context.API.GetTranslation("flowlauncher_plugin_sys_restart_computer"),
                        MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                        if (EnableShutdownPrivilege())
                            PInvoke.ExitWindowsEx(EXIT_WINDOWS_FLAGS.EWX_REBOOT | EXIT_WINDOWS_FLAGS.EWX_BOOTOPTIONS, REASON);
                        else
                            Process.Start("shutdown", "/r /o /t 0");

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
                    var result = Context.API.ShowMsgBox(
                        Context.API.GetTranslation("flowlauncher_plugin_sys_dlgtext_logoff_computer"),
                        Context.API.GetTranslation("flowlauncher_plugin_sys_log_off"),
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
                            string.Format(Context.API.GetTranslation("flowlauncher_plugin_sys_dlgtext_empty_recycle_bin_failed"), Environment.NewLine),
                            Context.API.GetTranslation("flowlauncher_plugin_sys_dlgtitle_error"),
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
            },
            new Result
            {
                Title = "Save Settings",
                Glyph = new GlyphInfo (FontFamily:"/Resources/#Segoe Fluent Icons", Glyph:"\xea35"),
                IcoPath = "Images\\app.png",
                Action = c =>
                {
                    Context.API.SaveAppAllSettings();
                    Context.API.ShowMsg(Context.API.GetTranslation("flowlauncher_plugin_sys_dlgtitle_success"),
                        Context.API.GetTranslation("flowlauncher_plugin_sys_dlgtext_all_settings_saved"));
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
                            Context.API.GetTranslation("flowlauncher_plugin_sys_dlgtitle_success"),
                            Context.API.GetTranslation(
                                "flowlauncher_plugin_sys_dlgtext_all_applicableplugins_reloaded")),
                        System.Threading.Tasks.TaskScheduler.Current);

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
        return Context.API.GetTranslation("flowlauncher_plugin_sys_plugin_name");
    }

    public string GetTranslatedPluginDescription()
    {
        return Context.API.GetTranslation("flowlauncher_plugin_sys_plugin_description");
    }

    public void OnCultureInfoChanged(CultureInfo _)
    {
        UpdateLocalizedNameDescription(true);
    }
}
