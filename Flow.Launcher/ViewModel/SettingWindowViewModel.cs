using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel;

public partial class SettingWindowViewModel : BaseModel
{
    public Settings Settings { get; set; }

    public SettingWindowViewModel(Settings settings)
    {
        Settings = settings;
    }

    /// <summary>
    /// Save Flow settings. Plugins settings are not included.
    /// </summary>
    public void Save()
    {
        Settings.Save();
    }

    public double SettingWindowWidth
    {
        get => Settings.SettingWindowWidth;
        set => Settings.SettingWindowWidth = value;
    }

    public double SettingWindowHeight
    {
        get => Settings.SettingWindowHeight;
        set => Settings.SettingWindowHeight = value;
    }

    public double? SettingWindowTop
    {
        get => Settings.SettingWindowTop;
        set => Settings.SettingWindowTop = value;
    }

    public double? SettingWindowLeft
    {
        get => Settings.SettingWindowLeft;
        set => Settings.SettingWindowLeft = value;
    }
}
