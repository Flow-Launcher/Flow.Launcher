using System;
using System.Globalization;
using System.Windows.Data;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Converters
{
    public class TextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var id = value?.ToString();
            return id switch
            {
                PluginStoreItemViewModel.NewRelease => InternationalizationManager.Instance.GetTranslation("pluginStore_NewRelease"),
                PluginStoreItemViewModel.RecentlyUpdated => InternationalizationManager.Instance.GetTranslation("pluginStore_RecentlyUpdated"),
                PluginStoreItemViewModel.None => InternationalizationManager.Instance.GetTranslation("pluginStore_None"),
                PluginStoreItemViewModel.Installed => InternationalizationManager.Instance.GetTranslation("pluginStore_Installed"),
                _ => id
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new InvalidOperationException();
    }
}
