using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.Url;

public partial class SettingsControl : UserControl
{
    public Settings Settings => Main.Settings;

    public SettingsControl()
    {
        InitializeComponent();
    }

    private void SelectBrowserPath(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = Main.Context.API.GetTranslation("flowlauncher_plugin_url_plugin_filter")
        };

        if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.FileName))
        {
            Settings.BrowserPath = dlg.FileName;
        }
    }
}
