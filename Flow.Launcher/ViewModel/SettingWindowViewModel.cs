using System;
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

    private Type _pendingPageType;
    private string _pendingFilterText;

    /// <summary>
    /// Stores a one-shot navigation destination for the settings window, set before
    /// the window is opened (deep links). Consumed once by the window and panes so a
    /// later manual visit does not re-apply a stale destination or filter.
    /// </summary>
    public void SetPendingNavigation(Type pageType, string filterText = null)
    {
        _pendingPageType = pageType;
        _pendingFilterText = filterText;
    }

    public Type ConsumePendingPageType()
    {
        var pageType = _pendingPageType;
        _pendingPageType = null;
        return pageType;
    }

    public string ConsumePendingFilterText()
    {
        var filterText = _pendingFilterText;
        _pendingFilterText = null;
        return filterText;
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
