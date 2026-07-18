using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.Shell.Views
{
    public partial class CMDSetting : UserControl
    {
        private readonly Settings _settings;

        public CMDSetting(Settings settings)
        {
            _settings = settings;
            DataContext = new ViewModels.ShellSettingViewModel(settings);;
            InitializeComponent();
        }

        private void BrowseExecutablePath_Click(object sender, RoutedEventArgs e)
        {
            var exeLabel = Localize.flowlauncher_plugin_cmd_custom_template_browse_filter_exe();
            var allLabel = Localize.flowlauncher_plugin_cmd_custom_template_browse_filter_all();

            var dialog = new OpenFileDialog
            {
                Filter = $"{exeLabel} (*.exe)|*.exe|{allLabel} (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.FileName))
            {
                _settings.CustomTemplateShellConfig.ExecutablePath = dialog.FileName;
            }
        }
    }
}
