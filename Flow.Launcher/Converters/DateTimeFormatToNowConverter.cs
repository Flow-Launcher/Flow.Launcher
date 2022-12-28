using System;
using System.Globalization;
using System.Windows.Data;

namespace Flow.Launcher.Converters
{
    public class DateTimeFormatToNowConverter : IValueConverter, IMultiValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is not string format ? null : DateTime.Now.ToString(format, culture);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is string format)
            {
                if (values[1] is CultureInfo cultureInfo)
                {
                    return DateTime.Now.ToString(format, cultureInfo);
                }
                else
                {
                    return DateTime.Now.ToString(format, culture);
                }
            }
            else
            {
                return null;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
