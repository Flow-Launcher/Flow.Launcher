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
using Droplex;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;
using ModernWpf;

namespace Flow.Launcher.Pages
{
    /// <summary>
    /// Theme.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Theme : Page
    {
        public Theme()
        {
            InitializeComponent();
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            //   API.OpenUrl(e.Uri.AbsoluteUri);
            // e.Handled = true;
        }

        private void ColorSchemeSelectedIndexChanged(object sender, EventArgs e)
        {
            /*   => ThemeManager.Current.ApplicationTheme = settings.ColorScheme switch
               {
                   Constant.Light => ApplicationTheme.Light,
                   Constant.Dark => ApplicationTheme.Dark,
                   Constant.System => null,
                   _ => ThemeManager.Current.ApplicationTheme
               };*/
        }

        private void OpenThemeFolder(object sender, RoutedEventArgs e)
        {
            //PluginManager.API.OpenDirectory(Path.Combine(DataLocation.DataDirectory(), Constant.Themes));
        }
    }

}
