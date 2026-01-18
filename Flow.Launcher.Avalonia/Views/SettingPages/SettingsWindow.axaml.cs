using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Flow.Launcher.Avalonia.ViewModel.SettingPages;
using System;

namespace Flow.Launcher.Avalonia.Views.SettingPages;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        
        NavView.SelectionChanged += NavView_SelectionChanged;
        
        // Load default page
        LoadPage("General");
    }

    private void NavView_SelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (e.SelectedItem is NavigationViewItem item && item.Tag is string tag)
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
