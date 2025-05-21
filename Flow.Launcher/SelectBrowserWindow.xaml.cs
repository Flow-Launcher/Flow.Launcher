using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher
{
    [INotifyPropertyChanged]
    public partial class SelectBrowserWindow : Window
    {
        private readonly Settings _settings;

        private int selectedCustomBrowserIndex;

        public int SelectedCustomBrowserIndex
        {
            get => selectedCustomBrowserIndex;
            set
            {
                selectedCustomBrowserIndex = value;
                OnPropertyChanged(nameof(CustomBrowser));
            }
        }

        public ObservableCollection<CustomBrowserViewModel> CustomBrowsers { get; }

        public CustomBrowserViewModel CustomBrowser => CustomBrowsers[SelectedCustomBrowserIndex];

        public SelectBrowserWindow(Settings settings)
        {
            _settings = settings;
            CustomBrowsers = new ObservableCollection<CustomBrowserViewModel>(_settings.CustomBrowserList.Select(x => x.Copy()));
            SelectedCustomBrowserIndex = _settings.CustomBrowserIndex;
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnDone_Click(object sender, RoutedEventArgs e)
        {
            _settings.CustomBrowserList = CustomBrowsers.ToList();
            _settings.CustomBrowserIndex = SelectedCustomBrowserIndex;
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
