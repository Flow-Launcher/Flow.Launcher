using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Flow.Launcher.Avalonia.ViewModel.SettingPages;

namespace Flow.Launcher.Avalonia.Views.SettingPages;

public partial class PluginsSettingsPage : UserControl
{
    public PluginsSettingsPage()
    {
        InitializeComponent();
        DataContext = new PluginsSettingsViewModel();
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        AddHandler(KeyDownEvent, OnPageKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPageKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0)
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
            e.Handled = true;
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        DetachedFromVisualTree -= OnDetachedFromVisualTree;

        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
            DataContext = null;
        }
    }

    private void ClearSearchText_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PluginsSettingsViewModel vm)
        {
            vm.SearchText = string.Empty;
        }
    }
}
