using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Avalonia.ViewModel.SettingPages;
using Flow.Launcher.Infrastructure.UserSettings;
using FluentAvalonia.UI.Controls;
using System;
using System.ComponentModel;
using System.Linq;
using SettingState = System.Windows.WindowState;

namespace Flow.Launcher.Avalonia.Views.SettingPages;

public partial class SettingsWindow : Window
{
    private static SettingsWindow? _instance;

    private readonly Settings _settings;

    public SettingsWindow()
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();

        InitializeComponent();

        RestoreSizeAndPosition();
        ApplyFont();

        // Tunnel so the shortcut works even when a focused TextBox or ComboBox would otherwise handle the key.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Bubble, handledEventsToo: false);
        NavView.SelectionChanged += NavView_SelectionChanged;
        _settings.PropertyChanged += Settings_PropertyChanged;
        Closing += OnClosing;
        Closed += OnClosed;

        // Load default page
        LoadPage("General");
    }

    /// <summary>
    /// Shows the single settings window, creating it if needed. When <paramref name="pluginId"/> is given,
    /// navigates to the Plugins page and focuses that plugin.
    /// </summary>
    public static void Open(string? pluginId = null)
    {
        _instance ??= new SettingsWindow();
        var window = _instance;

        if (!window.IsVisible)
            window.Show();
        if (window.WindowState == global::Avalonia.Controls.WindowState.Minimized)
            window.WindowState = global::Avalonia.Controls.WindowState.Normal;
        window.Activate();

        if (!string.IsNullOrEmpty(pluginId))
            window.NavigateToPlugin(pluginId);
    }

    private void NavigateToPlugin(string pluginId)
    {
        var item = NavView.MenuItems.OfType<FANavigationViewItem>().FirstOrDefault(i => i.Tag as string == "Plugins");
        if (item != null && !ReferenceEquals(NavView.SelectedItem, item))
            NavView.SelectedItem = item; // triggers LoadPage via SelectionChanged

        if (ContentFrame.Content is not PluginsSettingsPage)
            LoadPage("Plugins");

        if (ContentFrame.Content is PluginsSettingsPage { DataContext: PluginsSettingsViewModel vm })
            vm.FocusPlugin(pluginId);
    }

    private void RestoreSizeAndPosition()
    {
        if (_settings.SettingWindowWidth >= MinWidth)
            Width = _settings.SettingWindowWidth;
        if (_settings.SettingWindowHeight >= MinHeight)
            Height = _settings.SettingWindowHeight;

        if (_settings.SettingWindowTop is { } top && _settings.SettingWindowLeft is { } left && IsPositionValid(left, top))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint((int)left, (int)top);
        }

        if (_settings.SettingWindowState == SettingState.Maximized)
            WindowState = global::Avalonia.Controls.WindowState.Maximized;
    }

    // The saved top-left must sit inside some screen's working area, otherwise the window opens off-screen.
    private bool IsPositionValid(double left, double top)
    {
        var point = new PixelPoint((int)left, (int)top);
        return Screens.All.Any(s => s.WorkingArea.Contains(point));
    }

    private void SaveSizeAndPosition()
    {
        var state = WindowState;
        _settings.SettingWindowState = state == global::Avalonia.Controls.WindowState.Maximized
            ? SettingState.Maximized
            : SettingState.Normal;

        // Maximized/minimized bounds are not the user's chosen geometry.
        if (state != global::Avalonia.Controls.WindowState.Normal)
            return;

        _settings.SettingWindowWidth = Width;
        _settings.SettingWindowHeight = Height;
        _settings.SettingWindowTop = Position.Y;
        _settings.SettingWindowLeft = Position.X;
    }

    private void ApplyFont()
    {
        if (!string.IsNullOrWhiteSpace(_settings.SettingWindowFont))
            FontFamily = new FontFamily(_settings.SettingWindowFont);
    }

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.SettingWindowFont))
            global::Avalonia.Threading.Dispatcher.UIThread.Post(ApplyFont);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e) => SaveSizeAndPosition();

    private void OnClosed(object? sender, EventArgs e)
    {
        _settings.PropertyChanged -= Settings_PropertyChanged;
        if (ReferenceEquals(_instance, this))
            _instance = null;

        _settings.Save();
        App.API.SavePluginSettings();
    }

    // Clicking empty space drops focus from text boxes / hotkey recorders.
    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Control { Focusable: true })
            return;
        FocusManager?.ClearFocus();
    }

    // Esc, or Cmd+W on macOS / Ctrl+W elsewhere: the platform's "command" modifier.
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var commandModifiers = global::Avalonia.Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        if ((e.Key == Key.W && e.KeyModifiers == commandModifiers) || (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None))
        {
            e.Handled = true;
            Close();
        }
    }

    private void NavView_SelectionChanged(object? sender, FANavigationViewSelectionChangedEventArgs e)
    {
        if (e.SelectedItem is FANavigationViewItem item && item.Tag is string tag)
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
            "PluginStore" => new PluginStoreSettingsPage(),
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
