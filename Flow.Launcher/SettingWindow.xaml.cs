using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.SettingPages.Views;
using Flow.Launcher.ViewModel;
using ModernWpf.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace Flow.Launcher;

public partial class SettingWindow
{
    private readonly IPublicAPI _api;
    private readonly Settings _settings;
    private readonly SettingWindowViewModel _viewModel;

    public SettingWindow(IPublicAPI api, SettingWindowViewModel viewModel)
    {
        _settings = viewModel.Settings;
        DataContext = viewModel;
        _viewModel = viewModel;
        _api = api;
        InitializePosition();
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshMaximizeRestoreButton();
        // Fix (workaround) for the window freezes after lock screen (Win+L)
        // https://stackoverflow.com/questions/4951058/software-rendering-mode-wpf
        HwndSource hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        HwndTarget hwndTarget = hwndSource.CompositionTarget;
        hwndTarget.RenderMode = RenderMode.Default;

        InitializePosition();
    }

    private void OnClosed(object sender, EventArgs e)
    {
        _settings.SettingWindowState = WindowState;
        _settings.SettingWindowTop = Top;
        _settings.SettingWindowLeft = Left;
        _viewModel.Save();
        _api.SavePluginSettings();
    }

    private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        Close();
    }

    private void window_MouseDown(object sender, MouseButtonEventArgs e) /* for close hotkey popup */
    {
        if (Keyboard.FocusedElement is not TextBox textBox)
        {
            return;
        }
        var tRequest = new TraversalRequest(FocusNavigationDirection.Next);
        textBox.MoveFocus(tRequest);
    }

    /* Custom TitleBar */

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

    private void RefreshMaximizeRestoreButton()
    {
        if (WindowState == WindowState.Maximized)
        {
            MaximizeButton.Visibility = Visibility.Collapsed;
            RestoreButton.Visibility = Visibility.Visible;
        }
        else
        {
            MaximizeButton.Visibility = Visibility.Visible;
            RestoreButton.Visibility = Visibility.Collapsed;
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        RefreshMaximizeRestoreButton();
    }

    public void InitializePosition()
    {
        var previousTop = _settings.SettingWindowTop;
        var previousLeft = _settings.SettingWindowLeft;

        if (previousTop == null || previousLeft == null || !IsPositionValid(previousTop.Value, previousLeft.Value))
        {
            Top = WindowTop();
            Left = WindowLeft();
        }
        else
        {
            Top = previousTop.Value;
            Left = previousLeft.Value;
        }
        WindowState = _settings.SettingWindowState;
    }

    private bool IsPositionValid(double top, double left)
    {
        foreach (var screen in Screen.AllScreens)
        {
            var workingArea = screen.WorkingArea;

            if (left >= workingArea.Left && left < workingArea.Right &&
                top >= workingArea.Top && top < workingArea.Bottom)
            {
                return true;
            }
        }
        return false;
    }

    private double WindowLeft()
    {
        var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
        var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
        var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
        var left = (dip2.X - this.ActualWidth) / 2 + dip1.X;
        return left;
    }

    private double WindowTop()
    {
        var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
        var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
        var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
        var top = (dip2.Y - this.ActualHeight) / 2 + dip1.Y - 20;
        return top;
    }

    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var paneData = new PaneData(_settings, _viewModel.Updater, _viewModel.Portable);
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPaneGeneral), paneData);
        }
        else
        {
            var selectedItem = (NavigationViewItem)args.SelectedItem;
            if (selectedItem == null)
            {
                NavView_Loaded(sender, null); /* Reset First Page */
                return;
            }

            var pageType = selectedItem.Name switch
            {
                nameof(General) => typeof(SettingsPaneGeneral),
                nameof(Plugins) => typeof(SettingsPanePlugins),
                nameof(PluginStore) => typeof(SettingsPanePluginStore),
                nameof(Theme) => typeof(SettingsPaneTheme),
                nameof(Hotkey) => typeof(SettingsPaneHotkey),
                nameof(Proxy) => typeof(SettingsPaneProxy),
                nameof(About) => typeof(SettingsPaneAbout),
                _ => typeof(SettingsPaneGeneral)
            };
            ContentFrame.Navigate(pageType, paneData);
        }
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        if (ContentFrame.IsLoaded)
        {
            ContentFrame_Loaded(sender, e);
        }
        else
        {
            ContentFrame.Loaded += ContentFrame_Loaded;
        }
    }

    private void ContentFrame_Loaded(object sender, RoutedEventArgs e)
    {
        NavView.SelectedItem ??= NavView.MenuItems[0]; /* Set First Page */
    }

    public record PaneData(Settings Settings, Updater Updater, IPortable Portable);
}
