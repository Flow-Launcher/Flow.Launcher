using System;
using System.Collections.Generic;
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
        UpdateLastQueryModeDisplay();
    }

    public class SearchWindowScreen : DropdownDataGeneric<SearchWindowScreens> { }
    public class SearchWindowAlign : DropdownDataGeneric<SearchWindowAligns> { }
    // todo a better name?
    public class LastQueryMode : DropdownDataGeneric<Infrastructure.UserSettings.LastQueryMode> { }

    public bool StartFlowLauncherOnSystemStartup
    {
        get => Settings.StartFlowLauncherOnSystemStartup;
        set
        {
            Settings.StartFlowLauncherOnSystemStartup = value;

            try
            {
                if (value)
                    AutoStartup.Enable();
                else
                    AutoStartup.Disable();
            }
            catch (Exception e)
            {
                Notification.Show(InternationalizationManager.Instance.GetTranslation("setAutoStartFailed"),
                    e.Message);
            }
        }
    }


    public List<SearchWindowScreen> SearchWindowScreens =>
        DropdownDataGeneric<SearchWindowScreens>.GetValues<SearchWindowScreen>("SearchWindowScreen");

    public List<SearchWindowAlign> SearchWindowAligns =>
        DropdownDataGeneric<SearchWindowAligns>.GetValues<SearchWindowAlign>("SearchWindowAlign");

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

    private List<LastQueryMode> _lastQueryModes = new();

    public List<LastQueryMode> LastQueryModes
    {
        get
        {
            if (_lastQueryModes.Count == 0)
            {
                _lastQueryModes =
                    DropdownDataGeneric<Infrastructure.UserSettings.LastQueryMode>
                        .GetValues<LastQueryMode>("LastQuery");
            }

            return _lastQueryModes;
        }
    }

    private void UpdateLastQueryModeDisplay()
    {
        foreach (var item in LastQueryModes)
        {
            item.Display = InternationalizationManager.Instance.GetTranslation($"LastQuery{item.Value}");
        }
    }

    public string Language
    {
        get => Settings.Language;
        set
        {
            InternationalizationManager.Instance.ChangeLanguage(value);

            if (InternationalizationManager.Instance.PromptShouldUsePinyin(value))
                ShouldUsePinyin = true;

            UpdateLastQueryModeDisplay();
        }
    }

    public bool ShouldUsePinyin
    {
        get => Settings.ShouldUsePinyin;
        set => Settings.ShouldUsePinyin = value;
    }

    public List<string> QuerySearchPrecisionStrings => Enum
        .GetValues(typeof(SearchPrecisionScore))
        .Cast<SearchPrecisionScore>()
        .Select(v => v.ToString())
        .ToList();

    public List<Language> Languages => InternationalizationManager.Instance.LoadAvailableLanguages();
    public IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);

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
        _ = _updater.UpdateAppAsync(App.API, false);
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
            "*.exe"
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
