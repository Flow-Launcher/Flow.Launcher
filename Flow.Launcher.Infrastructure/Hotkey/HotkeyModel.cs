using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace Flow.Launcher.Infrastructure.Hotkey
{
    /// <summary>
    /// Represents a hotkey binding, supporting both traditional combo hotkeys (e.g., Ctrl+Space)
    /// and double-tap hotkeys (e.g., Ctrl+Ctrl, where the same modifier key is pressed twice).
    /// </summary>
    public record struct HotkeyModel
    {
        /// <summary>
        /// Whether the Alt modifier is part of this hotkey combo.
        /// </summary>
        public bool Alt { get; set; }

        /// <summary>
        /// Whether the Shift modifier is part of this hotkey combo.
        /// </summary>
        public bool Shift { get; set; }

        /// <summary>
        /// Whether the Win modifier is part of this hotkey combo.
        /// </summary>
        public bool Win { get; set; }

        /// <summary>
        /// Whether the Ctrl modifier is part of this hotkey combo.
        /// </summary>
        public bool Ctrl { get; set; }

        /// <summary>
        /// The primary key of the hotkey. For combo hotkeys, this is the non-modifier key.
        /// For double-tap hotkeys, this is the modifier key (e.g., Key.LeftCtrl for "Ctrl + Ctrl").
        /// </summary>
        public Key CharKey { get; set; } = Key.None;

        /// <summary>
        /// Indicates this is a double-tap hotkey (e.g., "Ctrl + Ctrl" means press Ctrl twice).
        /// When true, the hotkey is triggered by pressing the same modifier key twice within a time interval.
        /// Only modifier keys (Ctrl, Alt, Shift, Win) are valid for double-tap bindings.
        /// </summary>
        public bool DoubleTap { get; set; } = false;

        /// <summary>
        /// Maps special keys to their display string representations.
        /// </summary>
        private static readonly Dictionary<Key, string> specialSymbolDictionary = new Dictionary<Key, string>
        {
            { Key.Space, "Space" }, { Key.Oem3, "~" }
        };

        /// <summary>
        /// Maps modifier key names to their WPF Key equivalents for double-tap parsing.
        /// Only these keys are valid for double-tap hotkey bindings.
        /// </summary>
        private static readonly Dictionary<string, Key> modifierKeyMap = new Dictionary<string, Key>
        {
            { "Ctrl", Key.LeftCtrl },
            { "Alt", Key.LeftAlt },
            { "Shift", Key.LeftShift },
            { "Win", Key.LWin }
        };

        /// <summary>
        /// Gets the combined modifier keys for this hotkey as a ModifierKeys value.
        /// For double-tap hotkeys, this returns ModifierKeys.None since modifiers
        /// are not used in the traditional sense.
        /// </summary>
        public ModifierKeys ModifierKeys
        {
            get
            {
                ModifierKeys modifierKeys = ModifierKeys.None;
                if (Alt)
                {
                    modifierKeys |= ModifierKeys.Alt;
                }

                if (Shift)
                {
                    modifierKeys |= ModifierKeys.Shift;
                }

                if (Win)
                {
                    modifierKeys |= ModifierKeys.Windows;
                }

                if (Ctrl)
                {
                    modifierKeys |= ModifierKeys.Control;
                }

                return modifierKeys;
            }
        }

        /// <summary>
        /// Creates a HotkeyModel by parsing a hotkey string.
        /// Supports combo format (e.g., "Ctrl+Space") and double-tap format (e.g., "Ctrl+Ctrl").
        /// </summary>
        /// <param name="hotkeyString">The hotkey string to parse.</param>
        public HotkeyModel(string hotkeyString)
        {
            Parse(hotkeyString);
        }

        /// <summary>
        /// Creates a HotkeyModel for a traditional combo hotkey.
        /// </summary>
        /// <param name="alt">Whether Alt is pressed.</param>
        /// <param name="shift">Whether Shift is pressed.</param>
        /// <param name="win">Whether Win is pressed.</param>
        /// <param name="ctrl">Whether Ctrl is pressed.</param>
        /// <param name="key">The primary key.</param>
        public HotkeyModel(bool alt, bool shift, bool win, bool ctrl, Key key)
        {
            Alt = alt;
            Shift = shift;
            Win = win;
            Ctrl = ctrl;
            CharKey = key;
        }

        /// <summary>
        /// Creates a HotkeyModel with explicit double-tap support.
        /// When doubleTap is true, modifier flags are cleared and only the CharKey is used.
        /// </summary>
        /// <param name="alt">Whether Alt is pressed (ignored if doubleTap is true).</param>
        /// <param name="shift">Whether Shift is pressed (ignored if doubleTap is true).</param>
        /// <param name="win">Whether Win is pressed (ignored if doubleTap is true).</param>
        /// <param name="ctrl">Whether Ctrl is pressed (ignored if doubleTap is true).</param>
        /// <param name="key">The key to monitor for double-tap.</param>
        /// <param name="doubleTap">Whether this is a double-tap hotkey.</param>
        public HotkeyModel(bool alt, bool shift, bool win, bool ctrl, Key key, bool doubleTap)
        {
            DoubleTap = doubleTap;
            if (doubleTap)
            {
                Alt = false;
                Shift = false;
                Win = false;
                Ctrl = false;
            }
            else
            {
                Alt = alt;
                Shift = shift;
                Win = win;
                Ctrl = ctrl;
            }
            CharKey = key;
        }

        /// <summary>
        /// Parses a hotkey string into this HotkeyModel.
        /// Supports combo format (e.g., "Ctrl+Space") and double-tap format (e.g., "Ctrl+Ctrl").
        /// For double-tap, only modifier keys (Ctrl, Alt, Shift, Win) are valid.
        /// </summary>
        /// <param name="hotkeyString">The hotkey string to parse.</param>
        private void Parse(string hotkeyString)
        {
            if (string.IsNullOrEmpty(hotkeyString))
            {
                return;
            }

            var parts = hotkeyString.Replace(" ", "").Split('+').ToList();

            // Double-tap format: "Key + Key" where both parts are the same (e.g., "Ctrl + Ctrl")
            // Only modifier keys are valid for double-tap, consistent with DoubleTapDetector.IsValidDoubleTapKey
            if (parts.Count == 2 && parts[0] == parts[1])
            {
                var keyName = parts[0];

                if (modifierKeyMap.TryGetValue(keyName, out var modifierKey))
                {
                    DoubleTap = true;
                    CharKey = modifierKey;
                }
                // Non-modifier keys are not valid for double-tap — leave DoubleTap as false

                return;
            }

            // Regular hotkey format (existing logic)
            List<string> keys = parts;
            if (keys.Contains("Alt"))
            {
                Alt = true;
                keys.Remove("Alt");
            }

            if (keys.Contains("Shift"))
            {
                Shift = true;
                keys.Remove("Shift");
            }

            if (keys.Contains("Win"))
            {
                Win = true;
                keys.Remove("Win");
            }

            if (keys.Contains("Ctrl"))
            {
                Ctrl = true;
                keys.Remove("Ctrl");
            }

            if (keys.Count == 1)
            {
                string charKey = keys[0];
                KeyValuePair<Key, string>? specialSymbolPair =
                    specialSymbolDictionary.FirstOrDefault(pair => pair.Value == charKey);
                if (specialSymbolPair.Value.Value != null)
                {
                    CharKey = specialSymbolPair.Value.Key;
                }
                else
                {
                    try
                    {
                        CharKey = (Key)Enum.Parse(typeof(Key), charKey);
                    }
                    catch (ArgumentException)
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Returns the string representation of this hotkey (e.g., "Ctrl + Space" or "Ctrl + Ctrl").
        /// </summary>
        public override string ToString()
        {
            return string.Join(" + ", EnumerateDisplayKeys());
        }

        /// <summary>
        /// Enumerates the display names of the keys in this hotkey for UI rendering.
        /// For double-tap hotkeys, yields the modifier name twice (e.g., "Ctrl", "Ctrl").
        /// </summary>
        public IEnumerable<string> EnumerateDisplayKeys()
        {
            // Double-tap display: show the key name twice (e.g., "Ctrl" + "Ctrl")
            if (DoubleTap && CharKey != Key.None)
            {
                var keyName = specialSymbolDictionary.TryGetValue(CharKey, out var value)
                    ? value
                    : CharKey.ToString();

                // Map modifier key names back to friendly names
                keyName = CharKey switch
                {
                    Key.LeftCtrl or Key.RightCtrl => "Ctrl",
                    Key.LeftAlt or Key.RightAlt => "Alt",
                    Key.LeftShift or Key.RightShift => "Shift",
                    Key.LWin or Key.RWin => "Win",
                    _ => keyName
                };

                yield return keyName;
                yield return keyName;
                yield break;
            }

            if (Ctrl && CharKey is not (Key.LeftCtrl or Key.RightCtrl))
            {
                yield return "Ctrl";
            }

            if (Alt && CharKey is not (Key.LeftAlt or Key.RightAlt))
            {
                yield return "Alt";
            }

            if (Shift && CharKey is not (Key.LeftShift or Key.RightShift))
            {
                yield return "Shift";
            }

            if (Win && CharKey is not (Key.LWin or Key.RWin))
            {
                yield return "Win";
            }

            if (CharKey != Key.None)
            {
                yield return specialSymbolDictionary.TryGetValue(CharKey, out var value)
                    ? value
                    : CharKey.ToString();
            }
        }

        /// <summary>
        /// Validates this hotkey. For double-tap hotkeys, only modifier keys are valid.
        /// For combo hotkeys, validates that the key is not a lone modifier or printable character
        /// without modifiers, and optionally validates as a WPF KeyGesture.
        /// </summary>
        /// <param name="validateKeyGestrue">Whether to also validate as a WPF KeyGesture.</param>
        /// <returns>True if the hotkey is valid.</returns>
        public bool Validate(bool validateKeyGestrue = false)
        {
            // Double-tap hotkeys are only valid for modifier keys
            if (DoubleTap)
            {
                return CharKey is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
            }

            switch (CharKey)
            {
                case Key.LeftAlt:
                case Key.RightAlt:
                case Key.LeftCtrl:
                case Key.RightCtrl:
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LWin:
                case Key.RWin:
                case Key.None:
                    return false;
                default:
                    if (validateKeyGestrue)
                    {
                        try
                        {
                            KeyGesture keyGesture = new KeyGesture(CharKey, ModifierKeys);
                        }
                        catch (System.Exception e) when
                            (e is NotSupportedException || e is InvalidEnumArgumentException)
                        {
                            return false;
                        }
                    }

                    if (ModifierKeys == ModifierKeys.None)
                    {
                        return !IsPrintableCharacter(CharKey);
                    }
                    else
                    {
                        return true;
                    }
            }
        }

        /// <summary>
        /// Determines whether a key represents a printable character that should not be
        /// used as a standalone hotkey (without modifiers).
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key is a printable character.</returns>
        private static bool IsPrintableCharacter(Key key)
        {
            // https://stackoverflow.com/questions/11881199/identify-if-a-event-key-is-text-not-only-alphanumeric
            return (key >= Key.A && key <= Key.Z) ||
                   (key >= Key.D0 && key <= Key.D9) ||
                   (key >= Key.NumPad0 && key <= Key.NumPad9) ||
                   key == Key.OemQuestion ||
                   key == Key.OemQuotes ||
                   key == Key.OemPlus ||
                   key == Key.OemOpenBrackets ||
                   key == Key.OemCloseBrackets ||
                   key == Key.OemMinus ||
                   key == Key.DeadCharProcessed ||
                   key == Key.Oem1 ||
                   key == Key.Oem7 ||
                   key == Key.OemPeriod ||
                   key == Key.OemComma ||
                   key == Key.OemMinus ||
                   key == Key.Add ||
                   key == Key.Divide ||
                   key == Key.Multiply ||
                   key == Key.Subtract ||
                   key == Key.Oem102 ||
                   key == Key.Decimal;
        }

        /// <summary>
        /// Returns a hash code combining the modifier keys, character key, and double-tap flag.
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(ModifierKeys, CharKey, DoubleTap);
        }
    }
}
