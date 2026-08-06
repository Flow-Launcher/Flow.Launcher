using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.DependencyInjection;
using FluentAvalonia.UI.Controls;
using Flow.Launcher.Avalonia.Resource;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Avalonia.Views.SettingPages;

public partial class SelectBrowserWindow : Window, INotifyPropertyChanged
{
    private readonly Internationalization _i18n;
    private readonly Settings _settings;
    private int _selectedCustomBrowserIndex;

    public SelectBrowserWindow()
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();
        _i18n = Ioc.Default.GetRequiredService<Internationalization>();
        CustomBrowsers = new ObservableCollection<CustomBrowserViewModel>(_settings.CustomBrowserList.Select(x => x.Copy()));
        if (CustomBrowsers.Count == 0)
        {
            CustomBrowsers.Add(new CustomBrowserViewModel
            {
                Name = Translate("defaultBrowser_new_profile", "New profile")
            });
        }
        _selectedCustomBrowserIndex = Math.Clamp(_settings.CustomBrowserIndex, 0, Math.Max(0, CustomBrowsers.Count - 1));

        InitializeComponent();
        DataContext = this;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CustomBrowserViewModel> CustomBrowsers { get; }

    public int SelectedCustomBrowserIndex
    {
        get => _selectedCustomBrowserIndex;
        set
        {
            var clampedValue = Math.Clamp(value, 0, Math.Max(0, CustomBrowsers.Count - 1));
            if (_selectedCustomBrowserIndex == clampedValue)
            {
                return;
            }

            _selectedCustomBrowserIndex = clampedValue;
            RaiseCurrentBrowserChanged();
        }
    }

    public CustomBrowserViewModel CurrentBrowser => CustomBrowsers[SelectedCustomBrowserIndex];

    public bool IsOpenInTab
    {
        get => CurrentBrowser.OpenInTab;
        set
        {
            if (CurrentBrowser.OpenInTab == value)
            {
                return;
            }

            CurrentBrowser.OpenInTab = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOpenInNewWindow));
        }
    }

    public bool IsOpenInNewWindow
    {
        get => !CurrentBrowser.OpenInTab;
        set
        {
            if (value == !CurrentBrowser.OpenInTab)
            {
                return;
            }

            CurrentBrowser.OpenInTab = !value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOpenInTab));
        }
    }

    public bool HasPrivateArg => !string.IsNullOrWhiteSpace(CurrentBrowser.PrivateArg);

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        CustomBrowsers.Add(new CustomBrowserViewModel
        {
            Name = Translate("defaultBrowser_new_profile", "New profile")
        });

        SelectedCustomBrowserIndex = CustomBrowsers.Count - 1;
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (!CurrentBrowser.Editable)
        {
            return;
        }

        CustomBrowsers.RemoveAt(SelectedCustomBrowserIndex);
        if (CustomBrowsers.Count == 0)
        {
            CustomBrowsers.Add(new CustomBrowserViewModel
            {
                Name = Translate("defaultBrowser_new_profile", "New profile")
            });
        }

        SelectedCustomBrowserIndex = Math.Clamp(SelectedCustomBrowserIndex, 0, Math.Max(0, CustomBrowsers.Count - 1));
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Translate("defaultBrowser_path", "Browser executable"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Executable files")
                {
                    Patterns = ["*.exe", "*.cmd", "*.bat", "*.com"]
                },
                FilePickerFileTypes.All
            ]
        });

        var selectedPath = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        CurrentBrowser.Path = selectedPath;
        RaiseCurrentBrowserChanged();
    }

    private void OnProfileTextChanged(object? sender, TextChangedEventArgs e)
    {
        CurrentBrowser.OnDisplayNameChanged();
        OnPropertyChanged(nameof(CustomBrowsers));
    }

    private void OnPrivateArgTextChanged(object? sender, TextChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasPrivateArg));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnDoneClick(object? sender, RoutedEventArgs e)
    {
        _settings.CustomBrowserList = CustomBrowsers.Select(x => x.Copy()).ToList();
        _settings.CustomBrowserIndex = SelectedCustomBrowserIndex;
        Close(true);
    }

    private void RaiseCurrentBrowserChanged()
    {
        OnPropertyChanged(nameof(CurrentBrowser));
        OnPropertyChanged(nameof(IsOpenInTab));
        OnPropertyChanged(nameof(IsOpenInNewWindow));
        OnPropertyChanged(nameof(HasPrivateArg));
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
}
