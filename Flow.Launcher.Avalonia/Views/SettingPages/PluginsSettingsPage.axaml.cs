using System;
using Avalonia;
using Avalonia.Controls;
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
