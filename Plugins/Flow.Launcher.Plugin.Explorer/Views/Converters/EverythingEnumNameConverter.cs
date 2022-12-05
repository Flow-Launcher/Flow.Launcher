using Flow.Launcher.Plugin.Everything.Everything;
using Flow.Launcher.Plugin.Explorer.Helper;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Flow.Launcher.Plugin.Explorer.Views.Converters;

public class EnumNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is SortOption option ? option.GetTranslatedName() : value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}