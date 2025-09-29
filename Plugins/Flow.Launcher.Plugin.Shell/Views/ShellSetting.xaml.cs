using System.Windows.Controls;
using Flow.Launcher.Plugin.Shell.ViewModels;

namespace Flow.Launcher.Plugin.Shell.Views
{
    public partial class CMDSetting : UserControl
    {
        public CMDSetting(Settings settings)
        {
            var viewModel = new ShellSettingViewModel(settings);
            DataContext = viewModel;
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
