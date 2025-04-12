using System.Collections.Generic;

namespace Flow.Launcher.Infrastructure.Hotkey;

/// <summary>
/// Interface that you should implement in your settings class to be able to pass it to
/// <c>Flow.Launcher.HotkeyControlDialog</c>. It allows the dialog to display the hotkeys that have already been
/// registered, and optionally provide a way to unregister them.
/// </summary>
public interface IHotkeySettings
{
    /// <summary>
    /// A list of hotkeys that have already been registered. The dialog will display these hotkeys and provide a way to
    /// unregister them.
    /// </summary>
    public List<RegisteredHotkeyData> RegisteredHotkeys { get; }
}
