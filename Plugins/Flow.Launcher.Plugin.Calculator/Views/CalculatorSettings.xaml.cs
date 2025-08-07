using System.Windows.Controls;
using Flow.Launcher.Plugin.Calculator.ViewModels;

namespace Flow.Launcher.Plugin.Calculator.Views
{
    /// <summary>
    /// Interaction logic for CalculatorSettings.xaml
    /// </summary>
    public partial class CalculatorSettings : UserControl
    {
        private readonly SettingsViewModel _viewModel;
        private readonly Settings _settings;

        public CalculatorSettings(Settings settings)
        {
            _viewModel = new SettingsViewModel(settings);
            _settings = _viewModel.Settings;
            DataContext = _viewModel;
            InitializeComponent();
        }
    }
}
