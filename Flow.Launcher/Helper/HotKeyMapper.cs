using System;
using ChefKeys;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.DialogJump;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;
using NHotkey;
using NHotkey.Wpf;

namespace Flow.Launcher.Helper;

internal static class HotKeyMapper
{
    private static readonly string ClassName = nameof(HotKeyMapper);

    private static Settings _settings;
    private static MainViewModel _mainViewModel;

    internal static void Initialize()
    {
        _mainViewModel = Ioc.Default.GetRequiredService<MainViewModel>();
        _settings = Ioc.Default.GetService<Settings>();

        ChefKeysManager.RegisterHotkey(_settings.Hotkey, ToggleHotkey);
        ChefKeysManager.Start();

        // TODO: Resolve this
        if (_settings.EnableDialogJump)
        {
            SetHotkey(_settings.DialogJumpHotkey, DialogJump.OnToggleHotkey);
        }

        LoadCustomPluginHotkey();
    }

    internal static void ToggleHotkey()
    {
        if (!_mainViewModel.ShouldIgnoreHotkeys())
            _mainViewModel.ToggleFlowLauncher();
    }

    internal static void RegisterHotkey(string hotkey, string previousHotkey, Action action)
    {
        try
        {
            ChefKeysManager.RegisterHotkey(hotkey, previousHotkey, action);
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName,
                string.Format("|HotkeyMapper.SetHotkey|Error registering hotkey {2}: {0} \nStackTrace:{1}",
                              e.Message,
                              e.StackTrace,
                              hotkeyStr));
            string errorMsg = string.Format(App.API.GetTranslation("registerHotkeyFailed"), hotkeyStr);
            string errorMsgTitle = App.API.GetTranslation("MessageBoxTitle");
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    internal static void UnregisterHotkey(string hotkey)
    {
        try
        {
            if (!string.IsNullOrEmpty(hotkey))
                ChefKeysManager.UnregisterHotkey(hotkey);
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName,
                string.Format("|HotkeyMapper.RemoveHotkey|Error removing hotkey: {0} \nStackTrace:{1}",
                              e.Message,
                              e.StackTrace));
            string errorMsg = string.Format(App.API.GetTranslation("unregisterHotkeyFailed"), hotkeyStr);
            string errorMsgTitle = App.API.GetTranslation("MessageBoxTitle");
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    private static void RemoveWithChefKeys(string hotkeyStr)
    {
        ChefKeysManager.UnregisterHotkey(hotkeyStr);
        ChefKeysManager.Stop();
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

            App.API.ShowMainWindow();
            App.API.ChangeQuery(hotkey.ActionKeyword, true);
        });
    }

    internal static bool CanRegisterHotkey(string hotkey)
    {
        return ChefKeysManager.CanRegisterHotkey(hotkey);
    }

    internal static bool CheckHotkeyAvailability(string hotkey) => ChefKeysManager.IsAvailable(hotkey);
    
    internal static bool CheckHotkeyValid(string hotkey) => ChefKeysManager.IsValidHotkey(hotkey);

}
