namespace Flow.Launcher.Plugin.Url
{
    public class Settings : BaseModel
    {
        private bool _useCustomBrowser = false;
        public bool UseCustomBrowser
        {
            get => _useCustomBrowser;
            set
            {
                if (_useCustomBrowser != value)
                {
                    _useCustomBrowser = value;
                    OnPropertyChanged();
                }
            }
        }

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

        private bool _openInNewBrowserWindow = true;
        public bool OpenInNewBrowserWindow
        {
            get => _openInNewBrowserWindow;
            set
            {
                if (_openInNewBrowserWindow != value)
                {
                    _openInNewBrowserWindow = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool OpenInPrivateMode { get; set; } = false;

        public string PrivateModeArgument { get; set; } = string.Empty;

        public bool AlwaysOpenWithHttps { get; set; } = false;
    }
}
