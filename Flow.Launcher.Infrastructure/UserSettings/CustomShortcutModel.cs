using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    #region Base

    public abstract class ShortcutBaseModel
    {
        public string Key { get; set; }

        public override bool Equals(object obj)
        {
            return obj is ShortcutBaseModel other &&
                   Key == other.Key;
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }
    }

    public class BaseCustomShortcutModel : ShortcutBaseModel
    {
        public string Value { get; set; }

        public BaseCustomShortcutModel(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public void Deconstruct(out string key, out string value)
        {
            key = Key;
            value = Value;
        }

        public static implicit operator (string Key, string Value)(BaseCustomShortcutModel shortcut)
        {
            return (shortcut.Key, shortcut.Value);
        }

        public static implicit operator BaseCustomShortcutModel((string Key, string Value) shortcut)
        {
            return new BaseCustomShortcutModel(shortcut.Key, shortcut.Value);
        }
    }

    public class BaseBuiltinShortcutModel : ShortcutBaseModel
    {
        public string Description { get; set; }

        public string LocalizedDescription => PublicApi.Instance.GetTranslation(Description);

        public BaseBuiltinShortcutModel(string key, string description)
        {
            Key = key;
            Description = description;
        }
    }

    #endregion

    #region Custom Shortcut

    public class CustomShortcutModel : BaseCustomShortcutModel
    {
        [JsonIgnore]
        public Func<string> Expand { get; set; } = () => { return string.Empty; };

        [JsonConstructor]
        public CustomShortcutModel(string key, string value) : base(key, value)
        {
            Expand = () => { return Value; };
        }
    }

    #endregion

    #region Builtin Shortcut

    public class BuiltinShortcutModel : BaseBuiltinShortcutModel
    {
        [JsonIgnore]
        public Func<string> Expand { get; set; } = () => { return string.Empty; };

        public BuiltinShortcutModel(string key, string description, Func<string> expand) : base(key, description)
        {
            Expand = expand ?? (() => { return string.Empty; });
        }
    }

    public class AsyncBuiltinShortcutModel : BaseBuiltinShortcutModel
    {
        [JsonIgnore]
        public Func<Task<string>> ExpandAsync { get; set; } = () => { return Task.FromResult(string.Empty); };

        public AsyncBuiltinShortcutModel(string key, string description, Func<Task<string>> expandAsync) : base(key, description)
        {
            ExpandAsync = expandAsync ?? (() => { return Task.FromResult(string.Empty); });
        }
    }

    #endregion
}
