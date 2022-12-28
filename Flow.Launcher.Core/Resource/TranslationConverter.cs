using System;
using System.Globalization;
using System.Windows.Data;

namespace Flow.Launcher.Core.Resource
{
    public class TranslationConverter : IValueConverter, IMultiValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = value.ToString();
            if (string.IsNullOrEmpty(key))
                return key;
            return InternationalizationManager.Instance.GetTranslation(key);
        }

        /// <summary>
        /// Translate with args.
        /// </summary>
        /// <param name="values">key, culture (or something else to trigger onpropertychanged), [arg1, arg2, ...]</param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var key = values[0]?.ToString();
            if (string.IsNullOrEmpty(key))
            {
                return key;
            }
            else
            {
                var translated = InternationalizationManager.Instance.GetTranslation(key);
                if (values.Length > 2)
                {
                    string[] args = new string[values.Length - 2];
                    for (int i = 2; i < values.Length; ++i)
                    {
                        args[i - 2] = values[i]?.ToString();
                    }
                    return string.Format(translated, args);
                }
                else
                {
                    return translated;
                }
            }
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture) => throw new System.InvalidOperationException();

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
