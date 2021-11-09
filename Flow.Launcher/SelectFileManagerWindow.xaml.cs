using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Flow.Launcher
{
    /// <summary>
    /// SelectFileManagerWindow.xaml에 대한 상호 작용 논리
    /// </summary>
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
    }
}
