using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
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

    /// <summary>
    /// Whether this dialog is in dedicated double-tap capture mode (the separate DoubleTapHotkey control).
    /// In this mode, any key press immediately creates a "Key + Key" double-tap binding.
    /// </summary>
    private readonly bool _isDoubleTapMode;

    /// <summary>
    /// State machine for detecting lone modifier key presses vs combo vs double-tap.
    /// 
    /// Idle: Waiting for any key press.
    /// ModifierDown: A lone modifier key was pressed. Waiting to see if user presses another key (combo)
    ///               or releases the modifier (potential double-tap).
    /// WaitingSecondPress: The lone modifier was released. Waiting for a second press within the configured interval (double-tap).
    /// </summary>
    private enum ModifierDetectionState
    {
        Idle,
        ModifierDown,
        WaitingSecondPress
    }

    private ModifierDetectionState _detectionState = ModifierDetectionState.Idle;

    /// <summary>
    /// The modifier key that was pressed first in the detection state machine.
    /// </summary>
    private Key? _pendingModifierKey;

    /// <summary>
    /// Stores the previous valid hotkey before entering the detection state,
    /// so it can be restored if the double-tap detection times out.
    /// </summary>
    private HotkeyModel _previousValidHotkey;

    /// <summary>
    /// Timer used to detect double-tap: after a lone modifier key is released,
    /// we wait this interval for a second press. If no second press within the interval,
    /// we reset to Idle (the modifier was just pressed alone, not a double-tap).
    /// The interval is read from <see cref="_hotkeySettings"/>.DoubleTapHotkeyInterval
    /// so that capture and runtime detection agree.
    /// </summary>
    private readonly DispatcherTimer _doubleTapDetectionTimer;

    public enum EResultType
    {
        Cancel,
        Save,
        Delete
    }

    public EResultType ResultType { get; private set; } = EResultType.Cancel;
    public string ResultValue { get; private set; } = string.Empty;
    public static string EmptyHotkey => Localize.none();

    private static bool isOpenFlowHotkey;

    public HotkeyControlDialog(string hotkey, string defaultHotkey, string windowTitle = "", bool isDoubleTapMode = false)
    {
        WindowTitle = windowTitle switch
        {
            "" or null => Localize.hotkeyRegTitle(),
            _ => windowTitle
        };
        DefaultHotkey = defaultHotkey;
        _isDoubleTapMode = isDoubleTapMode;
        CurrentHotkey = new HotkeyModel(hotkey);
        SetKeysToDisplay(CurrentHotkey);

        InitializeComponent();

        _doubleTapDetectionTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(_hotkeySettings.DoubleTapHotkeyInterval)
        };
        _doubleTapDetectionTimer.Tick += OnDoubleTapDetectionTimeout;

        // TODO: This is a temporary way to enforce changing only the open flow hotkey to Win, and will be removed by PR #3157
        isOpenFlowHotkey = _hotkeySettings.RegisteredHotkeys
                             .Any(x => x.DescriptionResourceKey == "flowlauncherHotkey"
                                    && x.Hotkey.ToString() == hotkey);

        ChefKeysManager.StartMenuEnableBlocking = true;
        ChefKeysManager.Start();
    }

    /// <summary>
    /// Resets the hotkey display to the default hotkey value.
    /// </summary>
    private void Reset(object sender, RoutedEventArgs routedEventArgs)
    {
        ResetDetectionState();
        SetKeysToDisplay(new HotkeyModel(DefaultHotkey));
    }

    /// <summary>
    /// Clears the current hotkey binding, setting it to empty.
    /// </summary>
    private void Delete(object sender, RoutedEventArgs routedEventArgs)
    {
        ResetDetectionState();
        KeysToDisplay.Clear();
        KeysToDisplay.Add(EmptyHotkey);
    }

    /// <summary>
    /// Cancels the dialog without saving changes, stopping ChefKeys and resetting detection state.
    /// </summary>
    private void Cancel(object sender, RoutedEventArgs routedEventArgs)
    {
        ResetDetectionState();
        ChefKeysManager.StartMenuEnableBlocking = false;
        ChefKeysManager.Stop();

        ResultType = EResultType.Cancel;
        Hide();
    }

    /// <summary>
    /// Saves the current hotkey binding and closes the dialog.
    /// If the display shows the empty hotkey, deletes the binding instead.
    /// </summary>
    private void Save(object sender, RoutedEventArgs routedEventArgs)
    {
        ResetDetectionState();
        ChefKeysManager.StartMenuEnableBlocking = false;
        ChefKeysManager.Stop();

        if (KeysToDisplay.Count == 1 && KeysToDisplay[0] == EmptyHotkey)
        {
            ResultType = EResultType.Delete;
            Hide();
            return;
        }
        ResultType = EResultType.Save;
        ResultValue = CurrentHotkey.ToString();
        Hide();
    }

    /// <summary>
    /// Resets the modifier detection state machine back to Idle.
    /// </summary>
    private void ResetDetectionState()
    {
        _detectionState = ModifierDetectionState.Idle;
        _pendingModifierKey = null;
        _previousValidHotkey = default;
        _doubleTapDetectionTimer.Stop();
    }

    /// <summary>
    /// Handles the Unloaded event by resetting the detection state machine.
    /// </summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ResetDetectionState();
    }

    /// <summary>
    /// Called when the double-tap detection timer expires.
    /// This means the user released a lone modifier key but didn't press it again
    /// within the configured interval, so it was just a single modifier press — not a double-tap.
    /// We reset to Idle and clear the pending display.
    /// </summary>
    private void OnDoubleTapDetectionTimeout(object sender, EventArgs e)
    {
        _doubleTapDetectionTimer.Stop();

        if (_detectionState == ModifierDetectionState.WaitingSecondPress)
        {
            // The modifier was pressed once and released, but no second press came.
            // This was just a lone modifier press — reset to Idle.
            _detectionState = ModifierDetectionState.Idle;
            _pendingModifierKey = null;

            // Restore the previous valid hotkey instead of setting an invalid one
            CurrentHotkey = _previousValidHotkey;
            SetKeysToDisplay(CurrentHotkey);
        }
    }

    /// <summary>
    /// Handles key-down events for hotkey capture. In double-tap mode, immediately creates
    /// a double-tap binding. In normal mode, uses the modifier detection state machine to
    /// distinguish between combo hotkeys and double-tap hotkeys.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        // When alt is pressed, the real key should be e.SystemKey
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (ChefKeysManager.StartMenuBlocked && key.ToString() == ChefKeysManager.StartMenuSimulatedKey)
            return;

        SpecialKeyState specialKeyState = GlobalHotkey.CheckModifiers();

        // In dedicated double-tap mode (the separate DoubleTapHotkey control),
        // any key press immediately creates a double-tap binding
        if (_isDoubleTapMode)
        {
            var hotkeyModel = CreateDoubleTapHotkey(key);
            CurrentHotkey = hotkeyModel;
            SetKeysToDisplay(CurrentHotkey);
            return;
        }

        // Normal mode: auto-detect combo vs double-tap vs lone modifier
        bool isModifierKey = IsModifierKey(key);
        bool isLoneModifier = isModifierKey && IsLoneModifier(key, specialKeyState);

        switch (_detectionState)
        {
            case ModifierDetectionState.Idle:
                if (isLoneModifier)
                {
                    // A lone modifier key was pressed. Enter ModifierDown state.
                    // Show the modifier name as "pending" (e.g., just "Ctrl" displayed).
                    // Don't create a hotkey yet — we need to see if the user will:
                    //   1. Press another key while holding this modifier (combo)
                    //   2. Release and press again quickly (double-tap)
                    //   3. Release without pressing again (lone modifier — invalid)
                    _detectionState = ModifierDetectionState.ModifierDown;
                    _pendingModifierKey = key;

                    // Show the modifier as a pending/intermediate state
                    ShowPendingModifier(key);
                }
                else
                {
                    // Non-modifier key pressed, or modifier+other combo pressed.
                    // Create a normal hotkey model immediately.
                    var hotkeyModel = new HotkeyModel(
                        specialKeyState.AltPressed,
                        specialKeyState.ShiftPressed,
                        specialKeyState.WinPressed,
                        specialKeyState.CtrlPressed,
                        key);

                    CurrentHotkey = hotkeyModel;
                    SetKeysToDisplay(CurrentHotkey);
                }
                break;

            case ModifierDetectionState.ModifierDown:
                // We're in ModifierDown state — a lone modifier was pressed first.
                // Now the user pressed another key.
                if (isLoneModifier && key == _pendingModifierKey)
                {
                    // Same modifier pressed again while still held down — this is auto-repeat.
                    // Ignore it (the key is still physically down).
                    // Stay in ModifierDown state.
                    return;
                }
                else if (isModifierKey && key != _pendingModifierKey)
                {
                    // A different modifier was pressed while the first is held.
                    // This is still a modifier-only state — update pending modifier tracking.
                    // Actually, this means multiple modifiers are pressed, which is not "lone".
                    // Treat it as a combo: create a normal hotkey with multiple modifiers but no CharKey.
                    // But HotkeyModel requires a CharKey for combos. Let's just stay in ModifierDown
                    // and wait for a non-modifier key.
                    _detectionState = ModifierDetectionState.Idle;
                    _pendingModifierKey = null;
                    _doubleTapDetectionTimer.Stop();

                    var hotkeyModel = new HotkeyModel(
                        specialKeyState.AltPressed,
                        specialKeyState.ShiftPressed,
                        specialKeyState.WinPressed,
                        specialKeyState.CtrlPressed,
                        key);

                    CurrentHotkey = hotkeyModel;
                    SetKeysToDisplay(CurrentHotkey);
                }
                else if (!isModifierKey)
                {
                    // A non-modifier key was pressed while the modifier is held.
                    // This is a combo hotkey (e.g., Ctrl+Space).
                    _detectionState = ModifierDetectionState.Idle;
                    _pendingModifierKey = null;
                    _doubleTapDetectionTimer.Stop();

                    var hotkeyModel = new HotkeyModel(
                        specialKeyState.AltPressed,
                        specialKeyState.ShiftPressed,
                        specialKeyState.WinPressed,
                        specialKeyState.CtrlPressed,
                        key);

                    CurrentHotkey = hotkeyModel;
                    SetKeysToDisplay(CurrentHotkey);
                }
                break;

            case ModifierDetectionState.WaitingSecondPress:
                // The modifier was released, and we're waiting for a second press within the configured interval.
                if (isLoneModifier && _pendingModifierKey.HasValue && IsSameModifierFamily(key, _pendingModifierKey.Value))
                {
                    // Second press of the same modifier within 300ms!
                    // This is a double-tap hotkey (e.g., Ctrl + Ctrl).
                    _detectionState = ModifierDetectionState.Idle;
                    _pendingModifierKey = null;
                    _doubleTapDetectionTimer.Stop();

                    var hotkeyModel = CreateDoubleTapHotkey(key);
                    CurrentHotkey = hotkeyModel;
                    SetKeysToDisplay(CurrentHotkey);
                }
                else
                {
                    // A different key was pressed — not a double-tap.
                    // Reset and handle this as a new key press.
                    _detectionState = ModifierDetectionState.Idle;
                    _pendingModifierKey = null;
                    _doubleTapDetectionTimer.Stop();

                    if (isLoneModifier)
                    {
                        // New lone modifier — start the detection cycle again
                        _detectionState = ModifierDetectionState.ModifierDown;
                        _pendingModifierKey = key;
                        ShowPendingModifier(key);
                    }
                    else
                    {
                        // Non-lone-modifier key — create a normal hotkey
                        var hotkeyModel = new HotkeyModel(
                            specialKeyState.AltPressed,
                            specialKeyState.ShiftPressed,
                            specialKeyState.WinPressed,
                            specialKeyState.CtrlPressed,
                            key);

                        CurrentHotkey = hotkeyModel;
                        SetKeysToDisplay(CurrentHotkey);
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Handles key-up events to detect when a lone modifier key is released,
    /// transitioning from ModifierDown to WaitingSecondPress state.
    /// </summary>
    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        // Don't handle key-up in double-tap mode
        if (_isDoubleTapMode)
            return;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Only handle key-up in the detection state machine
        if (_detectionState == ModifierDetectionState.ModifierDown && _pendingModifierKey.HasValue)
        {
            // Check if the released key is the same modifier family as the pending one
            if (IsSameModifierFamily(key, _pendingModifierKey.Value))
            {
                // Verify the modifier is actually no longer pressed by checking the physical state
                var currentState = GlobalHotkey.CheckModifiers();
                bool modifierStillDown = _pendingModifierKey.Value switch
                {
                    Key.LeftCtrl or Key.RightCtrl => currentState.CtrlPressed,
                    Key.LeftAlt or Key.RightAlt => currentState.AltPressed,
                    Key.LeftShift or Key.RightShift => currentState.ShiftPressed,
                    Key.LWin or Key.RWin => currentState.WinPressed,
                    _ => false
                };

                if (!modifierStillDown)
                {
                    // The modifier key was truly released (no left+right variant still held)
                    _detectionState = ModifierDetectionState.WaitingSecondPress;
                    _doubleTapDetectionTimer.Stop(); // Defensive: stop before start
                    _doubleTapDetectionTimer.Start();
                }
            }
        }
    }

    /// <summary>
    /// Shows a pending modifier key in the display (intermediate state).
    /// Displays just the modifier name (e.g., "Ctrl") without creating a hotkey model yet.
    /// </summary>
    private void ShowPendingModifier(Key key)
    {
        _previousValidHotkey = CurrentHotkey;
        var displayName = GetModifierDisplayName(key);
        KeysToDisplay.Clear();
        KeysToDisplay.Add(displayName);

        // Disable Save button while in pending state — no valid hotkey yet
        if (tbMsg != null)
        {
            Alert.Visibility = Visibility.Collapsed;
            SaveBtn.IsEnabled = false;
            SaveBtn.Visibility = Visibility.Visible;
            OverwriteBtn.IsEnabled = false;
            OverwriteBtn.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Gets the display name for a modifier key (e.g., "Ctrl" for LeftCtrl/RightCtrl).
    /// </summary>
    private static string GetModifierDisplayName(Key key)
    {
        return key switch
        {
            Key.LeftCtrl or Key.RightCtrl => "Ctrl",
            Key.LeftAlt or Key.RightAlt => "Alt",
            Key.LeftShift or Key.RightShift => "Shift",
            Key.LWin or Key.RWin => "Win",
            _ => key.ToString()
        };
    }

    /// <summary>
    /// Checks if a key is a modifier key (Ctrl, Alt, Shift, Win).
    /// </summary>
    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
               or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
    }

    /// <summary>
    /// Checks if the user pressed a lone modifier key — a single modifier with no other keys.
    /// </summary>
    private static bool IsLoneModifier(Key key, SpecialKeyState specialKeyState)
    {
        if (!IsModifierKey(key))
            return false;

        // Count how many modifier keys are currently pressed
        int modifierCount = 0;
        if (specialKeyState.CtrlPressed) modifierCount++;
        if (specialKeyState.AltPressed) modifierCount++;
        if (specialKeyState.ShiftPressed) modifierCount++;
        if (specialKeyState.WinPressed) modifierCount++;

        // If only one modifier is pressed (the key itself), it's a lone modifier
        return modifierCount == 1;
    }

    /// <summary>
    /// Checks if two keys belong to the same modifier family.
    /// For example, LeftCtrl and RightCtrl are the same family (both are "Ctrl").
    /// </summary>
    private static bool IsSameModifierFamily(Key key1, Key key2)
    {
        return (key1 is Key.LeftCtrl or Key.RightCtrl && key2 is Key.LeftCtrl or Key.RightCtrl) ||
               (key1 is Key.LeftAlt or Key.RightAlt && key2 is Key.LeftAlt or Key.RightAlt) ||
               (key1 is Key.LeftShift or Key.RightShift && key2 is Key.LeftShift or Key.RightShift) ||
               (key1 is Key.LWin or Key.RWin && key2 is Key.LWin or Key.RWin) ||
               key1 == key2;
    }

    /// <summary>
    /// Creates a double-tap HotkeyModel from a single key press.
    /// Only modifier keys (Ctrl, Alt, Shift, Win) are valid for double-tap bindings.
    /// Non-modifier keys return an invalid HotkeyModel that will fail validation.
    /// </summary>
    private static HotkeyModel CreateDoubleTapHotkey(Key key)
    {
        // Only modifier keys are valid for double-tap hotkeys.
        // For modifier keys, we use the left variant as the CharKey.
        // Non-modifier keys return a HotkeyModel with DoubleTap=true but CharKey=None,
        // which will fail HotkeyModel.Validate() and prevent registration.
        var doubleTapKey = key switch
        {
            Key.LeftCtrl or Key.RightCtrl => Key.LeftCtrl,
            Key.LeftAlt or Key.RightAlt => Key.LeftAlt,
            Key.LeftShift or Key.RightShift => Key.LeftShift,
            Key.LWin or Key.RWin => Key.LWin,
            _ => Key.None // Non-modifier keys are not valid for double-tap
        };

        return new HotkeyModel(false, false, false, false, doubleTapKey, doubleTapKey != Key.None);
    }

    /// <summary>
    /// Updates the display with the given hotkey model and checks availability.
    /// Shows conflict warnings if the hotkey is already registered elsewhere.
    /// </summary>
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

    /// <summary>
    /// Checks if a hotkey is available for registration. Double-tap hotkeys use
    /// a different validation path than combo hotkeys.
    /// </summary>
    private static bool CheckHotkeyAvailability(HotkeyModel hotkey, bool validateKeyGesture)
    {
        // Double-tap hotkeys use a different validation path
        if (hotkey.DoubleTap)
        {
            return hotkey.Validate(validateKeyGesture) &&
                HotKeyMapper.CheckDoubleTapAvailability(hotkey.ToString());
        }

        if (isOpenFlowHotkey && (hotkey.ToString() == "LWin" || hotkey.ToString() == "RWin"))
            return true;

        return hotkey.Validate(validateKeyGesture) && HotKeyMapper.CheckAvailability(hotkey);
    }

    /// <summary>
    /// Overwrites an existing hotkey conflict and saves the new binding.
    /// </summary>
    private void Overwrite(object sender, RoutedEventArgs e)
    {
        _overwriteOtherHotkey?.Invoke();
        Save(sender, e);
    }
}