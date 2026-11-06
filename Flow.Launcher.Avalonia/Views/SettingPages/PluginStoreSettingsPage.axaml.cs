using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Flow.Launcher.Avalonia.ViewModel.SettingPages;

namespace Flow.Launcher.Avalonia.Views.SettingPages
{
    public partial class PluginStoreSettingsPage : UserControl
    {
        public PluginStoreSettingsPage()
        {
            InitializeComponent();
            DataContext = new PluginStoreSettingsViewModel();
            AddHandler(KeyDownEvent, OnPageKeyDown, RoutingStrategies.Tunnel);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnPageKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 &&
                this.FindControl<TextBox>("SearchTextBox") is { } searchBox)
            {
                searchBox.Focus();
                searchBox.SelectAll();
                e.Handled = true;
            }
        }
    }
}
