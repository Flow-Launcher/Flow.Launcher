using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Flow.Launcher.Resources.MarkupExtensions;

#nullable enable

public class HideableVisibilityConverter : IMultiValueConverter, IValueConverter {
    public Visibility DefaultVisibility { get; init; } = Visibility.Visible;
    public Visibility InvertedVisibility { get; init; } = Visibility.Collapsed;

    public object? IsEqualTo { get; set; }

    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) {
        if (values is not { Length: 2 })
            return DependencyProperty.UnsetValue;

        var value1 = values[0];
        var value2 = values[1];
        if (value1 is Enum enum1 && value2 is Enum enum2)
        {
            value1 = System.Convert.ToInt32(enum1);
            value2 = System.Convert.ToInt32(enum2);
        }

        return Equals(value1, value2) ? InvertedVisibility : DefaultVisibility;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Equals(value, IsEqualTo) ? InvertedVisibility : DefaultVisibility;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) {
        throw new NotSupportedException();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
