using System;
using System.Windows.Input;
using Flow.Launcher.Plugin;

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
    /// Type of this hotkey in the context of the application.
    /// </summary>
    public RegisteredHotkeyType RegisteredType { get; }

    /// <summary>
    /// Type of this hotkey.
    /// </summary>
    public HotkeyType Type { get; }

    /// <summary>
    /// <see cref="HotkeyModel"/> representation of this hotkey.
    /// </summary>
    public HotkeyModel Hotkey { get; private set; }

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
    /// Command of this hotkey. If it's <c>null</c>, the hotkey is assumed to be registered by system.
    /// </summary>
    public ICommand? Command { get; }

    /// <summary>
    /// Command parameter of this hotkey.
    /// </summary>
    public object? CommandParameter { get; }

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
    /// <param name="registeredType">
    /// The type of this hotkey in the context of the application.
    /// </param>
    /// <param name="type">
    /// Whether this hotkey is global or search window specific.
    /// </param>
    /// <param name="hotkey">
    /// The hotkey this class will represent.
    /// Example values: <c>F1</c>, <c>Ctrl+Shift+Enter</c>
    /// </param>
    /// <param name="descriptionResourceKey">
    /// The key in the localization dictionary that represents this hotkey. For example, <c>ReloadPluginHotkey</c>,
    /// which represents the string "Reload Plugins Data" in <c>en.xaml</c>
    /// </param>
    /// <param name="command">
    /// The command that will be executed when this hotkey is triggered. If it's <c>null</c>, the hotkey is assumed to be registered by system.
    /// </param>
    /// <param name="parameter">
    /// The command parameter that will be passed to the command when this hotkey is triggered. If it's <c>null</c>, no parameter will be passed.
    /// </param>
    /// <param name="removeHotkey">
    /// An action that, when called, will unregister this hotkey. If it's <c>null</c>, it's assumed that this hotkey
    /// can't be unregistered, and the "Overwrite" option will not appear in the hotkey dialog.
    /// </param>
    public RegisteredHotkeyData(
        RegisteredHotkeyType registeredType, HotkeyType type, string hotkey, string descriptionResourceKey,
        ICommand? command, object? parameter = null, Action? removeHotkey = null)
    {
        RegisteredType = registeredType;
        Type = type;
        Hotkey = new HotkeyModel(hotkey);
        DescriptionResourceKey = descriptionResourceKey;
        Command = command;
        CommandParameter = parameter;
        RemoveHotkey = removeHotkey;
    }

    /// <summary>
    /// Creates an instance of <c>RegisteredHotkeyData</c>. Assumes that the key specified in
    /// <c>descriptionResourceKey</c> doesn't need any arguments for <c>string.Format</c>. If it does,
    /// use one of the other constructors.
    /// </summary>
    /// <param name="registeredType">
    /// The type of this hotkey in the context of the application.
    /// </param>
    /// <param name="type">
    /// Whether this hotkey is global or search window specific.
    /// </param>
    /// <param name="hotkey">
    /// The hotkey this class will represent.
    /// Example values: <c>F1</c>, <c>Ctrl+Shift+Enter</c>
    /// </param>
    /// <param name="descriptionResourceKey">
    /// The key in the localization dictionary that represents this hotkey. For example, <c>ReloadPluginHotkey</c>,
    /// which represents the string "Reload Plugins Data" in <c>en.xaml</c>
    /// </param>
    /// <param name="command">
    /// The command that will be executed when this hotkey is triggered. If it's <c>null</c>, the hotkey is assumed to be registered by system.
    /// </param>
    /// <param name="parameter">
    /// The command parameter that will be passed to the command when this hotkey is triggered. If it's <c>null</c>, no parameter will be passed.
    /// </param>
    /// <param name="removeHotkey">
    /// An action that, when called, will unregister this hotkey. If it's <c>null</c>, it's assumed that this hotkey
    /// can't be unregistered, and the "Overwrite" option will not appear in the hotkey dialog.
    /// </param>
    public RegisteredHotkeyData(
        RegisteredHotkeyType registeredType, HotkeyType type, HotkeyModel hotkey, string descriptionResourceKey,
        ICommand? command, object? parameter = null, Action? removeHotkey = null)
    {
        RegisteredType = registeredType;
        Type = type;
        Hotkey = hotkey;
        DescriptionResourceKey = descriptionResourceKey;
        Command = command;
        CommandParameter = parameter;
        RemoveHotkey = removeHotkey;
    }

    /// <summary>
    /// Creates an instance of <c>RegisteredHotkeyData</c>. Assumes that the key specified in
    /// <c>descriptionResourceKey</c> needs exactly one argument for <c>string.Format</c>.
    /// </summary>
    /// <param name="registeredType">
    /// The type of this hotkey in the context of the application.
    /// </param>
    /// <param name="type">
    /// Whether this hotkey is global or search window specific.
    /// </param>
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
    /// <param name="command">
    /// The command that will be executed when this hotkey is triggered. If it's <c>null</c>, the hotkey is assumed to be registered by system.
    /// </param>
    /// <param name="parameter">
    /// The command parameter that will be passed to the command when this hotkey is triggered. If it's <c>null</c>, no parameter will be passed.
    /// </param>
    /// <param name="removeHotkey">
    /// An action that, when called, will unregister this hotkey. If it's <c>null</c>, it's assumed that this hotkey
    /// can't be unregistered, and the "Overwrite" option will not appear in the hotkey dialog.
    /// </param>
    public RegisteredHotkeyData(
        RegisteredHotkeyType registeredType, HotkeyType type, string hotkey, string descriptionResourceKey, object? descriptionFormatVariable,
        ICommand? command, object? parameter = null, Action? removeHotkey = null
    )
    {
        RegisteredType = registeredType;
        Type = type;
        Hotkey = new HotkeyModel(hotkey);
        DescriptionResourceKey = descriptionResourceKey;
        DescriptionFormatVariables = new[] { descriptionFormatVariable };
        Command = command;
        CommandParameter = parameter;
        RemoveHotkey = removeHotkey;
    }

    /// <summary>
    /// Creates an instance of <c>RegisteredHotkeyData</c>. Assumes that the key specified in
    /// <paramref name="descriptionResourceKey"/> needs multiple arguments for <c>string.Format</c>.
    /// </summary>
    /// <param name="registeredType">
    /// The type of this hotkey in the context of the application.
    /// </param>
    /// <param name="type">
    /// Whether this hotkey is global or search window specific.
    /// </param>
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
    /// <param name="command">
    /// The command that will be executed when this hotkey is triggered. If it's <c>null</c>, the hotkey is assumed to be registered by system.
    /// </param>
    /// <param name="parameter">
    /// The command parameter that will be passed to the command when this hotkey is triggered. If it's <c>null</c>, no parameter will be passed.
    /// </param>
    /// <param name="removeHotkey">
    /// An action that, when called, will unregister this hotkey. If it's <c>null</c>, it's assumed that this hotkey
    /// can't be unregistered, and the "Overwrite" option will not appear in the hotkey dialog.
    /// </param>
    public RegisteredHotkeyData(
        RegisteredHotkeyType registeredType, HotkeyType type, string hotkey, string descriptionResourceKey, object?[] descriptionFormatVariables,
        ICommand? command, object? parameter = null, Action? removeHotkey = null
    )
    {
        RegisteredType = registeredType;
        Type = type;
        Hotkey = new HotkeyModel(hotkey);
        DescriptionResourceKey = descriptionResourceKey;
        DescriptionFormatVariables = descriptionFormatVariables;
        Command = command;
        CommandParameter = parameter;
        RemoveHotkey = removeHotkey;
    }

    /// <summary>
    /// Sets the hotkey for this registered hotkey data.
    /// </summary>
    /// <param name="hotkey"></param>
    public void SetHotkey(HotkeyModel hotkey)
    {
        Hotkey = hotkey;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Hotkey.IsEmpty ? $"{RegisteredType} - None" : $"{RegisteredType} - {Hotkey}";
    }
}

public enum RegisteredHotkeyType
{
    CtrlShiftEnter,
    CtrlEnter,
    AltEnter,

    Up,
    Down,
    Left,
    Right,

    Esc,
    Reload,
    SelectFirstResult,
    SelectLastResult,
    ReQuery,
    IncreaseWidth,
    DecreaseWidth,
    IncreaseMaxResult,
    DecreaseMaxResult,
    ShiftEnter,
    Enter,
    ToggleGameMode,
    CopyFilePath,
    OpenResultN1,
    OpenResultN2,
    OpenResultN3,
    OpenResultN4,
    OpenResultN5,
    OpenResultN6,
    OpenResultN7,
    OpenResultN8,
    OpenResultN9,
    OpenResultN10,

    Toggle,
    DialogJump,

    Preview,
    AutoComplete,
    AutoComplete2,
    SelectNextItem,
    SelectNextItem2,
    SelectPrevItem,
    SelectPrevItem2,
    SettingWindow,
    OpenHistory,
    OpenContextMenu,
    SelectNextPage,
    SelectPrevPage,
    CycleHistoryUp,
    CycleHistoryDown,

    CustomQuery,

    PluginGlobalHotkey,

    PluginWindowHotkey,
}
