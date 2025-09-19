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
using ModernWpf.Controls;
using ChefKeys;
using System.Collections.Generic;

namespace Flow.Launcher;

#nullable enable

public partial class HotkeyControlDialog : ContentDialog
{
    private static readonly IHotkeySettings _hotkeySettings = Ioc.Default.GetRequiredService<Settings>();
    private Action? _overwriteOtherHotkey;
    private string DefaultHotkey { get; }
    public string WindowTitle { get; }

    public HotkeyModel CurrentHotkey;

    public HotkeyModel HotkeyToUpdate;

    private bool isWPFHotkeyControl = true;

    private bool clearKeysOnFirstType;

    public ObservableCollection<string> KeysToDisplay { get; } = new();

    public enum EResultType
    {
        Cancel,
        Save,
        Delete
    }

    public EResultType ResultType { get; private set; } = EResultType.Cancel;
    public string ResultValue { get; private set; } = string.Empty;
    public static string EmptyHotkey => App.API.GetTranslation("none");

    public HotkeyControlDialog(
        string hotkey,
        string defaultHotkey,
        bool isWPFHotkeyControl,
        string windowTitle = "")
    {
        this.isWPFHotkeyControl = isWPFHotkeyControl;

        WindowTitle = windowTitle switch
        {
            "" or null => App.API.GetTranslation("hotkeyRegTitle"),
            _ => windowTitle
        };
        DefaultHotkey = defaultHotkey;

        CurrentHotkey = new HotkeyModel(hotkey);
        // This is a requirement to be set with current hotkey for the WPF hotkey control when saving without any new changes
        HotkeyToUpdate = new HotkeyModel(hotkey);

        _hotkeySettings = hotkeySettings;

        SetKeysToDisplay(CurrentHotkey);
        clearKeysOnFirstType = true;

        InitializeComponent();

        ChefKeysManager.StartMenuEnableBlocking = true;
    }

    private void Reset(object sender, RoutedEventArgs routedEventArgs)
    {
        HotkeyToUpdate = new HotkeyModel(DefaultHotkey);
        SetKeysToDisplay(HotkeyToUpdate);
        clearKeysOnFirstType = true;
    }

    private void Delete(object sender, RoutedEventArgs routedEventArgs)
    {
        HotkeyToUpdate.Clear();
        KeysToDisplay.Clear();
        KeysToDisplay.Add(EmptyHotkey);
        tbMsg.Text = string.Empty;
        SaveBtn.IsEnabled = true;
        SaveBtn.Visibility = Visibility.Visible;
        OverwriteBtn.IsEnabled = false;
        OverwriteBtn.Visibility = Visibility.Collapsed;
        Alert.Visibility = Visibility.Collapsed;
    }

    private void Cancel(object sender, RoutedEventArgs routedEventArgs)
    {
        ChefKeysManager.StartMenuEnableBlocking = false;

        ResultType = EResultType.Cancel;
        Hide();
    }

    private void Save(object sender, RoutedEventArgs routedEventArgs)
    {
        ChefKeysManager.StartMenuEnableBlocking = false;

        if (KeysToDisplay.Count == 1 && KeysToDisplay[0] == EmptyHotkey)
        {
            ResultType = EResultType.Delete;
            Hide();
            return;
        }
        ResultType = EResultType.Save;
        var newHotkey = string.Join("+", KeysToDisplay);
        var oldHotkey = !string.IsNullOrEmpty(CurrentHotkey.HotkeyRaw) ? CurrentHotkey.HotkeyRaw : newHotkey;
        ResultValue = string.Format("{0}:{1}", newHotkey, oldHotkey);
        Hide();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        //when alt is pressed, the real key should be e.SystemKey
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (clearKeysOnFirstType)
        {
            KeysToDisplay.Clear();
            HotkeyToUpdate.Clear();
            clearKeysOnFirstType = false;
        }

        if (ChefKeysManager.StartMenuBlocked && key.ToString() == ChefKeysManager.StartMenuSimulatedKey)
            return;

        AddKey(key);
        
        SetKeysToDisplay(HotkeyToUpdate);
    }

    private void AddKey(Key key)
    {
        if (HotkeyToUpdate.GetLastKeySet() == key.ToString())
            return;

        if (MaxKeysLimitReached())
            return;

        HotkeyToUpdate.AddString(key.ToString());
    }

    private void SetKeysToDisplay(HotkeyModel? hotkey)
    {
        _overwriteOtherHotkey = null;
        KeysToDisplay.Clear();

        if (hotkey is null || string.IsNullOrEmpty(hotkey.Value.HotkeyRaw))
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

    if (_hotkeySettings.RegisteredHotkeys
                .FirstOrDefault(v => v.Hotkey == hotkey 
                                || v.Hotkey.HotkeyRaw == hotkey.Value.HotkeyRaw)
                is { } registeredHotkeyData)
        {
            var description = string.Format(
                App.API.GetTranslation(registeredHotkeyData.DescriptionResourceKey),
                registeredHotkeyData.DescriptionFormatVariables
            );
            Alert.Visibility = Visibility.Visible;
            if (registeredHotkeyData.RemoveHotkey is not null)
            {
                tbMsg.Text = string.Format(
                    App.API.GetTranslation("hotkeyUnavailableEditable"),
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
                    App.API.GetTranslation("hotkeyUnavailableUneditable"),
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

        var isHotkeyAvailable = !isWPFHotkeyControl 
            ? CheckHotkeyAvailability(hotkey.Value.HotkeyRaw)
            : CheckWPFHotkeyAvailability(hotkey.Value, true);

        if (!isHotkeyAvailable)
        {
            tbMsg.Text = App.API.GetTranslation("hotkeyUnavailable");
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

    private static bool CheckHotkeyAvailability(string hotkey)
        => HotKeyMapper.CanRegisterHotkey(hotkey);

    private static bool CheckWPFHotkeyAvailability(HotkeyModel hotkey, bool validateKeyGesture)
            => hotkey.ValidateForWpf(validateKeyGesture) && HotKeyMapper.CheckHotkeyAvailability(hotkey.HotkeyRaw);

    private bool MaxKeysLimitReached() => isWPFHotkeyControl ? KeysToDisplay.Count == 2 : KeysToDisplay.Count == 4;

    private void Overwrite(object sender, RoutedEventArgs e)
    {
        _overwriteOtherHotkey?.Invoke();
        Save(sender, e);
    }
}
