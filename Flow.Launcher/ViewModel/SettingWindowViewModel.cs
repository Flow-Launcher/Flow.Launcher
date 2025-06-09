using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel;

public partial class SettingWindowViewModel : BaseModel
{
    private readonly Settings _settings;

    public SettingWindowViewModel(Settings settings)
    {
        _settings = settings;
    }

    public bool SetPageType(Type pageType)
    {
        if (_pageType == pageType) return false;

        _pageType = pageType;
        return true;
    }

    private Type _pageType = null;
    public Type PageType
    {
        get => _pageType;
        set
        {
            if (_pageType != value)
            {
                _pageType = value;
                OnPropertyChanged();
            }
        }
    }

    public double SettingWindowWidth
    {
        get => _settings.SettingWindowWidth;
        set => _settings.SettingWindowWidth = value;
    }

    public double SettingWindowHeight
    {
        get => _settings.SettingWindowHeight;
        set => _settings.SettingWindowHeight = value;
    }

    public double? SettingWindowTop
    {
        get => _settings.SettingWindowTop;
        set => _settings.SettingWindowTop = value;
    }

    public double? SettingWindowLeft
    {
        get => _settings.SettingWindowLeft;
        set => _settings.SettingWindowLeft = value;
    }
}
