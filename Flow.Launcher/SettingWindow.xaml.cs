using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedModels;
using Flow.Launcher.SettingPages.Views;
using Flow.Launcher.ViewModel;
using ModernWpf.Controls;

namespace Flow.Launcher;

public partial class SettingWindow
{
    #region Private Fields

    private readonly Settings _settings;
    private readonly SettingWindowViewModel _viewModel;

    #endregion

    #region Constructor

    public SettingWindow()
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();
        _viewModel = Ioc.Default.GetRequiredService<SettingWindowViewModel>();
        DataContext = _viewModel;
        // Since WindowStartupLocation is set to Manual, initialize the window position before calling InitializeComponent
        UpdatePositionAndState();
        InitializeComponent();
    }

    #endregion

    #region Window Events

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshMaximizeRestoreButton();

        // Fix (workaround) for the window freezes after lock screen (Win+L) or sleep
        // https://stackoverflow.com/questions/4951058/software-rendering-mode-wpf
        HwndSource hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        HwndTarget hwndTarget = hwndSource.CompositionTarget;
        hwndTarget.RenderMode = RenderMode.SoftwareOnly;  // Must use software only render mode here

        UpdatePositionAndState();

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    // Sometimes the navigation is not triggered by button click,
    // so we need to update the selected item here
    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingWindowViewModel.PageType):
                var selectedIndex = _viewModel.PageType.Name switch
                {
                    nameof(SettingsPaneGeneral) => 0,
                    nameof(SettingsPanePlugins) => 1,
                    nameof(SettingsPanePluginStore) => 2,
                    nameof(SettingsPaneTheme) => 3,
                    nameof(SettingsPaneHotkey) => 4,
                    nameof(SettingsPaneProxy) => 5,
                    nameof(SettingsPaneAbout) => 6,
                    _ => 0
                };
                NavView.SelectedItem = NavView.MenuItems[selectedIndex];
                break;
        }
    }

    private void OnClosed(object sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;

        // If app is exiting, settings save is not needed because main window closing event will handle this
        if (App.LoadingOrExiting) return;
        // Save settings when window is closed
        _settings.Save();
        App.API.SavePluginSettings();
    }

    private void OnCloseExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        Close();
    }

    private void window_MouseDown(object sender, MouseButtonEventArgs e) /* for close hotkey popup */
    {
        if (Keyboard.FocusedElement is not TextBox textBox) return;
        var tRequest = new TraversalRequest(FocusNavigationDirection.Next);
        textBox.MoveFocus(tRequest);
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        RefreshMaximizeRestoreButton();
        if (IsLoaded)
        {
            _settings.SettingWindowState = WindowState;
        }
    }

    private void Window_LocationChanged(object sender, EventArgs e)
    {
        if (IsLoaded)
        {
            _settings.SettingWindowTop = Top;
            _settings.SettingWindowLeft = Left;
        }
    }

    #endregion

    #region Window Custom TitleBar

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
            MaximizeButton.Visibility = Visibility.Hidden;
            RestoreButton.Visibility = Visibility.Visible;
        }
        else
        {
            MaximizeButton.Visibility = Visibility.Visible;
            RestoreButton.Visibility = Visibility.Hidden;
        }
    }

    #endregion

    #region Window Position

    public void UpdatePositionAndState()
    {
        var previousTop = _settings.SettingWindowTop;
        var previousLeft = _settings.SettingWindowLeft;

        if (previousTop == null || previousLeft == null || !IsPositionValid(previousTop.Value, previousLeft.Value))
        {
            SetWindowPosition(WindowTop(), WindowLeft());
        }
        else
        {
            var left = _settings.SettingWindowLeft.Value;
            var top = _settings.SettingWindowTop.Value;
            AdjustWindowPosition(ref top, ref left);
            SetWindowPosition(top, left);
        }

        WindowState = _settings.SettingWindowState;
    }

    private void SetWindowPosition(double top, double left)
    {
        // Ensure window does not exceed screen boundaries
        top = Math.Max(top, SystemParameters.VirtualScreenTop);
        left = Math.Max(left, SystemParameters.VirtualScreenLeft);
        top = Math.Min(top, SystemParameters.VirtualScreenHeight - ActualHeight);
        left = Math.Min(left, SystemParameters.VirtualScreenWidth - ActualWidth);

        Top = top;
        Left = left;
    }

    private void AdjustWindowPosition(ref double top, ref double left)
    {
        // Adjust window position if it exceeds screen boundaries
        top = Math.Max(top, SystemParameters.VirtualScreenTop);
        left = Math.Max(left, SystemParameters.VirtualScreenLeft);
        top = Math.Min(top, SystemParameters.VirtualScreenHeight - ActualHeight);
        left = Math.Min(left, SystemParameters.VirtualScreenWidth - ActualWidth);
    }

    private static bool IsPositionValid(double top, double left)
    {
        foreach (var screen in MonitorInfo.GetDisplayMonitors())
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
        var screen = MonitorInfo.GetCursorDisplayMonitor();
        var dip1 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
        var dip2 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
        var left = (dip2.X - ActualWidth) / 2 + dip1.X;
        return left;
    }

    private double WindowTop()
    {
        var screen = MonitorInfo.GetCursorDisplayMonitor();
        var dip1 = Win32Helper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
        var dip2 = Win32Helper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
        var top = (dip2.Y - ActualHeight) / 2 + dip1.Y - 20;
        return top;
    }

    #endregion

    #region Navigation View Events

    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            _viewModel.SetPageType(typeof(SettingsPaneGeneral));
            ContentFrame.Navigate(typeof(SettingsPaneGeneral));
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
            // Only navigate if the page type changes to fix navigation forward/back issue
            if (_viewModel.SetPageType(pageType))
            {
                ContentFrame.Navigate(pageType);
            }
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
        _viewModel.SetPageType(null); // Set page type to null so that NavigationView_SelectionChanged can navigate the frame
        NavView.SelectedItem = NavView.MenuItems[0]; /* Set First Page */
    }

    #endregion
}
