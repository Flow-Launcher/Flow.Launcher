using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace Flow.Launcher.Converters;

public class OrdinalConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ListBoxItem listBoxItem
            || ItemsControl.ItemsControlFromItemContainer(listBoxItem) is not ListBox listBox)
        {
            return 0;
        }

        var res = listBox.ItemContainerGenerator.IndexFromContainer(listBoxItem) + 1;
        return res == 10 ? 0 : res;  // 10th item => HOTKEY+0

    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new InvalidOperationException();
}
