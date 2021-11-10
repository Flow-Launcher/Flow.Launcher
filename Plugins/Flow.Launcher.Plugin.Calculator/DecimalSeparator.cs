﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flow.Launcher.Core;

namespace Flow.Launcher.Plugin.Calculator
{    
    [TypeConverter(typeof(LocalizationConverter))]
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