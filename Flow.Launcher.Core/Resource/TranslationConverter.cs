using System;
using System.Globalization;
using System.Windows.Data;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Resource
{
    public class TranslationConverter : IValueConverter
    {
        // We should not initialize API in static constructor because it will create another API instance
        private static IPublicAPI api = null;
        private static IPublicAPI API => api ??= Ioc.Default.GetRequiredService<IPublicAPI>();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = value.ToString();
            if (string.IsNullOrEmpty(key)) return key;
            return API.GetTranslation(key);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new InvalidOperationException();
    }
}
