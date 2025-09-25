using Flow.Launcher.Localization.Attributes;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.BrowserBookmark.Models;

public class CustomBrowser : BaseModel
{
    private string _name;
    private string _dataDirectoryPath;
    private BrowserType _browserType = BrowserType.Unknown;

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

// Helper record for displaying enum values in the settings ComboBox.
public record BrowserTypeDisplay(string Display, BrowserType Value);

[EnumLocalize]
public enum BrowserType
{
    [EnumLocalizeValue("Unknown")]
    Unknown,

    [EnumLocalizeValue("Chromium")]
    Chromium,

    [EnumLocalizeValue("Firefox")]
    Firefox,
}
