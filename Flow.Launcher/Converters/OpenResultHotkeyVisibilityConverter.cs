using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Flow.Launcher.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class OpenResultHotkeyVisibilityConverter : IValueConverter
{
    private const int MaxVisibleHotkeys = 10;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var number = int.MaxValue;

        if (value is ListBoxItem listBoxItem
            && ItemsControl.ItemsControlFromItemContainer(listBoxItem) is ListBox listBox)
            number = listBox.ItemContainerGenerator.IndexFromContainer(listBoxItem) + 1;

        return number <= MaxVisibleHotkeys ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new InvalidOperationException();
}
