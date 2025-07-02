using System;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class CustomPluginHotkey : BaseModel
    {
        private string _hotkey = string.Empty;
        public string Hotkey
        {
            get => _hotkey;
            set
            {
                if (_hotkey != value)
                {
                    _hotkey = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ActionKeyword { get; set; }

        public override bool Equals(object other)
        {
            if (other is CustomPluginHotkey otherHotkey)
            {
                return Hotkey == otherHotkey.Hotkey && ActionKeyword == otherHotkey.ActionKeyword;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Hotkey, ActionKeyword);
        }
    }
}
