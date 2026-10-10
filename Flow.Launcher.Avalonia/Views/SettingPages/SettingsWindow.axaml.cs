using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Flow.Launcher.Avalonia.ViewModel.SettingPages;
using System;
using System.Collections.Generic;

namespace Flow.Launcher.Avalonia.Views.SettingPages;

public partial class SettingsWindow : Window
{
    // Pages are created lazily once per window and reused on tab swaps.
    private readonly Dictionary<string, Control> _pageCache = new();

    public SettingsWindow()
    {
        SystemFontCache.Warm();
        InitializeComponent();
        
        NavView.SelectionChanged += NavView_SelectionChanged;
        
        // Load default page
        LoadPage("General");
    }

    private void NavView_SelectionChanged(object? sender, FANavigationViewSelectionChangedEventArgs e)
    {
        if (e.SelectedItem is FANavigationViewItem item && item.Tag is string tag)
        {
            LoadPage(tag);
        }
    }

    private Control? _currentPage;

    // Plugin set each plugin-dependent page was built against. Their VMs snapshot the loaded plugins at
    // construction, so a cached page is rebuilt if a plugin was uninstalled meanwhile (as before caching).
    private readonly Dictionary<string, HashSet<string>> _pluginSnapshots = new();

    private static bool DependsOnPluginSet(string tag) => tag is "Plugins" or "PluginStore";

    private static HashSet<string> CurrentPluginIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var plugin in Flow.Launcher.Core.Plugin.PluginManager.GetAllLoadedPlugins())
        {
            ids.Add(plugin.Metadata.ID);
        }
        return ids;
    }

    // Visited pages stay attached and are toggled via IsVisible: re-attaching a page re-applies
    // styles/templates and resubscribes resources, which cost 20-200 ms per swap.
    private void LoadPage(string tag)
    {
        if (_pageCache.TryGetValue(tag, out var cached) && DependsOnPluginSet(tag) &&
            _pluginSnapshots.TryGetValue(tag, out var snapshot) && !snapshot.SetEquals(CurrentPluginIds()))
        {
            if (ReferenceEquals(_currentPage, cached))
            {
                _currentPage = null;
            }
            ContentFrame.Children.Remove(cached);
            DisposePage(cached);
            _pageCache.Remove(tag);
        }

        if (!_pageCache.TryGetValue(tag, out var page))
        {
            page = tag switch
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
            _pageCache[tag] = page;
            if (DependsOnPluginSet(tag))
            {
                _pluginSnapshots[tag] = CurrentPluginIds();
            }
            page.IsVisible = false;
            ContentFrame.Children.Add(page);
        }

        if (ReferenceEquals(_currentPage, page))
        {
            return;
        }

        if (_currentPage != null)
        {
            _currentPage.IsVisible = false;
        }

        page.IsVisible = true;
        _currentPage = page;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ContentFrame.Children.Clear();
        _currentPage = null;
        foreach (var page in _pageCache.Values)
        {
            DisposePage(page);
        }
        _pageCache.Clear();
        _pluginSnapshots.Clear();
    }

    private static void DisposePage(Control page)
    {
        if (page.DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
        page.DataContext = null;
    }
}
