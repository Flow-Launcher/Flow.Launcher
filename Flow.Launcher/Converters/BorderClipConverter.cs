using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Shapes;

// For Clipping inside listbox item

namespace Flow.Launcher.Converters
{
    public class BorderClipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 3 && values[0] is double && values[1] is double && values[2] is CornerRadius)
            {
                var width = (double)values[0];
                var height = (double)values[1];
                Path myPath = new Path();
                if (width < Double.Epsilon || height < Double.Epsilon)
                {
                    return Geometry.Empty;
                }                
                var radius = (CornerRadius)values[2];
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

            return DependencyProperty.UnsetValue;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

}
