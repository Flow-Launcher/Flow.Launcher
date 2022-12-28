using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace Flow.Launcher.Core.Resource
{
    public class LocalizationAttributeConverter : IValueConverter, IMultiValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType == typeof(string) && value != null)
            {
                FieldInfo fi = value.GetType().GetField(value.ToString());
                if (fi != null)
                {
                    string localizedDescription = string.Empty;
                    var attributes = (LocalizedDescriptionAttribute[])fi.GetCustomAttributes(typeof(LocalizedDescriptionAttribute), false);
                    if ((attributes.Length > 0) && (!String.IsNullOrEmpty(attributes[0].Description)))
                    {
                        localizedDescription = attributes[0].Description;
                    }

                    return (!String.IsNullOrEmpty(localizedDescription)) ? localizedDescription : value.ToString();
                }
            }
            
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType == typeof(string) && values[0] != null)
            {
                var value = values[0];
                FieldInfo fi = value.GetType().GetField(value.ToString());
                if (fi != null)
                {
                    string localizedDescription = string.Empty;
                    var attributes = (LocalizedDescriptionAttribute[])fi.GetCustomAttributes(typeof(LocalizedDescriptionAttribute), false);
                    if ((attributes.Length > 0) && (!String.IsNullOrEmpty(attributes[0].Description)))
                    {
                        localizedDescription = attributes[0].Description;
                    }

                    return (!String.IsNullOrEmpty(localizedDescription)) ? localizedDescription : value.ToString();
                }
            }

            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
