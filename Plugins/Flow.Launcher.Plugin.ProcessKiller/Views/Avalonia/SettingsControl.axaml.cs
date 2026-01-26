using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Flow.Launcher.Plugin.ProcessKiller.ViewModels;

namespace Flow.Launcher.Plugin.ProcessKiller.Views.Avalonia;

public partial class SettingsControl : UserControl
{
    public SettingsControl()
    {
        InitializeComponent();
    }

    public SettingsControl(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
