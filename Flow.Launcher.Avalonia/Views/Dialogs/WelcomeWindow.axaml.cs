using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Avalonia.ViewModel.SettingPages;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedModels;

namespace Flow.Launcher.Avalonia.Views.Dialogs;

public partial class WelcomeWindow : Window, INotifyPropertyChanged
{
    private int _pageNumber = 1;
    private readonly Settings _settings;

    public WelcomeWindow()
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();
        GeneralSettings = new GeneralSettingsViewModel();

        InitializeComponent();
        DataContext = this;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public Settings Settings => _settings;

    public GeneralSettingsViewModel GeneralSettings { get; }

    public int PageNumber
    {
        get => _pageNumber;
        private set
        {
            if (_pageNumber == value)
            {
                return;
            }

            _pageNumber = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PageDisplay));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
            OnPropertyChanged(nameof(IsPage1));
            OnPropertyChanged(nameof(IsPage2));
            OnPropertyChanged(nameof(IsPage3));
            OnPropertyChanged(nameof(IsPage4));
            OnPropertyChanged(nameof(IsPage5));
        }
    }

    public string PageDisplay => $"{PageNumber}/5";

    public bool CanGoBack => PageNumber > 1;

    public bool CanGoForward => PageNumber < 5;

    public bool IsPage1 => PageNumber == 1;
    public bool IsPage2 => PageNumber == 2;
    public bool IsPage3 => PageNumber == 3;
    public bool IsPage4 => PageNumber == 4;
    public bool IsPage5 => PageNumber == 5;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _settings.Save();
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        if (CanGoBack)
        {
            PageNumber--;
        }
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        if (CanGoForward)
        {
            PageNumber++;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
