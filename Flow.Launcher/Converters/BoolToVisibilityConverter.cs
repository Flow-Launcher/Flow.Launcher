using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Flow.Launcher.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                true when parameter is not null => Visibility.Collapsed,
                _ when parameter is not null => Visibility.Visible,
                
                true => Visibility.Visible,
                _ => Visibility.Collapsed
            };
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture) => throw new System.InvalidOperationException();
    }

    public class SplitterConverter : IValueConverter
    /* Prevents the dragging part of the preview area from working when preview is turned off. */
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                true when parameter is not null => 0,
                _ when parameter is not null => 5,
                
                true => 5,
                _ => 0,
            };
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture) => throw new System.InvalidOperationException();
    }
}
