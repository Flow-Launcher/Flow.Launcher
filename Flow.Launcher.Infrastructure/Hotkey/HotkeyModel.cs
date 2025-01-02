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
        public bool LeftAlt { get; set; }
        public bool LeftShift { get; set; }
        public bool LWin { get; set; }
        public bool LeftCtrl { get; set; }
        public bool RightAlt { get; set; }
        public bool RightShift { get; set; }
        public bool RWin { get; set; }
        public bool RightCtrl { get; set; }

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
                if (LeftAlt || RightAlt)
                {
                    modifierKeys |= ModifierKeys.Alt;
                }

                if (LeftShift || RightShift)
                {
                    modifierKeys |= ModifierKeys.Shift;
                }

                if (LWin || RWin)
                {
                    modifierKeys |= ModifierKeys.Windows;
                }

                if (LeftCtrl || RightCtrl)
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
            HotkeyRaw = FromWPFKeysToString();
        }

        internal void SetHotkeyFromWPFControl(SpecialKeyState specialKeyState, Key key)
        {
            LeftAlt = specialKeyState.LeftAltPressed;
            LeftShift = specialKeyState.LeftShiftPressed;
            LWin = specialKeyState.LWinPressed;
            LeftCtrl = specialKeyState.LeftCtrlPressed;
            RightAlt = specialKeyState.RightAltPressed;
            RightShift = specialKeyState.RightShiftPressed;
            RWin = specialKeyState.RWinPressed;
            RightCtrl = specialKeyState.RightCtrlPressed;
            CharKey = key;
            HotkeyRaw = FromWPFKeysToString();
            PreviousHotkey = string.Empty;
        }

        public HotkeyModel(string hotkey) 
        {
            SetHotkeyFromString(hotkey);
        }

        // Use for ChefKeys only
        internal void AddString(string key)
        {
            HotkeyRaw = string.IsNullOrEmpty(HotkeyRaw) ? key : HotkeyRaw + "+" + key;
            Parse(HotkeyRaw);
        }

        internal string GetLastKeySet() => !string.IsNullOrEmpty(HotkeyRaw) ? HotkeyRaw.Split('+').Last() : string.Empty;    

        internal void Clear()
        {
            LeftAlt = false;
            LeftShift = false;
            LWin = false;
            LeftCtrl = false;
            RightAlt = false;
            RightShift = false;
            RWin = false;
            RightCtrl = false;
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

            List<string> keys = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (keys.Contains("Alt") || keys.Contains("LeftAlt"))
            {
                LeftAlt = true;
                keys.Remove("Alt");
                keys.Remove("LeftAlt");
            }
            if (keys.Contains("RightAlt"))
            {
                RightAlt = true;
                keys.Remove("RightAlt");
            }

            if (keys.Contains("Shift") || keys.Contains("LeftShift"))
            {
                LeftShift = true;
                keys.Remove("Shift");
                keys.Remove("LeftShift");
            }
            if (keys.Contains("RightShift"))
            {
                RightShift = true;
                keys.Remove("RightShift");
            }

            if (keys.Contains("Win") || keys.Contains("LWin"))
            {
                LWin = true;
                keys.Remove("Win");
                keys.Remove("LWin");
            }
            if (keys.Contains("RWin"))
            {
                RWin = true;
                keys.Remove("RWin");
            }

            if (keys.Contains("Ctrl") || keys.Contains("LeftCtrl"))
            {
                LeftCtrl = true;
                keys.Remove("Ctrl");
                keys.Remove("LeftCtrl");
            }
            if (keys.Contains("RightCtrl"))
            {
                RightCtrl = true;
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

        public readonly string ToWPFHotkeyString()
        {
            var hotkey = string.Empty;

            foreach (var key in HotkeyRaw.Split('+'))
            {
                if (!string.IsNullOrEmpty(hotkey))
                    hotkey += " + ";

                switch (key)
                {
                    case "LeftCtrl" or "RightCtrl":
                        hotkey += "Ctrl";
                        break;
                    case "LeftAlt" or "RightAlt":
                        hotkey += "Alt";
                        break;
                    case "LeftShift" or "RightShift":
                        hotkey += "Shift";
                        break;
                    case "LWin" or "RWin":
                        hotkey += "Win";
                        break;

                    default:
                        hotkey += key;
                        break;
                }
            }

            return hotkey;
        }

        public IEnumerable<string> EnumerateDisplayKeys() => !string.IsNullOrEmpty(HotkeyRaw) ? HotkeyRaw.Split('+') : Array.Empty<string>();

        //For WPF hotkey control
        public string FromWPFKeysToString() => string.Join("+", EnumerateWPFKeys());

        public IEnumerable<string> EnumerateWPFKeys()
        {
            if (LeftCtrl && CharKey is not Key.LeftCtrl)
            {
                yield return "LeftCtrl";
            }

            if (LeftAlt && CharKey is not Key.LeftAlt)
            {
                yield return "LeftAlt";
            }

            if (LeftShift && CharKey is not Key.LeftShift)
            {
                yield return "LeftShift";
            }

            if (LWin && CharKey is not Key.LWin)
            {
                yield return "LWin";
            }
            if (RightCtrl && CharKey is not Key.RightCtrl)
            {
                yield return "RightCtrl";
            }

            if (RightAlt && CharKey is not Key.RightAlt)
            {
                yield return "RightAlt";
            }

            if (RightShift && CharKey is not Key.RightShift)
            {
                yield return "RightShift";
            }

            if (RWin && CharKey is not Key.RWin)
            {
                yield return "RWin";
            }

            if (CharKey != Key.None)
            {
                yield return specialSymbolDictionary.TryGetValue(CharKey, out var value)
                    ? value
                    : CharKey.ToString();
            }
        }

        /// <summary>
        /// Validate hotkey for WPF control only
        /// </summary>
        /// <param name="validateKeyGestrue">Try to validate hotkey as a KeyGesture.</param>
        /// <returns></returns>
        public bool ValidateForWpf(bool validateKeyGestrue = false)
        {
            Parse(HotkeyRaw);

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
