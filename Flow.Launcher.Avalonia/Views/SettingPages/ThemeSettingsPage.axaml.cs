using System;
using Avalonia;
using Avalonia.Controls;
using Flow.Launcher.Avalonia.ViewModel.SettingPages;

namespace Flow.Launcher.Avalonia.Views.SettingPages;

public partial class ThemeSettingsPage : UserControl
{
    public ThemeSettingsPage()
    {
        InitializeComponent();
        DataContext = new ThemeSettingsViewModel();
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
}
