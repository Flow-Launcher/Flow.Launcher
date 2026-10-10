using System.Windows.Controls;
using Flow.Launcher.Plugin.PluginsManager.Views;

namespace Flow.Launcher.Plugin.PluginsManager
{
    public partial class Main
    {
        public Control CreateSettingPanel()
        {
            return new PluginsManagerSettings(viewModel);
        }
    }
}
