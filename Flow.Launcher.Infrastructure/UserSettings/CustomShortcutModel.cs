using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class CustomShortcutModel
    {
        public string Key { get; set; }
        public string Value { get; set; }

        public CustomShortcutModel(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public override bool Equals(object obj)
        {
            return obj is CustomShortcutModel other &&
                   Key == other.Key &&
                   Value == other.Value;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key, Value);
        }

        public void Deconstruct(out string key, out string value)
        {
            key = Key;
            value = Value;
        }

        public static implicit operator (string Key, string Value)(CustomShortcutModel shortcut)
        {
            return (shortcut.Key, shortcut.Value);
        }

        public static implicit operator CustomShortcutModel((string Key, string Value) shortcut)
        {
            return new CustomShortcutModel(shortcut.Key, shortcut.Value);
        }
    }
}
