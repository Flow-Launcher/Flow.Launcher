using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Flow.Launcher.Plugin.Shell.ViewModels;

namespace Flow.Launcher.Plugin.Shell.Views.Avalonia
{
    public partial class ShellSetting : UserControl
    {
        public ShellSetting()
        {
            InitializeComponent();
        }

        public ShellSetting(Settings settings)
        {
            InitializeComponent();
            DataContext = new ShellSettingViewModel(settings);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
