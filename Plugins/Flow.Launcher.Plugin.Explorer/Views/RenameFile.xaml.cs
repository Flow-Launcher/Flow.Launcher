using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        private readonly IPublicAPI _api;
        private readonly string _oldFilePath;

        private readonly FileSystemInfo _info;

        public RenameFile(IPublicAPI api, FileSystemInfo info)
        {
            _api = api;
            _info = info;
            _oldFilePath = _info.FullName;
            NewFileName = _info.Name;

            InitializeComponent();

            ShowInTaskbar = false;
            RenameTb.Focus();
            var window = Window.GetWindow(this);
            window.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Close();
                }
            };
        }

        /// <summary>
        /// https://stackoverflow.com/a/59560352/24045055
        /// </summary>
        private async void SelectAll_OnTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox) return;
            if (_info is DirectoryInfo)
            {
                await Application.Current.Dispatcher.InvokeAsync(textBox.SelectAll, DispatcherPriority.Background);
            }
            // select everything but the extension
            else if (_info is FileInfo info)
            {
                string properName = Path.GetFileNameWithoutExtension(info.Name);
                Application.Current.Dispatcher.Invoke(textBox.Select, DispatcherPriority.Background, textBox.Text.IndexOf(properName), properName.Length);
            }
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
