using System.Collections.Generic;

namespace Flow.Launcher.Infrastructure.Hotkey;

public interface IHotkeySettings
{
    public List<RegisteredHotkeyData> RegisteredHotkeys { get; }
}
