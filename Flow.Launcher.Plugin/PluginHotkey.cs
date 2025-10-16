using System;

namespace Flow.Launcher.Plugin;

/// <summary>
/// Represents a base plugin hotkey model.
/// </summary>
/// <remarks>
/// Do not use this class directly. Use <see cref="GlobalPluginHotkey"/> or <see cref="SearchWindowPluginHotkey"/> instead.
/// </remarks>
public class BasePluginHotkey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BasePluginHotkey"/> class with the specified hotkey type.
    /// </summary>
    /// <param name="type">The type of hotkey (Global or SearchWindow).</param>
    protected BasePluginHotkey(HotkeyType type)
    {
        HotkeyType = type;
    }

    /// <summary>
    /// The unique identifier for the hotkey, which is used to identify and rank the hotkey in the settings page.
    /// </summary>
    public int Id { get; set; } = 0;

    /// <summary>
    /// The name of the hotkey, which will be displayed in the settings page.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The description of the hotkey, which will be displayed in the settings page.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The glyph information for the hotkey, which will be displayed in the settings page.
    /// </summary>
    public GlyphInfo Glyph { get; set; }

    /// <summary>
    /// The default hotkey that will be used if the user does not set a custom hotkey.
    /// </summary>
    public string DefaultHotkey { get; set; } = string.Empty;

    /// <summary>
    /// The type of the hotkey, which can be either global or search window specific.
    /// </summary>
    public HotkeyType HotkeyType { get; } = HotkeyType.Global;

    /// <summary>
    /// Indicates whether the hotkey is editable by the user in the settings page.
    /// </summary>
    public bool Editable { get; set; } = false;

    /// <summary>
    /// Whether to show the hotkey in the settings page.
    /// </summary>
    public bool Visible { get; set; } = true;
}

/// <summary>
/// Represent a global plugin hotkey model.
/// </summary>
public class GlobalPluginHotkey : BasePluginHotkey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalPluginHotkey"/> class.
    /// </summary>
    public GlobalPluginHotkey() : base(HotkeyType.Global)
    {
    }

    /// <summary>
    /// An action that will be executed when the hotkey is triggered.
    /// </summary>
    public Action Action { get; set; } = null;
}

/// <summary>
/// Represents a plugin hotkey that is specific to the search window.
/// </summary>
public class SearchWindowPluginHotkey : BasePluginHotkey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchWindowPluginHotkey"/> class.
    /// </summary>
    public SearchWindowPluginHotkey() : base(HotkeyType.SearchWindow)
    {
    }

    /// <summary>
    /// An action that will be executed when the hotkey is triggered and a result is selected.
    /// </summary>
    public Func<Result, bool> Action { get; set; } = null;
}

/// <summary>
/// Represents the type of hotkey for a plugin.
/// </summary>
public enum HotkeyType
{
    /// <summary>
    /// A hotkey that will be triggered globally, regardless of the active window.
    /// </summary>
    Global,

    /// <summary>
    /// A hotkey that will be triggered only when the search window is active.
    /// </summary>
    SearchWindow
}

/// <summary>
/// Represents a plugin hotkey model which is used to store the hotkey information for a plugin.
/// </summary>
public class PluginHotkey
{
    /// <summary>
    /// The unique identifier for the hotkey.
    /// </summary>
    public int Id { get; set; } = 0;

    /// <summary>
    /// The default hotkey that will be used if the user does not set a custom hotkey.
    /// </summary>
    public string DefaultHotkey { get; set; } = string.Empty;

    /// <summary>
    /// The current hotkey that the user has set for the plugin.
    /// </summary>
    public string Hotkey { get; set; } = string.Empty;
}
