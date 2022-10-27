using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using System.Windows.Forms;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.ViewModel;
using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Core.Plugin;
using System.IO;
using MessageBox = System.Windows.Forms.MessageBox;
using Path = System.IO.Path;

namespace Flow.Launcher.Pages
{
    public partial class General : Page
    {
        public readonly IPublicAPI API;
        private Settings settings;
        private SettingWindowViewModel viewModel;
        public General()
        {
            //settings = viewModel.Settings;
            DataContext = viewModel;
            this.viewModel = viewModel;
            InitializeComponent();
        }

        private void OnSelectPythonDirectoryClick(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderBrowserDialog
            {
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            };

            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                string pythonDirectory = dlg.SelectedPath;
                if (!string.IsNullOrEmpty(pythonDirectory))
                {
                    var pythonPath = Path.Combine(pythonDirectory, PluginsLoader.PythonExecutable);
                    if (File.Exists(pythonPath))
                    {
                        settings.PluginSettings.PythonDirectory = pythonDirectory;
                        MessageBox.Show("Remember to restart Flow Launcher use new Python path");
                    }
                    else
                    {
                        MessageBox.Show("Can't find python in given directory");
                    }
                }
            }
        }

        private void OnSelectFileManagerClick(object sender, RoutedEventArgs e)
        {
            SelectFileManagerWindow fileManagerChangeWindow = new SelectFileManagerWindow(settings);
            fileManagerChangeWindow.ShowDialog();
        }

        private void OnSelectDefaultBrowserClick(object sender, RoutedEventArgs e)
        {
            var browserWindow = new SelectBrowserWindow(settings);
            browserWindow.ShowDialog();
        }
    }
}
