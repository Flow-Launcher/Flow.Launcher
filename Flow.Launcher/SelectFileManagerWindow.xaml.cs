using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher
{
    public partial class SelectFileManagerWindow : Window, INotifyPropertyChanged
    {
        private int selectedCustomExplorerIndex;

        public event PropertyChangedEventHandler PropertyChanged;

        public Settings Settings { get; }

        public int SelectedCustomExplorerIndex
        {
            get => selectedCustomExplorerIndex; set
            {
                selectedCustomExplorerIndex = value;
                PropertyChanged?.Invoke(this, new(nameof(CustomExplorer)));
            }
        }
        public ObservableCollection<CustomExplorerViewModel> CustomExplorers { get; set; }

        public CustomExplorerViewModel CustomExplorer => CustomExplorers[SelectedCustomExplorerIndex];
        public SelectFileManagerWindow(Settings settings)
        {
            Settings = settings;
            CustomExplorers = new ObservableCollection<CustomExplorerViewModel>(Settings.CustomExplorerList.Select(x => x.Copy()));
            SelectedCustomExplorerIndex = Settings.CustomExplorerIndex;
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnDone_Click(object sender, RoutedEventArgs e)
        {
            Settings.CustomExplorerList = CustomExplorers.ToList();
            Settings.CustomExplorerIndex = SelectedCustomExplorerIndex;
            Close();
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
