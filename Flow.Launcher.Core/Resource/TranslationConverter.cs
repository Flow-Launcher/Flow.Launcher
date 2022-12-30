using System;
using System.Globalization;
using System.Windows.Data;

namespace Flow.Launcher.Core.Resource
{
    public class TranslationConverter : IValueConverter
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
