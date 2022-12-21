using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;

namespace Flow.Launcher.Converters
{
    internal class BoolToIMEConversionModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool v)
            {
                if (v)
                {
                    return ImeConversionModeValues.Alphanumeric;
                }
                else
                {
                    return ImeConversionModeValues.DoNotCare;
                }
            }
            return ImeConversionModeValues.DoNotCare;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    internal class BoolToIMEStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool v)
            {
                if (v)
                {
                    return InputMethodState.Off;
                }
                else
                {
                    return InputMethodState.DoNotCare;
                }
            }
            return InputMethodState.DoNotCare;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
