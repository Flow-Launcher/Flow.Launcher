using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Flow.Launcher.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
    {
        return (value, parameter) switch
        {
            (true, not null) => Visibility.Collapsed,
            (_, not null) => Visibility.Visible,

            (true, null) => Visibility.Visible,
            (_, null) => Visibility.Collapsed
        };
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture) => throw new System.InvalidOperationException();
}

public class SplitterConverter : IValueConverter
/* Prevents the dragging part of the preview area from working when preview is turned off. */
{
    public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
    {
        return (value, parameter) switch
        {
            (true, not null) => 0,
            (_, not null) => 5,

            (true, null) => 5,
            (_, null) => 0
        };
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture) => throw new System.InvalidOperationException();
}
