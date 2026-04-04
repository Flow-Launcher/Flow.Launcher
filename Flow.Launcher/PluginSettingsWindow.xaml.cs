using System;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;
using iNKORE.UI.WPF.Modern.Controls;

namespace Flow.Launcher;

public partial class PluginSettingsWindow
{
    private readonly Settings _settings;

    public string PluginId { get; }

    public PluginSettingsWindow(string pluginId)
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();

        if (string.IsNullOrWhiteSpace(pluginId)){
            throw new ArgumentException("Plugin ID cannot be null or whitespace.", nameof(pluginId));
        }

        PluginId = pluginId;

        var pluginPair = PluginManager.GetPluginForId(pluginId);
        if (pluginPair == null)
        {
            throw new InvalidOperationException($"Unable to find plugin: {pluginId}");
        }

        var pluginSettings = _settings.PluginSettings.GetPluginSettings(pluginId);
        if (pluginSettings == null)
        {
            throw new InvalidOperationException($"Unable to load settings for plugin: {pluginPair.Metadata.Name}");
        }

        var pluginViewModel = new PluginViewModel
        {
            PluginPair = pluginPair,
            PluginSettingsObject = pluginSettings,
            IsExpanded = true,
        };

        DataContext = pluginViewModel;
        Title = Localize.pluginSettingsWindowTitle(pluginPair.Metadata.Name);
        InitializeComponent();
    }

    // This is used for Priority control to force its value to be 0 when the user clears the value.
    private void NumberBox_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue))
        {
            sender.Value = 0;
        }
    }

    private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreButtonClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState switch
        {
            WindowState.Maximized => WindowState.Normal,
            _ => WindowState.Maximized
        };
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        Close();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshMaximizeRestoreButton();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        RefreshMaximizeRestoreButton();
    }

    private void RefreshMaximizeRestoreButton()
    {
        if (WindowState == WindowState.Maximized)
        {
            MaximizeButton.Visibility = Visibility.Hidden;
            RestoreButton.Visibility = Visibility.Visible;
        }
        else
        {
            MaximizeButton.Visibility = Visibility.Visible;
            RestoreButton.Visibility = Visibility.Hidden;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!App.LoadingOrExiting)
        {
            _settings.Save();
            App.API.SavePluginSettings();
        }

        base.OnClosed(e);
    }
}
