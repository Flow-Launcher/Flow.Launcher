using Flow.Launcher.Localization.Attributes;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.BrowserBookmarks.Models;

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

    // WORKAROUND: Manually create the list for the ComboBox to resolve the CS0246 source generator error.
    // This provides the necessary data structure without relying on the auto-generated 'BrowserTypeLocalized' type.
    public List<BrowserTypeDisplay> AllBrowserTypes { get; } =
        System.Enum.GetValues<BrowserType>()
              .Select(e => new BrowserTypeDisplay(e.ToString(), e))
              .ToList();

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
    [EnumLocalizeValue("Chromium")]
    Chromium,

    [EnumLocalizeValue("Firefox")]
    Firefox,
}
