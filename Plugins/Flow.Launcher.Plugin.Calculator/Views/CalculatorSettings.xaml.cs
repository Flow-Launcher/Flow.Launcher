using System.Windows.Controls;
using Flow.Launcher.Plugin.Calculator.ViewModels;

namespace Flow.Launcher.Plugin.Calculator.Views
{
    public partial class CalculatorSettings : UserControl
    {
        private readonly SettingsViewModel _viewModel;
        private readonly Settings _settings;

        public CalculatorSettings(SettingsViewModel viewModel)
        {
            _viewModel = viewModel;
            _settings = viewModel.Settings;
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
