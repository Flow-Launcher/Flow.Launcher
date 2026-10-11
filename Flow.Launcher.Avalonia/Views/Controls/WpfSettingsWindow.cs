using System;
using Avalonia.Controls.ApplicationLifetimes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;

namespace Flow.Launcher.Avalonia.Views.Controls;

/// <summary>
/// A standalone WPF Window that hosts plugin settings controls.
/// This avoids scrolling and rendering issues with embedded HwndSource.
/// </summary>
public class WpfSettingsWindow : Window
{
    public WpfSettingsWindow(Control settingsControl, string pluginName)
    {
        Title = $"{pluginName} Settings";
        Width = 800;
        Height = 600;
        MinWidth = 400;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        
        // Set proper background to avoid black background issue
        Background = SystemColors.ControlBrush;
        
        // Wrap in a ScrollViewer for proper scrolling
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = settingsControl,
            Padding = new Thickness(10)
        };
        
        Content = scrollViewer;
    }

    private static void SetAvaloniaOwner(WpfSettingsWindow window)
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var ownerHandle = desktop.MainWindow?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (ownerHandle == IntPtr.Zero)
        {
            return;
        }

        new WindowInteropHelper(window).Owner = ownerHandle;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
    }

    /// <summary>
    /// Shows the settings window for the given plugin.
    /// </summary>
    public static void Show(Control settingsControl, string pluginName)
    {
        var window = new WpfSettingsWindow(settingsControl, pluginName);
        SetAvaloniaOwner(window);
        window.Show();
    }

    /// <summary>
    /// Shows the settings window as a modal dialog.
    /// </summary>
    public static void ShowDialog(Control settingsControl, string pluginName)
    {
        var window = new WpfSettingsWindow(settingsControl, pluginName);
        SetAvaloniaOwner(window);
        window.ShowDialog();
    }
}
