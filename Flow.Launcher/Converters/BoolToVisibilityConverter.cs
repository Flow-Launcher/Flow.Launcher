using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Flow.Launcher.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null)
            {
                if (value is true)
                {
                    return Visibility.Collapsed;
                }

                else
                {
                    return Visibility.Visible;
                }
            }
            else { 
                if (value is true)
                {
                    return Visibility.Visible;
                }

                else { 
                    return Visibility.Collapsed;
                }
            }
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture) => throw new System.InvalidOperationException();
    }

    public class SplitterConverter : IValueConverter
    /* Prevents the dragging part of the preview area from working when preview is turned off. */
    {
        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null)
            {
                if (value is true)
                {
                    return 0;
                }

                else
                {
                    return 5;
                }
            }
            else
            {
                if (value is true)
                {
                    return 5;
                }

                else
                {
                    return 0;
                }
            }
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture) => throw new System.InvalidOperationException();
    }
}
