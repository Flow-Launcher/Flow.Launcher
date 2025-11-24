using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.DialogJump;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class SettingsPaneHotkeyViewModel : BaseModel
{
    public Settings Settings { get; }

    public CustomPluginHotkey SelectedCustomPluginHotkey { get; set; }
    public CustomShortcutModel SelectedCustomShortcut { get; set; }

    public string[] OpenResultModifiersList => new[]
    {
        KeyConstant.Alt,
        KeyConstant.Ctrl,
        $"{KeyConstant.Ctrl}+{KeyConstant.Alt}"
    };

    public SettingsPaneHotkeyViewModel(Settings settings)
    {
        Settings = settings;
    }

    [RelayCommand]
    private void SetTogglingHotkey(HotkeyModel hotkey)
    {
        HotKeyMapper.SetHotkey(hotkey, HotKeyMapper.OnToggleHotkey);
    }

    [RelayCommand]
    private void SetDialogJumpHotkey(HotkeyModel hotkey)
    {
        if (Settings.EnableDialogJump)
        {
            HotKeyMapper.SetHotkey(hotkey, DialogJump.OnToggleHotkey);
        }
    }

    [RelayCommand]
    private void CustomHotkeyDelete()
    {
        var item = SelectedCustomPluginHotkey;
        if (item is null)
        {
            App.API.ShowMsgBox(Localize.pleaseSelectAnItem());
            return;
        }

        var result = App.API.ShowMsgBox(
            Localize.deleteCustomHotkeyWarning(item.Hotkey),
            Localize.delete(),
            MessageBoxButton.YesNo
        );

        if (result is MessageBoxResult.Yes)
        {
            Settings.CustomPluginHotkeys.Remove(item);
            HotKeyMapper.RemoveHotkey(item.Hotkey);
        }
    }

    [RelayCommand]
    private void CustomHotkeyEdit()
    {
        var item = SelectedCustomPluginHotkey;
        if (item is null)
        {
            App.API.ShowMsgBox(Localize.pleaseSelectAnItem());
            return;
        }

        var settingItem = Settings.CustomPluginHotkeys.FirstOrDefault(o =>
            o.ActionKeyword == item.ActionKeyword && o.Hotkey == item.Hotkey);
        if (settingItem == null)
        {
            App.API.ShowMsgBox(Localize.invalidPluginHotkey());
            return;
        }

        var window = new CustomQueryHotkeySetting(settingItem);
        if (window.ShowDialog() is not true) return;

        var index = Settings.CustomPluginHotkeys.IndexOf(settingItem);
        if (index >= 0 && index < Settings.CustomPluginHotkeys.Count)
        {
            Settings.CustomPluginHotkeys[index] = new CustomPluginHotkey(window.Hotkey, window.ActionKeyword);
            HotKeyMapper.RemoveHotkey(settingItem.Hotkey); // remove origin hotkey
            HotKeyMapper.SetCustomQueryHotkey(Settings.CustomPluginHotkeys[index]); // set new hotkey
        }
    }

    [RelayCommand]
    private void CustomHotkeyAdd()
    {
        var window = new CustomQueryHotkeySetting();
        if (window.ShowDialog() is true)
        {
            var customHotkey = new CustomPluginHotkey(window.Hotkey, window.ActionKeyword);
            Settings.CustomPluginHotkeys.Add(customHotkey);
            HotKeyMapper.SetCustomQueryHotkey(customHotkey); // set new hotkey
        }
    }

    [RelayCommand]
    private void CustomShortcutDelete()
    {
        var item = SelectedCustomShortcut;
        if (item is null)
        {
            App.API.ShowMsgBox(Localize.pleaseSelectAnItem());
            return;
        }

        var result = App.API.ShowMsgBox(
            Localize.deleteCustomShortcutWarning(item.Key, item.Value),
            Localize.delete(),
            MessageBoxButton.YesNo
        );

        if (result is MessageBoxResult.Yes)
        {
            Settings.CustomShortcuts.Remove(item);
        }
    }

    [RelayCommand]
    private void CustomShortcutEdit()
    {
        var item = SelectedCustomShortcut;
        if (item is null)
        {
            App.API.ShowMsgBox(Localize.pleaseSelectAnItem());
            return;
        }

        var settingItem = Settings.CustomShortcuts.FirstOrDefault(o =>
            o.Key == item.Key && o.Value == item.Value);
        if (settingItem == null)
        {
            App.API.ShowMsgBox(Localize.invalidShortcut());
            return;
        }

        var window = new CustomShortcutSetting(settingItem.Key, settingItem.Value, this);
        if (window.ShowDialog() is not true) return;

        var index = Settings.CustomShortcuts.IndexOf(settingItem);
        if (index >= 0 && index < Settings.CustomShortcuts.Count)
        {
            Settings.CustomShortcuts[index] = new CustomShortcutModel(window.Key, window.Value);
        }
    }

    [RelayCommand]
    private void CustomShortcutAdd()
    {
        var window = new CustomShortcutSetting(this);
        if (window.ShowDialog() is true)
        {
            var shortcut = new CustomShortcutModel(window.Key, window.Value);
            Settings.CustomShortcuts.Add(shortcut);
        }
    }

    internal bool DoesShortcutExist(string key)
    {
        return Settings.CustomShortcuts.Any(v => v.Key == key) ||
               Settings.BuiltinShortcuts.Any(v => v.Key == key);
    }
}
