using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin;

namespace Flow.Launcher;

public partial class HotkeyControl2Dialog : Window
{
    public string Hotkey { get; set; } = string.Empty;
    public HotkeyModel CurrentHotkey { get; set; } = new(false, false, false, false, Key.None);
    public ObservableCollection<string> KeysToDisplay { get; set; } = new();

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
    public HotkeyControl2Dialog()
    {
        InitializeComponent();
    }

    public void Reset(object sender, RoutedEventArgs routedEventArgs)
    {
        ResultType = EResultType.Reset;
        Close();
    }

    public void Delete(object sender, RoutedEventArgs routedEventArgs)
    {
        ResultType = EResultType.Delete;
        Close();
    }

    public void Cancel(object sender, RoutedEventArgs routedEventArgs)
    {
        ResultType = EResultType.Cancel;
        Close();
    }

    public void Save(object sender, RoutedEventArgs routedEventArgs)
    {
        ResultType = EResultType.Save;
        ResultValue = string.Join("+", KeysToDisplay);
        Close();
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
    }

}
