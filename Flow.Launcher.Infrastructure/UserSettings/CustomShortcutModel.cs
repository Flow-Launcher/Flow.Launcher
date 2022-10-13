using System;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Infrastructure.UserSettings
{

    public class CustomShortcutModel
    {
        public string Key { get; set; }
        public string Value { get; set; }

        [JsonIgnore]
        public bool CanBeEdited { get; private set; } // Can be edited by user from settings window

        [JsonIgnore]
        public Func<string> Expand { get; set; } = () => { return ""; };

        [JsonConstructorAttribute]
        public CustomShortcutModel(string key, string value)
        {
            Key = key;
            Value = value;
            CanBeEdited = true;
            Expand = () => { return Value; };
        }

        public CustomShortcutModel(string key, string description, Func<string> expand)
        {
            Key = key;
            Value = description;
            CanBeEdited = false;
            Expand = expand;
        }

        public override bool Equals(object obj)
        {
            return obj is CustomShortcutModel other &&
                   Key == other.Key;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key);
        }

        public void Deconstruct(out string key, out string value)
        {
            key = Key;
            value = Expand();
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
