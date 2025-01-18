using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;

namespace Flow.Launcher.Converters;

internal class BoolToIMEConversionModeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            true => ImeConversionModeValues.Alphanumeric,
            _ => ImeConversionModeValues.DoNotCare
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

internal class BoolToIMEStateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            true => InputMethodState.Off,
            _ => InputMethodState.DoNotCare
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
