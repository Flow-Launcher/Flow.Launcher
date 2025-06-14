using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Flow.Launcher.Converters;

public class PlacementRectangleConverter : IMultiValueConverter
{
    public Thickness Margin { get; set; }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 &&
            values[0] is double width &&
            values[1] is double height)
        {
            var margin = Margin;
            var topLeft = new Point(margin.Left, margin.Top);
            var bottomRight = new Point(width - margin.Right, height - margin.Bottom);
            var rect = new Rect(topLeft, bottomRight);
            return rect;
        }

        return Rect.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
