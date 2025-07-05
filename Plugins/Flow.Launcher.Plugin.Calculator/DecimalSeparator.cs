using Flow.Launcher.Localization.Attributes;

namespace Flow.Launcher.Plugin.Calculator
{
    [EnumLocalize]
    public enum DecimalSeparator
    {
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_calculator_decimal_seperator_use_system_locale))]
        UseSystemLocale,
        
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_calculator_decimal_seperator_dot))]
        Dot, 
        
        [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_calculator_decimal_seperator_comma))]
        Comma
    }
}
