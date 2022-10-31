using System;
using System.Windows;
using System.Windows.Navigation;

namespace Flow.Launcher.SettingPages.Views
{
    /// <summary>
    /// Theme.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Theme
    {
        public Theme()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
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
