using System;
using System.Collections.Generic;
using System.ComponentModel;
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

/// <summary>
/// Set Flow Launcher global hotkeys & window hotkeys
/// </summary>
internal static class HotKeyMapper
{
    private static readonly string ClassName = nameof(HotKeyMapper);

    private static Settings _settings;
    private static MainViewModel _mainViewModel;

    // Registered hotkeys for ActionContext
    private static List<RegisteredHotkeyData> _actionContextRegisteredHotkeys;

    #region Initialization

    internal static void Initialize()
    {
        _mainViewModel = Ioc.Default.GetRequiredService<MainViewModel>();
        _settings = Ioc.Default.GetService<Settings>();

        InitializeRegisteredHotkeys();

        _settings.PropertyChanged += Settings_PropertyChanged;
    }

    private static void InitializeRegisteredHotkeys()
    {
        // Fixed hotkeys for ActionContext
        _actionContextRegisteredHotkeys = new List<RegisteredHotkeyData>
        {
            new(RegisteredHotkeyType.CtrlShiftEnter, HotkeyType.SearchWindow, "Ctrl+Shift+Enter", "HotkeyCtrlShiftEnterDesc", _mainViewModel.OpenResultCommand),
            new(RegisteredHotkeyType.CtrlEnter, HotkeyType.SearchWindow, "Ctrl+Enter", "OpenContainFolderHotkey", _mainViewModel.OpenResultCommand),
            new(RegisteredHotkeyType.AltEnter, HotkeyType.SearchWindow, "Alt+Enter", "HotkeyOpenResult", _mainViewModel.OpenResultCommand),
        };

        // Fixed hotkeys & Editable hotkeys
        var list = new List<RegisteredHotkeyData>
        {
            // System default window hotkeys
            new(RegisteredHotkeyType.Up, HotkeyType.SearchWindow, "Up", "HotkeyLeftRightDesc", null),
            new(RegisteredHotkeyType.Down, HotkeyType.SearchWindow, "Down", "HotkeyLeftRightDesc", null),
            new(RegisteredHotkeyType.Left, HotkeyType.SearchWindow, "Left", "HotkeyUpDownDesc", null),
            new(RegisteredHotkeyType.Right, HotkeyType.SearchWindow, "Right", "HotkeyUpDownDesc", null),

            // Flow Launcher window hotkeys
            new(RegisteredHotkeyType.Esc, HotkeyType.SearchWindow, "Escape", "HotkeyESCDesc", _mainViewModel.EscCommand),
            new(RegisteredHotkeyType.Reload, HotkeyType.SearchWindow, "F5", "ReloadPluginHotkey", _mainViewModel.ReloadPluginDataCommand),
            new(RegisteredHotkeyType.SelectFirstResult, HotkeyType.SearchWindow, "Alt+Home", "HotkeySelectFirstResult", _mainViewModel.SelectFirstResultCommand),
            new(RegisteredHotkeyType.SelectLastResult, HotkeyType.SearchWindow, "Alt+End", "HotkeySelectLastResult", _mainViewModel.SelectLastResultCommand),
            new(RegisteredHotkeyType.ReQuery, HotkeyType.SearchWindow, "Ctrl+R", "HotkeyRequery", _mainViewModel.ReQueryCommand),
            new(RegisteredHotkeyType.IncreaseWidth, HotkeyType.SearchWindow, "Ctrl+OemCloseBrackets", "QuickWidthHotkey", _mainViewModel.IncreaseWidthCommand),
            new(RegisteredHotkeyType.DecreaseWidth, HotkeyType.SearchWindow, "Ctrl+OemOpenBrackets", "QuickWidthHotkey", _mainViewModel.DecreaseWidthCommand),
            new(RegisteredHotkeyType.IncreaseMaxResult, HotkeyType.SearchWindow, "Ctrl+OemPlus", "QuickHeightHotkey", _mainViewModel.IncreaseMaxResultCommand),
            new(RegisteredHotkeyType.DecreaseMaxResult, HotkeyType.SearchWindow, "Ctrl+OemMinus", "QuickHeightHotkey", _mainViewModel.DecreaseMaxResultCommand),
            new(RegisteredHotkeyType.ShiftEnter, HotkeyType.SearchWindow, "Shift+Enter", "OpenContextMenuHotkey", _mainViewModel.LoadContextMenuCommand),
            new(RegisteredHotkeyType.Enter, HotkeyType.SearchWindow, "Enter", "HotkeyRunDesc", _mainViewModel.OpenResultCommand),
            new(RegisteredHotkeyType.ToggleGameMode, HotkeyType.SearchWindow, "Ctrl+F12", "ToggleGameModeHotkey", _mainViewModel.ToggleGameModeCommand),
            new(RegisteredHotkeyType.CopyFilePath, HotkeyType.SearchWindow, "Ctrl+Shift+C", "CopyFilePathHotkey", _mainViewModel.CopyAlternativeCommand),
            new(RegisteredHotkeyType.OpenResultN1, HotkeyType.SearchWindow, $"{_settings.OpenResultModifiers}+D1", "HotkeyOpenResultN", 1, _mainViewModel.OpenResultCommand, 0),
            new(RegisteredHotkeyType.OpenResultN2, HotkeyType.SearchWindow, $"{_settings.OpenResultModifiers}+D2", "HotkeyOpenResultN", 2, _mainViewModel.OpenResultCommand, 1),
            new(RegisteredHotkeyType.OpenResultN3, HotkeyType.SearchWindow, $"{_settings.OpenResultModifiers}+D3", "HotkeyOpenResultN", 3, _mainViewModel.OpenResultCommand, 2),
            new(RegisteredHotkeyType.OpenResultN4, HotkeyType.SearchWindow, $"{_settings.OpenResultModifiers}+D4", "HotkeyOpenResultN", 4, _mainViewModel.OpenResultCommand, 3),
            new(RegisteredHotkeyType.OpenResultN5, HotkeyType.SearchWindow, $"{_settings.OpenResultModifiers}+D5", "HotkeyOpenResultN", 5, _mainViewModel.OpenResultCommand, 4),
            new(RegisteredHotkeyType.OpenResultN6, HotkeyType.SearchWindow, $"{_settings.OpenResultModifiers}+D6", "HotkeyOpenResultN", 6, _mainViewModel.OpenResultCommand, 5),
            new(RegisteredHotkeyType.OpenResultN7, HotkeyType.SearchWindow, $"{_settings.OpenResultModifiers}+D7", "HotkeyOpenResultN", 7, _mainViewModel.OpenResultCommand, 6),
            new(RegisteredHotkeyType.OpenResultN8, HotkeyType.SearchWindow, $"{_settings.OpenResultModifiers}+D8", "HotkeyOpenResultN", 8, _mainViewModel.OpenResultCommand, 7),
            new(RegisteredHotkeyType.OpenResultN9, HotkeyType.SearchWindow, $"{_settings.OpenResultModifiers}+D9", "HotkeyOpenResultN", 9, _mainViewModel.OpenResultCommand, 8),
            new(RegisteredHotkeyType.OpenResultN10, HotkeyType.SearchWindow, $"{_settings.OpenResultModifiers}+D0", "HotkeyOpenResultN", 10, _mainViewModel.OpenResultCommand, 9),
            
            // Flow Launcher global hotkeys
            new(RegisteredHotkeyType.Toggle, HotkeyType.Global, _settings.Hotkey, "flowlauncherHotkey", _mainViewModel.CheckAndToggleFlowLauncherCommand, null, () => _settings.Hotkey = ""),

            // Flow Launcher window hotkeys
            new(RegisteredHotkeyType.Preview, HotkeyType.SearchWindow, _settings.PreviewHotkey, "previewHotkey", _mainViewModel.TogglePreviewCommand, null, () => _settings.PreviewHotkey = ""),
            new(RegisteredHotkeyType.AutoComplete, HotkeyType.SearchWindow, _settings.AutoCompleteHotkey, "autoCompleteHotkey", _mainViewModel.AutocompleteQueryCommand, null, () => _settings.AutoCompleteHotkey = ""),
            new(RegisteredHotkeyType.AutoComplete2, HotkeyType.SearchWindow, _settings.AutoCompleteHotkey2, "autoCompleteHotkey", _mainViewModel.AutocompleteQueryCommand, null, () => _settings.AutoCompleteHotkey2 = ""),
            new(RegisteredHotkeyType.SelectNextItem, HotkeyType.SearchWindow, _settings.SelectNextItemHotkey, "SelectNextItemHotkey", _mainViewModel.SelectNextItemCommand, null, () => _settings.SelectNextItemHotkey = ""),
            new(RegisteredHotkeyType.SelectNextItem2, HotkeyType.SearchWindow, _settings.SelectNextItemHotkey2, "SelectNextItemHotkey", _mainViewModel.SelectNextItemCommand, null, () => _settings.SelectNextItemHotkey2 = ""),
            new(RegisteredHotkeyType.SelectPrevItem, HotkeyType.SearchWindow, _settings.SelectPrevItemHotkey, "SelectPrevItemHotkey", _mainViewModel.SelectPrevItemCommand, null, () => _settings.SelectPrevItemHotkey = ""),
            new(RegisteredHotkeyType.SelectPrevItem2, HotkeyType.SearchWindow, _settings.SelectPrevItemHotkey2, "SelectPrevItemHotkey", _mainViewModel.SelectPrevItemCommand, null, () => _settings.SelectPrevItemHotkey2 = ""),
            new(RegisteredHotkeyType.SettingWindow, HotkeyType.SearchWindow, _settings.SettingWindowHotkey, "SettingWindowHotkey", _mainViewModel.OpenSettingCommand, null, () => _settings.SettingWindowHotkey = ""),
            new(RegisteredHotkeyType.OpenHistory, HotkeyType.SearchWindow, _settings.OpenHistoryHotkey, "OpenHistoryHotkey", _mainViewModel.LoadHistoryCommand, null, () => _settings.OpenHistoryHotkey = ""),
            new(RegisteredHotkeyType.OpenContextMenu, HotkeyType.SearchWindow, _settings.OpenContextMenuHotkey, "OpenContextMenuHotkey", _mainViewModel.LoadContextMenuCommand, null, () => _settings.OpenContextMenuHotkey = ""),
            new(RegisteredHotkeyType.SelectNextPage, HotkeyType.SearchWindow, _settings.SelectNextPageHotkey, "SelectNextPageHotkey", _mainViewModel.SelectNextPageCommand, null, () => _settings.SelectNextPageHotkey = ""),
            new(RegisteredHotkeyType.SelectPrevPage, HotkeyType.SearchWindow, _settings.SelectPrevPageHotkey, "SelectPrevPageHotkey", _mainViewModel.SelectPrevPageCommand, null, () => _settings.SelectPrevPageHotkey = ""),
            new(RegisteredHotkeyType.CycleHistoryUp, HotkeyType.SearchWindow, _settings.CycleHistoryUpHotkey, "CycleHistoryUpHotkey", _mainViewModel.ReverseHistoryCommand, null, () => _settings.CycleHistoryUpHotkey = ""),
            new(RegisteredHotkeyType.CycleHistoryDown, HotkeyType.SearchWindow, _settings.CycleHistoryDownHotkey, "CycleHistoryDownHotkey", _mainViewModel.ForwardHistoryCommand, null, () => _settings.CycleHistoryDownHotkey = "")
        };
        
        // Custom query global hotkeys
        if (_settings.CustomPluginHotkeys != null)
        {
            foreach (var customPluginHotkey in _settings.CustomPluginHotkeys)
            {
                list.Add(new(RegisteredHotkeyType.CustomQuery, HotkeyType.Global, customPluginHotkey.Hotkey, "customQueryHotkey", CustomQueryHotkeyCommand, customPluginHotkey, () => customPluginHotkey.Hotkey = ""));
            }
        }

        // Plugin hotkeys
        // Global plugin hotkeys
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
                    // TODO: Support removeAction
                    Action removeHotkeyAction = hotkey.Editable ?
                        /*() => metadata.PluginHotkeys.RemoveAll(h => h.Id == hotkey.Id) :*/ null:
                        null;
                    // TODO: Handle pluginGlobalHotkey & get translation from PluginManager
                    list.Add(new(RegisteredHotkeyType.PluginGlobalHotkey, HotkeyType.Global, hotkeyStr, "pluginGlobalHotkey", GlobalPluginHotkeyCommand, new GlobalPluginHotkeyPair(metadata, globalHotkey), () => { }));
                }
            }
        }

        // Window plugin hotkeys
        var windowPluginHotkeys = PluginManager.GetWindowPluginHotkeys();
        foreach (var hotkey in windowPluginHotkeys)
        {
            var hotkeyModel = hotkey.Key;
            var windowHotkeys = hotkey.Value;
            // TODO: Support removeAction
            Action removeHotkeysAction = windowHotkeys.All(h => h.SearchWindowPluginHotkey.Editable) ?
                /*() => hotkeyModel.Metadata.PluginWindowHotkeys.RemoveAll(h => h.SearchWindowPluginHotkey.Editable) :*/ null :
                null;
            // TODO: Handle pluginWindowHotkey & get translation from PluginManager
            list.Add(new(RegisteredHotkeyType.PluginWindowHotkey, HotkeyType.SearchWindow, hotkeyModel, "pluginWindowHotkey", WindowPluginHotkeyCommand, new WindowPluginHotkeyPair(windowHotkeys)));
        }

        // Add registered hotkeys & Set them
        foreach (var hotkey in list)
        {
            _settings.RegisteredHotkeys.Add(hotkey);
            SetHotkey(hotkey);
        }

        App.API.LogDebug(ClassName, $"Initialize {_settings.RegisteredHotkeys.Count} hotkeys:\n[\n\t{string.Join(",\n\t", _settings.RegisteredHotkeys)}\n]");
    }

    private static void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // Flow Launcher global hotkeys
            case nameof(_settings.Hotkey):
                ChangeRegisteredHotkey(RegisteredHotkeyType.Toggle, _settings.Hotkey);
                break;

            // Flow Launcher window hotkeys
            case nameof(_settings.PreviewHotkey):
                ChangeRegisteredHotkey(RegisteredHotkeyType.Preview, _settings.PreviewHotkey);
                break;
            case nameof(_settings.AutoCompleteHotkey):
                ChangeRegisteredHotkey(RegisteredHotkeyType.AutoComplete, _settings.AutoCompleteHotkey);
                break;
            case nameof(_settings.AutoCompleteHotkey2):
                ChangeRegisteredHotkey(RegisteredHotkeyType.AutoComplete2, _settings.AutoCompleteHotkey2);
                break;
            case nameof(_settings.SelectNextItemHotkey):
                ChangeRegisteredHotkey(RegisteredHotkeyType.SelectNextItem, _settings.SelectNextItemHotkey);
                break;
            case nameof(_settings.SelectNextItemHotkey2):
                ChangeRegisteredHotkey(RegisteredHotkeyType.SelectNextItem2, _settings.SelectNextItemHotkey2);
                break;
            case nameof(_settings.SelectPrevItemHotkey):
                ChangeRegisteredHotkey(RegisteredHotkeyType.SelectPrevItem, _settings.SelectPrevItemHotkey);
                break;
            case nameof(_settings.SelectPrevItemHotkey2):
                ChangeRegisteredHotkey(RegisteredHotkeyType.SelectPrevItem2, _settings.SelectPrevItemHotkey2);
                break;
            case nameof(_settings.SettingWindowHotkey):
                ChangeRegisteredHotkey(RegisteredHotkeyType.SettingWindow, _settings.SettingWindowHotkey);
                break;
            case nameof(_settings.OpenHistoryHotkey):
                ChangeRegisteredHotkey(RegisteredHotkeyType.OpenHistory, _settings.OpenHistoryHotkey);
                break;
            case nameof(_settings.OpenContextMenuHotkey):
                ChangeRegisteredHotkey(RegisteredHotkeyType.OpenContextMenu, _settings.OpenContextMenuHotkey);
                break;
            case nameof(_settings.SelectNextPageHotkey):
                ChangeRegisteredHotkey(RegisteredHotkeyType.SelectNextPage, _settings.SelectNextPageHotkey);
                break;
            case nameof(_settings.SelectPrevPageHotkey):
                ChangeRegisteredHotkey(RegisteredHotkeyType.SelectPrevPage, _settings.SelectPrevPageHotkey);
                break;
            case nameof(_settings.CycleHistoryUpHotkey):
                ChangeRegisteredHotkey(RegisteredHotkeyType.CycleHistoryUp, _settings.CycleHistoryUpHotkey);
                break;
            case nameof(_settings.CycleHistoryDownHotkey):
                ChangeRegisteredHotkey(RegisteredHotkeyType.CycleHistoryDown, _settings.CycleHistoryDownHotkey);
                break;
        }
    }

    private static void ChangeRegisteredHotkey(RegisteredHotkeyType registeredType, string newHotkeyStr)
    {
        var newHotkey = new HotkeyModel(newHotkeyStr);
        ChangeRegisteredHotkey(registeredType, newHotkey);
    }

    private static void ChangeRegisteredHotkey(RegisteredHotkeyType registeredType, HotkeyModel newHotkey)
    {
        // Find the old registered hotkey data item
        var registeredHotkeyData = _settings.RegisteredHotkeys.FirstOrDefault(h => h.RegisteredType == registeredType);

        // If it is not found, return
        if (registeredHotkeyData == null)
        {
            return;
        }

        // Remove the old hotkey
        RemoveHotkey(registeredHotkeyData);

        // Update the hotkey string
        registeredHotkeyData.Hotkey = newHotkey;

        // Set the new hotkey
        SetHotkey(registeredHotkeyData);
    }

    #endregion

    // TODO: Deprecated
    internal static void OnToggleHotkey(object sender, HotkeyEventArgs args)
    {
        if (!_mainViewModel.ShouldIgnoreHotkeys())
            _mainViewModel.ToggleFlowLauncher();
    }

    // TODO: Deprecated
    private static void OnToggleHotkeyWithChefKeys()
    {
        if (!_mainViewModel.ShouldIgnoreHotkeys())
            _mainViewModel.ToggleFlowLauncher();
    }

    // TODO: Deprecated
    private static void SetHotkey(string hotkeyStr, EventHandler<HotkeyEventArgs> action)
    {
        var hotkey = new HotkeyModel(hotkeyStr);
        SetHotkey(hotkey, action);
    }

    // TODO: Deprecated
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

    // TODO: Deprecated
    internal static void SetHotkey(HotkeyModel hotkey, EventHandler<HotkeyEventArgs> action)
    {
        if (hotkey.IsEmpty)
        {
            return;
        }

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

    // TODO: Deprecated
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

    // TODO: Deprecated
    private static void RemoveWithChefKeys(string hotkeyStr)
    {
        ChefKeysManager.UnregisterHotkey(hotkeyStr);
        ChefKeysManager.Stop();
    }

    #region Hotkey Setting

    private static void SetHotkey(RegisteredHotkeyData hotkeyData)
    {
        if (hotkeyData is null || // Hotkey data is invalid
            hotkeyData.Hotkey.IsEmpty || // Hotkey is none
            hotkeyData.Command is null) // No need to set - it is a system command
        {
            return;
        }

        if (hotkeyData.Type == HotkeyType.Global)
        {
            SetGlobalHotkey(hotkeyData);
        }
        else if (hotkeyData.Type == HotkeyType.SearchWindow)
        {
            SetWindowHotkey(hotkeyData);
        }
    }

    private static void RemoveHotkey(RegisteredHotkeyData hotkeyData)
    {
        if (hotkeyData is null || // Hotkey data is invalid
            hotkeyData.Hotkey.IsEmpty || // Hotkey is none
            hotkeyData.Command is null) // No need to set - it is a system command
        {
            return;
        }

        if (hotkeyData.Type == HotkeyType.Global)
        {
            RemoveGlobalHotkey(hotkeyData);
        }
        else if (hotkeyData.Type == HotkeyType.SearchWindow)
        {
            RemoveWindowHotkey(hotkeyData);
        }
    }

    private static void SetGlobalHotkey(RegisteredHotkeyData hotkeyData)
    {
        var hotkey = hotkeyData.Hotkey;
        var hotkeyStr = hotkey.ToString();
        var hotkeyCommand = hotkeyData.Command;
        var hotkeyCommandParameter = hotkeyData.CommandParameter;
        try
        {
            if (hotkeyStr == "LWin" || hotkeyStr == "RWin")
            {
                SetGlobalHotkeyWithChefKeys(hotkeyData);
                return;
            }

            HotkeyManager.Current.AddOrReplace(
                hotkeyStr, hotkey.CharKey, hotkey.ModifierKeys,
                (s, e) => hotkeyCommand.Execute(hotkeyCommandParameter));
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName, $"Error registering hotkey {hotkeyStr}: {e.Message} \nStackTrace:{e.StackTrace}");
            var errorMsg = string.Format(App.API.GetTranslation("registerHotkeyFailed"), hotkeyStr);
            var errorMsgTitle = App.API.GetTranslation("MessageBoxTitle");
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    private static void SetGlobalHotkeyWithChefKeys(RegisteredHotkeyData hotkeyData)
    {
        var hotkey = hotkeyData.Hotkey;
        if (hotkey.IsEmpty)
        {
            return;
        }

        var hotkeyStr = hotkey.ToString();
        var hotkeyCommand = hotkeyData.Command;
        var hotkeyCommandParameter = hotkeyData.CommandParameter;
        try
        {
            ChefKeysManager.RegisterHotkey(hotkeyStr, hotkeyStr, () => hotkeyCommand.Execute(hotkeyCommandParameter));
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

    private static void SetWindowHotkey(RegisteredHotkeyData hotkeyData)
    {
        var hotkey = hotkeyData.Hotkey;
        var hotkeyCommand = hotkeyData.Command;
        var hotkeyCommandParameter = hotkeyData.CommandParameter;
        try
        {
            if (Application.Current?.MainWindow is MainWindow window)
            {
                // Check if the hotkey already exists
                var keyGesture = hotkey.ToKeyGesture();
                var existingBinding = window.InputBindings
                    .OfType<KeyBinding>()
                    .FirstOrDefault(kb =>
                        kb.Gesture is KeyGesture keyGesture1 &&
                        keyGesture.Key == keyGesture1.Key &&
                        keyGesture.Modifiers == keyGesture1.Modifiers);
                if (existingBinding != null)
                {
                    throw new InvalidOperationException($"Windows key {hotkey} already exists");
                }

                // Add the new hotkey binding
                var keyBinding = new KeyBinding()
                {
                    Gesture = keyGesture,
                    Command = hotkeyCommand,
                    CommandParameter = hotkeyCommandParameter
                };
                window.InputBindings.Add(keyBinding);
            }
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName, $"Error registering window hotkey {hotkey}: {e.Message} \nStackTrace:{e.StackTrace}");
            var errorMsg = string.Format(App.API.GetTranslation("registerWindowHotkeyFailed"), hotkey);
            var errorMsgTitle = App.API.GetTranslation("MessageBoxTitle");
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    private static void RemoveGlobalHotkey(RegisteredHotkeyData hotkeyData)
    {
        var hotkey = hotkeyData.Hotkey;
        var hotkeyStr = hotkey.ToString();
        try
        {
            if (hotkeyStr == "LWin" || hotkeyStr == "RWin")
            {
                RemoveGlobalHotkeyWithChefKeys(hotkeyData);
                return;
            }

            if (!string.IsNullOrEmpty(hotkeyStr))
                HotkeyManager.Current.Remove(hotkeyStr);
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName, $"Error removing hotkey: {e.Message} \nStackTrace:{e.StackTrace}");
            var errorMsg = string.Format(App.API.GetTranslation("unregisterHotkeyFailed"), hotkeyStr);
            var errorMsgTitle = App.API.GetTranslation("MessageBoxTitle");
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    private static void RemoveGlobalHotkeyWithChefKeys(RegisteredHotkeyData hotkeyData)
    {
        var hotkey = hotkeyData.Hotkey;
        var hotkeyStr = hotkey.ToString();
        try
        {
            ChefKeysManager.UnregisterHotkey(hotkeyStr);
            ChefKeysManager.Stop();
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName, $"Error removing hotkey: {e.Message} \nStackTrace:{e.StackTrace}");
            var errorMsg = string.Format(App.API.GetTranslation("unregisterHotkeyFailed"), hotkeyStr);
            var errorMsgTitle = App.API.GetTranslation("MessageBoxTitle");
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    private static void RemoveWindowHotkey(RegisteredHotkeyData hotkeyData)
    {
        var hotkey = hotkeyData.Hotkey;
        try
        {
            if (Application.Current?.MainWindow is MainWindow window)
            {
                // Remove the key binding
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
            }
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName, $"Error removing window hotkey: {e.Message} \nStackTrace:{e.StackTrace}");
            var errorMsg = string.Format(App.API.GetTranslation("unregisterWindowHotkeyFailed"), hotkey);
            var errorMsgTitle = App.API.GetTranslation("MessageBoxTitle");
            App.API.ShowMsgBox(errorMsg, errorMsgTitle);
        }
    }

    #endregion

    #region Commands

    private static RelayCommand<CustomPluginHotkey> _customQueryHotkeyCommand;
    private static IRelayCommand CustomQueryHotkeyCommand => _customQueryHotkeyCommand ??= new RelayCommand<CustomPluginHotkey>(CustomQueryHotkey);
    
    private static RelayCommand<GlobalPluginHotkeyPair> _globalPluginHotkeyCommand;
    private static IRelayCommand GlobalPluginHotkeyCommand => _globalPluginHotkeyCommand ??= new RelayCommand<GlobalPluginHotkeyPair>(GlobalPluginHotkey);
    
    private static RelayCommand<WindowPluginHotkeyPair> _windowPluginHotkeyCommand;
    private static IRelayCommand WindowPluginHotkeyCommand => _windowPluginHotkeyCommand ??= new RelayCommand<WindowPluginHotkeyPair>(WindowPluginHotkey);

    private static void CustomQueryHotkey(CustomPluginHotkey customPluginHotkey)
    {
        if (_mainViewModel.ShouldIgnoreHotkeys())
            return;

        App.API.ShowMainWindow();
        App.API.ChangeQuery(customPluginHotkey.ActionKeyword, true);
    }

    private static void GlobalPluginHotkey(GlobalPluginHotkeyPair pair)
    {
        if (_mainViewModel.ShouldIgnoreHotkeys() || pair.Metadata.Disabled)
            return;

        pair.GlobalPluginHotkey.Action?.Invoke();
    }

    private static void WindowPluginHotkey(WindowPluginHotkeyPair pair)
    {
        // Get selected result
        var selectedResult = _mainViewModel.GetSelectedResults().SelectedItem?.Result;

        // Check result nullability
        if (selectedResult != null)
        {
            var pluginId = selectedResult.PluginID;
            foreach (var hotkeyModel in pair.HotkeyModels)
            {
                var metadata = hotkeyModel.Metadata;
                var pluginHotkey = hotkeyModel.PluginHotkey;

                if (metadata.ID != pluginId || // Check plugin ID match
                    metadata.Disabled || // Check plugin enabled state
                    !selectedResult.HotkeyIds.Contains(pluginHotkey.Id) || // Check hotkey supported state
                    pluginHotkey.Action == null) // Check action nullability
                    continue;

                // TODO: Remove return to skip other commands & Organize main window hotkeys
                if (pluginHotkey.Action.Invoke(selectedResult))
                    App.API.HideMainWindow();
            }
        }
    }

    #endregion

    // TODO: Deprecated
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

    // TODO: Deprecated
    internal static void SetGlobalPluginHotkey(GlobalPluginHotkey globalHotkey, PluginMetadata metadata, string hotkeyStr)
    {
        var hotkey = new HotkeyModel(hotkeyStr);
        SetGlobalPluginHotkey(globalHotkey, metadata, hotkey);
    }

    // TODO: Deprecated
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

    // TODO: Deprecated
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
                    throw new InvalidOperationException($"Key binding {hotkey} already exists");  
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

    // TODO: Deprecated
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
        });
    }

    // TODO: Deprecated
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

    #region Check Hotkey

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

    #endregion

    #region Private Classes

    private class GlobalPluginHotkeyPair
    {
        public PluginMetadata Metadata { get; }

        public GlobalPluginHotkey GlobalPluginHotkey { get; }

        public GlobalPluginHotkeyPair(PluginMetadata metadata, GlobalPluginHotkey globalPluginHotkey)
        {
            Metadata = metadata;
            GlobalPluginHotkey = globalPluginHotkey;
        }
    }

    private class WindowPluginHotkeyPair
    {
        public List<(PluginMetadata Metadata, SearchWindowPluginHotkey PluginHotkey)> HotkeyModels { get; }

        public WindowPluginHotkeyPair(List<(PluginMetadata Metadata, SearchWindowPluginHotkey PluginHotkey)> hotkeys)
        {
            HotkeyModels = hotkeys;
        }
    }

    #endregion
}
