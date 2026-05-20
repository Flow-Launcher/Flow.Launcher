using System;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using Flow.Launcher.Plugin;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Flow.Launcher.Infrastructure.Hotkey
{
    /// <summary>
    /// Detects double-tap (double-press) of a single modifier key within a configurable time interval.
    /// For example, pressing Ctrl twice within 300ms triggers the double-tap action.
    /// Uses the GlobalHotkey WH_KEYBOARD_LL hook to track key-down and key-up events.
    /// 
    /// Important: For modifier keys, Windows generates auto-repeat WM_KEYDOWN events when
    /// the key is held down. To avoid false positives, we only count a second press if
    /// the key was released between the two presses (key-up detected between key-downs).
    /// 
    /// Only modifier keys (Ctrl, Alt, Shift, Win) are supported for double-tap bindings.
    /// </summary>
    public class DoubleTapDetector : IDisposable
    {
        private const string ClassName = nameof(DoubleTapDetector);

        private readonly DispatcherTimer _timeoutTimer;
        private readonly Action _doubleTapAction;
        private readonly Action _singleTapAction;
        private readonly int _intervalMs;

        /// <summary>
        /// The virtual key code to monitor for double-tap.
        /// </summary>
        private VIRTUAL_KEY _targetVkCode;

        /// <summary>
        /// Timestamp of the last key-down event for interval measurement.
        /// </summary>
        private long _lastKeyDownTimestamp = 0;

        /// <summary>
        /// Whether a first key press is pending (waiting for a second press within the interval).
        /// </summary>
        private bool _firstPressPending = false;

        /// <summary>
        /// Tracks whether the target key is physically held down, to distinguish
        /// auto-repeat from genuine double-tap.
        /// </summary>
        private bool _keyIsCurrentlyDown = false;

        /// <summary>
        /// TickCount64 of the last key-down event, used for desync recovery
        /// when a key-up event was missed.
        /// </summary>
        private long _lastKeyDownTickCount = 0;

        /// <summary>
        /// Whether this instance has been disposed.
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// Whether the double-tap detector is currently active and monitoring key events.
        /// </summary>
        public bool IsEnabled { get; private set; }

        /// <summary>
        /// The hotkey string representation (e.g., "Ctrl + Ctrl", "Alt + Alt").
        /// </summary>
        public string HotkeyString { get; private set; }

        /// <summary>
        /// Creates a DoubleTapDetector that monitors a specific modifier key for double-press within an interval.
        /// </summary>
        /// <param name="hotkeyString">The hotkey string (e.g., "Ctrl + Ctrl")</param>
        /// <param name="intervalMs">Maximum interval between two presses in milliseconds</param>
        /// <param name="doubleTapAction">Action to invoke when double-tap is detected</param>
        /// <param name="singleTapAction">Action to invoke when only a single press occurs (after timeout)</param>
        public DoubleTapDetector(string hotkeyString, int intervalMs, Action doubleTapAction, Action singleTapAction = null)
        {
            HotkeyString = hotkeyString;
            _intervalMs = intervalMs;
            _doubleTapAction = doubleTapAction;
            _singleTapAction = singleTapAction;

            ParseTargetKey(hotkeyString);

            Log($"Created: hotkey='{hotkeyString}', targetVk=0x{((int)_targetVkCode):X}, interval={intervalMs}ms");

            _timeoutTimer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(_intervalMs)
            };
            _timeoutTimer.Tick += OnTimeout;
        }

        /// <summary>
        /// Writes a debug message to the debug output.
        /// </summary>
        private static void Log(string message)
        {
            Debug.WriteLine($"[{ClassName}] {message}");
        }

        /// <summary>
        /// Parses the hotkey string to determine which modifier key to monitor.
        /// Double-tap hotkeys are represented as "Key + Key" (e.g., "Ctrl + Ctrl", "Alt + Alt").
        /// Only modifier keys (Ctrl, Alt, Shift, Win) are valid for double-tap bindings.
        /// </summary>
        private void ParseTargetKey(string hotkeyString)
        {
            if (string.IsNullOrEmpty(hotkeyString))
            {
                _targetVkCode = (VIRTUAL_KEY)0;
                return;
            }

            var parts = hotkeyString.Replace(" ", "").Split('+');

            // For double-tap, the format is "Key + Key" where both parts are the same
            if (parts.Length == 2 && parts[0] == parts[1])
            {
                var keyName = parts[0];

                switch (keyName)
                {
                    case "Ctrl":
                        _targetVkCode = VIRTUAL_KEY.VK_CONTROL;
                        break;
                    case "Alt":
                        _targetVkCode = VIRTUAL_KEY.VK_MENU;
                        break;
                    case "Shift":
                        _targetVkCode = VIRTUAL_KEY.VK_SHIFT;
                        break;
                    case "Win":
                        _targetVkCode = VIRTUAL_KEY.VK_LWIN;
                        break;
                    default:
                        // Non-modifier keys are not valid for double-tap hotkeys.
                        // This aligns with IsValidDoubleTapKey and HotkeyModel.Validate.
                        _targetVkCode = (VIRTUAL_KEY)0;
                        break;
                }
            }
            else if (parts.Length == 1)
            {
                // Single key format — only valid if it's a modifier key name
                var keyName = parts[0];
                _targetVkCode = keyName switch
                {
                    "Ctrl" => VIRTUAL_KEY.VK_CONTROL,
                    "Alt" => VIRTUAL_KEY.VK_MENU,
                    "Shift" => VIRTUAL_KEY.VK_SHIFT,
                    "Win" => VIRTUAL_KEY.VK_LWIN,
                    _ => (VIRTUAL_KEY)0
                };
            }
            else
            {
                _targetVkCode = (VIRTUAL_KEY)0;
            }
        }

        /// <summary>
        /// Maps a WPF Key to its virtual key code equivalent.
        /// </summary>
        private static VIRTUAL_KEY MapWpfKeyToVirtualKey(Key key)
        {
            return (VIRTUAL_KEY)KeyInterop.VirtualKeyFromKey(key);
        }

        /// <summary>
        /// Enables the double-tap detector. If the target key is invalid (0), the detector
        /// remains disabled.
        /// </summary>
        public void Enable()
        {
            if (_targetVkCode == (VIRTUAL_KEY)0)
            {
                Log($"Enable skipped: targetVkCode is 0 (no valid key)");
                return;
            }

            IsEnabled = true;
            _firstPressPending = false;
            _keyIsCurrentlyDown = false;
            _lastKeyDownTimestamp = 0;

            Log($"Enabled: monitoring for double-tap of VK=0x{((int)_targetVkCode):X}");
        }

        /// <summary>
        /// Disables the double-tap detector and resets all tracking state.
        /// </summary>
        public void Disable()
        {
            IsEnabled = false;
            _firstPressPending = false;
            _keyIsCurrentlyDown = false;
            _lastKeyDownTimestamp = 0;
            _timeoutTimer.Stop();
        }

        /// <summary>
        /// Processes a keyboard event from the GlobalHotkey hook.
        /// Returns true if the event should continue to other handlers,
        /// returns false if the event was consumed (double-tap detected).
        /// 
        /// Handles both key-down and key-up events. Key-up tracking is essential
        /// to distinguish between auto-repeat (key held down) and genuine double-tap
        /// (key pressed, released, pressed again).
        /// </summary>
        /// <param name="keyEvent">The keyboard event type (WM_KEYDOWN, WM_KEYUP, etc.)</param>
        /// <param name="vkCode">The virtual key code of the key event</param>
        /// <param name="state">The current state of modifier keys</param>
        public bool ProcessKeyEvent(KeyEvent keyEvent, int vkCode, SpecialKeyState state)
        {
            if (!IsEnabled || _targetVkCode == (VIRTUAL_KEY)0)
                return true;

            bool isTargetKey = IsTargetKey(vkCode);

            // Handle key-up events for the target key
            if ((keyEvent == KeyEvent.WM_KEYUP || keyEvent == KeyEvent.WM_SYSKEYUP) && isTargetKey)
            {
                _keyIsCurrentlyDown = false;
                Log($"Key UP: vkCode=0x{vkCode:X2}, firstPressPending={_firstPressPending}");
                return true;
            }

            // Only process key-down events from here on
            if (keyEvent != KeyEvent.WM_KEYDOWN && keyEvent != KeyEvent.WM_SYSKEYDOWN)
                return true;

            if (!isTargetKey)
            {
                // If a different key is pressed while waiting for timeout,
                // cancel the pending first press
                if (_firstPressPending)
                {
                    Log("Different key pressed while pending - cancelling first press");
                    _timeoutTimer.Stop();
                    _firstPressPending = false;
                    _singleTapAction?.Invoke();
                }
                return true;
            }

            // Target key pressed down
            // If the key is already down (auto-repeat), ignore this event
            // unless a long time has passed, which likely means a key-up was missed (desync recovery)
            if (_keyIsCurrentlyDown)
            {
                if (Environment.TickCount64 - _lastKeyDownTickCount > 2000)
                {
                    Log($"Key DOWN desync recovery: vkCode=0x{vkCode:X2}, no key-up seen for >2s, resetting state");
                    _keyIsCurrentlyDown = false;
                    // Fall through to treat as a new press
                }
                else
                {
                    Log($"Key DOWN (auto-repeat, ignoring): vkCode=0x{vkCode:X2}");
                    return true;
                }
            }

            _keyIsCurrentlyDown = true;
            _lastKeyDownTickCount = Environment.TickCount64;
            var now = Environment.TickCount64;

            Log($"Key DOWN: vkCode=0x{vkCode:X2}, firstPressPending={_firstPressPending}");

            if (_firstPressPending)
            {
                // Second press detected (key was released and pressed again) - check if within interval
                var elapsed = now - _lastKeyDownTimestamp;

                Log($"Second press: elapsed={elapsed}ms, interval={_intervalMs}ms, withinInterval={elapsed <= _intervalMs}");

                if (elapsed <= _intervalMs)
                {
                    // Double-tap detected!
                    _timeoutTimer.Stop();
                    _firstPressPending = false;
                    _lastKeyDownTimestamp = 0;

                    Log("*** DOUBLE-TAP DETECTED! Invoking action ***");
                    _doubleTapAction?.Invoke();
                    return true;
                }
                else
                {
                    // Too slow - this is a new first press
                    _timeoutTimer.Stop();
                    _firstPressPending = false;
                    _singleTapAction?.Invoke();

                    // Start tracking this as a new first press
                    _lastKeyDownTimestamp = now;
                    _firstPressPending = true;
                    _timeoutTimer.Start();

                    Log($"Too slow ({elapsed}ms), restarting first press timer");
                }
            }
            else
            {
                // First press detected - start timer
                _lastKeyDownTimestamp = now;
                _firstPressPending = true;
                _timeoutTimer.Start();

                Log($"First press detected at {now}, timer started ({_intervalMs}ms)");
            }

            return true;
        }

        /// <summary>
        /// Checks if the given virtual key code matches the target key for double-tap.
        /// For modifier keys, both left and right variants are considered matches.
        /// </summary>
        /// <param name="vkCode">The virtual key code to check.</param>
        private bool IsTargetKey(int vkCode)
        {
            // The WH_KEYBOARD_LL hook sends specific VK codes for left/right modifier keys:
            // VK_LCONTROL (0xA2), VK_RCONTROL (0xA3) for Ctrl
            // VK_LMENU (0xA4), VK_RMENU (0xA5) for Alt
            // VK_LSHIFT (0xA0), VK_RSHIFT (0xA1) for Shift
            // VK_LWIN (0x5B), VK_RWIN (0x5C) for Win
            //
            // Note: VK_CONTROL (0x11) is a generic code that is NOT sent by the hook.
            // The hook always sends the specific left/right codes.
            switch (_targetVkCode)
            {
                case VIRTUAL_KEY.VK_CONTROL:
                    return vkCode == 0xA2 || vkCode == 0xA3;
                case VIRTUAL_KEY.VK_MENU:
                    return vkCode == 0xA4 || vkCode == 0xA5;
                case VIRTUAL_KEY.VK_SHIFT:
                    return vkCode == 0xA0 || vkCode == 0xA1;
                case VIRTUAL_KEY.VK_LWIN:
                    return vkCode == 0x5B || vkCode == 0x5C;
                default:
                    return vkCode == (int)_targetVkCode;
            }
        }

        /// <summary>
        /// Called when the timeout timer expires — means only one press was detected within the interval.
        /// Invokes the single-tap action if configured.
        /// </summary>
        private void OnTimeout(object sender, EventArgs e)
        {
            _timeoutTimer.Stop();

            Log("Timeout expired - no second press within interval");

            if (_firstPressPending)
            {
                _firstPressPending = false;
                _lastKeyDownTimestamp = 0;
                _singleTapAction?.Invoke();
            }
        }

        /// <summary>
        /// Checks if the given hotkey string is a valid double-tap hotkey format.
        /// A valid format is "Key + Key" where Key is a modifier key (Ctrl, Alt, Shift, Win).
        /// </summary>
        /// <param name="hotkeyString">The hotkey string to validate.</param>
        /// <returns>True if the hotkey string is a valid double-tap format.</returns>
        public static bool IsValidDoubleTapHotkey(string hotkeyString)
        {
            if (string.IsNullOrEmpty(hotkeyString))
                return false;

            var parts = hotkeyString.Replace(" ", "").Split('+');

            if (parts.Length != 2 || parts[0] != parts[1])
                return false;

            var keyName = parts[0];
            return IsValidDoubleTapKey(keyName);
        }

        /// <summary>
        /// Checks if a key name is valid for double-tap binding.
        /// Only modifier keys (Ctrl, Alt, Shift, Win) are valid for double-tap hotkeys,
        /// consistent with <see cref="HotkeyModel.Validate"/> which restricts double-tap
        /// to modifier keys only.
        /// </summary>
        /// <param name="keyName">The key name to check.</param>
        /// <returns>True if the key name is a valid modifier key for double-tap.</returns>
        public static bool IsValidDoubleTapKey(string keyName)
        {
            return keyName is "Ctrl" or "Alt" or "Shift" or "Win";
        }

        /// <summary>
        /// Gets the display string for a double-tap hotkey.
        /// For example, "Ctrl + Ctrl" becomes "Ctrl ×2".
        /// </summary>
        /// <param name="hotkeyString">The hotkey string to format.</param>
        /// <returns>The formatted display string.</returns>
        public static string GetDisplayString(string hotkeyString)
        {
            if (string.IsNullOrEmpty(hotkeyString))
                return "";

            var parts = hotkeyString.Replace(" ", "").Split('+');
            if (parts.Length == 2 && parts[0] == parts[1])
            {
                return $"{parts[0]} ×2";
            }

            return hotkeyString;
        }

        /// <summary>
        /// Disposes the double-tap detector, stopping the timer and disabling detection.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            GC.SuppressFinalize(this);
            _timeoutTimer.Stop();
            _timeoutTimer.Tick -= OnTimeout;
            Disable();
        }
    }
}
