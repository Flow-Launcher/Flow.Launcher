using Flow.Launcher.Core;
using Flow.Launcher.Core.Configuration;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel;

public class SettingWindowViewModel : BaseModel
{
    private readonly FlowLauncherJsonStorage<Settings> _storage;

    public Updater Updater { get; }

    public IPortable Portable { get; }

    public Settings Settings { get; }

    public SettingWindowViewModel(Updater updater, IPortable portable)
    {
        _storage = new FlowLauncherJsonStorage<Settings>();

        Updater = updater;
        Portable = portable;
        Settings = _storage.Load();
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
        _storage.Save();
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
