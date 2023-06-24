using System.Windows.Controls;

namespace Flow.Launcher.Plugin.Url
{
    public partial class SettingsControl : UserControl
    {
        private Settings _settings;
        private IPublicAPI _flowlauncherAPI;

        public SettingsControl(IPublicAPI flowlauncherAPI,Settings settings)
        {
            InitializeComponent();
            _settings = settings;
            _flowlauncherAPI = flowlauncherAPI;

        }
    }
}
