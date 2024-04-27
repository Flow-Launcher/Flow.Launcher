using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin;
using ModernWpf.Controls;

namespace Flow.Launcher;

#nullable enable

public partial class HotkeyControlDialog : ContentDialog
{
    private IHotkeySettings _hotkeySettings;
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
    public static string EmptyHotkey => InternationalizationManager.Instance.GetTranslation("none");

    public HotkeyControlDialog(string hotkey, string defaultHotkey, IHotkeySettings hotkeySettings, string windowTitle = "")
    {
        WindowTitle = windowTitle switch
        {
            "" or null => InternationalizationManager.Instance.GetTranslation("hotkeyRegTitle"),
            _ => windowTitle
        };
        DefaultHotkey = defaultHotkey;
        CurrentHotkey = new HotkeyModel(hotkey);
        _hotkeySettings = hotkeySettings;
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
                InternationalizationManager.Instance.GetTranslation(registeredHotkeyData.DescriptionResourceKey),
                registeredHotkeyData.DescriptionFormatVariables
            );
            Alert.Visibility = Visibility.Visible;
            if (registeredHotkeyData.RemoveHotkey is not null)
            {
                tbMsg.Text = string.Format(
                    InternationalizationManager.Instance.GetTranslation("hotkeyUnavailableEditable"),
                    description
                );
                SaveBtn.IsEnabled = false;
                SaveBtn.Visibility = Visibility.Collapsed;
                OverwriteBtn.IsEnabled = true;
                OverwriteBtn.Visibility = Visibility.Visible;
                _overwriteOtherHotkey = registeredHotkeyData.RemoveHotkey;
            }
            else
            {
                tbMsg.Text = string.Format(
                    InternationalizationManager.Instance.GetTranslation("hotkeyUnavailableUneditable"),
                    description
                );
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
            tbMsg.Text = InternationalizationManager.Instance.GetTranslation("hotkeyUnavailable");
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

    private static bool CheckHotkeyAvailability(HotkeyModel hotkey, bool validateKeyGesture) =>
        hotkey.Validate(validateKeyGesture) && HotKeyMapper.CheckAvailability(hotkey);

    private void Overwrite(object sender, RoutedEventArgs e)
    {
        _overwriteOtherHotkey?.Invoke();
        Save(sender, e);
    }
}
