using Flow.Launcher.Plugin;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class CustomPluginHotkey : BaseModel
    {
        public string Hotkey { get; set; }
        public string ActionKeyword { get; set; }
    }
}
