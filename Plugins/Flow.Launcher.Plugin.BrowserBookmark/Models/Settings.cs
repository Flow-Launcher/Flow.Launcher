using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.BrowserBookmark.Models
{
    public class Settings : BaseModel
    {
        public bool OpenInNewBrowserWindow { get; set; } = true;

        public string BrowserPath { get; set; }

        public bool LoadChromeBookmark { get; set; } = true;
        public bool LoadFirefoxBookmark { get; set; } = true;
        public bool LoadEdgeBookmark { get; set; } = true;

        public ObservableCollection<CustomBrowser> CustomChromiumBrowsers { get; set; } = new();
    }
}