using System.Windows;
using System.Windows.Navigation;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Path = System.IO.Path;

namespace Flow.Launcher.SettingPages.Views
{
    //public readonly IPublicAPI API;
    //private Settings settings;
    //private SettingWindowViewModel viewModel;
    public partial class About
    {
        public About()
        {
            InitializeComponent();
        }

        
        
        private void OpenSettingFolder(object sender, RoutedEventArgs e)
        {
            PluginManager.API.OpenDirectory(Path.Combine(DataLocation.DataDirectory(), Constant.Settings));
        }

        private void OpenWelcomeWindow(object sender, RoutedEventArgs e)
        {
            //var WelcomeWindow = new WelcomeWindow(settings);
            //WelcomeWindow.ShowDialog();
        }
        private void OpenLogFolder(object sender, RoutedEventArgs e)
        {
            PluginManager.API.OpenDirectory(Path.Combine(DataLocation.DataDirectory(), Constant.Logs, Constant.Version));
        }
        private void ClearLogFolder(object sender, RoutedEventArgs e)
        {
            /*
            var confirmResult = MessageBox.Show(
                InternationalizationManager.Instance.GetTranslation("clearlogfolderMessage"),
                InternationalizationManager.Instance.GetTranslation("clearlogfolder"), 
                MessageBoxButton.YesNo);

            if (confirmResult == MessageBoxResult.Yes)
            {
                viewModel.ClearLogFolder();

                ClearLogFolderBtn.Content = viewModel.CheckLogFolder;
            }
            */
        }

        private void OnCheckUpdates(object sender, RoutedEventArgs e)
        {
            //viewModel.UpdateApp(); // TODO: change to command
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            //API.OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

    }
}
