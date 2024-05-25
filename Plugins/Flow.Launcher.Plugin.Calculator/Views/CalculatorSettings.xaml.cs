using System.Windows;
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

        public CalculatorSettings(SettingsViewModel viewModel)
        {
            _viewModel = viewModel;
            _settings = viewModel.Settings;
            DataContext = viewModel;
            InitializeComponent();
        }

        private void CalculatorSettings_Loaded(object sender, RoutedEventArgs e)
        {
            DecimalSeparatorComboBox.SelectedItem = _settings.DecimalSeparator;
            MaxDecimalPlaces.SelectedItem = _settings.MaxDecimalPlaces;
        }
    }

    
}
