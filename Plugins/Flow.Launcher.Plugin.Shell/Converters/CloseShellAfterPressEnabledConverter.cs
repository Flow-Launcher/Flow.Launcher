using System;
using System.Globalization;
using System.Windows.Data;

namespace Flow.Launcher.Plugin.Shell.Converters;

public class CloseShellAfterPressEnabledConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool)
            return Binding.DoNothing;

        var leaveShellOpen = (bool)value;
        return !leaveShellOpen;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
