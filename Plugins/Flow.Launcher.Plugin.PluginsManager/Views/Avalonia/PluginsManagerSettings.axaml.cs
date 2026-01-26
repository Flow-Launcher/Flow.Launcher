using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Flow.Launcher.Plugin.PluginsManager.ViewModels;

namespace Flow.Launcher.Plugin.PluginsManager.Views.Avalonia;

public partial class PluginsManagerSettings : UserControl
{
    public PluginsManagerSettings()
    {
        InitializeComponent();
    }

    internal PluginsManagerSettings(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}