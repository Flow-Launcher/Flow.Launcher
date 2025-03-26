using System.Windows.Controls;
using Flow.Launcher.Plugin.ProcessKiller.ViewModels;

namespace Flow.Launcher.Plugin.ProcessKiller.Views;

public partial class SettingsControl : UserControl
{
    private readonly SettingsViewModel _viewModel;

    public SettingsControl(SettingsViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;

        DataContext = viewModel;
    }
}
