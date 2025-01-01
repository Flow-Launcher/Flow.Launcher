using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Infrastructure.Hotkey
{
    public record struct HotkeyModel
    {
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }
        public bool Ctrl { get; set; }

        public string HotkeyRaw { get; set; } = string.Empty;
        public string PreviousHotkey { get; set; } = string.Empty;

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

        // Used for WPF control only
        public void SetHotkeyFromString(string hotkeyString)
        {
            Clear();
            Parse(hotkeyString);
            HotkeyRaw = ToChefKeysString();
        }

        internal void SetHotkeyFromWPFControl(SpecialKeyState specialKeyState, Key key)
        {
            Alt = specialKeyState.AltPressed;
            Shift = specialKeyState.ShiftPressed;
            Win = specialKeyState.WinPressed;
            Ctrl = specialKeyState.CtrlPressed;
            CharKey = key;
            HotkeyRaw = ToChefKeysString();
            PreviousHotkey = string.Empty;
        }

        public HotkeyModel(string hotkey) 
        {
            SetHotkeyFromString(hotkey);
        }

        //public HotkeyModel(bool alt, bool shift, bool win, bool ctrl, Key key)
        //{
        //    Alt = alt;
        //    Shift = shift;
        //    Win = win;
        //    Ctrl = ctrl;
        //    CharKey = key;
        //}

        // Use for ChefKeys only
        internal void AddString(string key)
        {
            HotkeyRaw = string.IsNullOrEmpty(HotkeyRaw) ? key : HotkeyRaw + "+" + key;
        }

        internal bool MaxKeysReached() => DisplayKeysRaw().Count() == 4;

        internal void Clear()
        {
            Alt = false;
            Shift = false;
            Win = false;
            Ctrl = false;
            HotkeyRaw = string.Empty;
            PreviousHotkey = string.Empty;
            CharKey = Key.None;
        }

        private void Parse(string hotkeyString)
        {
            if (string.IsNullOrEmpty(hotkeyString))
            {
                return;
            }

            List<string> keys = hotkeyString.Replace(" ", "").Split('+').ToList();
            if (keys.Contains("Alt") || keys.Contains("LeftAlt") || keys.Contains("RightAlt"))
            {
                Alt = true;
                keys.Remove("Alt");
                keys.Remove("LeftAlt");
                keys.Remove("RightAlt");
            }

            if (keys.Contains("Shift") || keys.Contains("LeftShift") || keys.Contains("RightShift"))
            {
                Shift = true;
                keys.Remove("Shift");
                keys.Remove("LeftShift");
                keys.Remove("RightShift");
            }

            if (keys.Contains("Win") || keys.Contains("LWin") || keys.Contains("RWin"))
            {
                Win = true;
                keys.Remove("Win");
                keys.Remove("LWin");
                keys.Remove("RWin");
            }

            if (keys.Contains("Ctrl") || keys.Contains("LeftCtrl")|| keys.Contains("RightCtrl"))
            {
                Ctrl = true;
                keys.Remove("Ctrl");
                keys.Remove("LeftCtrl");
                keys.Remove("RightCtrl");
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

        public string ToChefKeysString()
        {
            var key = string.Join("+", EnumerateDisplayKeys(true));
            
            return key;
        }

        public IEnumerable<string> DisplayKeysRaw() => !string.IsNullOrEmpty(HotkeyRaw) ? HotkeyRaw.Split('+') : Array.Empty<string>();

        public IEnumerable<string> EnumerateDisplayKeys(bool forChefKeys = false)
        {
            if (Ctrl && CharKey is not (Key.LeftCtrl or Key.RightCtrl))
            {
                yield return GetKeyString("Ctrl", forChefKeys);
            }

            if (Alt && CharKey is not (Key.LeftAlt or Key.RightAlt))
            {
                yield return GetKeyString("Alt", forChefKeys);
            }

            if (Shift && CharKey is not (Key.LeftShift or Key.RightShift))
            {
                yield return GetKeyString("Shift", forChefKeys);
            }

            if (Win && CharKey is not (Key.LWin or Key.RWin))
            {
                yield return GetKeyString("Win", forChefKeys);
            }

            if (CharKey != Key.None)
            {
                yield return specialSymbolDictionary.TryGetValue(CharKey, out var value)
                    ? value
                    : GetKeyString(CharKey.ToString(), forChefKeys);
            }
        }

        private string GetKeyString(string key, bool convertToChefKeysString)
        {
            if (!convertToChefKeysString)
                return key;

            switch (key)
            {
                case "Alt":
                    return "LeftAlt";
                case "Ctrl":
                    return "LeftCtrl";
                case "Shift":
                    return "LeftShift";
                case "Win":
                    return "LWin";
                default:
                    return key;
            }
        }

        /// <summary>
        /// Validate hotkey for WPF control only
        /// </summary>
        /// <param name="validateKeyGestrue">Try to validate hotkey as a KeyGesture.</param>
        /// <returns></returns>
        public bool ValidateForWpf(bool validateKeyGestrue = false)
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
