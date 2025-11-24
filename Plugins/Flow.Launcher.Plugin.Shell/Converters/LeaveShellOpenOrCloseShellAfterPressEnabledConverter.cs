using System;
using System.Globalization;
using System.Windows.Data;

namespace Flow.Launcher.Plugin.Shell.Converters;

public class LeaveShellOpenOrCloseShellAfterPressEnabledConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (
            values.Length != 2 ||
            values[0] is not bool closeShellAfterPressOrLeaveShellOpen ||
            values[1] is not Shell shell
        )
            return Binding.DoNothing;

        return (!closeShellAfterPressOrLeaveShellOpen) && shell != Shell.RunCommand;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
