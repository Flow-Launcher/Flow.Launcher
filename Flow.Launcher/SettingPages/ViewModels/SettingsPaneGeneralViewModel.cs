using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;
using Microsoft.Win32;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using System.Windows.Input;


namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneGeneralViewModel : BaseModel
{
    public Settings Settings { get; }
    private readonly Updater _updater;
    private readonly IPortable _portable;
    public ICommand OpenImeSettingsCommand { get; }
    
    public SettingsPaneGeneralViewModel(Settings settings, Updater updater, IPortable portable)
    {
        Settings = settings;
        _updater = updater;
        _portable = portable;
        UpdateEnumDropdownLocalizations();
        IsLegacyKoreanIMEEnabled();
        OpenImeSettingsCommand = new RelayCommand(OpenImeSettings);
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
                        AutoStartup.EnableViaLogonTask();
                    }
                    else
                    {
                        AutoStartup.EnableViaRegistry();
                    }
                }
                else
                {
                    AutoStartup.DisableViaLogonTaskAndRegistry();
                }  
            }
            catch (Exception e)
            {
                Notification.Show(InternationalizationManager.Instance.GetTranslation("setAutoStartFailed"),
                    e.Message);
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
                    if (UseLogonTaskForStartup)
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
                    Notification.Show(InternationalizationManager.Instance.GetTranslation("setAutoStartFailed"),
                        e.Message);
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
    private bool _portableMode = DataLocation.PortableDataLocationInUse();

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
            InternationalizationManager.Instance.ChangeLanguage(value);

            if (InternationalizationManager.Instance.PromptShouldUsePinyin(value))
                ShouldUsePinyin = true;

            UpdateEnumDropdownLocalizations();
        }
    }
    
     public bool LegacyKoreanIMEEnabled
    {
        get => IsLegacyKoreanIMEEnabled();
        set
        {
            SetLegacyKoreanIMEEnabled(value);
            OnPropertyChanged(nameof(LegacyKoreanIMEEnabled));
            OnPropertyChanged(nameof(KoreanIMERegistryValueIsZero));
        }
    }

    public bool KoreanIMERegistryKeyExists => IsKoreanIMEExist();

    public bool KoreanIMERegistryValueIsZero
    {
        get
        {
            object value = GetLegacyKoreanIMERegistryValue();
            if (value is int intValue)
            {
                return intValue == 0;
            }
            else if (value != null && int.TryParse(value.ToString(), out int parsedValue))
            {
                return parsedValue == 0;
            }

            return false;
        }
    }

    bool IsKoreanIMEExist()
    {
        return GetLegacyKoreanIMERegistryValue() != null;
    }

    bool IsLegacyKoreanIMEEnabled()
    {
        object value = GetLegacyKoreanIMERegistryValue();

        if (value is int intValue)
        {
            return intValue == 1;
        }
        else if (value != null && int.TryParse(value.ToString(), out int parsedValue))
        {
            return parsedValue == 1;
        }

        return false;
    }

    bool SetLegacyKoreanIMEEnabled(bool enable)
    {
        const string subKeyPath = @"Software\Microsoft\input\tsf\tsf3override\{A028AE76-01B1-46C2-99C4-ACD9858AE02F}";
        const string valueName = "NoTsf3Override5";

        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(subKeyPath))
            {
                if (key != null)
                {
                    int value = enable ? 1 : 0;
                    key.SetValue(valueName, value, RegistryValueKind.DWord);
                    return true;
                }
                else
                {
                    Debug.WriteLine($"[IME DEBUG] 레지스트리 키 생성 또는 열기 실패: {subKeyPath}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[IME DEBUG] 레지스트리 설정 중 예외 발생: {ex.Message}");
        }

        return false;
    }

    private object GetLegacyKoreanIMERegistryValue()
    {
        const string subKeyPath = @"Software\Microsoft\input\tsf\tsf3override\{A028AE76-01B1-46C2-99C4-ACD9858AE02F}";
        const string valueName = "NoTsf3Override5";

        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subKeyPath))
            {
                if (key != null)
                {
                    return key.GetValue(valueName);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[IME DEBUG] 예외 발생: {ex.Message}");
        }

        return null;
    }

    private void OpenImeSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:regionlanguage") { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Error opening IME settings: {e.Message}");
        }
    }

    public bool ShouldUsePinyin
    {
        get => Settings.ShouldUsePinyin;
        set => Settings.ShouldUsePinyin = value;
    }

    public List<Language> Languages => InternationalizationManager.Instance.LoadAvailableLanguages();

    public string AlwaysPreviewToolTip => string.Format(
        InternationalizationManager.Instance.GetTranslation("AlwaysPreviewToolTip"),
        Settings.PreviewHotkey
    );

    private string GetFileFromDialog(string title, string filter = "")
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
            InternationalizationManager.Instance.GetTranslation("selectPythonExecutable"),
            "Python|pythonw.exe"
        );

        if (!string.IsNullOrEmpty(selectedFile))
            Settings.PluginSettings.PythonExecutablePath = selectedFile;
    }

    [RelayCommand]
    private void SelectNode()
    {
        var selectedFile = GetFileFromDialog(
            InternationalizationManager.Instance.GetTranslation("selectNodeExecutable"),
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
