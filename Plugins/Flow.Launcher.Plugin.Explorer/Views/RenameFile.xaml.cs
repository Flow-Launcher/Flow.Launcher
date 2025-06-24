using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Plugin.Explorer.Helper;
using Microsoft.VisualBasic.Logging;


namespace Flow.Launcher.Plugin.Explorer.Views
{

    [INotifyPropertyChanged]
    public partial class RenameFile : Window
    {


        public string NewFileName
        {
            get => _newFileName;
            set
            {
                _ = SetProperty(ref _newFileName, value);
            }
        }


        private string _newFileName;

        private readonly IPublicAPI _api;
        private readonly string _oldFilePath;

        private readonly FileSystemInfo _info;

        public RenameFile(IPublicAPI api, FileSystemInfo info)
        {
            _api = api;


            InitializeComponent();

            RenameTb.Focus();

            ShowInTaskbar = false;

            RenameTb.SelectAll();


            _info = info;
            _oldFilePath = _info.FullName;
            NewFileName = _info.Name;


        }

        private void OnDoneButtonClick(object sender, RoutedEventArgs e)
        {
            RenameThing.Rename(NewFileName, _info, _api);
            Close();

        }

        private void BtnCancel(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RenameTb_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnDone.Focus();
                OnDoneButtonClick(sender, e);
                e.Handled = true;
            }



        }
    }
}
