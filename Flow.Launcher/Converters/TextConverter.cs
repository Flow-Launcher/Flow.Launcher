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
            var ID = value.ToString();
            switch(ID)
            {
                case PluginStoreItemViewModel.NewRelease:
                    return InternationalizationManager.Instance.GetTranslation("pluginStore_NewRelease");
                case PluginStoreItemViewModel.RecentlyUpdated:
                    return InternationalizationManager.Instance.GetTranslation("pluginStore_RecentlyUpdated");
                case PluginStoreItemViewModel.None:
                    return InternationalizationManager.Instance.GetTranslation("pluginStore_None");
                case PluginStoreItemViewModel.Installed:
                    return InternationalizationManager.Instance.GetTranslation("pluginStore_Installed");
                default:
                    return ID;
            }
            
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture) => throw new System.InvalidOperationException();
    }
}
