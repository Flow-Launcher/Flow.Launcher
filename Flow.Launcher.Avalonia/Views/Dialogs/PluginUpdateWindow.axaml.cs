using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using FluentAvalonia.UI.Controls;
using Flow.Launcher.Avalonia.Resource;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Avalonia.Views.Dialogs;

public partial class PluginUpdateWindow : Window, INotifyPropertyChanged
{
    private readonly Internationalization _i18n;

    public PluginUpdateWindow(List<PluginUpdateInfo> allPlugins)
    {
        _i18n = Ioc.Default.GetRequiredService<Internationalization>();
        var settings = Ioc.Default.GetRequiredService<Settings>();

        Restart = settings.AutoRestartAfterChanging;
        Plugins = new ObservableCollection<PluginUpdateItem>(allPlugins.Select(x => new PluginUpdateItem(x, FormatPluginText(x))));

        TitleText = Translate("updateAllPluginsTitle", "Plugin updates available");
        UpdateButtonText = Translate("updateAllPluginsButtonContent", "Update plugins");

        InitializeComponent();
        DataContext = this;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PluginUpdateItem> Plugins { get; }

    public string TitleText { get; }

    public string UpdateButtonText { get; }

    public bool Restart { get; set; }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnUpdateClick(object? sender, RoutedEventArgs e)
    {
        var selectedPlugins = Plugins.Where(x => x.IsSelected).Select(x => x.Plugin).ToList();
        if (selectedPlugins.Count == 0)
        {
            var dialog = new ContentDialog
            {
                Title = Translate("updatePluginNoSelected", "No plugin selected"),
                CloseButtonText = Translate("commonOK", "OK")
            };
            await dialog.ShowAsync(this);
            return;
        }

        await PluginInstaller.UpdateAllPluginsAsync(selectedPlugins, Restart);
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private string FormatPluginText(PluginUpdateInfo plugin)
    {
        var format = Translate("updatePluginCheckboxContent", "{0}: Update from v{1} to v{2}");
        return string.Format(format, plugin.Name, plugin.CurrentVersion, plugin.NewVersion);
    }

    private string Translate(string key, string fallback)
    {
        var value = _i18n.GetTranslation(key);
        return value.StartsWith('[') ? fallback : value;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class PluginUpdateItem
    {
        public PluginUpdateItem(PluginUpdateInfo plugin, string displayText)
        {
            Plugin = plugin;
            DisplayText = displayText;
            IsSelected = true;
        }

        public PluginUpdateInfo Plugin { get; }

        public string DisplayText { get; }

        public bool IsSelected { get; set; }
    }
}
