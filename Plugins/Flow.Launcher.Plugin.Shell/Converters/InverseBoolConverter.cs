using System;
using System.Globalization;
using System.Windows.Data;

namespace Flow.Launcher.Plugin.Shell.Converters;

[ValueConversion(typeof(bool), typeof(bool))]
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Invert(value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Invert(value);

    private static object Invert(object value)
    {
        if (value is not bool boolValue)
            return Binding.DoNothing;

        return !boolValue;
    }
}
