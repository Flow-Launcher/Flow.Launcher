namespace Flow.Launcher.Plugin.Url
{
    public class Settings
    {
        public string BrowserPath { get; set; }

        public bool OpenInNewBrowserWindow { get; set; } = true;
        public bool AlwaysOpenWithHttps { get; set; } = false;
    }
}
