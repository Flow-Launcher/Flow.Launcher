using System;
using Flow.Launcher.Avalonia.Views.Controls;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class PluginItemViewModel
{
    partial void OpenWpfSettingsWindow()
    {
        try
        {
            // Create the WPF settings panel and show in a standalone WPF window
            var settingsControl = _settingProvider!.CreateSettingPanel();
            if (settingsControl != null)
            {
                WpfSettingsWindow.Show(settingsControl, Name);
            }
        }
        catch (Exception ex)
        {
            Flow.Launcher.Infrastructure.Logger.Log.Exception(nameof(PluginItemViewModel), $"Failed to open settings for {Name}", ex);
        }
    }
}
