using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;
using Flow.Launcher.Plugin.Explorer.Helper;
using Flow.Launcher.Plugin.Explorer.Search;
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
    private ResultType AccessLinkType { get; set; } = ResultType.Folder;

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
        AccessLinkType = selectedAccessLink.Type;
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
            if (SelectedAccessLink != null)
            {
                var index = QuickAccessLinks.IndexOf(SelectedAccessLink);
                if (index >= 0)
                {
                    var updatedLink = new AccessLink
                    {
                        Name = SelectedName,
                        Type = AccessLinkType,
                        Path = SelectedPath
                    };
                    QuickAccessLinks[index] = updatedLink;
                }
                DialogResult = true;
                Close();
            }
            // Add a new one if the selected access link is null (should not happen in edit mode, but just in case)
            else
            {
                AddNewAccessLink();
            }
        }
        // Otherwise, add a new one
        else
        {
            AddNewAccessLink();
        }

        void AddNewAccessLink()
        {
            var newAccessLink = new AccessLink
            {
                Name = SelectedName,
                Type = AccessLinkType,
                Path = SelectedPath
            };
            QuickAccessLinks.Add(newAccessLink);
            DialogResult = true;
            Close();
        }
    }

    private void SelectPath_OnClick(object commandParameter, RoutedEventArgs e)
    {
        // If we are editing an existing access link, we should keep the file/folder type consistent.
        ResultType type;
        if (SelectedAccessLink != null)
        {
            type = SelectedAccessLink.Type;
        }
        // Else we should allow the user to select any file or folder.
        else
        {
            var result = Main.Context.API.ShowMsgBox(
                string.Format(
                    Main.Context.API.GetTranslation("plugin_explorer_quickAccessLinkFileOrFolderSubtitle"),
                    Environment.NewLine),
                Main.Context.API.GetTranslation("plugin_explorer_quickAccessLinkFileOrFolderTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            type = result == MessageBoxResult.Yes ? ResultType.File : ResultType.Folder;
        }

        if (type == ResultType.File)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK ||
                string.IsNullOrEmpty(dialog.FileName))
                return;

            SelectedPath = dialog.FileName;
        }
        else
        {
            var dialog = new FolderBrowserDialog
            {
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK ||
                string.IsNullOrEmpty(dialog.SelectedPath))
                return;

            SelectedPath = dialog.SelectedPath;
        }

        AccessLinkType = GetResultType(SelectedPath);
    }

    private static ResultType GetResultType(string path)
    {
        // Check if the path is a file or folder
        if (File.Exists(path))
        {
            return ResultType.File;
        }
        else if (Directory.Exists(path))
        {
            if (string.Equals(Path.GetPathRoot(path), path, StringComparison.OrdinalIgnoreCase))
            {
                return ResultType.Volume;
            }
            else
            {
                return ResultType.Folder;
            }
        }
        else
        {
            // This should not happen, but just in case, we assume it's a folder
            return ResultType.Folder;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
