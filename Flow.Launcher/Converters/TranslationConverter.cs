using System;
using System.Globalization;
using System.Windows.Data;
using Flow.Launcher.Core.Resource;

namespace Flow.Launcher.Converters
{
    public class TranlationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = value.ToString();
            if (String.IsNullOrEmpty(key))
                return key;
            return InternationalizationManager.Instance.GetTranslation(key);
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture) => throw new System.InvalidOperationException();
    }
}
