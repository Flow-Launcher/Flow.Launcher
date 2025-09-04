namespace Flow.Launcher.Plugin.Url
{
    public class Settings : BaseModel
    {
        public bool UseCustomBrowser { get; set; } = false;

        private string _browserPath = string.Empty;
        public string BrowserPath
        {
            get => _browserPath;
            set
            {
                if (_browserPath != value)
                {
                    _browserPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool OpenInNewBrowserWindow { get; set; } = false;

        public bool OpenInPrivateMode { get; set; } = false;
    }
}
