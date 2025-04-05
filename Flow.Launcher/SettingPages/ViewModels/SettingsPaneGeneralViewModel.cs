using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
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

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneGeneralViewModel : BaseModel
{
    public Settings Settings { get; }
    private readonly Updater _updater;
    private readonly IPortable _portable;

    public SettingsPaneGeneralViewModel(Settings settings, Updater updater, IPortable portable)
    {
        Settings = settings;
        _updater = updater;
        _portable = portable;
        UpdateEnumDropdownLocalizations();
        IsLegacyKoreanIMEEnabled();
    }

    public class SearchWindowScreenData : DropdownDataGeneric<SearchWindowScreens> { }
    public class SearchWindowAlignData : DropdownDataGeneric<SearchWindowAligns> { }
    public class SearchPrecisionData : DropdownDataGeneric<SearchPrecisionScore> { }
    public class LastQueryModeData : DropdownDataGeneric<LastQueryMode> { }
    public class SearchDelayTimeData : DropdownDataGeneric<SearchDelayTime> { }

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

    public List<SearchDelayTimeData> SearchDelayTimes { get; } =
        DropdownDataGeneric<SearchDelayTime>.GetValues<SearchDelayTimeData>("SearchDelayTime");

    public SearchDelayTimeData SearchDelayTime
    {
        get => SearchDelayTimes.FirstOrDefault(x => x.Value == Settings.SearchDelayTime) ?? 
               SearchDelayTimes.FirstOrDefault(x => x.Value == Plugin.SearchDelayTime.Normal) ?? 
               SearchDelayTimes.FirstOrDefault();
        set
        {
            if (value == null)
                return;
                
            if (Settings.SearchDelayTime != value.Value)
            {
                Settings.SearchDelayTime = value.Value;
            }
        }
    }

    private void UpdateEnumDropdownLocalizations()
    {
        DropdownDataGeneric<SearchWindowScreens>.UpdateLabels(SearchWindowScreens);
        DropdownDataGeneric<SearchWindowAligns>.UpdateLabels(SearchWindowAligns);
        DropdownDataGeneric<SearchPrecisionScore>.UpdateLabels(SearchPrecisionScores);
        DropdownDataGeneric<LastQueryMode>.UpdateLabels(LastQueryModes);
        DropdownDataGeneric<SearchDelayTime>.UpdateLabels(SearchDelayTimes);
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

    bool IsLegacyKoreanIMEEnabled()
    {
        const string subKeyPath = @"Software\Microsoft\input\tsf\tsf3override\{A028AE76-01B1-46C2-99C4-ACD9858AE02F}";
        const string valueName = "NoTsf3Override5";

        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subKeyPath))
            {
                if (key != null)
                {
                    object value = key.GetValue(valueName);
                    if (value != null)
                    {
                        Debug.WriteLine($"[IME DEBUG] '{valueName}' 값: {value} (타입: {value.GetType()})");

                        if (value is int intValue)
                            return intValue == 1;

                        if (int.TryParse(value.ToString(), out int parsed))
                            return parsed == 1;
                    }
                    else
                    {
                        Debug.WriteLine($"[IME DEBUG] '{valueName}' 값이 존재하지 않습니다.");
                    }
                }
                else
                {
                    Debug.WriteLine($"[IME DEBUG] 레지스트리 키를 찾을 수 없습니다: {subKeyPath}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[IME DEBUG] 예외 발생: {ex.Message}");
        }

        return false; // 기본적으로 새 IME 사용 중으로 간주
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
