using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace Flow.Launcher.Infrastructure.Hotkey
{
    public record struct HotkeyModel
    {
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }
        public bool Ctrl { get; set; }

        public Key CharKey { get; set; } = Key.None;

        private static readonly Dictionary<Key, string> specialSymbolDictionary = new Dictionary<Key, string>
        {
            { Key.Space, "Space" }, { Key.Oem3, "~" }
        };

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

        public HotkeyModel(string hotkeyString)
        {
            Parse(hotkeyString);
        }

        public HotkeyModel(bool alt, bool shift, bool win, bool ctrl, Key key)
        {
            Alt = alt;
            Shift = shift;
            Win = win;
            Ctrl = ctrl;
            CharKey = key;
        }

        private void Parse(string hotkeyString)
        {
            if (string.IsNullOrEmpty(hotkeyString))
            {
                return;
            }

            List<string> keys = hotkeyString.Replace(" ", "").Split('+').ToList();
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

        public override string ToString()
        {
            return string.Join(" + ", EnumerateDisplayKeys());
        }

        public IEnumerable<string> EnumerateDisplayKeys()
        {
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
        /// Validate hotkey
        /// </summary>
        /// <param name="validateKeyGestrue">Try to validate hotkey as a KeyGesture.</param>
        /// <returns></returns>
        public bool Validate(bool validateKeyGestrue = false)
        {
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

        public override int GetHashCode()
        {
            return HashCode.Combine(ModifierKeys, CharKey);
        }
    }
}
