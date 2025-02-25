using System;
using System.Globalization;
using System.Windows.Data;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Resource
{
    public class TranslationConverter : IValueConverter
    {
        private static readonly IPublicAPI _api = Ioc.Default.GetRequiredService<IPublicAPI>();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = value.ToString();
            if (string.IsNullOrEmpty(key))
                return key;
            return _api.GetTranslation(key);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new InvalidOperationException();
    }
}
