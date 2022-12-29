using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;

namespace Flow.Launcher.Converters
{
    class StringToKeyBindingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var mode = parameter as string;
            var hotkeyStr = value as string;
            var converter = new KeyGestureConverter();
            var key = (KeyGesture)converter.ConvertFromString(hotkeyStr);
            if (mode == "key")
            {
                return key.Key;
            }
            else if (mode == "modifiers")
            {
                return key.Modifiers;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
