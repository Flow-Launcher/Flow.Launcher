using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Flow.Launcher.Infrastructure.Hotkey
{
    public class HotkeyModel
    {
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }
        public bool Ctrl { get; set; }

        private Key charKey = Key.None;
        public Key CharKey
        {
            get => charKey;
            set
            {
                if (ValidateHotkey(value))
                {
                    charKey = value;
                }
            }
        }

        private static readonly Dictionary<Key, string> specialSymbolDictionary = new Dictionary<Key, string>
        {
            {Key.Space, "Space"},
            {Key.Oem3, "~"}
        };

        public ModifierKeys ModifierKeys
        {
            get
            {
                ModifierKeys modifierKeys = ModifierKeys.None;
                if (Alt)
                {
                    modifierKeys = ModifierKeys.Alt;
                }
                if (Shift)
                {
                    modifierKeys = modifierKeys | ModifierKeys.Shift;
                }
                if (Win)
                {
                    modifierKeys = modifierKeys | ModifierKeys.Windows;
                }
                if (Ctrl)
                {
                    modifierKeys = modifierKeys | ModifierKeys.Control;
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
                KeyValuePair<Key, string>? specialSymbolPair = specialSymbolDictionary.FirstOrDefault(pair => pair.Value == charKey);
                if (specialSymbolPair.Value.Value != null)
                {
                    CharKey = specialSymbolPair.Value.Key;
                }
                else
                {
                    try
                    {
                        CharKey = (Key) Enum.Parse(typeof (Key), charKey);
                    }
                    catch (ArgumentException)
                    {

                    }
                }
            }
        }

        public override string ToString()
        {
            List<string> keys = new List<string>();
            if (Ctrl)
            {
                keys.Add("Ctrl");
            }
            if (Alt)
            {
                keys.Add("Alt");
            }
            if (Shift)
            {
                keys.Add("Shift");
            }
            if (Win)
            {
                keys.Add("Win");
            }

            if (CharKey != Key.None)
            {
                keys.Add(specialSymbolDictionary.ContainsKey(CharKey)
                    ? specialSymbolDictionary[CharKey]
                    : CharKey.ToString());
            }
            return string.Join(" + ", keys);
        }

        private static bool ValidateHotkey(Key key)
        {
            HashSet<Key> invalidKeys = new()
            {
                Key.LeftAlt, Key.RightAlt,
                Key.LeftCtrl, Key.RightCtrl,
                Key.LeftShift, Key.RightShift,
                Key.LWin, Key.RWin,
            };

            return !invalidKeys.Contains(key);
        }

        public override bool Equals(object obj)
        {
            if (obj is HotkeyModel other)
            {
                return ModifierKeys == other.ModifierKeys && CharKey == other.charKey;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }
    }
}
