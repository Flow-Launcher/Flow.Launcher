using Flow.Launcher.Localization.Attributes;

namespace Flow.Launcher.Plugin.Calculator
{
    /// <summary>
    /// Represents the different modes for saving calculator calculations into the history.
    /// </summary>
    [EnumLocalize]
    public enum HistoryCreationMode
    {
        /// <summary>
        /// Saves calculations into the history automatically as the query is typed.
        /// </summary>
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_calculator_history_creation_mode_on_query))]
        OnQuery,

        /// <summary>
        /// Saves calculations into the history only when the action is executed (e.g. Enter is pressed).
        /// </summary>
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_calculator_history_creation_mode_on_enter))]
        OnEnter
    }
}
