using System.Windows.Controls;
using Flow.Launcher.Plugin.SharedCommands;

namespace Flow.Launcher.Plugin.Url
{
#pragma warning disable FLAN0005 // The plugin context property is declared in Main.cs
    public partial class Main
#pragma warning restore FLAN0005
    {
        private partial void OpenInCustomBrowser(string url)
        {
            if (Settings.OpenInNewBrowserWindow)
            {
                SearchWeb.OpenInBrowserWindow(url, Settings.BrowserPath, Settings.OpenInPrivateMode, Settings.PrivateModeArgument);
            }
            else
            {
                SearchWeb.OpenInBrowserTab(url, Settings.BrowserPath, Settings.OpenInPrivateMode, Settings.PrivateModeArgument);
            }
        }

        public Control CreateSettingPanel()
        {
            return new SettingsControl();
        }
    }
}
