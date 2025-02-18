using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.Core;

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
    private void CustomHotkeyDelete()
    {
        var item = SelectedCustomPluginHotkey;
        if (item is null)
        {
            MessageBoxEx.Show(InternationalizationManager.Instance.GetTranslation("pleaseSelectAnItem"));
            return;
        }

        var result = MessageBoxEx.Show(
            string.Format(
                InternationalizationManager.Instance.GetTranslation("deleteCustomHotkeyWarning"), item.Hotkey
            ),
            InternationalizationManager.Instance.GetTranslation("delete"),
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
            MessageBoxEx.Show(InternationalizationManager.Instance.GetTranslation("pleaseSelectAnItem"));
            return;
        }

        var window = new CustomQueryHotkeySetting(null, Settings);
        window.UpdateItem(item);
        window.ShowDialog();
    }

    [RelayCommand]
    private void CustomHotkeyAdd()
    {
        new CustomQueryHotkeySetting(null, Settings).ShowDialog();
    }

    [RelayCommand]
    private void CustomShortcutDelete()
    {
        var item = SelectedCustomShortcut;
        if (item is null)
        {
            MessageBoxEx.Show(InternationalizationManager.Instance.GetTranslation("pleaseSelectAnItem"));
            return;
        }

        var result = MessageBoxEx.Show(
            string.Format(
                InternationalizationManager.Instance.GetTranslation("deleteCustomShortcutWarning"), item.Key, item.Value
            ),
            InternationalizationManager.Instance.GetTranslation("delete"),
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
            MessageBoxEx.Show(InternationalizationManager.Instance.GetTranslation("pleaseSelectAnItem"));
            return;
        }

        var window = new CustomShortcutSetting(item.Key, item.Value, this);
        if (window.ShowDialog() is not true) return;

        var index = Settings.CustomShortcuts.IndexOf(item);
        Settings.CustomShortcuts[index] = new CustomShortcutModel(window.Key, window.Value);
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
