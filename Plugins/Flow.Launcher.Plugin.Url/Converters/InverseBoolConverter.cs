using System;
using System.Globalization;
using System.Windows.Data;

namespace Flow.Launcher.Plugin.Url.Converters;

[ValueConversion(typeof(bool), typeof(bool))]
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool)
            throw new ArgumentException("value should be boolean", nameof(value));

        return !(bool)value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool)
            throw new ArgumentException("value should be boolean", nameof(value));

        return !(bool)value;
    }
}
