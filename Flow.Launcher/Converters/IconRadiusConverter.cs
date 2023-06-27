using System;
using System.Globalization;
using System.Windows.Data;

namespace Flow.Launcher.Converters
{
    public class IconRadiusConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2)
                throw new ArgumentException("IconRadiusConverter must have 2 parameters");

            return values[1] switch
            {
                true => (double)values[0] / 2,
                false => (double)values[0],
                _ => throw new ArgumentException("The second argument should be boolean", nameof(values))
            };
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
