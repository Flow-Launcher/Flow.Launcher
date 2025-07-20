﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher
{
    public partial class SelectFileManagerWindow : Window
    {
        private readonly SelectFileManagerViewModel _viewModel;

        public SelectFileManagerWindow()
        {
            _viewModel = Ioc.Default.GetRequiredService<SelectFileManagerViewModel>();
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

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            _viewModel.OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void btnBrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var selectedFilePath = _viewModel.SelectFile();

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
