using System;

namespace Flow.Launcher.Infrastructure.Hotkey;

#nullable enable

/// <summary>
/// Represents a hotkey that has been registered. Used in <c>Flow.Launcher.HotkeyControlDialog</c> via
/// <see cref="UserSettings"/> and <see cref="IHotkeySettings"/> to display errors if user tries to register a hotkey
/// that has already been registered, and optionally provides a way to unregister the hotkey.
/// </summary>
public record RegisteredHotkeyData
{
    /// <summary>
    /// <see cref="HotkeyModel"/> representation of this hotkey.
    /// </summary>
    public HotkeyModel Hotkey { get; }

    /// <summary>
    /// String key in the localization dictionary that represents this hotkey. For example, <c>ReloadPluginHotkey</c>,
    /// which represents the string "Reload Plugins Data" in <c>en.xaml</c>
    /// </summary>
    public string DescriptionResourceKey { get; }

    /// <summary>
    /// Array of values that will replace <c>{0}</c>, <c>{1}</c>, <c>{2}</c>, etc. in the localized string found via
    /// <see cref="DescriptionResourceKey"/>.
    /// </summary>
    public object?[] DescriptionFormatVariables { get; } = Array.Empty<object?>();

    /// <summary>
    /// An action that, when called, will unregister this hotkey. If it's <c>null</c>, it's assumed that
    /// this hotkey can't be unregistered, and the "Overwrite" option will not appear in the hotkey dialog.
    /// </summary>
    public Action? RemoveHotkey { get; }

    /// <summary>
    /// Creates an instance of <c>RegisteredHotkeyData</c>. Assumes that the key specified in
    /// <c>descriptionResourceKey</c> doesn't need any arguments for <c>string.Format</c>. If it does,
    /// use one of the other constructors.
    /// </summary>
    /// <param name="hotkey">
    /// The hotkey this class will represent.
    /// Example values: <c>F1</c>, <c>Ctrl+Shift+Enter</c>
    /// </param>
    /// <param name="descriptionResourceKey">
    /// The key in the localization dictionary that represents this hotkey. For example, <c>ReloadPluginHotkey</c>,
    /// which represents the string "Reload Plugins Data" in <c>en.xaml</c>
    /// </param>
    /// <param name="removeHotkey">
    /// An action that, when called, will unregister this hotkey. If it's <c>null</c>, it's assumed that this hotkey
    /// can't be unregistered, and the "Overwrite" option will not appear in the hotkey dialog.
    /// </param>
    public RegisteredHotkeyData(string hotkey, string descriptionResourceKey, Action? removeHotkey = null)
    {
        Hotkey = new HotkeyModel(hotkey);
        DescriptionResourceKey = descriptionResourceKey;
        RemoveHotkey = removeHotkey;
    }

    /// <summary>
    /// Creates an instance of <c>RegisteredHotkeyData</c>. Assumes that the key specified in
    /// <c>descriptionResourceKey</c> needs exactly one argument for <c>string.Format</c>.
    /// </summary>
    /// <param name="hotkey">
    /// The hotkey this class will represent.
    /// Example values: <c>F1</c>, <c>Ctrl+Shift+Enter</c>
    /// </param>
    /// <param name="descriptionResourceKey">
    /// The key in the localization dictionary that represents this hotkey. For example, <c>ReloadPluginHotkey</c>,
    /// which represents the string "Reload Plugins Data" in <c>en.xaml</c>
    /// </param>
    /// <param name="descriptionFormatVariable">
    /// The value that will replace <c>{0}</c> in the localized string found via <c>description</c>.
    /// </param>
    /// <param name="removeHotkey">
    /// An action that, when called, will unregister this hotkey. If it's <c>null</c>, it's assumed that this hotkey
    /// can't be unregistered, and the "Overwrite" option will not appear in the hotkey dialog.
    /// </param>
    public RegisteredHotkeyData(
        string hotkey, string descriptionResourceKey, object? descriptionFormatVariable, Action? removeHotkey = null
    )
    {
        Hotkey = new HotkeyModel(hotkey);
        DescriptionResourceKey = descriptionResourceKey;
        DescriptionFormatVariables = new[] { descriptionFormatVariable };
        RemoveHotkey = removeHotkey;
    }

    /// <summary>
    /// Creates an instance of <c>RegisteredHotkeyData</c>. Assumes that the key specified in
    /// <paramref name="descriptionResourceKey"/> needs multiple arguments for <c>string.Format</c>.
    /// </summary>
    /// <param name="hotkey">
    /// The hotkey this class will represent.
    /// Example values: <c>F1</c>, <c>Ctrl+Shift+Enter</c>
    /// </param>
    /// <param name="descriptionResourceKey">
    /// The key in the localization dictionary that represents this hotkey. For example, <c>ReloadPluginHotkey</c>,
    /// which represents the string "Reload Plugins Data" in <c>en.xaml</c>
    /// </param>
    /// <param name="descriptionFormatVariables">
    /// Array of values that will replace <c>{0}</c>, <c>{1}</c>, <c>{2}</c>, etc.
    /// in the localized string found via <c>description</c>.
    /// </param>
    /// <param name="removeHotkey">
    /// An action that, when called, will unregister this hotkey. If it's <c>null</c>, it's assumed that this hotkey
    /// can't be unregistered, and the "Overwrite" option will not appear in the hotkey dialog.
    /// </param>
    public RegisteredHotkeyData(
        string hotkey, string descriptionResourceKey, object?[] descriptionFormatVariables, Action? removeHotkey = null
    )
    {
        Hotkey = new HotkeyModel(hotkey);
        DescriptionResourceKey = descriptionResourceKey;
        DescriptionFormatVariables = descriptionFormatVariables;
        RemoveHotkey = removeHotkey;
    }
}
