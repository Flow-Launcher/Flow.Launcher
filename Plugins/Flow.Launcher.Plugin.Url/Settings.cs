namespace Flow.Launcher.Plugin.Url
{
    public class Settings
    {
        public bool UseCustomBrowser { get; set; } = false;

        public string BrowserPath { get; set; } = string.Empty;

        public bool OpenInNewBrowserWindow { get; set; } = false;

        public bool OpenInPrivateMode { get; set; } = false;
    }
}
