using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Flow.Launcher.Avalonia.Views.Dialogs;

public partial class ProgressBoxWindow : Window, INotifyPropertyChanged
{
    private readonly Action? _cancelProgress;
    private bool _isIndeterminate;
    private double _progressValue;
    private string _progressText = "0%";

    private ProgressBoxWindow(string caption, Action? cancelProgress)
    {
        TitleText = caption;
        _cancelProgress = cancelProgress;

        InitializeComponent();
        DataContext = this;
        KeyDown += OnWindowKeyDown;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public string TitleText { get; }

    public double ProgressValue
    {
        get => _progressValue;
        private set
        {
            if (Math.Abs(_progressValue - value) < 0.001)
            {
                return;
            }

            _progressValue = value;
            OnPropertyChanged();
        }
    }

    public string ProgressText
    {
        get => _progressText;
        private set
        {
            if (_progressText == value)
            {
                return;
            }

            _progressText = value;
            OnPropertyChanged();
        }
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        private set
        {
            if (_isIndeterminate == value)
            {
                return;
            }

            _isIndeterminate = value;
            OnPropertyChanged();
        }
    }

    public static async System.Threading.Tasks.Task ShowAsync(string caption, Func<Action<double>, System.Threading.Tasks.Task> reportProgressAsync, Action? cancelProgress = null)
    {
        ProgressBoxWindow? progressWindow = null;
        try
        {
            // Callers may be on a worker thread; the Window must be created on the UI thread.
            progressWindow = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var window = new ProgressBoxWindow(caption, cancelProgress);
                window.Show();
                return window;
            });

            await reportProgressAsync(progressWindow.ReportProgress);
        }
        catch (Exception e)
        {
            App.API?.LogError(nameof(ProgressBoxWindow), $"An error occurred: {e.Message}");

            // Like WPF: run the work again without progress reporting so it still completes.
            await reportProgressAsync(null!);
        }
        finally
        {
            if (progressWindow != null)
            {
                await Dispatcher.UIThread.InvokeAsync(progressWindow.Close);
            }
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ReportProgress(double progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (progress < 0)
            {
                IsIndeterminate = true;
                ProgressText = "Working...";
                return;
            }

            IsIndeterminate = false;
            ProgressValue = Math.Clamp(progress, 0, 100);
            ProgressText = $"{Math.Round(ProgressValue)}%";

            if (ProgressValue >= 100 && IsVisible)
            {
                Close();
            }
        });
    }

    private void OnHideClick(object? sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ForceClose();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            ForceClose();
        }
    }

    private void ForceClose()
    {
        Close();
        _cancelProgress?.Invoke();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
