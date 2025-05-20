using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher
{
    [INotifyPropertyChanged]
    public partial class SelectFileManagerWindow : Window
    {
        private readonly Settings _settings;

        private int selectedCustomExplorerIndex;

        public int SelectedCustomExplorerIndex
        {
            get => selectedCustomExplorerIndex;
            set
            {
                selectedCustomExplorerIndex = value;
                OnPropertyChanged(nameof(CustomExplorer));
            }
        }
        public ObservableCollection<CustomExplorerViewModel> CustomExplorers { get; set; }

        public CustomExplorerViewModel CustomExplorer => CustomExplorers[SelectedCustomExplorerIndex];
        public SelectFileManagerWindow(Settings settings)
        {
            _settings = settings;
            CustomExplorers = new ObservableCollection<CustomExplorerViewModel>(_settings.CustomExplorerList.Select(x => x.Copy()));
            SelectedCustomExplorerIndex = _settings.CustomExplorerIndex;
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnDone_Click(object sender, RoutedEventArgs e)
        {
            // Check if the selected file manager path is valid
            if (!IsFileManagerValid(CustomExplorer.Path))
            {
                MessageBoxResult result = MessageBoxEx.Show(
                    string.Format((string)Application.Current.FindResource("fileManagerPathNotFound"), 
                        CustomExplorer.Name, CustomExplorer.Path),
                    (string)Application.Current.FindResource("fileManagerPathError"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            _settings.CustomExplorerList = CustomExplorers.ToList();
            _settings.CustomExplorerIndex = SelectedCustomExplorerIndex;
            Close();
        }

        private bool IsFileManagerValid(string path)
        {
            if (string.Equals(path, "explorer", StringComparison.OrdinalIgnoreCase))
                return true;
            
            if (Path.IsPathRooted(path))
            {
                return File.Exists(path);
            }

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = path,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return !string.IsNullOrEmpty(output);
            }
            catch
            {
                return false;
            }
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            CustomExplorers.Add(new()
            {
                Name = "New Profile"
            });
            SelectedCustomExplorerIndex = CustomExplorers.Count - 1;
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            CustomExplorers.RemoveAt(SelectedCustomExplorerIndex--);
        }

        private void btnBrowseFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            var result = dlg.ShowDialog();
            if (result == true)
            {
                TextBox path = (TextBox)(((FrameworkElement)sender).Parent as FrameworkElement).FindName("PathTextBox");
                path.Text = dlg.FileName;
                path.Focus();
                ((Button)sender).Focus();
            }
        }
    }
}
