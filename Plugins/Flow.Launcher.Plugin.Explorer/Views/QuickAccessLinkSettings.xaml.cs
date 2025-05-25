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
                SelectedName = GetPathName();
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


    public QuickAccessLinkSettings()
    {
        InitializeComponent();
    }
    
    
    
    private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnDoneButtonClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(SelectedName) && string.IsNullOrEmpty(SelectedPath))
        {
            var warning = Main.Context.API.GetTranslation("plugin_explorer_quick_access_link_no_folder_selected");
            Main.Context.API.ShowMsgBox(warning);
            return;
        }
        var container = Settings.QuickAccessLinks;

        
        // Lembrar de colocar uma logica pra evitar path e name vazios
        var newAccessLink = new AccessLink
        {
            Name = SelectedName,
            Path = SelectedPath
        };
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

