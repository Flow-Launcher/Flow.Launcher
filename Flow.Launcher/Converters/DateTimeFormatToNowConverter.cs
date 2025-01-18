using System;
using System.Globalization;
using System.Windows.Data;

namespace Flow.Launcher.Converters;

public class DateTimeFormatToNowConverter : IValueConverter
{

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is not string format ? null : DateTime.Now.ToString(format);
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
