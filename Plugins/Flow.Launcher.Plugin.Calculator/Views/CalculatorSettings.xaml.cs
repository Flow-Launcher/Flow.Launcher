using System.Windows.Controls;
using Flow.Launcher.Plugin.Calculator.ViewModels;

namespace Flow.Launcher.Plugin.Calculator.Views;

public partial class CalculatorSettings : UserControl
{
    private readonly SettingsViewModel _viewModel;

    public CalculatorSettings(Settings settings)
    {
        _viewModel = new SettingsViewModel(settings);
        DataContext = _viewModel;
        InitializeComponent();
    }
}
