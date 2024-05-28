using System;
using System.Windows.Input;
using System.Windows.Navigation;
using Flow.Launcher.SettingPages.ViewModels;

namespace Flow.Launcher.SettingPages.Views;

public partial class SettingsPanePlugins
{
    private SettingsPanePluginsViewModel _viewModel = null!;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (!IsInitialized)
        {
            if (e.ExtraData is not SettingWindow.PaneData { Settings: { } settings })
                throw new ArgumentException("Settings are required for SettingsPaneHotkey.");
            _viewModel = new SettingsPanePluginsViewModel(settings);
            DataContext = _viewModel;
            InitializeComponent();
        }
        base.OnNavigatedTo(e);
    }

    private void SettingsPanePlugins_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not Key.F || Keyboard.Modifiers is not ModifierKeys.Control) return;
        PluginFilterTextbox.Focus();
    }
}
