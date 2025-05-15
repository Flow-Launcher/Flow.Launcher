using System;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.SettingPages.Views;

namespace Flow.Launcher.ViewModel;

public partial class SettingWindowViewModel : BaseModel
{
    private readonly Settings _settings;

    public SettingWindowViewModel(Settings settings)
    {
        _settings = settings;
    }

    public void SetPageType(Type pageType)
    {
        _pageType = pageType;
    }

    private Type _pageType = typeof(SettingsPaneGeneral);
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
