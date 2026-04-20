using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

public partial class SelectFileManagerWindow : Window, INotifyPropertyChanged
{
    private readonly Internationalization _i18n;
    private readonly Settings _settings;
    private int _selectedCustomExplorerIndex;

    public SelectFileManagerWindow()
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();
        _i18n = Ioc.Default.GetRequiredService<Internationalization>();
        CustomExplorers = new ObservableCollection<CustomExplorerViewModel>(_settings.CustomExplorerList.Select(x => x.Copy()));
        _selectedCustomExplorerIndex = Math.Clamp(_settings.CustomExplorerIndex, 0, Math.Max(0, CustomExplorers.Count - 1));

        InitializeComponent();
        DataContext = this;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<CustomExplorerViewModel> CustomExplorers { get; }

    public int SelectedCustomExplorerIndex
    {
        get => _selectedCustomExplorerIndex;
        set
        {
            var clampedValue = Math.Clamp(value, 0, Math.Max(0, CustomExplorers.Count - 1));
            if (_selectedCustomExplorerIndex == clampedValue)
            {
                return;
            }

            _selectedCustomExplorerIndex = clampedValue;
            RaiseCurrentExplorerChanged();
        }
    }

    public CustomExplorerViewModel CurrentExplorer => CustomExplorers[SelectedCustomExplorerIndex];

    public bool CanSave =>
        !string.IsNullOrWhiteSpace(CurrentExplorer.Name) &&
        !string.IsNullOrWhiteSpace(CurrentExplorer.Path);

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        CustomExplorers.Add(new CustomExplorerViewModel
        {
            Name = Translate("defaultBrowser_new_profile", "New profile")
        });

        SelectedCustomExplorerIndex = CustomExplorers.Count - 1;
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (!CurrentExplorer.Editable)
        {
            return;
        }

        CustomExplorers.RemoveAt(SelectedCustomExplorerIndex);
        SelectedCustomExplorerIndex = Math.Clamp(SelectedCustomExplorerIndex, 0, Math.Max(0, CustomExplorers.Count - 1));
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Translate("fileManager_path", "File manager executable"),
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

        CurrentExplorer.Path = selectedPath;
        RaiseCurrentExplorerChanged();
    }

    private void OnProfileTextChanged(object? sender, TextChangedEventArgs e)
    {
        CurrentExplorer.OnDisplayNameChanged();
        OnPropertyChanged(nameof(CustomExplorers));
        OnPropertyChanged(nameof(CanSave));
    }

    private void OnValueTextChanged(object? sender, TextChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private async void OnDoneClick(object? sender, RoutedEventArgs e)
    {
        if (!CanSave)
        {
            return;
        }

        if (!await ConfirmInvalidFileManagerAsync())
        {
            return;
        }

        _settings.CustomExplorerList = CustomExplorers.Select(x => x.Copy()).ToList();
        _settings.CustomExplorerIndex = SelectedCustomExplorerIndex;
        Close(true);
    }

    private async System.Threading.Tasks.Task<bool> ConfirmInvalidFileManagerAsync()
    {
        if (IsFileManagerValid(CurrentExplorer.Path))
        {
            return true;
        }

        var dialog = new ContentDialog
        {
            Title = Translate("fileManagerPathError", "File manager path error"),
            Content = string.Format(
                Translate("fileManagerPathNotFound", "The file manager path for {0} was not found: {1}"),
                CurrentExplorer.Name,
                CurrentExplorer.Path),
            PrimaryButtonText = Translate("yes", "Yes"),
            CloseButtonText = Translate("no", "No")
        };

        var result = await dialog.ShowAsync(this);
        return result == ContentDialogResult.Primary;
    }

    private static bool IsFileManagerValid(string path)
    {
        if (string.Equals(path, "explorer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Path.IsPathRooted(path))
        {
            return File.Exists(path);
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = path,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return false;
        }
    }

    private void RaiseCurrentExplorerChanged()
    {
        OnPropertyChanged(nameof(CurrentExplorer));
        OnPropertyChanged(nameof(CanSave));
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
