using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;
using Flow.Launcher.Plugin.Explorer.Search;
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
                if (string.IsNullOrEmpty(_selectedName))  SelectedName = GetPathName();
            }
        }
    }


    private string _selectedName;

    public string SelectedName
    {
        get
        {
            if (string.IsNullOrEmpty(_selectedName)) return GetPathName();
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
    
    private Settings Settings { get; }
    public QuickAccessLinkSettings(Settings settings)
    {
        Settings = settings;
        InitializeComponent();
    }

    public QuickAccessLinkSettings(Settings settings,AccessLink selectedAccessLink)
    {
        IsEdit = true;
        _selectedName = selectedAccessLink.Name;
        _selectedPath = selectedAccessLink.Path;
        SelectedAccessLink = selectedAccessLink;
        Settings = settings;
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

        if (Settings.QuickAccessLinks.Any(x => x.Path == SelectedPath && x.Name == SelectedName))
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
        var container = Settings.QuickAccessLinks;
        var newAccessLink = new AccessLink { Name = SelectedName, Path = SelectedPath };
        container.Add(newAccessLink);
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

    private string GetPathName()
    {
        if (string.IsNullOrEmpty(SelectedPath)) return "";
        var path = SelectedPath.EndsWith(Constants.DirectorySeparator) ? SelectedPath[0..^1] : SelectedPath;

        if (path.EndsWith(':'))
            return path[0..^1] + " Drive";

        return path.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.None)
            .Last();
    }

    private void EditAccessLink()
    {
        if (SelectedAccessLink == null)throw new ArgumentException("Access Link object is null");

        
        // Talvez nao seja preciso buscar pelo hash code, mas sim pelo nome ou path
        // Uma possivel validação, se pode nomes e paths iguais
        var obj =  Settings.QuickAccessLinks.FirstOrDefault(x => x.GetHashCode() == SelectedAccessLink.GetHashCode());
        int index = Settings.QuickAccessLinks.IndexOf(obj);
        if (index >= 0)
        {
            SelectedAccessLink = new AccessLink { Name = SelectedName, Path = SelectedPath };
            Settings.QuickAccessLinks[index] = SelectedAccessLink;
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

