using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

    /// <summary>
    /// Shows the settings window for the given plugin.
    /// </summary>
    public static void Show(Control settingsControl, string pluginName)
    {
        var window = new WpfSettingsWindow(settingsControl, pluginName);
        window.Show();
    }

    /// <summary>
    /// Shows the settings window as a modal dialog.
    /// </summary>
    public static void ShowDialog(Control settingsControl, string pluginName)
    {
        var window = new WpfSettingsWindow(settingsControl, pluginName);
        window.ShowDialog();
    }
}
