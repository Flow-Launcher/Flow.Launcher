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

    private static readonly Dictionary<HotkeyModel, ICommand> _windowHotkeyEvents = new();

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
                    var hotkeyStr = metadata.PluginHotkeys.Find(h => h.Id == hotkey.Id)?.Hotkey ?? hotkey.DefaultHotkey;
                    SetGlobalPluginHotkey(globalHotkey, metadata, hotkeyStr);
                }
            }
        }
    }

    internal static void SetGlobalPluginHotkey(GlobalPluginHotkey globalHotkey, PluginMetadata metadata, string hotkeyStr)
    {
        var hotkey = new HotkeyModel(hotkeyStr);
        SetGlobalPluginHotkey(globalHotkey, metadata, hotkey);
    }

    internal static void SetGlobalPluginHotkey(GlobalPluginHotkey globalHotkey, PluginMetadata metadata, HotkeyModel hotkey)
    {
        var hotkeyStr = hotkey.ToString();
        SetHotkey(hotkeyStr, (s, e) =>
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
            SetWindowHotkey(hotkey.Key, hotkey.Value);
        }
    }

    internal static void SetWindowHotkey(HotkeyModel hotkey, List<(PluginMetadata Metadata, SearchWindowPluginHotkey PluginHotkey)> hotkeyModels)
    {
        try
        {
            if (hotkeyModels.Count == 0) return;
            if (Application.Current?.MainWindow is MainWindow window)
            {
                // Cache the command for the hotkey if it already exists
                var keyGesture = hotkey.ToKeyGesture();
                var existingBinding = window.InputBindings
                    .OfType<KeyBinding>()
                    .FirstOrDefault(kb => 
                        kb.Gesture is KeyGesture keyGesture1 &&
                        keyGesture.Key == keyGesture1.Key &&
                        keyGesture.Modifiers == keyGesture1.Modifiers);
                if (existingBinding != null)
                {
                    // If the hotkey exists, remove the old command
                    if (_windowHotkeyEvents.ContainsKey(hotkey))
                    {
                        window.InputBindings.Remove(existingBinding);
                    }
                    // If the hotkey does not exist, save the old command
                    else
                    {
                        _windowHotkeyEvents[hotkey] = existingBinding.Command;
                    }   
                }

                // Create and add the new key binding
                var command = BuildCommand(hotkey, hotkeyModels);
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
                              hotkey));
            string errorMsg = string.Format(App.API.GetTranslation("registerWindowHotkeyFailed"), hotkey);
            string errorMsgTitle = App.API.GetTranslation("MessageBoxTitle");
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    private static ICommand BuildCommand(HotkeyModel hotkey, List<(PluginMetadata Metadata, SearchWindowPluginHotkey PluginHotkey)> hotkeyModels)
    {
        return new RelayCommand(() =>
        {
            var selectedResult = _mainViewModel.GetSelectedResults().SelectedItem?.Result;
            // Check result nullability
            if (selectedResult != null)
            {
                var pluginId = selectedResult.PluginID;
                foreach (var hotkeyModel in hotkeyModels)
                {
                    var metadata = hotkeyModel.Metadata;
                    var pluginHotkey = hotkeyModel.PluginHotkey;

                    // Check plugin ID match
                    if (metadata.ID != pluginId)
                        continue;

                    // Check plugin enabled state
                    if (metadata.Disabled)
                        continue;

                    // Check hotkey supported state
                    if (!selectedResult.HotkeyIds.Contains(pluginHotkey.Id))
                        continue;

                    // Check action nullability
                    if (pluginHotkey.Action == null)
                        continue;

                    // TODO: Remove return to skip other commands & Organize main window hotkeys
                    // Invoke action & return to skip other commands
                    if (pluginHotkey.Action.Invoke(selectedResult))
                        App.API.HideMainWindow();

                    return;
                }
            }

            if (_windowHotkeyEvents.TryGetValue(hotkey, out var existingCommand))
            {
                existingCommand.Execute(null);
            }
        });
    }

    internal static void RemoveWindowHotkey(HotkeyModel hotkey)
    {
        try
        {
            if (Application.Current?.MainWindow is MainWindow window)
            {
                // Find and remove the key binding with the specified gesture
                var keyGesture = hotkey.ToKeyGesture();
                var existingBinding = window.InputBindings
                    .OfType<KeyBinding>()
                    .FirstOrDefault(kb =>
                        kb.Gesture is KeyGesture keyGesture1 &&
                        keyGesture.Key == keyGesture1.Key &&
                        keyGesture.Modifiers == keyGesture1.Modifiers);
                if (existingBinding != null)
                {
                    window.InputBindings.Remove(existingBinding);
                }

                // Restore the command if it exists
                if (_windowHotkeyEvents.TryGetValue(hotkey, out var command))
                {
                    var keyBinding = new KeyBinding(command, keyGesture);
                    window.InputBindings.Add(keyBinding);
                }
            }
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName,
                string.Format("Error removing window hotkey: {0} \nStackTrace:{1}",
                              e.Message,
                              e.StackTrace));
            string errorMsg = string.Format(App.API.GetTranslation("unregisterWindowHotkeyFailed"), hotkey);
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
