using System;
using System.Globalization;
using System.Windows.Data;

namespace Flow.Launcher.Converters;

public class BadgePositionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double actualWidth && parameter is string param)
        {
            double offset = actualWidth / 2 - 8;

            if (param == "1") // X-Offset
            {
                return offset + 2;
            }
            else if (param == "2") // Y-Offset
            {
                return offset + 2;
            }
        }

        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
