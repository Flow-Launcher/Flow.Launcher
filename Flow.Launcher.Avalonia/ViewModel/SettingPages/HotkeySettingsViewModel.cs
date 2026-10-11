using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using FluentAvalonia.UI.Controls;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Avalonia.Resource;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Avalonia.Views.SettingPages;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class HotkeySettingsViewModel : ObservableObject
{
    private readonly Settings _settings;
    private readonly Internationalization _i18n;
    private readonly MainViewModel _mainViewModel;

    public HotkeySettingsViewModel()
    {
        _settings = Ioc.Default.GetRequiredService<Settings>();
        _i18n = Ioc.Default.GetRequiredService<Internationalization>();
        _mainViewModel = Ioc.Default.GetRequiredService<MainViewModel>();
    }

    // Expose settings collections for custom hotkeys/shortcuts
    public ObservableCollection<CustomPluginHotkey> CustomPluginHotkeys => _settings.CustomPluginHotkeys;
    public ObservableCollection<CustomShortcutModel> CustomShortcuts => _settings.CustomShortcuts;
    public ObservableCollection<BaseBuiltinShortcutModel> BuiltinShortcuts => _settings.BuiltinShortcuts;

    // Open Result Modifiers
    public string[] OpenResultModifiersList => new[]
    {
        KeyConstant.Alt,
        KeyConstant.Ctrl,
        $"{KeyConstant.Ctrl}+{KeyConstant.Alt}"
    };

    public string OpenResultModifiers
    {
        get => _settings.OpenResultModifiers;
        set
        {
            if (_settings.OpenResultModifiers != value)
            {
                _settings.OpenResultModifiers = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowOpenResultHotkey
    {
        get => _settings.ShowOpenResultHotkey;
        set
        {
            if (_settings.ShowOpenResultHotkey != value)
            {
                _settings.ShowOpenResultHotkey = value;
                OnPropertyChanged();
            }
        }
    }

    // Main Toggle Hotkey
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

    // Preview Hotkey
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

    // Dialog Jump Hotkey
    public string DialogJumpHotkey
    {
        get => _settings.DialogJumpHotkey;
        set
        {
            if (_settings.DialogJumpHotkey != value)
            {
                _settings.DialogJumpHotkey = value;
                OnPropertyChanged();
            }
        }
    }

    // Auto-complete Hotkeys
    public string AutoCompleteHotkey
    {
        get => _settings.AutoCompleteHotkey;
        set
        {
            if (_settings.AutoCompleteHotkey != value)
            {
                _settings.AutoCompleteHotkey = value;
                OnPropertyChanged();
            }
        }
    }

    public string AutoCompleteHotkey2
    {
        get => _settings.AutoCompleteHotkey2;
        set
        {
            if (_settings.AutoCompleteHotkey2 != value)
            {
                _settings.AutoCompleteHotkey2 = value;
                OnPropertyChanged();
            }
        }
    }

    // Select Next Item Hotkeys
    public string SelectNextItemHotkey
    {
        get => _settings.SelectNextItemHotkey;
        set
        {
            if (_settings.SelectNextItemHotkey != value)
            {
                _settings.SelectNextItemHotkey = value;
                OnPropertyChanged();
            }
        }
    }

    public string SelectNextItemHotkey2
    {
        get => _settings.SelectNextItemHotkey2;
        set
        {
            if (_settings.SelectNextItemHotkey2 != value)
            {
                _settings.SelectNextItemHotkey2 = value;
                OnPropertyChanged();
            }
        }
    }

    // Select Prev Item Hotkeys
    public string SelectPrevItemHotkey
    {
        get => _settings.SelectPrevItemHotkey;
        set
        {
            if (_settings.SelectPrevItemHotkey != value)
            {
                _settings.SelectPrevItemHotkey = value;
                OnPropertyChanged();
            }
        }
    }

    public string SelectPrevItemHotkey2
    {
        get => _settings.SelectPrevItemHotkey2;
        set
        {
            if (_settings.SelectPrevItemHotkey2 != value)
            {
                _settings.SelectPrevItemHotkey2 = value;
                OnPropertyChanged();
            }
        }
    }

    // Select Page Hotkeys
    public string SelectNextPageHotkey
    {
        get => _settings.SelectNextPageHotkey;
        set
        {
            if (_settings.SelectNextPageHotkey != value)
            {
                _settings.SelectNextPageHotkey = value;
                OnPropertyChanged();
            }
        }
    }

    public string SelectPrevPageHotkey
    {
        get => _settings.SelectPrevPageHotkey;
        set
        {
            if (_settings.SelectPrevPageHotkey != value)
            {
                _settings.SelectPrevPageHotkey = value;
                OnPropertyChanged();
            }
        }
    }

    // Context Menu Hotkey
    public string OpenContextMenuHotkey
    {
        get => _settings.OpenContextMenuHotkey;
        set
        {
            if (_settings.OpenContextMenuHotkey != value)
            {
                _settings.OpenContextMenuHotkey = value;
                OnPropertyChanged();
            }
        }
    }

    // Setting Window Hotkey
    public string SettingWindowHotkey
    {
        get => _settings.SettingWindowHotkey;
        set
        {
            if (_settings.SettingWindowHotkey != value)
            {
                _settings.SettingWindowHotkey = value;
                OnPropertyChanged();
            }
        }
    }

    // History Hotkeys
    public string OpenHistoryHotkey
    {
        get => _settings.OpenHistoryHotkey;
        set
        {
            if (_settings.OpenHistoryHotkey != value)
            {
                _settings.OpenHistoryHotkey = value;
                OnPropertyChanged();
            }
        }
    }

    public string CycleHistoryUpHotkey
    {
        get => _settings.CycleHistoryUpHotkey;
        set
        {
            if (_settings.CycleHistoryUpHotkey != value)
            {
                _settings.CycleHistoryUpHotkey = value;
                OnPropertyChanged();
            }
        }
    }

    public string CycleHistoryDownHotkey
    {
        get => _settings.CycleHistoryDownHotkey;
        set
        {
            if (_settings.CycleHistoryDownHotkey != value)
            {
                _settings.CycleHistoryDownHotkey = value;
                OnPropertyChanged();
            }
        }
    }

    // Selected items for lists
    [ObservableProperty]
    private CustomPluginHotkey? _selectedCustomPluginHotkey;

    [ObservableProperty]
    private CustomShortcutModel? _selectedCustomShortcut;

    // Custom Plugin Hotkey Commands
    [RelayCommand]
    private async Task CustomHotkeyDelete()
    {
        if (SelectedCustomPluginHotkey is null)
        {
            await ShowMessageAsync("Custom Query Hotkey", "Please select a custom query hotkey first.");
            return;
        }

        var confirmed = await ShowConfirmationAsync(
            Translate("delete", "Delete"),
            $"Delete the custom query hotkey '{SelectedCustomPluginHotkey.Hotkey}'?");

        if (!confirmed)
        {
            return;
        }

        HotKeyMapper.RemoveHotkey(SelectedCustomPluginHotkey.Hotkey);
        CustomPluginHotkeys.Remove(SelectedCustomPluginHotkey);
    }

    [RelayCommand]
    private async Task CustomHotkeyEdit()
    {
        if (SelectedCustomPluginHotkey is null)
        {
            await ShowMessageAsync("Custom Query Hotkey", "Please select a custom query hotkey first.");
            return;
        }

        var settingItem = CustomPluginHotkeys.FirstOrDefault(o =>
            o.ActionKeyword == SelectedCustomPluginHotkey.ActionKeyword && o.Hotkey == SelectedCustomPluginHotkey.Hotkey);

        if (settingItem is null)
        {
            await ShowMessageAsync("Custom Query Hotkey", "The selected custom query hotkey is no longer valid.");
            return;
        }

        HotKeyMapper.RemoveHotkey(settingItem.Hotkey);

        var window = new CustomQueryHotkeyWindow(settingItem, DoesCustomHotkeyExist);
        var result = await ShowWindowDialogAsync(window);
        if (!result)
        {
            if (!string.IsNullOrWhiteSpace(settingItem.Hotkey))
            {
                _ = HotKeyMapper.SetCustomQueryHotkey(settingItem);
            }

            return;
        }

        var index = CustomPluginHotkeys.IndexOf(settingItem);
        if (index >= 0 && index < CustomPluginHotkeys.Count)
        {
            var updatedHotkey = new CustomPluginHotkey(window.Hotkey, window.ActionKeyword);
            if (HotKeyMapper.SetCustomQueryHotkey(updatedHotkey))
            {
                CustomPluginHotkeys[index] = updatedHotkey;
            }
            else
            {
                _ = HotKeyMapper.SetCustomQueryHotkey(settingItem);
                await ShowMessageAsync("Custom Query Hotkey", $"Failed to register hotkey '{updatedHotkey.Hotkey}'. It may already be in use by another application.");
            }
        }
    }

    [RelayCommand]
    private async Task CustomHotkeyAdd()
    {
        var window = new CustomQueryHotkeyWindow(DoesCustomHotkeyExist);
        var result = await ShowWindowDialogAsync(window);
        if (!result)
        {
            return;
        }

        var customHotkey = new CustomPluginHotkey(window.Hotkey, window.ActionKeyword);
        if (HotKeyMapper.SetCustomQueryHotkey(customHotkey))
        {
            CustomPluginHotkeys.Add(customHotkey);
        }
        else
        {
            await ShowMessageAsync("Custom Query Hotkey", $"Failed to register hotkey '{customHotkey.Hotkey}'. It may already be in use by another application.");
        }
    }

    // Custom Shortcut Commands
    [RelayCommand]
    private async Task CustomShortcutDelete()
    {
        if (SelectedCustomShortcut is null)
        {
            await ShowMessageAsync("Custom Shortcut", "Please select a custom shortcut first.");
            return;
        }

        var confirmed = await ShowConfirmationAsync(
            Translate("delete", "Delete"),
            $"Delete the custom shortcut '{SelectedCustomShortcut.Key}'?");

        if (!confirmed)
        {
            return;
        }

        CustomShortcuts.Remove(SelectedCustomShortcut);
    }

    [RelayCommand]
    private async Task CustomShortcutEdit()
    {
        if (SelectedCustomShortcut is null)
        {
            await ShowMessageAsync("Custom Shortcut", "Please select a custom shortcut first.");
            return;
        }

        var settingItem = CustomShortcuts.FirstOrDefault(o =>
            o.Key == SelectedCustomShortcut.Key && o.Value == SelectedCustomShortcut.Value);

        if (settingItem is null)
        {
            await ShowMessageAsync("Custom Shortcut", "The selected custom shortcut is no longer valid.");
            return;
        }

        var window = new CustomShortcutWindow(settingItem.Key, settingItem.Value, DoesShortcutExist);
        var result = await ShowWindowDialogAsync(window);
        if (!result)
        {
            return;
        }

        var index = CustomShortcuts.IndexOf(settingItem);
        if (index >= 0 && index < CustomShortcuts.Count)
        {
            CustomShortcuts[index] = new CustomShortcutModel(window.ShortcutKey, window.ShortcutValue);
        }
    }

    [RelayCommand]
    private async Task CustomShortcutAdd()
    {
        var window = new CustomShortcutWindow(DoesShortcutExist);
        var result = await ShowWindowDialogAsync(window);
        if (!result)
        {
            return;
        }

        CustomShortcuts.Add(new CustomShortcutModel(window.ShortcutKey, window.ShortcutValue));
    }

    internal bool DoesShortcutExist(string key)
    {
        return CustomShortcuts.Any(v => v.Key == key) ||
               BuiltinShortcuts.Any(v => v.Key == key);
    }

    internal bool DoesCustomHotkeyExist(string hotkey)
    {
        return CustomPluginHotkeys.Any(v => v.Hotkey == hotkey);
    }

    private async Task<bool> ShowWindowDialogAsync(Window window)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is not null)
        {
            return await window.ShowDialog<bool>(desktop.MainWindow);
        }

        window.Show();
        return false;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = Translate("commonOK", "OK")
        };

        await dialog.ShowAsync(desktop.MainWindow);
    }

    private async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null)
        {
            return false;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = Translate("delete", "Delete"),
            CloseButtonText = Translate("cancel", "Cancel")
        };

        var result = await dialog.ShowAsync(desktop.MainWindow);
        return result == ContentDialogResult.Primary;
    }

    private string Translate(string key, string fallback)
    {
        var value = _i18n.GetTranslation(key);
        return value.StartsWith('[') ? fallback : value;
    }
}
