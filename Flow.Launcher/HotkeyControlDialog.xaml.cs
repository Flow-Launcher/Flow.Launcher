using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ChefKeys;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using iNKORE.UI.WPF.Modern.Controls;

namespace Flow.Launcher;

#nullable enable

public partial class HotkeyControlDialog : ContentDialog
{
    private static readonly IHotkeySettings _hotkeySettings = Ioc.Default.GetRequiredService<Settings>();
    private Action? _overwriteOtherHotkey;
    private string DefaultHotkey { get; }
    public string WindowTitle { get; }
    public HotkeyModel CurrentHotkey { get; private set; }
    public ObservableCollection<string> KeysToDisplay { get; } = new();

    public enum EResultType
    {
        Cancel,
        Save,
        Delete
    }

    public EResultType ResultType { get; private set; } = EResultType.Cancel;
    public string ResultValue { get; private set; } = string.Empty;
    public static string EmptyHotkey => Localize.none();

    private bool isOpenFlowHotkey;
    private Func<int, int, SpecialKeyState, bool>? _winComboInterceptor;

    public HotkeyControlDialog(string hotkey, string defaultHotkey, string windowTitle = "")
    {
        WindowTitle = windowTitle switch
        {
            "" or null => Localize.hotkeyRegTitle(),
            _ => windowTitle
        };
        DefaultHotkey = defaultHotkey;
        CurrentHotkey = new HotkeyModel(hotkey);
        SetKeysToDisplay(CurrentHotkey);

        InitializeComponent();

        // TODO: This is a temporary way to enforce changing only the open flow hotkey to Win, and will be removed by PR #3157
        isOpenFlowHotkey = _hotkeySettings.RegisteredHotkeys
                             .Any(x => x.DescriptionResourceKey == "flowlauncherHotkey"
                                    && x.Hotkey.ToString() == hotkey);

        ChefKeysManager.StartMenuEnableBlocking = true;
        ChefKeysManager.Start();

        // Cancel/Save explicitly clean up ChefKeys before calling Hide(). The Closed handler
        // covers the X-button path where neither Cancel nor Save runs.
        this.Closed += (_, _) =>
        {
            ChefKeysManager.StartMenuEnableBlocking = false;
            ChefKeysManager.Stop();
        };

        if (isOpenFlowHotkey)
        {
            _winComboInterceptor = (keyEvent, vkCode, state) =>
            {
                const int VK_LWIN = 0x5B;
                const int VK_RWIN = 0x5C;
                if ((keyEvent == (int)KeyEvent.WM_KEYDOWN || keyEvent == (int)KeyEvent.WM_SYSKEYDOWN)
                    && state.WinPressed
                    && vkCode != VK_LWIN && vkCode != VK_RWIN)
                {
                    var key = KeyInterop.KeyFromVirtualKey(vkCode);
                    if (key is Key.None
                        or Key.LeftCtrl or Key.RightCtrl
                        or Key.LeftAlt or Key.RightAlt
                        or Key.LeftShift or Key.RightShift
                        or Key.LWin or Key.RWin)
                    {
                        // Pass unrecognised/modifier keys through — suppressing them would
                        // silently eat media keys and other unmapped VK codes system-wide.
                        return true;
                    }
                    _ = App.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!IsLoaded) return;
                        var hotkeyModel = new HotkeyModel(state.AltPressed, state.ShiftPressed, state.WinPressed, state.CtrlPressed, key);
                        CurrentHotkey = hotkeyModel;
                        SetKeysToDisplay(CurrentHotkey);
                    });
                    return false;
                }
                return true;
            };
            App.API.RegisterGlobalKeyboardCallback(_winComboInterceptor);
            this.Closed += (_, _) => UnregisterWinComboInterceptor();
        }
    }

    private void Reset(object sender, RoutedEventArgs routedEventArgs)
    {
        SetKeysToDisplay(new HotkeyModel(DefaultHotkey));
    }

    private void Delete(object sender, RoutedEventArgs routedEventArgs)
    {
        KeysToDisplay.Clear();
        KeysToDisplay.Add(EmptyHotkey);
    }

    private void UnregisterWinComboInterceptor()
    {
        if (_winComboInterceptor != null)
        {
            App.API.RemoveGlobalKeyboardCallback(_winComboInterceptor);
            _winComboInterceptor = null;
        }
    }

    private void Cancel(object sender, RoutedEventArgs routedEventArgs)
    {
        ChefKeysManager.StartMenuEnableBlocking = false;
        ChefKeysManager.Stop();
        UnregisterWinComboInterceptor();

        ResultType = EResultType.Cancel;
        Hide();
    }

    private void Save(object sender, RoutedEventArgs routedEventArgs)
    {
        ChefKeysManager.StartMenuEnableBlocking = false;
        ChefKeysManager.Stop();
        UnregisterWinComboInterceptor();

        if (KeysToDisplay.Count == 1 && KeysToDisplay[0] == EmptyHotkey)
        {
            ResultType = EResultType.Delete;
            Hide();
            return;
        }
        ResultType = EResultType.Save;
        ResultValue = string.Join("+", KeysToDisplay);
        Hide();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        //when alt is pressed, the real key should be e.SystemKey
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (ChefKeysManager.StartMenuBlocked && key.ToString() == ChefKeysManager.StartMenuSimulatedKey)
            return;

        SpecialKeyState specialKeyState = GlobalHotkey.CheckModifiers();

        var hotkeyModel = new HotkeyModel(
            specialKeyState.AltPressed,
            specialKeyState.ShiftPressed,
            specialKeyState.WinPressed,
            specialKeyState.CtrlPressed,
            key);

        CurrentHotkey = hotkeyModel;
        SetKeysToDisplay(CurrentHotkey);
    }

    private void SetKeysToDisplay(HotkeyModel? hotkey)
    {
        _overwriteOtherHotkey = null;
        KeysToDisplay.Clear();

        if (hotkey == null || hotkey == default(HotkeyModel))
        {
            KeysToDisplay.Add(EmptyHotkey);
            return;
        }

        foreach (var key in hotkey.Value.EnumerateDisplayKeys()!)
        {
            KeysToDisplay.Add(key);
        }

        if (tbMsg == null)
            return;

        if (_hotkeySettings.RegisteredHotkeys.FirstOrDefault(v => v.Hotkey == hotkey) is { } registeredHotkeyData)
        {
            var description = string.Format(
                App.API.GetTranslation(registeredHotkeyData.DescriptionResourceKey),
                registeredHotkeyData.DescriptionFormatVariables
            );
            Alert.Visibility = Visibility.Visible;
            if (registeredHotkeyData.RemoveHotkey is not null)
            {
                tbMsg.Text = Localize.hotkeyUnavailableEditable(description);
                SaveBtn.IsEnabled = false;
                SaveBtn.Visibility = Visibility.Collapsed;
                OverwriteBtn.IsEnabled = true;
                OverwriteBtn.Visibility = Visibility.Visible;
                _overwriteOtherHotkey = registeredHotkeyData.RemoveHotkey;
            }
            else
            {
                tbMsg.Text = Localize.hotkeyUnavailableUneditable(description);
                SaveBtn.IsEnabled = false;
                SaveBtn.Visibility = Visibility.Visible;
                OverwriteBtn.IsEnabled = false;
                OverwriteBtn.Visibility = Visibility.Collapsed;
            }
            return;
        }

        OverwriteBtn.IsEnabled = false;
        OverwriteBtn.Visibility = Visibility.Collapsed;

        if (!CheckHotkeyAvailability(hotkey.Value, true))
        {
            tbMsg.Text = Localize.hotkeyUnavailable();
            Alert.Visibility = Visibility.Visible;
            SaveBtn.IsEnabled = false;
            SaveBtn.Visibility = Visibility.Visible;
        }
        else
        {
            Alert.Visibility = Visibility.Collapsed;
            SaveBtn.IsEnabled = true;
            SaveBtn.Visibility = Visibility.Visible;
        }
    }

    private bool CheckHotkeyAvailability(HotkeyModel hotkey, bool validateKeyGesture)
    {
        if (isOpenFlowHotkey && (hotkey.ToString() == "LWin" || hotkey.ToString() == "RWin"
            || (hotkey.Win && hotkey.CharKey != Key.None)))
            return hotkey.Validate(validateKeyGesture);

        return hotkey.Validate(validateKeyGesture) && HotKeyMapper.CheckAvailability(hotkey);
    }

    private void Overwrite(object sender, RoutedEventArgs e)
    {
        _overwriteOtherHotkey?.Invoke();
        Save(sender, e);
    }
}
