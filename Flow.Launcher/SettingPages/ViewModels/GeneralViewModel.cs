using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.SettingPages.ViewModels
{
    public partial class GeneralViewModel
    {
        public Settings Settings { get; set; }

        public LastQueryModeModel[] LastQueryModeModels { get; set; } = Enum
            .GetValues<Infrastructure.UserSettings.LastQueryMode>()
            .Select(x => new LastQueryModeModel
            {
                Display = InternationalizationManager.Instance.GetTranslation($"LastQuery{x}"), Value = x
            }).ToArray();

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
                UpdateLastQueryModeDisplay();
            }
        }

        public IEnumerable<int> MaxResultsRange => Enumerable.Range(2, 16);

        public List<Language> Languages { get; set; } = InternationalizationManager.Instance.LoadAvailableLanguages();

        public GeneralViewModel(Settings settings)
        {
            Settings = settings;
        }
        
        private void UpdateLastQueryModeDisplay()
        {
            foreach (var item in LastQueryModeModels)
            {
                item.Display = InternationalizationManager.Instance.GetTranslation($"LastQuery{item.Value}");
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
    }

    public partial class LastQueryModeModel : BaseModel
    {
        public string Display { get; set; }
        public Infrastructure.UserSettings.LastQueryMode Value { get; set; }
    }
}
