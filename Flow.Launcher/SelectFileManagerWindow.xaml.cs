using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;
using System;
using System.Collections.Generic;
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
        public List<CustomExplorerViewModel> CustomExplorers { get; set; }

        public CustomExplorerViewModel CustomExplorer => CustomExplorers[SelectedCustomExplorerIndex];
        public SelectFileManagerWindow(Settings settings)
        {
            Settings = settings;
            CustomExplorers = Settings.CustomExplorerList.Select(x => x.Copy()).ToList();
            SelectedCustomExplorerIndex = Settings.CustomExplorerIndex;
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnDone_Click(object sender, RoutedEventArgs e)
        {
            Settings.CustomExplorerIndex = SelectedCustomExplorerIndex;
            Settings.CustomExplorerList = CustomExplorers;
            Close();
        }
    }
}
