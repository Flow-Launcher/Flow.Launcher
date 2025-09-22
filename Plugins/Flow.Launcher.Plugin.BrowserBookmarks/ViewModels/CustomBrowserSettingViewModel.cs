#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Plugin.BrowserBookmarks.Models;
using System;
using System.Windows.Forms;

namespace Flow.Launcher.Plugin.BrowserBookmarks.ViewModels;

public partial class CustomBrowserSettingViewModel : ObservableObject
{
    private readonly CustomBrowser _originalBrowser;
    private readonly Action<bool> _closeAction;

    [ObservableProperty]
    private CustomBrowser _editableBrowser;

    public CustomBrowserSettingViewModel(CustomBrowser browser, Action<bool> closeAction)
    {
        _originalBrowser = browser;
        _closeAction = closeAction;
        EditableBrowser = new CustomBrowser
        {
            Name = browser.Name,
            DataDirectoryPath = browser.DataDirectoryPath,
            BrowserType = browser.BrowserType
        };
    }

    [RelayCommand]
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
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            EditableBrowser.DataDirectoryPath = dialog.SelectedPath;
        }
    }
}
