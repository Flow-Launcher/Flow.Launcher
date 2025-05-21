using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;
using ModernWpf.Controls;

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

        public ObservableCollection<CustomExplorerViewModel> CustomExplorers { get; }

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
        
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            App.API.OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void btnDone_Click(object sender, RoutedEventArgs e)
        {
            // Check if the selected file manager path is valid
            if (!IsFileManagerValid(CustomExplorer.Path))
            {
                var result = App.API.ShowMsgBox(
                    string.Format(App.API.GetTranslation("fileManagerPathNotFound"), 
                        CustomExplorer.Name, CustomExplorer.Path),
                        App.API.GetTranslation("fileManagerPathError"),
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "<Pending>")]
        private async void btnTips_Click(object sender, RoutedEventArgs e)
        {
            var tipText = (string)Application.Current.Resources["fileManager_files_tips"];
            var url = "https://files.community/docs/contributing/updates";
    
            var textBlock = new TextBlock
            {
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 0)
            };
            
            textBlock.Inlines.Add(tipText);
            
            var hyperlink = new Hyperlink
            {
                NavigateUri = new Uri(url)
            };
            hyperlink.Inlines.Add(url);
            hyperlink.RequestNavigate += (s, args) =>
            {
                App.API.OpenUrl(args.Uri.AbsoluteUri);
                args.Handled = true;
            };
    
            textBlock.Inlines.Add(hyperlink);
    
            var tipsDialog = new ContentDialog()
            {
                Owner = Window.GetWindow((DependencyObject)sender),
                Title = (string)Application.Current.Resources["fileManager_files_btn"],
                Content = textBlock,
                PrimaryButtonText = (string)Application.Current.Resources["commonOK"],
                CornerRadius = new CornerRadius(8),
                Style = (Style)Application.Current.Resources["ContentDialog"]
            };

            await tipsDialog.ShowAsync();
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
