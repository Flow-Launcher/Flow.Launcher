using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher
{
    public partial class SelectBrowserWindow : Window
    {
        private readonly SelectBrowserViewModel _viewModel;

        public SelectBrowserWindow()
        {
            _viewModel = Ioc.Default.GetRequiredService<SelectBrowserViewModel>();
            DataContext = _viewModel;
            InitializeComponent();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnDone_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SaveSettings())
            {
                Close();
            }
        }

        private void btnBrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var selectedFilePath = Win32Helper.SelectFile();

            if (!string.IsNullOrEmpty(selectedFilePath))
            {
                var path = (TextBox)(((FrameworkElement)sender).Parent as FrameworkElement).FindName("PathTextBox");
                path.Text = selectedFilePath;
                path.Focus();
                ((Button)sender).Focus();
            }
        }
    }
}
