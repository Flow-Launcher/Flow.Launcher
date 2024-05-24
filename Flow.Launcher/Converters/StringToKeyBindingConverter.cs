using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;

namespace Flow.Launcher.Converters;

class StringToKeyBindingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is not string mode || value is not string hotkeyStr)
            return null;

        var converter = new KeyGestureConverter();
        var key = (KeyGesture)converter.ConvertFromString(hotkeyStr);
        return mode switch
        {
            "key" => key?.Key,
            "modifiers" => key?.Modifiers,
            _ => null
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
