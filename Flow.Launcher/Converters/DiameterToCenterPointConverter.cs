using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Flow.Launcher.Converters;

public class DiameterToCenterPointConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return new Point(d / 2, d / 2);
        }

        return new Point(0, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
