using System;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using Flow.Launcher.Plugin;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Flow.Launcher.Infrastructure.Hotkey
{
    /// <summary>
    /// Detects double-tap (double-press) of a single key within a configurable time interval.
    /// For example, pressing Ctrl twice within 300ms triggers the double-tap action.
    /// Uses the GlobalHotkey WH_KEYBOARD_LL hook to track key-down and key-up events.
    /// 
    /// Important: For modifier keys, Windows generates auto-repeat WM_KEYDOWN events when
    /// the key is held down. To avoid false positives, we only count a second press if
    /// the key was released between the two presses (key-up detected between key-downs).
    /// </summary>
    public class DoubleTapDetector : IDisposable
    {
        private const string ClassName = nameof(DoubleTapDetector);

        private readonly DispatcherTimer _timeoutTimer;
        private readonly Action _doubleTapAction;
        private readonly Action _singleTapAction;
        private readonly int _intervalMs;

        // The virtual key code to monitor for double-tap
        private VIRTUAL_KEY _targetVkCode;

        // Timestamp tracking
        private long _lastKeyDownTimestamp = 0;
        private bool _firstPressPending = false;
        private bool _keyIsCurrentlyDown = false; // Tracks whether the target key is physically held down
        private long _lastKeyDownTickCount = 0; // TickCount64 of last key-down, for desync recovery
        private bool _disposed = false;

        /// <summary>
        /// Whether the double-tap detector is currently active and monitoring key events.
        /// </summary>
        public bool IsEnabled { get; private set; }

        /// <summary>
        /// The hotkey string representation (e.g., "Ctrl + Ctrl", "Alt + Alt", "Space + Space").
        /// </summary>
        public string HotkeyString { get; private set; }

        /// <summary>
        /// Creates a DoubleTapDetector that monitors a specific key for double-press within an interval.
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

        private static void Log(string message)
        {
            Debug.WriteLine($"[{ClassName}] {message}");
        }

        /// <summary>
        /// Parses the hotkey string to determine which key to monitor.
        /// Double-tap hotkeys are represented as "Key + Key" (e.g., "Ctrl + Ctrl", "Alt + Alt").
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
                        if (Enum.TryParse<VIRTUAL_KEY>("VK_" + keyName.ToUpper(), out var vk))
                        {
                            _targetVkCode = vk;
                        }
                        else
                        {
                            try
                            {
                                var wpfKey = (Key)Enum.Parse(typeof(Key), keyName);
                                _targetVkCode = MapWpfKeyToVirtualKey(wpfKey);
                            }
                            catch
                            {
                                _targetVkCode = (VIRTUAL_KEY)0;
                            }
                        }
                        break;
                }
            }
            else if (parts.Length == 1)
            {
                var keyName = parts[0];

                try
                {
                    var wpfKey = (Key)Enum.Parse(typeof(Key), keyName);
                    _targetVkCode = MapWpfKeyToVirtualKey(wpfKey);
                }
                catch
                {
                    _targetVkCode = (VIRTUAL_KEY)0;
                }
            }
            else
            {
                _targetVkCode = (VIRTUAL_KEY)0;
            }
        }

        private static VIRTUAL_KEY MapWpfKeyToVirtualKey(Key key)
        {
            return (VIRTUAL_KEY)KeyInterop.VirtualKeyFromKey(key);
        }

        /// <summary>
        /// Enables the double-tap detector.
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
        /// Disables the double-tap detector.
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
                    // Match VK_LCONTROL (0xA2) or VK_RCONTROL (0xA3)
                    return vkCode == 0xA2 || vkCode == 0xA3;
                case VIRTUAL_KEY.VK_MENU:
                    // Match VK_LMENU (0xA4) or VK_RMENU (0xA5)
                    return vkCode == 0xA4 || vkCode == 0xA5;
                case VIRTUAL_KEY.VK_SHIFT:
                    // Match VK_LSHIFT (0xA0) or VK_RSHIFT (0xA1)
                    return vkCode == 0xA0 || vkCode == 0xA1;
                case VIRTUAL_KEY.VK_LWIN:
                    // Match VK_LWIN (0x5B) or VK_RWIN (0x5C)
                    return vkCode == 0x5B || vkCode == 0x5C;
                default:
                    return vkCode == (int)_targetVkCode;
            }
        }

        /// <summary>
        /// Called when the timeout timer expires - means only one press was detected within the interval.
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
        /// </summary>
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
        /// </summary>
        public static bool IsValidDoubleTapKey(string keyName)
        {
            switch (keyName)
            {
                case "Ctrl":
                case "Alt":
                case "Shift":
                case "Win":
                    return true;
                default:
                    try
                    {
                        var key = (Key)Enum.Parse(typeof(Key), keyName);
                        return key != Key.None &&
                               key != Key.LeftCtrl && key != Key.RightCtrl &&
                               key != Key.LeftAlt && key != Key.RightAlt &&
                               key != Key.LeftShift && key != Key.RightShift &&
                               key != Key.LWin && key != Key.RWin;
                    }
                    catch
                    {
                        return false;
                    }
            }
        }

        /// <summary>
        /// Gets the display string for a double-tap hotkey.
        /// </summary>
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
