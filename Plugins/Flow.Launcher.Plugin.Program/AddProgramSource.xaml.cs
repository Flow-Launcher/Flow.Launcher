using System.Windows;
using Flow.Launcher.Plugin.Program.ViewModels;

namespace Flow.Launcher.Plugin.Program
{
    /// <summary>
    /// Interaction logic for AddProgramSource.xaml
    /// </summary>
    public partial class AddProgramSource : Window
    {
        private readonly AddProgramSourceViewModel ViewModel;

        public AddProgramSource(AddProgramSourceViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Browse();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            var (modified, msg) = ViewModel.AddOrUpdate();
            if (modified == false && msg != null)
            {
                ViewModel.API.ShowMsgBox(msg);  // Invalid
                return;
            }
            DialogResult = modified;
            Close();
        }
    }
}
