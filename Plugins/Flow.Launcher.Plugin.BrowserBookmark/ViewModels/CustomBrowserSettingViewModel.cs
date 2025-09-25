#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Plugin.BrowserBookmark.Models;
using Flow.Launcher.Plugin.BrowserBookmark.Services;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Plugin.BrowserBookmark.ViewModels;

public partial class CustomBrowserSettingViewModel : ObservableObject
{
    private readonly CustomBrowser _originalBrowser;
    private readonly Action<bool> _closeAction;

    [ObservableProperty]
    private CustomBrowser _editableBrowser;

    public string DetectedEngineText
    {
        get
        {
            if (string.IsNullOrEmpty(EditableBrowser.DataDirectoryPath))
            {
                return Localize.flowlauncher_plugin_browserbookmark_engine_detection_select_directory();
            }

            return EditableBrowser.BrowserType switch
            {
                BrowserType.Unknown => Localize.flowlauncher_plugin_browserbookmark_engine_detection_invalid(),
                BrowserType.Chromium => Localize.flowlauncher_plugin_browserbookmark_engine_detection_chromium(),
                BrowserType.Firefox => Localize.flowlauncher_plugin_browserbookmark_engine_detection_firefox(),
                _ => string.Empty
            };
        }
    }

    public bool IsValidPath => EditableBrowser.BrowserType != BrowserType.Unknown;

    public CustomBrowserSettingViewModel(CustomBrowser browser, Action<bool> closeAction)
    {
        _originalBrowser = browser;
        _closeAction = closeAction;
        EditableBrowser = new CustomBrowser
        {
            Name = browser.Name,
            DataDirectoryPath = browser.DataDirectoryPath
        };
        EditableBrowser.PropertyChanged += EditableBrowser_PropertyChanged;
        DetectEngineType();
    }

    private void EditableBrowser_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CustomBrowser.DataDirectoryPath))
        {
            DetectEngineType();
        }
    }

    private void DetectEngineType()
    {
        EditableBrowser.BrowserType = BrowserDetector.DetectBrowserType(EditableBrowser.DataDirectoryPath);
        OnPropertyChanged(nameof(DetectedEngineText));
        OnPropertyChanged(nameof(IsValidPath));
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(IsValidPath))]
    private void Save()
    {
        _originalBrowser.Name = EditableBrowser.Name;
        _originalBrowser.DataDirectoryPath = EditableBrowser.DataDirectoryPath;
        _originalBrowser.BrowserType = EditableBrowser.BrowserType;
        _closeAction(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        _closeAction(false);
    }

    [RelayCommand]
    private void BrowseDataDirectory()
    {
        var dialog = new FolderBrowserDialog();
        if (!string.IsNullOrEmpty(EditableBrowser.DataDirectoryPath) && Directory.Exists(EditableBrowser.DataDirectoryPath))
        {
            dialog.SelectedPath = EditableBrowser.DataDirectoryPath;
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            EditableBrowser.DataDirectoryPath = dialog.SelectedPath;
        }
    }
}
