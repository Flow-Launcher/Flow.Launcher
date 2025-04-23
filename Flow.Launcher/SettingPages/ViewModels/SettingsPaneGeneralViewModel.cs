﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneGeneralViewModel : BaseModel
{
    public Settings Settings { get; }

    private readonly Updater _updater;
    private readonly IPortable _portable;
    private readonly Internationalization _translater;

    public SettingsPaneGeneralViewModel(Settings settings, Updater updater, IPortable portable, Internationalization translater)
    {
        Settings = settings;
        _updater = updater;
        _portable = portable;
        _translater = translater;
        UpdateEnumDropdownLocalizations();
    }

    public class SearchWindowScreenData : DropdownDataGeneric<SearchWindowScreens> { }
    public class SearchWindowAlignData : DropdownDataGeneric<SearchWindowAligns> { }
    public class SearchPrecisionData : DropdownDataGeneric<SearchPrecisionScore> { }
    public class LastQueryModeData : DropdownDataGeneric<LastQueryMode> { }

    public bool StartFlowLauncherOnSystemStartup
    {
        get => Settings.StartFlowLauncherOnSystemStartup;
        set
        {
            Settings.StartFlowLauncherOnSystemStartup = value;

            try
            {
                if (value)
                {
                    if (UseLogonTaskForStartup)
                    {
                        AutoStartup.ChangeToViaLogonTask();
                    }
                    else
                    {
                        AutoStartup.ChangeToViaRegistry();
                    }
                }
                else
                {
                    AutoStartup.DisableViaLogonTaskAndRegistry();
                }  
            }
            catch (Exception e)
            {
                App.API.ShowMsg(App.API.GetTranslation("setAutoStartFailed"), e.Message);
            }
        }
    }

    public bool UseLogonTaskForStartup
    {
        get => Settings.UseLogonTaskForStartup;
        set
        {
            Settings.UseLogonTaskForStartup = value;

            if (StartFlowLauncherOnSystemStartup)
            {
                try
                {
                    if (value)
                    {
                        AutoStartup.ChangeToViaLogonTask();
                    }
                    else
                    {
                        AutoStartup.ChangeToViaRegistry();
                    }
                }
                catch (Exception e)
                {
                    App.API.ShowMsg(App.API.GetTranslation("setAutoStartFailed"), e.Message);
                }
            } 
        }
    }

    public List<SearchWindowScreenData> SearchWindowScreens { get; } =
        DropdownDataGeneric<SearchWindowScreens>.GetValues<SearchWindowScreenData>("SearchWindowScreen");

    public List<SearchWindowAlignData> SearchWindowAligns { get; } =
        DropdownDataGeneric<SearchWindowAligns>.GetValues<SearchWindowAlignData>("SearchWindowAlign");

    public List<SearchPrecisionData> SearchPrecisionScores { get; } =
        DropdownDataGeneric<SearchPrecisionScore>.GetValues<SearchPrecisionData>("SearchPrecision");

    public List<int> ScreenNumbers
    {
        get
        {
            var screens = Screen.AllScreens;
            var screenNumbers = new List<int>();
            for (int i = 1; i <= screens.Length; i++)
            {
                screenNumbers.Add(i);
            }

            return screenNumbers;
        }
    }

    // This is only required to set at startup. When portable mode enabled/disabled a restart is always required
    private static bool _portableMode = DataLocation.PortableDataLocationInUse();

    public bool PortableMode
    {
        get => _portableMode;
        set
        {
            if (!_portable.CanUpdatePortability())
                return;

            if (DataLocation.PortableDataLocationInUse())
            {
                _portable.DisablePortableMode();
            }
            else
            {
                _portable.EnablePortableMode();
            }
        }
    }

    public List<LastQueryModeData> LastQueryModes { get; } =
        DropdownDataGeneric<LastQueryMode>.GetValues<LastQueryModeData>("LastQuery");

    public int SearchDelayTimeValue
    {
        get => Settings.SearchDelayTime;
        set
        {
            if (Settings.SearchDelayTime != value)
            {
                Settings.SearchDelayTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SearchDelayTimeDisplay));
            }
        }
    }
    public string SearchDelayTimeDisplay => $"{SearchDelayTimeValue}ms";

    private void UpdateEnumDropdownLocalizations()
    {
        DropdownDataGeneric<SearchWindowScreens>.UpdateLabels(SearchWindowScreens);
        DropdownDataGeneric<SearchWindowAligns>.UpdateLabels(SearchWindowAligns);
        DropdownDataGeneric<SearchPrecisionScore>.UpdateLabels(SearchPrecisionScores);
        DropdownDataGeneric<LastQueryMode>.UpdateLabels(LastQueryModes);
    }

    public string Language
    {
        get => Settings.Language;
        set
        {
            _translater.ChangeLanguage(value);

            if (_translater.PromptShouldUsePinyin(value))
                ShouldUsePinyin = true;

            UpdateEnumDropdownLocalizations();
        }
    }

    #region Korean IME

    // The new Korean IME used in Windows 11 has compatibility issues with WPF. This issue is difficult to resolve within
    // WPF itself, but it can be avoided by having the user switch to the legacy IME at the system level. Therefore,
    // we provide guidance and a direct button for users to make this change themselves. If the relevant registry key does
    // not exist (i.e., the Korean IME is not installed), this setting will not be shown at all.

    public bool LegacyKoreanIMEEnabled
    {
        get => Win32Helper.IsLegacyKoreanIMEEnabled();
        set
        {
            if (Win32Helper.SetLegacyKoreanIMEEnabled(value))
            {
                OnPropertyChanged();
                OnPropertyChanged(nameof(KoreanIMERegistryValueIsZero));
            }
            else
            {
                //Since this is rarely seen text, language support is not provided.
                App.API.ShowMsg("Failed to change Korean IME setting", "Please check your system registry access or contact support.");
            }
        }
    }

    public bool KoreanIMERegistryKeyExists
    {
        get
        {
            var registryKeyExists = Win32Helper.IsKoreanIMEExist();
            var koreanLanguageInstalled = InputLanguage.InstalledInputLanguages.Cast<InputLanguage>().Any(lang => lang.Culture.Name.StartsWith("ko"));
            var isWindows11 = Win32Helper.IsWindows11();

            // Return true if Windows 11 with Korean IME installed, or if the registry key exists
            return (isWindows11 && koreanLanguageInstalled) || registryKeyExists;
        }
    }

    public bool KoreanIMERegistryValueIsZero
    {
        get
        {
            var value = Win32Helper.GetLegacyKoreanIMERegistryValue();
            if (value is int intValue)
            {
                return intValue == 0;
            }
            else if (value != null && int.TryParse(value.ToString(), out var parsedValue))
            {
                return parsedValue == 0;
            }

            return false;
        }
    }

    [RelayCommand]
    private void OpenImeSettings()
    {
        Win32Helper.OpenImeSettings();
    }

    #endregion

    public bool ShouldUsePinyin
    {
        get => Settings.ShouldUsePinyin;
        set => Settings.ShouldUsePinyin = value;
    }

    public List<Language> Languages => _translater.LoadAvailableLanguages();

    public string AlwaysPreviewToolTip => string.Format(
        App.API.GetTranslation("AlwaysPreviewToolTip"),
        Settings.PreviewHotkey
    );

    private static string GetFileFromDialog(string title, string filter = "")
    {
        var dlg = new OpenFileDialog
        {
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Multiselect = false,
            CheckFileExists = true,
            CheckPathExists = true,
            Title = title,
            Filter = filter
        };

        return dlg.ShowDialog() switch
        {
            DialogResult.OK => dlg.FileName,
            _ => string.Empty
        };
    }

    private void UpdateApp()
    {
        _ = _updater.UpdateAppAsync(false);
    }

    public bool AutoUpdates
    {
        get => Settings.AutoUpdates;
        set
        {
            Settings.AutoUpdates = value;

            if (value)
            {
                UpdateApp();
            }
        }
    }

    [RelayCommand]
    private void SelectPython()
    {
        var selectedFile = GetFileFromDialog(
            App.API.GetTranslation("selectPythonExecutable"),
            "Python|pythonw.exe"
        );

        if (!string.IsNullOrEmpty(selectedFile))
            Settings.PluginSettings.PythonExecutablePath = selectedFile;
    }

    [RelayCommand]
    private void SelectNode()
    {
        var selectedFile = GetFileFromDialog(
            App.API.GetTranslation("selectNodeExecutable"),
            "node|*.exe"
        );

        if (!string.IsNullOrEmpty(selectedFile))
            Settings.PluginSettings.NodeExecutablePath = selectedFile;
    }

    [RelayCommand]
    private void SelectFileManager()
    {
        var fileManagerChangeWindow = new SelectFileManagerWindow(Settings);
        fileManagerChangeWindow.ShowDialog();
    }

    [RelayCommand]
    private void SelectBrowser()
    {
        var browserWindow = new SelectBrowserWindow(Settings);
        browserWindow.ShowDialog();
    }
}
