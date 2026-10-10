using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using System;

namespace Flow.Launcher.Avalonia.Views.SettingPages;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        // Tunnel so the shortcut works even when a focused TextBox or ComboBox would otherwise handle the key.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        NavView.SelectionChanged += NavView_SelectionChanged;

        // Load default page
        LoadPage("General");
    }

    // Cmd+W on macOS, Ctrl+W elsewhere: the platform's "command" modifier.
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var commandModifiers = global::Avalonia.Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        if (e.Key == Key.W && e.KeyModifiers == commandModifiers)
        {
            e.Handled = true;
            Close();
        }
    }

    private void NavView_SelectionChanged(object? sender, FANavigationViewSelectionChangedEventArgs e)
    {
        if (e.SelectedItem is FANavigationViewItem item && item.Tag is string tag)
        {
            LoadPage(tag);
        }
    }

    private void LoadPage(string tag)
    {
        Control? page = tag switch
        {
            "General" => new GeneralSettingsPage(),
            "Plugins" => new PluginsSettingsPage(),
            "PluginStore" => new PluginStoreSettingsPage(),
            "Theme" => new ThemeSettingsPage(),
            "Hotkey" => new HotkeySettingsPage(),
            "Proxy" => new ProxySettingsPage(),
            "About" => new AboutSettingsPage(),
            _ => new TextBlock { Text = $"Page {tag} not implemented yet", HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center }
        };

        if (page != null)
        {
            ContentFrame.Content = page;
        }
    }
}
