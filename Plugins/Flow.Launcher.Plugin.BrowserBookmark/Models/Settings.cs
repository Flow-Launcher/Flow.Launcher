using System.Collections.ObjectModel;

namespace Flow.Launcher.Plugin.BrowserBookmark.Models;

public class Settings : BaseModel
{
    private bool _loadChromeBookmark = true;
    private bool _loadFirefoxBookmark = true;
    private bool _loadEdgeBookmark = true;
    private bool _loadChromiumBookmark = true;
    private bool _enableFavicons = true;
    private bool _fetchMissingFavicons = false;

    public bool LoadChromeBookmark
    {
        get => _loadChromeBookmark;
        set
        {
            if (_loadChromeBookmark != value)
            {
                _loadChromeBookmark = value;
                OnPropertyChanged();
            }
        }
    }

    public bool LoadFirefoxBookmark
    {
        get => _loadFirefoxBookmark;
        set
        {
            if (_loadFirefoxBookmark != value)
            {
                _loadFirefoxBookmark = value;
                OnPropertyChanged();
            }
        }
    }

    public bool LoadEdgeBookmark
    {
        get => _loadEdgeBookmark;
        set
        {
            if (_loadEdgeBookmark != value)
            {
                _loadEdgeBookmark = value;
                OnPropertyChanged();
            }
        }
    }

    public bool LoadChromiumBookmark
    {
        get => _loadChromiumBookmark;
        set
        {
            if (_loadChromiumBookmark != value)
            {
                _loadChromiumBookmark = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EnableFavicons
    {
        get => _enableFavicons;
        set
        {
            if (_enableFavicons != value)
            {
                _enableFavicons = value;
                OnPropertyChanged();
            }
        }
    }

    public bool FetchMissingFavicons
    {
        get => _fetchMissingFavicons;
        set
        {
            if (_fetchMissingFavicons != value)
            {
                _fetchMissingFavicons = value;
                OnPropertyChanged();
            }
        }
    }

    private ObservableCollection<CustomBrowser> _customBrowsers = new();

    public ObservableCollection<CustomBrowser> CustomBrowsers
    {
        get => _customBrowsers;
        set
        {
            if (_customBrowsers != value)
            {
                _customBrowsers = value ?? new();
                OnPropertyChanged();
            }
        }
    }
}
