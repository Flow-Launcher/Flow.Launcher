using System.ComponentModel;
using Flow.Launcher.Core.Resource;

namespace Flow.Launcher.Plugin.Caculator
{    
    [TypeConverter(typeof(LocalizationAttributeConverter))]
    public enum DecimalSeparator
    {
        [LocalizedDescription("flowlauncher_plugin_calculator_decimal_seperator_use_system_locale")]
        UseSystemLocale,
        
        [LocalizedDescription("flowlauncher_plugin_calculator_decimal_seperator_dot")]
        Dot, 
        
        [LocalizedDescription("flowlauncher_plugin_calculator_decimal_seperator_comma")]
        Comma
    }
}
