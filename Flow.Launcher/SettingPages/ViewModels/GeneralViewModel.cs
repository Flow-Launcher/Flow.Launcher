using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Helper;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.SettingPages.ViewModels
{
    public partial class GeneralViewModel
    {
        public Settings Settings { get; set; }
        private readonly Updater _updater;
        private readonly IPortable _portable;
        public ResourceBindingModel<LastQueryMode>[] LastQueryModeModels { get; set; } = Enum
            .GetValues<LastQueryMode>()
            .Select(x => new ResourceBindingModel<LastQueryMode>($"LastQuery{x}", x))
            .ToArray();

        public List<string> QuerySearchPrecisionStrings { get; set; } =
            Enum.GetValues<SearchPrecisionScore>()
                .Select(x => x.ToString())
                .ToList();

        public string Language
        {
            get => Settings.Language;
            set
            {
                InternationalizationManager.Instance.ChangeLanguage(value);

                if (InternationalizationManager.Instance.PromptShouldUsePinyin(value))
                    Settings.ShouldUsePinyin = true;

                Settings.Language = value;
            }
        }
        private Internationalization _translater => InternationalizationManager.Instance;
        public IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);

        public List<Language> Languages { get; set; } = InternationalizationManager.Instance.LoadAvailableLanguages();

        public GeneralViewModel(Settings settings)
        {
            Settings = settings;
        }

        public async void UpdateApp()
        {
            await _updater.UpdateAppAsync(App.API, false);
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
                    Notification.Show(InternationalizationManager.Instance.GetTranslation("setAutoStartFailed"), e.Message);
                }
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


        [RelayCommand]
        private void SelectPythonDirectory()
        {
            var dlg = new FolderBrowserDialog
            {
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            };

            var result = dlg.ShowDialog();
            if (result != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            string pythonDirectory = dlg.SelectedPath;
            if (string.IsNullOrEmpty(pythonDirectory))
            {
                return;
            }

            var pythonPath = Path.Combine(pythonDirectory, PluginsLoader.PythonExecutable);
            if (File.Exists(pythonPath))
            {
                Settings.PluginSettings.PythonDirectory = pythonDirectory;
                MessageBox.Show("Remember to restart Flow Launcher use new Python path");
            }
            else
            {
                MessageBox.Show("Can't find python in given directory");
            }
        }

        [RelayCommand]
        private void SelectExplorer()
        {
            SelectFileManagerWindow fileManagerChangeWindow = new SelectFileManagerWindow(Settings);
            fileManagerChangeWindow.ShowDialog();
        }

        [RelayCommand]
        private void SelectDefaultBrowser()
        {
            var browserWindow = new SelectBrowserWindow(Settings);
            browserWindow.ShowDialog();
        }

        public class SearchWindowPosition
        {
            public string Display { get; set; }
            public SearchWindowPositions Value { get; set; }
        }

        public List<SearchWindowPosition> SearchWindowPositions
        {
            get
            {
                List<SearchWindowPosition> modes = new List<SearchWindowPosition>();
                var enums = (SearchWindowPositions[])Enum.GetValues(typeof(SearchWindowPositions));
                foreach (var e in enums)
                {
                    var key = $"SearchWindowPosition{e}";
                    var display = _translater.GetTranslation(key);
                    var m = new SearchWindowPosition { Display = display, Value = e, };
                    modes.Add(m);
                }
                return modes;
            }
        }


    }
}
