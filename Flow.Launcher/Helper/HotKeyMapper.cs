using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ChefKeys;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
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
        LoadCustomPluginHotkey();
        LoadGlobalPluginHotkey();
        LoadWindowPluginHotkey();
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
                string.Format("Error registering hotkey: {0} \nStackTrace:{1}",
                              e.Message,
                              e.StackTrace));
            string errorMsg = string.Format(App.API.GetTranslation("registerHotkeyFailed"), hotkeyStr);
            string errorMsgTitle = App.API.GetTranslation("MessageBoxTitle");
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
                string.Format("Error registering hotkey {2}: {0} \nStackTrace:{1}",
                              e.Message,
                              e.StackTrace,
                              hotkeyStr));
            string errorMsg = string.Format(App.API.GetTranslation("registerHotkeyFailed"), hotkeyStr);
            string errorMsgTitle = App.API.GetTranslation("MessageBoxTitle");
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
                string.Format("Error removing hotkey: {0} \nStackTrace:{1}",
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

    internal static void LoadGlobalPluginHotkey()
    {
        var pluginHotkeyInfos = PluginManager.GetPluginHotkeyInfo();
        foreach (var info in pluginHotkeyInfos)
        {
            var pluginPair = info.Key;
            var hotkeyInfo = info.Value;
            var metadata = pluginPair.Metadata;
            foreach (var hotkey in hotkeyInfo)
            {
                if (hotkey.HotkeyType == HotkeyType.Global && hotkey is GlobalPluginHotkey globalHotkey)
                {
                    var hotkeySetting = metadata.PluginHotkeys.Find(h => h.Id == hotkey.Id)?.Hotkey ?? hotkey.DefaultHotkey;
                    SetGlobalPluginHotkey(globalHotkey, metadata, hotkeySetting);
                }
            }
        }
    }

    internal static void SetGlobalPluginHotkey(GlobalPluginHotkey globalHotkey, PluginMetadata metadata, string hotkeySetting)
    {
        SetHotkey(hotkeySetting, (s, e) =>
        {
            if (_mainViewModel.ShouldIgnoreHotkeys() || metadata.Disabled)
                return;
            
            globalHotkey.Action?.Invoke();
        });
    }

    internal static void LoadWindowPluginHotkey()
    {
        var windowPluginHotkeys = PluginManager.GetWindowPluginHotkeys();
        foreach (var hotkey in windowPluginHotkeys)
        {
            var keyGesture = hotkey.Key;
            SetWindowHotkey(keyGesture, hotkey.Value);
        }
    }

    internal static void SetWindowHotkey(KeyGesture keyGesture, List<(PluginMetadata Metadata, SearchWindowPluginHotkey PluginHotkey)> hotkeyModels)
    {
        try
        {
            if (hotkeyModels.Count == 0) return;
            if (Application.Current?.MainWindow is MainWindow window)
            {
                var command = BuildCommand(hotkeyModels);

                // Remove any existing key binding with the same gesture to avoid duplication
                var existingBinding = window.InputBindings
                    .OfType<KeyBinding>()
                    .FirstOrDefault(kb => kb.Gesture == keyGesture);

                if (existingBinding != null)
                {
                    throw new InvalidOperationException($"Key binding with gesture {keyGesture} already exists");
                }

                // Create and add the new key binding
                var keyBinding = new KeyBinding(command, keyGesture);
                window.InputBindings.Add(keyBinding);
            }
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName,
                string.Format("Error registering window hotkey {2}: {0} \nStackTrace:{1}",
                              e.Message,
                              e.StackTrace,
                              keyGesture.DisplayString));
            string errorMsg = string.Format(App.API.GetTranslation("registerWindowHotkeyFailed"), keyGesture.DisplayString);
            string errorMsgTitle = App.API.GetTranslation("MessageBoxTitle");
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    private static ICommand BuildCommand(List<(PluginMetadata Metadata, SearchWindowPluginHotkey PluginHotkey)> hotkeyModels)
    {
        return new RelayCommand(() =>
        {
            foreach (var hotkeyModel in hotkeyModels)
            {
                var metadata = hotkeyModel.Metadata;
                if (metadata.Disabled)
                    continue;

                var pluginHotkey = hotkeyModel.PluginHotkey;
                if (pluginHotkey.Action?.Invoke(_mainViewModel.Results.SelectedItem?.Result) ?? false)
                    App.API.HideMainWindow();
            }
        });
    }

    internal static void RemoveWindowHotkey(KeyGesture keyGesture)
    {
        try
        {
            if (Application.Current?.MainWindow is MainWindow window)
            {
                // Find and remove the key binding with the specified gesture
                var existingBinding = window.InputBindings
                    .OfType<KeyBinding>()
                    .FirstOrDefault(kb => kb.Gesture == keyGesture);
                if (existingBinding != null)
                {
                    window.InputBindings.Remove(existingBinding);
                }
            }
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName,
                string.Format("Error removing window hotkey: {0} \nStackTrace:{1}",
                              e.Message,
                              e.StackTrace));
            string errorMsg = string.Format(App.API.GetTranslation("unregisterWindowHotkeyFailed"), keyGesture.DisplayString);
            string errorMsgTitle = App.API.GetTranslation("MessageBoxTitle");
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
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
