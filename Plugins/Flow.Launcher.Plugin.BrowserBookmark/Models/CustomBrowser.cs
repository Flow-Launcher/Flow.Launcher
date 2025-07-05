using System.Collections.Generic;
using Flow.Launcher.Localization.Attributes;

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
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public string DataDirectoryPath
    {
        get => _dataDirectoryPath;
        set
        {
            if (_dataDirectoryPath != value)
            {
                _dataDirectoryPath = value;
                OnPropertyChanged();
            }
        }
    }

    public List<BrowserTypeLocalized> AllBrowserTypes { get; } = BrowserTypeLocalized.GetValues();

    public BrowserType BrowserType
    {
        get => _browserType;
        set
        {
            if (_browserType != value)
            {
                _browserType = value;
                OnPropertyChanged();
            }
        }
    }
}

[EnumLocalize]
public enum BrowserType
{
    [EnumLocalizeValue("Chromium")]
    Chromium,

    [EnumLocalizeValue("Firefox")]
    Firefox,
}
