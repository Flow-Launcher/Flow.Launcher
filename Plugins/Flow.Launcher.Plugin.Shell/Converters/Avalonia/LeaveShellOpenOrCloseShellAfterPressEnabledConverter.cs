using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Flow.Launcher.Plugin.Shell.Converters.Avalonia
{
    public class LeaveShellOpenOrCloseShellAfterPressEnabledConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Count != 2)
                return false;

            if (values[0] is not bool closeShellAfterPressOrLeaveShellOpen ||
                values[1] is not Shell shell)
                return false;

            return (!closeShellAfterPressOrLeaveShellOpen) && shell != Shell.RunCommand;
        }
    }
}
