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

        SetHotkey(_settings.Hotkey, OnToggleHotkey);
        if (_settings.EnableDialogJump)
        {
            SetHotkey(_settings.DialogJumpHotkey, DialogJump.OnToggleHotkey);
        }
        LoadCustomPluginHotkey();
    }

    internal static void OnToggleHotkey(object sender, HotkeyEventArgs args)
    {
        if (!_mainViewModel.ShouldIgnoreHotkeys())
            _mainViewModel.ToggleFlowLauncher();
    }

    internal static void OnToggleHotkeyWithChefKeys()
    {
        if (!_mainViewModel.ShouldIgnoreHotkeys())
            _mainViewModel.ToggleFlowLauncher();
    }

    private static void SetHotkey(string hotkeyStr, EventHandler<HotkeyEventArgs> action)
    {
        var hotkey = new HotkeyModel(hotkeyStr);
        SetHotkey(hotkey, action);
    }

    private static void SetWithChefKeys(string hotkeyStr)
    {
        try
        {
            ChefKeysManager.RegisterHotkey(hotkeyStr, hotkeyStr, OnToggleHotkeyWithChefKeys);
            ChefKeysManager.Start();
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName,
                string.Format("|HotkeyMapper.SetWithChefKeys|Error registering hotkey: {0} \nStackTrace:{1}",
                              e.Message,
                              e.StackTrace));
            string errorMsg = Localize.registerHotkeyFailed(hotkeyStr);
            string errorMsgTitle = Localize.MessageBoxTitle();
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    internal static void SetHotkey(HotkeyModel hotkey, EventHandler<HotkeyEventArgs> action)
    {
        string hotkeyStr = hotkey.ToString();
        try
        {
            if (hotkeyStr == "LWin" || hotkeyStr == "RWin")
            {
                SetWithChefKeys(hotkeyStr);
                return;
            }

            HotkeyManager.Current.AddOrReplace(hotkeyStr, hotkey.CharKey, hotkey.ModifierKeys, action);
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName,
                string.Format("|HotkeyMapper.SetHotkey|Error registering hotkey {2}: {0} \nStackTrace:{1}",
                              e.Message,
                              e.StackTrace,
                              hotkeyStr));
            string errorMsg = Localize.registerHotkeyFailed(hotkeyStr);
            string errorMsgTitle = Localize.MessageBoxTitle();
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    internal static void RemoveHotkey(string hotkeyStr)
    {
        try
        {
            if (hotkeyStr == "LWin" || hotkeyStr == "RWin")
            {
                RemoveWithChefKeys(hotkeyStr);
                return;
            }

            if (!string.IsNullOrEmpty(hotkeyStr))
                HotkeyManager.Current.Remove(hotkeyStr);
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName,
                string.Format("|HotkeyMapper.RemoveHotkey|Error removing hotkey: {0} \nStackTrace:{1}",
                              e.Message,
                              e.StackTrace));
            string errorMsg = Localize.unregisterHotkeyFailed(hotkeyStr);
            string errorMsgTitle = Localize.MessageBoxTitle();
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
        if (_settings.CustomPluginHotkeys == null)
            return;

        foreach (CustomPluginHotkey hotkey in _settings.CustomPluginHotkeys)
        {
            SetCustomQueryHotkey(hotkey);
        }
    }

    internal static void SetCustomQueryHotkey(CustomPluginHotkey hotkey)
    {
        SetHotkey(hotkey.Hotkey, (s, e) =>
        {
            if (_mainViewModel.ShouldIgnoreHotkeys())
                return;

            App.API.ShowMainWindow();
            App.API.ChangeQuery(hotkey.ActionKeyword, true);
        });
    }

    internal static bool CheckAvailability(HotkeyModel currentHotkey)
    {
        try
        {
            HotkeyManager.Current.AddOrReplace("HotkeyAvailabilityTest", currentHotkey.CharKey, currentHotkey.ModifierKeys, (sender, e) => { });

            return true;
        }
        catch
        {
        }
        finally
        {
            HotkeyManager.Current.Remove("HotkeyAvailabilityTest");
        }

        return false;
    }
}
