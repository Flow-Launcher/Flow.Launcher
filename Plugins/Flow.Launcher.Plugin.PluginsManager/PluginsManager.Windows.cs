using System.Windows;

namespace Flow.Launcher.Plugin.PluginsManager
{
    public partial class PluginsManager
    {
        partial void ShowMainWindow()
        {
            var mainWindow = Application.Current.MainWindow;
            mainWindow.Show();
            mainWindow.Focus();
        }
    }
}
