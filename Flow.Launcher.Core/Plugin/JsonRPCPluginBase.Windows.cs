using Control = System.Windows.Controls.Control;

namespace Flow.Launcher.Core.Plugin
{
    public abstract partial class JsonRPCPluginBase
    {
        public Control CreateSettingPanel()
        {
            return Settings.CreateSettingPanel();
        }
    }
}
