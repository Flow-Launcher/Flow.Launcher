using System.Windows.Navigation;

namespace Flow.Launcher.SettingPages.Views
{

    public partial class SettingsPaneAbout
    {
        public SettingsPaneAbout()
        {
            InitializeComponent();
        }
        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            App.API.OpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }
    }
}
