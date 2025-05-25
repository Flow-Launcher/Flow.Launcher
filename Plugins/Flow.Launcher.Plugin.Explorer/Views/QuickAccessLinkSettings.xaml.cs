using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;
using Flow.Launcher.Plugin.Explorer.Helper;
using Flow.Launcher.Plugin.Explorer.Search.QuickAccessLinks;
using JetBrains.Annotations;

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
                if (string.IsNullOrEmpty(_selectedName))  SelectedName = _selectedPath.GetPathName();
            }
        }
    }


    private string _selectedName;

    public string SelectedName
    {
        get
        {
            if (string.IsNullOrEmpty(_selectedName)) return _selectedPath.GetPathName();
            return _selectedName;
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
    [CanBeNull] private AccessLink SelectedAccessLink { get; set; }
    
    public ObservableCollection<AccessLink> QuickAccessLinks { get; set; }
    
    public QuickAccessLinkSettings(ObservableCollection<AccessLink> quickAccessLinks)
    {
        QuickAccessLinks = quickAccessLinks;
        InitializeComponent();
    }

    public QuickAccessLinkSettings(ObservableCollection<AccessLink> quickAccessLinks,AccessLink selectedAccessLink)
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

        if (QuickAccessLinks.Any(x => x.Path == SelectedPath && x.Name == SelectedName))
        {
            var warning = Main.Context.API.GetTranslation("plugin_explorer_quick_access_link_select_different_folder");
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
        DialogResult = false;
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
        if (SelectedAccessLink == null)throw new ArgumentException("Access Link object is null");

        var obj =  QuickAccessLinks.FirstOrDefault(x => x.GetHashCode() == SelectedAccessLink.GetHashCode());
        int index = QuickAccessLinks.IndexOf(obj);
        if (index >= 0)
        {
            SelectedAccessLink = new AccessLink { Name = SelectedName, Path = SelectedPath };
            QuickAccessLinks[index] = SelectedAccessLink;
        }
        DialogResult = false;
        IsEdit = false;
        Close();
    }

public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

