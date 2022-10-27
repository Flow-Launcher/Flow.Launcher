using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using System.Windows.Forms;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.ViewModel;
using Flow.Launcher.Core.ExternalPlugins;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using System.IO;
using MessageBox = System.Windows.Forms.MessageBox;
using Path = System.IO.Path;
using Droplex;

namespace Flow.Launcher.Pages
{
    //public readonly IPublicAPI API;
    //private Settings settings;
    //private SettingWindowViewModel viewModel;
    public partial class About : Page
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
