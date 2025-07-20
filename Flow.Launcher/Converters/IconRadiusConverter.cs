using System;
using System.Globalization;
using System.Windows.Data;

namespace Flow.Launcher.Converters;

public class IconRadiusConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not [double size, bool isIconCircular])
            throw new ArgumentException("IconRadiusConverter must have 2 parameters: [double, bool]");

        return isIconCircular ? size / 2 : size;
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
