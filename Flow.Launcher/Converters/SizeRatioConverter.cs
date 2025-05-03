using System.Windows.Data;
using System;
using System.Globalization;
using System.Windows;

namespace Flow.Launcher.Converters;

public class SizeRatioConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double size && parameter is string ratioString)
        {
            if (double.TryParse(ratioString, NumberStyles.Any, CultureInfo.InvariantCulture, out double ratio))
            {
                return size * ratio;
            }
        }

        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
