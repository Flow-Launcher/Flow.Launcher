using System;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class CustomShortcutBaseModel
    {
        public string Key { get; set; }

        [JsonIgnore]
        public Func<string> Expand { get; set; } = () => { return ""; };

        public override bool Equals(object obj)
        {
            return obj is CustomShortcutBaseModel other &&
                   Key == other.Key;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key);
        }
    };

    public class CustomShortcutModel : CustomShortcutBaseModel
    {
        public string Value
        {
            get { return Expand(); }
            set { Expand = () => { return value; }; }
        }

        public CustomShortcutModel(string key, string value)
        {
            Key = key;
            Value = value;
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
