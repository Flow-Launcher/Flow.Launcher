using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;

namespace Flow.Launcher.Avalonia.Views.Dialogs;

public partial class NotificationWindow : Window, INotifyPropertyChanged
{
    private static readonly object ActiveWindowsLock = new();
    private static int _activeWindows;

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
        var screen = Screens.Primary;
        if (screen != null)
        {
            lock (ActiveWindowsLock)
            {
                var x = screen.WorkingArea.X + screen.WorkingArea.Width - (int)Width - 20;
                var y = screen.WorkingArea.Y + 20 + (_activeWindows * ((int)Height - 8));
                Position = new PixelPoint(x, y);
                _activeWindows++;
            }
        }

        _closeTimer.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closeTimer.Stop();

        lock (ActiveWindowsLock)
        {
            _activeWindows = Math.Max(0, _activeWindows - 1);
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
