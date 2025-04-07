using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.SettingPages.ViewModels;
using Flow.Launcher.Infrastructure.UserSettings;
using System.Windows.Controls;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPanePlugins
{
    private SettingsPanePluginsViewModel _viewModel = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!IsInitialized)
        {
            var settings = Ioc.Default.GetRequiredService<Settings>();
            _viewModel = new SettingsPanePluginsViewModel(settings);
            DataContext = _viewModel;
            InitializeComponent();
        }
        base.OnNavigatedTo(e);
    }
    
    private void DisplayModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SettingsPanePluginsViewModel viewModel)
        {
            viewModel.UpdateDisplayModeFromSelection();
        }
    }

    private void SettingsPanePlugins_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not Key.F || Keyboard.Modifiers is not ModifierKeys.Control) return;
        PluginFilterTextbox.Focus();
    }
}
