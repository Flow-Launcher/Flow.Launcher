using System.Windows.Controls;
using Flow.Launcher.Plugin.BrowserBookmark.Views;

namespace Flow.Launcher.Plugin.BrowserBookmark;

#pragma warning disable FLAN0005 // The plugin context property is declared in Main.cs
public partial class Main
#pragma warning restore FLAN0005
{
    public Control CreateSettingPanel()
    {
        return new SettingsControl(_settings);
    }
}
