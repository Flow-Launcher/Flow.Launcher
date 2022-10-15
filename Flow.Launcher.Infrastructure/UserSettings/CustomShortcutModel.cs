using System;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public abstract class ShortcutBaseModel
    {
        public string Key { get; set; }

        [JsonIgnore]
        public Func<string> Expand { get; set; } = () => { return ""; };

        public override bool Equals(object obj)
        {
            return obj is CustomShortcutModel other &&
                   Key == other.Key;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key);
        }
    }

    public class CustomShortcutModel : ShortcutBaseModel
    {
        public string Value { get; set; }

        [JsonConstructorAttribute]
        public CustomShortcutModel(string key, string value)
        {
            Key = key;
            Value = value;
            Expand = () => { return Value; };
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

    public class BuiltinShortcutModel : ShortcutBaseModel
    {
        public string Description { get; set; }

        public BuiltinShortcutModel(string key, string description, Func<string> expand)
        {
            Key = key;
            Description = description;
            Expand = expand ?? (() => { return ""; });
        }
    }
}
