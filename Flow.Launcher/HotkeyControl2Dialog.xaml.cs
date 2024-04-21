using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin;
using ModernWpf.Controls;

namespace Flow.Launcher;

public partial class HotkeyControl2Dialog : ContentDialog
{
    public string Hotkey { get; set; } = string.Empty;
    public string WindowTitle { get; }
    public HotkeyModel CurrentHotkey { get; private set; }
    public ObservableCollection<string> KeysToDisplay { get; } = new();

    public enum EResultType
    {
        Cancel,
        Save,
        Reset,
        Delete
    }

    public EResultType ResultType { get; private set; } = EResultType.Cancel;
    public string ResultValue { get; private set; } = string.Empty;
    public string EmptyHotkey => InternationalizationManager.Instance.GetTranslation("none");

    public HotkeyControl2Dialog(string hotkey, string windowTitle = "")
    {
        if (windowTitle == string.Empty)
        {
            WindowTitle = InternationalizationManager.Instance.GetTranslation("hotkeyRegTitle");
        }
        else 
        {
            WindowTitle = windowTitle;
        }        
        CurrentHotkey = new HotkeyModel(hotkey);
        SetKeysToDisplay(CurrentHotkey);

        InitializeComponent();
    }

    public void Reset(object sender, RoutedEventArgs routedEventArgs)
    {
        ResultType = EResultType.Reset;
        Hide();
    }

    public void Delete(object sender, RoutedEventArgs routedEventArgs)
    {
        ResultType = EResultType.Delete;
        Hide();
    }

    public void Cancel(object sender, RoutedEventArgs routedEventArgs)
    {
        ResultType = EResultType.Cancel;
        Hide();
    }

    public void Save(object sender, RoutedEventArgs routedEventArgs)
    {
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
        
        if (!CheckHotkeyAvailability(hotkey.Value, true))
        {
            tbMsg.Text = InternationalizationManager.Instance.GetTranslation("hotkeyUnavailable");
            Alert.Visibility = Visibility.Visible;
            SaveBtn.IsEnabled = false;
        }
        else
        {
            Alert.Visibility = Visibility.Collapsed;
            SaveBtn.IsEnabled = true;
        }
    }

    private static bool CheckHotkeyAvailability(HotkeyModel hotkey, bool validateKeyGesture) =>
        hotkey.Validate(validateKeyGesture) && HotKeyMapper.CheckAvailability(hotkey);
}
