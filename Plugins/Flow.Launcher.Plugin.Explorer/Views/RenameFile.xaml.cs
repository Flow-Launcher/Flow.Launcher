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

            // if it's just whitespace and nothing else
            _api.LogInfo(nameof(RenameFile), $"THIS IS NEW FILE NAME: {NewFileName}");
            if (NewFileName.Trim() == "" || NewFileName == "")
            {
                _api.ShowMsgError(string.Format(_api.GetTranslation("plugin_explorer_field_may_not_be_empty"), "New file name"));
                return;
            }

            try
            {
                _info.Rename(NewFileName, _api);
            }
            catch (Exception exception)
            {
                switch (exception)
                {
                    case FileNotFoundException:

                        _api.ShowMsgError(string.Format(_api.GetTranslation("plugin_explorer_file_not_found"), _oldFilePath));
                        break;
                    case NotANewNameException:
                        _api.ShowMsgError(string.Format(_api.GetTranslation("plugin_explorer_not_a_new_name"), NewFileName));
                        _api.ShowMainWindow();
                        break;
                    case InvalidNameException:
                        _api.ShowMsgError(string.Format(_api.GetTranslation("plugin_explorer_invalid_name"), NewFileName));
                        break;
                    case IOException iOException:
                        if (iOException.Message.Contains("incorrect"))
                        {
                            _api.ShowMsgError(string.Format(_api.GetTranslation("plugin_explorer_invalid_name"), NewFileName));
                            break;
                        }
                        else
                        {
                            goto default;
                        }
                    default:
                        _api.ShowMsgError(exception.ToString());
                        break;
                }
            }
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
