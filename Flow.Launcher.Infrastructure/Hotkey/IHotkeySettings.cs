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

    /// <summary>
    /// The maximum interval in milliseconds between two key presses for double-tap detection.
    /// Used by <see cref="HotkeyControlDialog"/> to set the detection timer interval
    /// so that capture and runtime detection agree on the same threshold.
    /// </summary>
    public int DoubleTapHotkeyInterval { get; }
}
