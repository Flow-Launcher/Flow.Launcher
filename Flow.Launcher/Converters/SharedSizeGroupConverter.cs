using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Flow.Launcher.Converters;

public class SharedSizeGroupConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (Visibility)value != Visibility.Collapsed ? (string)parameter : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
