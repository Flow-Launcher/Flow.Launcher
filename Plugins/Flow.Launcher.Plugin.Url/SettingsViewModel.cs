using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Url;

public class SettingsViewModel(Settings settings) : BaseModel
{
    public Settings Settings { get; } = settings;   
}
