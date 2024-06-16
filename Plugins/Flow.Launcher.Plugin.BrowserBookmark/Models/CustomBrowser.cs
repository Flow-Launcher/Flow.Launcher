namespace Flow.Launcher.Plugin.BrowserBookmark.Models;

public class CustomBrowser : BaseModel
{
    private string _name;
    private string _dataDirectoryPath;
    private BrowserType _browserType = BrowserType.Chromium;

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }

    public string DataDirectoryPath
    {
        get => _dataDirectoryPath;
        set
        {
            _dataDirectoryPath = value;
            OnPropertyChanged();
        }
    }

    public BrowserType BrowserType
    {
        get => _browserType;
        set
        {
            _browserType = value;
            OnPropertyChanged();
        }
    }
}

public enum BrowserType
{
    Chromium,
    Firefox,
}
