using System.Collections.ObjectModel;

namespace Flow.Launcher.Plugin.BrowserBookmarks.Models;

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
        set { _loadChromeBookmark = value; OnPropertyChanged(); }
    }
    
    public bool LoadFirefoxBookmark
    {
        get => _loadFirefoxBookmark;
        set { _loadFirefoxBookmark = value; OnPropertyChanged(); }
    }
    
    public bool LoadEdgeBookmark
    {
        get => _loadEdgeBookmark;
        set { _loadEdgeBookmark = value; OnPropertyChanged(); }
    }

    public bool LoadChromiumBookmark
    {
        get => _loadChromiumBookmark;
        set { _loadChromiumBookmark = value; OnPropertyChanged(); }
    }

    public bool EnableFavicons
    {
        get => _enableFavicons;
        set { _enableFavicons = value; OnPropertyChanged(); }
    }
    
    public bool FetchMissingFavicons
    {
        get => _fetchMissingFavicons;
        set { _fetchMissingFavicons = value; OnPropertyChanged(); }
    }
    
    public ObservableCollection<CustomBrowser> CustomBrowsers { get; set; } = new();
}
