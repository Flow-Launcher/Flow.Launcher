using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Avalonia.Helper;
using System;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class HotkeySettingsViewModel : ObservableObject
{
    private readonly Settings _settings;

    public HotkeySettingsViewModel()
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();
    }

    public string ToggleHotkey
    {
        get => _settings.Hotkey;
        set
        {
            if (_settings.Hotkey != value)
            {
                _settings.Hotkey = value;
                HotKeyMapper.SetToggleHotkey(value);
                OnPropertyChanged();
            }
        }
    }

    public string PreviewHotkey
    {
        get => _settings.PreviewHotkey;
        set
        {
            if (_settings.PreviewHotkey != value)
            {
                _settings.PreviewHotkey = value;
                OnPropertyChanged();
            }
        }
    }
}
