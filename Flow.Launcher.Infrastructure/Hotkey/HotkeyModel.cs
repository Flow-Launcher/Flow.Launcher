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
        public string HotkeyRaw { get; set; } = string.Empty;
        public string PreviousHotkey { get; set; } = string.Empty;

        // HotkeyRaw always be without spaces round '+'. WPF Control hotkey string saved to settings will contain spaces.
        public HotkeyModel(string hotkey) 
        {
            HotkeyRaw = ToHotkeyRawString(hotkey);
        }

        internal void AddString(string key)
        {
            HotkeyRaw = string.IsNullOrEmpty(HotkeyRaw) ? key : HotkeyRaw + "+" + key;
        }

        // Display in the form of WPF Control i.e. simplified text e.g. LeftAlt -> Alt
        public IEnumerable<string> EnumerateDisplayKeys() => !string.IsNullOrEmpty(HotkeyRaw) ? ToWPFHotkeyString().Split(" + ") : Array.Empty<string>();

        internal string GetLastKeySet() => !string.IsNullOrEmpty(HotkeyRaw) ? HotkeyRaw.Split('+').Last() : string.Empty;    

        internal void Clear()
        {
            HotkeyRaw = string.Empty;
            PreviousHotkey = string.Empty;
        }

        // WPF Control hotkey form i.e. simplified text e.g. LeftAlt+X -> Alt + X, includes space around '+'
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

        // Converts any WPF Control hotkey e.g. Alt + X -> LeftAlt+X
        public readonly string ToHotkeyRawString(string wpfHotkey)
        {
            var hotkey = string.Empty;

            foreach (var key in wpfHotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrEmpty(hotkey))
                    hotkey += "+";

                switch (key)
                {
                    case "Ctrl":
                        hotkey += "LeftCtrl";
                        break;
                    case "Alt":
                        hotkey += "LeftAlt";
                        break;
                    case "Shift":
                        hotkey += "LeftShift";
                        break;
                    case "Win":
                        hotkey += "LWin";
                        break;

                    default:
                        hotkey += key;
                        break;
                }
            }

            return hotkey;
        }

        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }
        public bool Ctrl { get; set; }

        public Key CharKey { get; set; } = Key.None;

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

        private void Parse(string hotkeyString)
        {
            if (string.IsNullOrEmpty(hotkeyString))
            {
                return;
            }

            List<string> keys = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

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

            if (keys.Contains("Ctrl") || keys.Contains("LeftCtrl") || keys.Contains("RightCtrl"))
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
            return HotkeyRaw.GetHashCode();
        }
    }
}
