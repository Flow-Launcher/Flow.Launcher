using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.DialogJump;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneGeneralViewModel : BaseModel
{
    public Settings Settings { get; }

    private readonly Updater _updater;
    private readonly Portable _portable;
    private readonly Internationalization _translater;

    public SettingsPaneGeneralViewModel(Settings settings, Updater updater, Portable portable, Internationalization translater)
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
    public class DoublePinyinSchemaData : DropdownDataGeneric<DoublePinyinSchemas> { }

    public bool StartFlowLauncherOnSystemStartup
    {
        get => Settings.StartFlowLauncherOnSystemStartup;
        set
        {
            if (Settings.StartFlowLauncherOnSystemStartup == value) return;

            Settings.StartFlowLauncherOnSystemStartup = value;

            try
            {
                if (value)
                {
                    if (UseLogonTaskForStartup)
                    {
                        AutoStartup.ChangeToViaLogonTask(AlwaysRunAsAdministrator);
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
                App.API.ShowMsgError(Localize.setAutoStartFailed(), e.Message);
            }

            // If we have enabled logon task startup, we need to check if we need to restart the app
            // even if we encounter an error while setting the startup method
            if (value && UseLogonTaskForStartup)
            {
                CheckAdminChangeAndAskForRestart();
            }
        }
    }

    public bool UseLogonTaskForStartup
    {
        get => Settings.UseLogonTaskForStartup;
        set
        {
            if (UseLogonTaskForStartup == value) return;

            Settings.UseLogonTaskForStartup = value;

            if (StartFlowLauncherOnSystemStartup)
            {
                try
                {
                    if (value)
                    {
                        AutoStartup.ChangeToViaLogonTask(AlwaysRunAsAdministrator);
                    }
                    else
                    {
                        AutoStartup.ChangeToViaRegistry();
                    }
                }
                catch (Exception e)
                {
                    App.API.ShowMsgError(Localize.setAutoStartFailed(), e.Message);
                }
            }

            // If we have enabled logon task startup, we need to check if we need to restart the app
            // even if we encounter an error while setting the startup method
            if (StartFlowLauncherOnSystemStartup && value)
            {
                CheckAdminChangeAndAskForRestart();
            }
        }
    }

    public bool AlwaysRunAsAdministrator
    {
        get => Settings.AlwaysRunAsAdministrator;
        set
        {
            if (AlwaysRunAsAdministrator == value) return;

            Settings.AlwaysRunAsAdministrator = value;

            if (StartFlowLauncherOnSystemStartup && UseLogonTaskForStartup)
            {
                try
                {
                    AutoStartup.ChangeToViaLogonTask(value);
                }
                catch (Exception e)
                {
                    App.API.ShowMsg(App.API.GetTranslation("setAutoStartFailed"), e.Message);
                }

                // If we have enabled logon task startup, we need to check if we need to restart the app
                // even if we encounter an error while setting the startup method
                CheckAdminChangeAndAskForRestart();
            }
        }
    }

    private void CheckAdminChangeAndAskForRestart()
    {
        // When we change from non-admin to admin, we need to restart the app as administrator to apply the changes
        // Under non-administrator, we cannot delete or set the logon task which is run as administrator
        if (AlwaysRunAsAdministrator && !Win32Helper.IsAdministrator())
        {
            if (App.API.ShowMsgBox(
                App.API.GetTranslation("runAsAdministratorChangeAndRestart"),
                App.API.GetTranslation("runAsAdministratorChange"),
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                // Restart the app as administrator
                App.API.RestartAppAsAdmin();
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
            var screens = MonitorInfo.GetDisplayMonitors();
            var screenNumbers = new List<int>();
            for (int i = 1; i <= screens.Count; i++)
            {
                screenNumbers.Add(i);
            }

            return screenNumbers;
        }
    }

    // This is only required to set at startup. When portable mode enabled/disabled a restart is always required
    private static readonly bool _portableMode = DataLocation.PortableDataLocationInUse();

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

    public bool EnableDialogJump
    {
        get => Settings.EnableDialogJump;
        set
        {
            if (Settings.EnableDialogJump != value)
            {
                Settings.EnableDialogJump = value;
                DialogJump.SetupDialogJump(value);
                if (Settings.EnableDialogJump)
                {
                    HotKeyMapper.SetHotkey(new(Settings.DialogJumpHotkey), DialogJump.OnToggleHotkey);
                }
                else
                {
                    HotKeyMapper.RemoveHotkey(Settings.DialogJumpHotkey);
                }
            }
        }
    }

    public class DialogJumpWindowPositionData : DropdownDataGeneric<DialogJumpWindowPositions> { }
    public class DialogJumpResultBehaviourData : DropdownDataGeneric<DialogJumpResultBehaviours> { }
    public class DialogJumpFileResultBehaviourData : DropdownDataGeneric<DialogJumpFileResultBehaviours> { }

    public List<DialogJumpWindowPositionData> DialogJumpWindowPositions { get; } =
        DropdownDataGeneric<DialogJumpWindowPositions>.GetValues<DialogJumpWindowPositionData>("DialogJumpWindowPosition");

    public List<DialogJumpResultBehaviourData> DialogJumpResultBehaviours { get; } =
        DropdownDataGeneric<DialogJumpResultBehaviours>.GetValues<DialogJumpResultBehaviourData>("DialogJumpResultBehaviour");

    public List<DialogJumpFileResultBehaviourData> DialogJumpFileResultBehaviours { get; } =
        DropdownDataGeneric<DialogJumpFileResultBehaviours>.GetValues<DialogJumpFileResultBehaviourData>("DialogJumpFileResultBehaviour");

    public int SearchDelayTimeValue
    {
        get => Settings.SearchDelayTime;
        set
        {
            if (Settings.SearchDelayTime != value)
            {
                Settings.SearchDelayTime = value;
                OnPropertyChanged();
            }
        }
    }

    public int MaxHistoryResultsToShowValue
    {
        get => Settings.MaxHistoryResultsToShowForHomePage;
        set
        {
            if (Settings.MaxHistoryResultsToShowForHomePage != value)
            {
                Settings.MaxHistoryResultsToShowForHomePage = value;
                OnPropertyChanged();
            }
        }
    }

    private void UpdateEnumDropdownLocalizations()
    {
        DropdownDataGeneric<SearchWindowScreens>.UpdateLabels(SearchWindowScreens);
        DropdownDataGeneric<SearchWindowAligns>.UpdateLabels(SearchWindowAligns);
        DropdownDataGeneric<SearchPrecisionScore>.UpdateLabels(SearchPrecisionScores);
        DropdownDataGeneric<LastQueryMode>.UpdateLabels(LastQueryModes);
        DropdownDataGeneric<DoublePinyinSchemas>.UpdateLabels(DoublePinyinSchemas);
        DropdownDataGeneric<DialogJumpWindowPositions>.UpdateLabels(DialogJumpWindowPositions);
        DropdownDataGeneric<DialogJumpResultBehaviours>.UpdateLabels(DialogJumpResultBehaviours);
        DropdownDataGeneric<DialogJumpFileResultBehaviours>.UpdateLabels(DialogJumpFileResultBehaviours);
        // Since we are using Binding instead of DynamicResource, we need to manually trigger the update
        OnPropertyChanged(nameof(AlwaysPreviewToolTip));
        Settings.CustomExplorer.OnDisplayNameChanged();
        Settings.CustomBrowser.OnDisplayNameChanged();
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
                // Since this is rarely seen text, language support is not provided.
                App.API.ShowMsgError(Localize.KoreanImeSettingChangeFailTitle(), Localize.KoreanImeSettingChangeFailSubTitle());
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
        set
        {
            if (value == false && UseDoublePinyin == true)
            {
                UseDoublePinyin = false;
            }
            Settings.ShouldUsePinyin = value;
        }
    }

    public bool UseDoublePinyin
    {
        set => Settings.UseDoublePinyin = value;
        get => Settings.UseDoublePinyin;
    }

    public List<DoublePinyinSchemaData> DoublePinyinSchemas { get; } =
        DropdownDataGeneric<DoublePinyinSchemas>.GetValues<DoublePinyinSchemaData>("DoublePinyinSchemas");

    public List<Language> Languages => _translater.LoadAvailableLanguages();

    public string AlwaysPreviewToolTip => Localize.AlwaysPreviewToolTip(Settings.PreviewHotkey);

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
            Localize.selectPythonExecutable(),
            "Python|pythonw.exe"
        );

        if (!string.IsNullOrEmpty(selectedFile))
            Settings.PluginSettings.PythonExecutablePath = selectedFile;
    }

    [RelayCommand]
    private void SelectNode()
    {
        var selectedFile = GetFileFromDialog(
            Localize.selectNodeExecutable(),
            "node|*.exe"
        );

        if (!string.IsNullOrEmpty(selectedFile))
            Settings.PluginSettings.NodeExecutablePath = selectedFile;
    }

    [RelayCommand]
    private void SelectFileManager()
    {
        var fileManagerChangeWindow = new SelectFileManagerWindow();
        fileManagerChangeWindow.ShowDialog();
    }

    [RelayCommand]
    private void SelectBrowser()
    {
        var browserWindow = new SelectBrowserWindow();
        browserWindow.ShowDialog();
    }
}
