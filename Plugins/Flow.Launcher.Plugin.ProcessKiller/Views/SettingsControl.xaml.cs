using System.Windows.Controls;
using Flow.Launcher.Plugin.ProcessKiller.ViewModels;

namespace Flow.Launcher.Plugin.ProcessKiller.Views;

public partial class SettingsControl : UserControl
{
    public SettingsControl(SettingsViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
    }
}
