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
}
