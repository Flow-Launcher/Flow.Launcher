﻿using System;
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

    private bool IsEdit { get; set; }
    private AccessLink SelectedAccessLink { get; }
    
    public ObservableCollection<AccessLink> QuickAccessLinks { get; }
    
    public QuickAccessLinkSettings(ObservableCollection<AccessLink> quickAccessLinks)
    {
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
        if (string.IsNullOrEmpty(SelectedName) || string.IsNullOrEmpty(SelectedPath))
        {
            var warning = Main.Context.API.GetTranslation("plugin_explorer_quick_access_link_no_folder_selected");
            Main.Context.API.ShowMsgBox(warning);
            return;
        }
        
        if (QuickAccessLinks.Any(x =>
                x.Path.Equals(SelectedPath, StringComparison.OrdinalIgnoreCase) &&
                                      x.Name.Equals(SelectedName, StringComparison.OrdinalIgnoreCase)))
        {
            var warning = Main.Context.API.GetTranslation("plugin_explorer_quick_access_link_path_already_exists");
            Main.Context.API.ShowMsgBox(warning);
            return;
        }
        if (IsEdit) 
        {
            EditAccessLink();
            return;
        }
        var newAccessLink = new AccessLink { Name = SelectedName, Path = SelectedPath };
        QuickAccessLinks.Add(newAccessLink);
        DialogResult = true;
        Close();
    }

    private void SelectPath_OnClick(object commandParameter, RoutedEventArgs e)
    {
        var folderBrowserDialog = new FolderBrowserDialog();

        if (folderBrowserDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        SelectedPath = folderBrowserDialog.SelectedPath;
    }
    
    private void EditAccessLink()
    {
        if (SelectedAccessLink == null) throw new ArgumentException("Access Link object is null");

        var index = QuickAccessLinks.IndexOf(SelectedAccessLink);
        if (index >= 0)
        {
            var updatedLink = new AccessLink { Name = SelectedName, Path = SelectedPath };
            QuickAccessLinks[index] = updatedLink;
        }
        DialogResult = true;
        IsEdit = false;
        Close();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
