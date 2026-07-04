using Flow.Launcher.Localization.Attributes;

namespace Flow.Launcher.Plugin.Calculator
{
    [EnumLocalize]
    public enum HistoryCreationMode
    {
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_calculator_history_creation_mode_on_query))]
        OnQuery,

        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_calculator_history_creation_mode_on_enter))]
        OnEnter
    }
}
