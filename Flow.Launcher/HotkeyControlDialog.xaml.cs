using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin;
using ModernWpf.Controls;

namespace Flow.Launcher;

public partial class HotkeyControlDialog : ContentDialog
{
    private string DefaultHotkey { get; }
    public string WindowTitle { get; }
    public HotkeyModel CurrentHotkey { get; private set; }
    public ObservableCollection<string> KeysToDisplay { get; } = new();

    private readonly Dictionary<HotkeyModel, string> StaticHotkeys = new()
    {
        [new HotkeyModel("Escape")] = "", // TODO
        [new HotkeyModel("F5")] = "ReloadPluginHotkey",
        [new HotkeyModel("Alt+Home")] = "Select first result", // TODO
        [new HotkeyModel("Alt+End")] = "Select last result", // TODO
        [new HotkeyModel("Ctrl+R")] = "Requery", // TODO
        [new HotkeyModel("Ctrl+H")] = "ToggleHistoryHotkey",
        [new HotkeyModel("Ctrl+OemCloseBrackets")] = "QuickWidthHotkey",
        [new HotkeyModel("Ctrl+OemOpenBrackets")] = "QuickWidthHotkey",
        [new HotkeyModel("Ctrl+OemPlus")] = "QuickHeightHotkey",
        [new HotkeyModel("Ctrl+OemMinus")] = "QuickHeightHotkey",
        [new HotkeyModel("Ctrl+Shift+Enter")] = "HotkeyCtrlShiftEnterDesc",
        [new HotkeyModel("Shift+Enter")] = "OpenContextMenuHotkey",
        [new HotkeyModel("Enter")] = "HotkeyRunDesc",
        [new HotkeyModel("Ctrl+Enter")] = "Open result", // TODO
        [new HotkeyModel("Alt+Enter")] = "Open result", // TODO
        // TODO D0-D9 But not here since they're not completely static, they can be Ctrl+D0-D9, Alt+D0-D9, or Ctrl+Alt+D0-D9
        [new HotkeyModel("Ctrl+F12")] = "ToggleGameModeHotkey",
        [new HotkeyModel("Ctrl+Shift+C")] = "Copy alternative", // TODO
    };

    public enum EResultType
    {
        Cancel,
        Save,
        Delete
    }

    public EResultType ResultType { get; private set; } = EResultType.Cancel;
    public string ResultValue { get; private set; } = string.Empty;
    public static string EmptyHotkey => InternationalizationManager.Instance.GetTranslation("none");

    public HotkeyControlDialog(string hotkey, string defaultHotkey, string windowTitle = "")
    {
        WindowTitle = windowTitle switch
        {
            "" or null => InternationalizationManager.Instance.GetTranslation("hotkeyRegTitle"),
            _ => windowTitle
        };
        DefaultHotkey = defaultHotkey;
        CurrentHotkey = new HotkeyModel(hotkey);
        SetKeysToDisplay(CurrentHotkey);

        InitializeComponent();
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

    private void Cancel(object sender, RoutedEventArgs routedEventArgs)
    {
        ResultType = EResultType.Cancel;
        Hide();
    }

    private void Save(object sender, RoutedEventArgs routedEventArgs)
    {
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


        if (StaticHotkeys.TryGetValue((HotkeyModel)hotkey, out var staticHotkey))
        {
            ShowWarningAndDisableSaveButton(
                string.Format(
                    InternationalizationManager.Instance.GetTranslation("hotkeyUnavailableInUseStatic"),
                    InternationalizationManager.Instance.GetTranslation(staticHotkey)
                )
            );
            return;
        }
        if (!CheckHotkeyAvailability(hotkey.Value, true))
        {
            ShowWarningAndDisableSaveButton(InternationalizationManager.Instance.GetTranslation("hotkeyUnavailable"));
        }
        else
        {
            Alert.Visibility = Visibility.Collapsed;
            SaveBtn.IsEnabled = true;
        }
    }

    private void ShowWarningAndDisableSaveButton(string message)
    {
        tbMsg.Text = message;
        Alert.Visibility = Visibility.Visible;
        SaveBtn.IsEnabled = false;
    }

    private static bool CheckHotkeyAvailability(HotkeyModel hotkey, bool validateKeyGesture) =>
        hotkey.Validate(validateKeyGesture) && HotKeyMapper.CheckAvailability(hotkey);
}
