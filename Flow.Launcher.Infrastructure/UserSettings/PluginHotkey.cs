using System;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class CustomPluginHotkey : BaseModel
    {
        public string Hotkey { get; set; }
        public string ActionKeyword { get; set; }

        public CustomPluginHotkey(string hotkey, string actionKeyword)
        {
            Hotkey = hotkey;
            ActionKeyword = actionKeyword;
        }

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
