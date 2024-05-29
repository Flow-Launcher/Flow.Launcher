using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

// For Clipping inside listbox item

namespace Flow.Launcher.Converters;

public class BorderClipConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not [double width, double height, CornerRadius radius])
        {
            return DependencyProperty.UnsetValue;
        }

        Path myPath = new Path();
        if (width < Double.Epsilon || height < Double.Epsilon)
        {
            return Geometry.Empty;
        }
        var radiusHeight = radius.TopLeft;

        // Drawing Round box for bottom round, and rect for top area of listbox.
        var corner = new RectangleGeometry(new Rect(0, 0, width, height), radius.TopLeft, radius.TopLeft);
        var box = new RectangleGeometry(new Rect(0, 0, width, radiusHeight), 0, 0);

        GeometryGroup myGeometryGroup = new GeometryGroup();
        myGeometryGroup.Children.Add(corner);
        myGeometryGroup.Children.Add(box);

        CombinedGeometry c1 = new CombinedGeometry(GeometryCombineMode.Union, corner, box);
        myPath.Data = c1;

        myPath.Data.Freeze();
        return myPath.Data;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
