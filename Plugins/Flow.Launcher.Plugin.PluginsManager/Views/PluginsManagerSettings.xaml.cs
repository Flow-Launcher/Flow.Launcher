
using Flow.Launcher.Plugin.PluginsManager.ViewModels;

namespace Flow.Launcher.Plugin.PluginsManager.Views
{
    /// <summary>
    /// Interaction logic for PluginsManagerSettings.xaml
    /// </summary>
    public partial class PluginsManagerSettings
    {
        private readonly SettingsViewModel viewModel;

        internal PluginsManagerSettings(SettingsViewModel viewModel)
        {
            InitializeComponent();

            this.viewModel = viewModel;

            this.DataContext = viewModel;
        }
    }
}
