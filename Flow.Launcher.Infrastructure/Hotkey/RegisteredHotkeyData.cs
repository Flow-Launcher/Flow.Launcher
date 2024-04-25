using System;

namespace Flow.Launcher.Infrastructure.Hotkey;

#nullable enable

public record RegisteredHotkeyData
{
    public HotkeyModel Hotkey { get; }
    public string Description { get; }
    public Action? RemoveHotkey { get; }

    public RegisteredHotkeyData(string hotkey, string description, Action? removeHotkey = null)
    {
        Hotkey = new HotkeyModel(hotkey);
        Description = description;
        RemoveHotkey = removeHotkey;
    }
}
