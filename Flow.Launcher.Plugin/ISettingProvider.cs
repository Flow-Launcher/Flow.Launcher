using System.Windows.Controls;

namespace Flow.Launcher.Plugin
{
    public interface ISettingProvider
    {
        Control CreateSettingPanel();
    }
}
