using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher
{
    public partial class SelectBrowserWindow : Window, INotifyPropertyChanged
    {
        private int selectedCustomBrowserIndex;

        public event PropertyChangedEventHandler PropertyChanged;

        public Settings Settings { get; }

        public int SelectedCustomBrowserIndex
        {
            get => selectedCustomBrowserIndex; set
            {
                selectedCustomBrowserIndex = value;
                PropertyChanged?.Invoke(this, new(nameof(CustomBrowser)));
            }
        }
        public ObservableCollection<CustomBrowserViewModel> CustomBrowsers { get; set; }

        public CustomBrowserViewModel CustomBrowser => CustomBrowsers[SelectedCustomBrowserIndex];
        public SelectBrowserWindow(Settings settings)
        {
            Settings = settings;
            CustomBrowsers = new ObservableCollection<CustomBrowserViewModel>(Settings.CustomBrowserList.Select(x => x.Copy()));
            SelectedCustomBrowserIndex = Settings.CustomBrowserIndex;
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnDone_Click(object sender, RoutedEventArgs e)
        {
            Settings.CustomBrowserList = CustomBrowsers.ToList();
            Settings.CustomBrowserIndex = SelectedCustomBrowserIndex;
            Close();
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            CustomBrowsers.Add(new()
            {
                Name = "New Profile"
            });
            SelectedCustomBrowserIndex = CustomBrowsers.Count - 1;
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            CustomBrowsers.RemoveAt(SelectedCustomBrowserIndex--);
        }

        private void btnBrowseFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            Nullable<bool> result = dlg.ShowDialog();

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
