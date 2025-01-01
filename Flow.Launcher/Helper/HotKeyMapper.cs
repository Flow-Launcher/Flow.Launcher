using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.ViewModel;
using Flow.Launcher.Core;
using ChefKeys;
using System.Windows.Input;

namespace Flow.Launcher.Helper;

internal static class HotKeyMapper
{
    private static Settings _settings;
    private static MainViewModel _mainViewModel;
    
    internal static void Initialize(MainViewModel mainVM)
    {
        _mainViewModel = mainVM;
        _settings = _mainViewModel.Settings;

        ChefKeysManager.RegisterHotkey(_settings.Hotkey, ToggleHotkey);
        ChefKeysManager.Start();
        
        LoadCustomPluginHotkey();
    }

    internal static void ToggleHotkey()
    {
        if (!_mainViewModel.ShouldIgnoreHotkeys())
            _mainViewModel.ToggleFlowLauncher();
    }

    internal static void RegisterHotkey(string hotkey, string previousHotkey, Action action)
    {
        ChefKeysManager.RegisterHotkey(hotkey, previousHotkey, action);
    }

    internal static void UnregisterHotkey(string hotkey)
    {
        if (!string.IsNullOrEmpty(hotkey))
            ChefKeysManager.UnregisterHotkey(hotkey);
    }

    internal static void LoadCustomPluginHotkey()
    {
        foreach (CustomPluginHotkey hotkey in _settings.CustomPluginHotkeys)
        {
            SetCustomQueryHotkey(hotkey);
        }
    }

    internal static void SetCustomQueryHotkey(CustomPluginHotkey hotkey)
    {
        ChefKeysManager.RegisterHotkey(hotkey.Hotkey, () =>
        {
            if (_mainViewModel.ShouldIgnoreHotkeys())
                return;

            _mainViewModel.Show();
            _mainViewModel.ChangeQueryText(hotkey.ActionKeyword, true);
        });
    }

    internal static bool CanRegisterHotkey(string hotkey)
    {
        return ChefKeysManager.CanRegisterHotkey(hotkey);
    }

    internal static bool CheckHotkeyAvailability(string hotkey) => ChefKeysManager.IsAvailable(hotkey);
    
    internal static bool CheckHotkeyValid(string hotkey) => ChefKeysManager.IsValidHotkey(hotkey);

}
