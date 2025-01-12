using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel;

public class SettingWindowViewModel : BaseModel
{
    public Updater Updater { get; }

    public IPortable Portable { get; }

    public Settings Settings { get; }

    public SettingWindowViewModel()
    {
        Settings = Ioc.Default.GetRequiredService<Settings>();
        Updater = Ioc.Default.GetRequiredService<Updater>();
        Portable = Ioc.Default.GetRequiredService<Portable>();
    }

    public async void UpdateApp()
    {
        await Updater.UpdateAppAsync(App.API, false);
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
