#nullable enable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Flow.Launcher.Plugin.Explorer.Helper;
using Flow.Launcher.Plugin.Explorer.Search;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;

namespace Flow.Launcher.Plugin.Explorer.Views.Avalonia;

public partial class QuickAccessLinkSettings : Window, INotifyPropertyChanged
{
    private static readonly string ClassName = nameof(QuickAccessLinkSettings);

    private string _selectedPath = string.Empty;
    public string SelectedPath
    {
        get => _selectedPath;
        set
        {
            if (_selectedPath != value)
            {
                _selectedPath = value;
                OnPropertyChanged();
                if (string.IsNullOrEmpty(_selectedName))
                {
                    SelectedName = _selectedPath.GetPathName();
                }
                if (!string.IsNullOrEmpty(_selectedPath))
                {
                    _accessLinkType = GetResultType(_selectedPath);
                }
            }
        }
    }

    private string _selectedName = string.Empty;
    public string SelectedName
    {
        get => string.IsNullOrEmpty(_selectedName) ? _selectedPath.GetPathName() : _selectedName;
        set
        {
            if (_selectedName != value)
            {
                _selectedName = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isFileSelected;
    public bool IsFileSelected
    {
        get => _isFileSelected;
        set
        {
            if (_isFileSelected != value)
            {
                _isFileSelected = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isFolderSelected = true;
    public bool IsFolderSelected
    {
        get => _isFolderSelected;
        set
        {
            if (_isFolderSelected != value)
            {
                _isFolderSelected = value;
                OnPropertyChanged();
            }
        }
    }

    private bool IsEdit { get; }
    private AccessLink? SelectedAccessLink { get; }
    public ObservableCollection<AccessLink> QuickAccessLinks { get; }
    private ResultType _accessLinkType = ResultType.Folder;

    public QuickAccessLinkSettings()
    {
        QuickAccessLinks = new ObservableCollection<AccessLink>();
        InitializeComponent();
        DataContext = this;
    }

    public QuickAccessLinkSettings(ObservableCollection<AccessLink> quickAccessLinks)
    {
        IsEdit = false;
        QuickAccessLinks = quickAccessLinks;
        InitializeComponent();
        DataContext = this;
    }

    public QuickAccessLinkSettings(ObservableCollection<AccessLink> quickAccessLinks, AccessLink selectedAccessLink)
    {
        IsEdit = true;
        _selectedName = selectedAccessLink.Name;
        _selectedPath = selectedAccessLink.Path;
        _accessLinkType = GetResultType(_selectedPath);
        _isFileSelected = selectedAccessLink.Type == ResultType.File;
        _isFolderSelected = !_isFileSelected;
        SelectedAccessLink = selectedAccessLink;
        QuickAccessLinks = quickAccessLinks;
        InitializeComponent();
        DataContext = this;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void BtnCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnDoneButtonClick(object? sender, RoutedEventArgs e)
    {
        // Validate the input before proceeding
        if (string.IsNullOrEmpty(SelectedName) || string.IsNullOrEmpty(SelectedPath))
        {
            var warning = Localize.plugin_explorer_quick_access_link_no_folder_selected();
            Main.Context.API.ShowMsgBox(warning);
            return;
        }

        // Check if the path already exists in the quick access links (when not editing)
        if (!IsEdit && QuickAccessLinks.Any(x =>
                x.Path.Equals(SelectedPath, StringComparison.OrdinalIgnoreCase) &&
                x.Name.Equals(SelectedName, StringComparison.OrdinalIgnoreCase)))
        {
            var warning = Localize.plugin_explorer_quick_access_link_path_already_exists();
            Main.Context.API.ShowMsgBox(warning);
            return;
        }

        // If editing, update the existing link
        if (IsEdit && SelectedAccessLink != null)
        {
            var index = QuickAccessLinks.IndexOf(SelectedAccessLink);
            if (index >= 0)
            {
                var updatedLink = new AccessLink
                {
                    Name = SelectedName,
                    Type = _accessLinkType,
                    Path = SelectedPath
                };
                QuickAccessLinks[index] = updatedLink;
            }
            Close(true);
        }
        else
        {
            // Add a new one
            var newAccessLink = new AccessLink
            {
                Name = SelectedName,
                Type = _accessLinkType,
                Path = SelectedPath
            };
            QuickAccessLinks.Add(newAccessLink);
            Close(true);
        }
    }

    private async void SelectPath_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        if (IsFileSelected)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                SelectedPath = files[0].Path.LocalPath;
                _accessLinkType = ResultType.File;
            }
        }
        else
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                SelectedPath = folders[0].Path.LocalPath;
                _accessLinkType = GetResultType(SelectedPath);
            }
        }
    }

    private static ResultType GetResultType(string path)
    {
        if (File.Exists(path))
        {
            return ResultType.File;
        }
        
        if (Directory.Exists(path))
        {
            if (string.Equals(Path.GetPathRoot(path), path, StringComparison.OrdinalIgnoreCase))
            {
                return ResultType.Volume;
            }
            return ResultType.Folder;
        }
        
        Main.Context.API.LogError(ClassName, $"The path '{path}' does not exist or is invalid. Defaulting to Folder type.");
        return ResultType.Folder;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
