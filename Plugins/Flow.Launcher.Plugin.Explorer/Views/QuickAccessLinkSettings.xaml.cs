using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;
using Flow.Launcher.Plugin.Explorer.Helper;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;

namespace Flow.Launcher.Plugin.Explorer.Views;

public partial class QuickAccessLinkSettings : INotifyPropertyChanged
{
    private string _selectedPath;
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
            }
        }
    }

    private string _selectedName;
    public string SelectedName
    {
        get
        {
            return string.IsNullOrEmpty(_selectedName) ? _selectedPath.GetPathName() : _selectedName;
        }
        set
        {
            if (_selectedName != value)
            {
                _selectedName = value;
                OnPropertyChanged();
            }
        }
    }

    private bool IsEdit { get; }
    private AccessLink SelectedAccessLink { get; }

    public ObservableCollection<AccessLink> QuickAccessLinks { get; }

    public QuickAccessLinkSettings(ObservableCollection<AccessLink> quickAccessLinks)
    {
        IsEdit = false;
        QuickAccessLinks = quickAccessLinks;
        InitializeComponent();
    }

    public QuickAccessLinkSettings(ObservableCollection<AccessLink> quickAccessLinks, AccessLink selectedAccessLink)
    {
        IsEdit = true;
        _selectedName = selectedAccessLink.Name;
        _selectedPath = selectedAccessLink.Path;
        SelectedAccessLink = selectedAccessLink;
        QuickAccessLinks = quickAccessLinks;
        InitializeComponent();
    }

    private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnDoneButtonClick(object sender, RoutedEventArgs e)
    {
        // Validate the input before proceeding
        if (string.IsNullOrEmpty(SelectedName) || string.IsNullOrEmpty(SelectedPath))
        {
            var warning = Main.Context.API.GetTranslation("plugin_explorer_quick_access_link_no_folder_selected");
            Main.Context.API.ShowMsgBox(warning);
            return;
        }

        // Check if the path already exists in the quick access links
        if (QuickAccessLinks.Any(x =>
                x.Path.Equals(SelectedPath, StringComparison.OrdinalIgnoreCase) &&
                x.Name.Equals(SelectedName, StringComparison.OrdinalIgnoreCase)))
        {
            var warning = Main.Context.API.GetTranslation("plugin_explorer_quick_access_link_path_already_exists");
            Main.Context.API.ShowMsgBox(warning);
            return;
        }

        // If editing, update the existing link
        if (IsEdit)
        {
            if (SelectedAccessLink == null) return;

            var index = QuickAccessLinks.IndexOf(SelectedAccessLink);
            if (index >= 0)
            {
                var updatedLink = new AccessLink
                {
                    Name = SelectedName,
                    Type = SelectedAccessLink.Type,
                    Path = SelectedPath
                };
                QuickAccessLinks[index] = updatedLink;
            }
            DialogResult = true;
            Close();
        }
        // Otherwise, add a new one
        else
        {
            var newAccessLink = new AccessLink
            {
                Name = SelectedName,
                Path = SelectedPath
            };
            QuickAccessLinks.Add(newAccessLink);
            DialogResult = true;
            Close();
        }
    }

    private void SelectPath_OnClick(object commandParameter, RoutedEventArgs e)
    {
        // Open file or folder selection dialog based on the selected radio button
        if (IsFileSelected)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                SelectedPath = openFileDialog.FileName;
            }
        }
        else // Folder selection
        {
            var folderBrowserDialog = new FolderBrowserDialog();

            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SelectedPath = folderBrowserDialog.SelectedPath;
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public bool IsFileSelected { get; set; }
    public bool IsFolderSelected { get; set; }
    
    public QuickAccessLinkSettings()
    {
        IsFolderSelected = true; // Default to folder selection
        InitializeComponent();
        DataContext = this;
    }
}
