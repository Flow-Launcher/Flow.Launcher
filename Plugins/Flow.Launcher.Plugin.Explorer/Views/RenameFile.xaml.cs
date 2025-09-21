using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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

        private readonly string _oldFilePath;

        private readonly FileSystemInfo _info;

        public RenameFile(FileSystemInfo info)
        {
            _info = info;
            _oldFilePath = _info.FullName;
            NewFileName = _info.Name;

            InitializeComponent();

            ShowInTaskbar = false;
            RenameTb.Focus();
            KeyDown += (s, e) =>
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
                await textBox.Dispatcher.InvokeAsync(textBox.SelectAll, DispatcherPriority.Background);
                return;
            }
            // select everything but the extension
            if (_info is FileInfo info)
            {
                string properName = Path.GetFileNameWithoutExtension(info.Name);
                int start = textBox.Text.LastIndexOf(properName, StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                {
                    await textBox.Dispatcher.InvokeAsync(textBox.SelectAll, DispatcherPriority.Background);
                    return;
                }
                await textBox.Dispatcher.InvokeAsync(() => textBox.Select(start, properName.Length), DispatcherPriority.Background);
            }
        }


        private void OnDoneButtonClick(object sender, RoutedEventArgs e)
        {
            RenameThing.Rename(NewFileName, _info);
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
