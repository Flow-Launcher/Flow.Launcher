using System.Collections.ObjectModel;

namespace Flow.Launcher.Plugin.BrowserBookmark.Models
{
    public class Settings : BaseModel
    {
        public bool OpenInNewBrowserWindow { get; set; } = true;

        public string BrowserPath { get; set; }

        public ObservableCollection<CustomBrowser> CustomChromiumBrowsers { get; set; } = new();
    }
}