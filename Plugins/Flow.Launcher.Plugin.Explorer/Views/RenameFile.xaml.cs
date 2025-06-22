using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Plugin.Explorer.Helper;


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
        private string renamingText = "fes";
        public string RenamingText
        {
            get => renamingText;
            set
            {
                _ = SetProperty(ref renamingText, value);
            }
        }
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
            _newFileName = _info.Name;
            
        }

        private void OnDoneButtonClick(object sender, RoutedEventArgs e)
        {
            // if it's just whitespace and nothing else
            if (_newFileName.Trim() == "")
            {
                _api.ShowMsgError(string.Format(_api.GetTranslation("plugin_explorer_field_may_not_be_empty"), "New file name"));
                Show();
                return;
            }
            if ()
            try
            {
                _info.Rename(_newFileName);
            }
            catch (Exception exception)
            {
                switch (exception)
                {
                    case FileNotFoundException:

                        _api.ShowMsgError(string.Format(_api.GetTranslation("plugin_explorer_file_not_found"), _oldFilePath));
                        break;
                    case NotANewNameException notANewNameException:
                        _api.ShowMsgError(string.Format(_api.GetTranslation("plugin_explorer_not_a_new_name"), _newFileName));
                        _api.ShowMainWindow();
                        break;
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
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }
        
        
        private void RenameTb_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string text = e.DataObject.GetData(DataFormats.Text) as string;
                if (!string.IsNullOrEmpty(text) && text.Any(char.IsWhiteSpace))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
    }
}
