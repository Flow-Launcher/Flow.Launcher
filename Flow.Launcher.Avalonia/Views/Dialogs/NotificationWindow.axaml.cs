using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Avalonia.Views.Dialogs;

public partial class NotificationWindow : Window, INotifyPropertyChanged
{
    private static readonly object ActiveWindowsLock = new();
    private static readonly System.Collections.Generic.List<NotificationWindow> ActiveWindows = [];

    private readonly Action? _buttonAction;
    private readonly DispatcherTimer _closeTimer;
    private IImage? _icon;

    public NotificationWindow(string title, string subTitle, string iconPath, string? actionButtonText = null, Action? buttonAction = null)
    {
        TitleText = title;
        SubtitleText = subTitle;
        ActionButtonText = actionButtonText ?? string.Empty;
        _buttonAction = buttonAction;

        _closeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _closeTimer.Tick += (_, _) => CloseWindow();

        InitializeComponent();
        DataContext = this;

        Opened += OnOpened;
        Closed += OnClosed;

        _ = LoadIconAsync(iconPath);
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public string TitleText { get; }

    public string SubtitleText { get; }

    public string ActionButtonText { get; }

    public bool HasActionButton => !string.IsNullOrWhiteSpace(ActionButtonText) && _buttonAction != null;

    public IImage? NotificationIcon
    {
        get => _icon;
        private set
        {
            if (_icon == value)
            {
                return;
            }

            _icon = value;
            OnPropertyChanged();
        }
    }

    public static void ShowNotification(string title, string subTitle, string iconPath, string? actionButtonText = null, Action? buttonAction = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var window = new NotificationWindow(title, subTitle, iconPath, actionButtonText, buttonAction);
            window.Show();
        });
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async System.Threading.Tasks.Task LoadIconAsync(string iconPath)
    {
        var resolvedPath = File.Exists(iconPath)
            ? iconPath
            : Path.Combine(Constant.ProgramDirectory, "Images", "app.png");

        NotificationIcon = await ImageLoader.LoadAsync(resolvedPath);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        InitializeWindowShape();

        var screen = Screens.Primary;
        if (screen != null)
        {
            lock (ActiveWindowsLock)
            {
                ActiveWindows.Add(this);
                RepositionActiveWindows(screen);
            }
        }

        _closeTimer.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closeTimer.Stop();

        lock (ActiveWindowsLock)
        {
            ActiveWindows.Remove(this);
            if (Screens.Primary is { } primary)
            {
                RepositionActiveWindows(primary);
            }
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        CloseWindow();
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            _buttonAction?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Exception(nameof(NotificationWindow), "Notification action failed", ex);
        }

        CloseWindow();
    }

    private void CloseWindow()
    {
        if (IsVisible)
        {
            Close();
        }
    }

    // Platform hook: shape the native window to match the rounded NotificationBorder.
    partial void InitializeWindowShape();

    private static void RepositionActiveWindows(Screen screen)
    {
        // Position is in physical pixels; Width/Height are in DIPs.
        var workingArea = screen.WorkingArea;
        var scaling = screen.Scaling;
        const double gap = 12;
        for (var index = 0; index < ActiveWindows.Count; index++)
        {
            var window = ActiveWindows[index];
            var x = workingArea.Right - (int)((window.Width + gap) * scaling);
            var y = workingArea.Y + (int)((gap + index * (window.Height + gap)) * scaling);
            window.Position = new PixelPoint(x, y);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
