using System.Windows.Controls;
using Flow.Launcher.Plugin.Calculator.ViewModels;

namespace Flow.Launcher.Plugin.Calculator.Views;

public partial class CalculatorSettings : UserControl
{
    public CalculatorSettings(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
