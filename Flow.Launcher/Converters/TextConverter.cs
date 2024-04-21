using System;
using System.Globalization;
using System.Windows.Data;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.ViewModel;

namespace Flow.Launcher.Converters;

public class TextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var id = value?.ToString();
        var translationKey = id switch
        {
            PluginStoreItemViewModel.NewRelease => "pluginStore_NewRelease",
            PluginStoreItemViewModel.RecentlyUpdated => "pluginStore_RecentlyUpdated",
            PluginStoreItemViewModel.None => "pluginStore_None",
            PluginStoreItemViewModel.Installed => "pluginStore_Installed",
            _ => null
        };

        if (translationKey is null)
            return id;

        return InternationalizationManager.Instance.GetTranslation(translationKey);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new InvalidOperationException();
}
