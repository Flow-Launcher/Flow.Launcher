using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Flow.Launcher.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class OpenResultHotkeyVisibilityConverter : IValueConverter
    {
        private const int MaxVisibleHotkeys = 9;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var hotkeyNumber = int.MaxValue;

            if (value is ListBoxItem listBoxItem
                && ItemsControl.ItemsControlFromItemContainer(listBoxItem) is ListBox listBox)
                hotkeyNumber = listBox.ItemContainerGenerator.IndexFromContainer(listBoxItem) + 1;

            return hotkeyNumber <= MaxVisibleHotkeys ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new System.InvalidOperationException();
    }
}
