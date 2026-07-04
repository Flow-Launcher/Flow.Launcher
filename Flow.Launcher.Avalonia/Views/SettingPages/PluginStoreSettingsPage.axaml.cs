using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Flow.Launcher.Avalonia.ViewModel.SettingPages;

namespace Flow.Launcher.Avalonia.Views.SettingPages
{
    public partial class PluginStoreSettingsPage : UserControl
    {
        public PluginStoreSettingsPage()
        {
            InitializeComponent();
            DataContext = new PluginStoreSettingsViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
