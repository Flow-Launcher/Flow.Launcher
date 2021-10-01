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

    public class PartiallyRoundedRectangle : Shape
    {
        public static readonly DependencyProperty RadiusXProperty;
        public static readonly DependencyProperty RadiusYProperty;

        public static readonly DependencyProperty RoundTopLeftProperty;
        public static readonly DependencyProperty RoundTopRightProperty;
        public static readonly DependencyProperty RoundBottomLeftProperty;
        public static readonly DependencyProperty RoundBottomRightProperty;

        public int RadiusX
        {
            get { return (int)GetValue(RadiusXProperty); }
            set { SetValue(RadiusXProperty, value); }
        }

        public int RadiusY
        {
            get { return (int)GetValue(RadiusYProperty); }
            set { SetValue(RadiusYProperty, value); }
        }

        public bool RoundTopLeft
        {
            get { return (bool)GetValue(RoundTopLeftProperty); }
            set { SetValue(RoundTopLeftProperty, value); }
        }

        public bool RoundTopRight
        {
            get { return (bool)GetValue(RoundTopRightProperty); }
            set { SetValue(RoundTopRightProperty, value); }
        }

        public bool RoundBottomLeft
        {
            get { return (bool)GetValue(RoundBottomLeftProperty); }
            set { SetValue(RoundBottomLeftProperty, value); }
        }

        public bool RoundBottomRight
        {
            get { return (bool)GetValue(RoundBottomRightProperty); }
            set { SetValue(RoundBottomRightProperty, value); }
        }

        static PartiallyRoundedRectangle()
        {
            RadiusXProperty = DependencyProperty.Register
                ("RadiusX", typeof(int), typeof(PartiallyRoundedRectangle));
            RadiusYProperty = DependencyProperty.Register
                ("RadiusY", typeof(int), typeof(PartiallyRoundedRectangle));

            RoundTopLeftProperty = DependencyProperty.Register
                ("RoundTopLeft", typeof(bool), typeof(PartiallyRoundedRectangle));
            RoundTopRightProperty = DependencyProperty.Register
                ("RoundTopRight", typeof(bool), typeof(PartiallyRoundedRectangle));
            RoundBottomLeftProperty = DependencyProperty.Register
                ("RoundBottomLeft", typeof(bool), typeof(PartiallyRoundedRectangle));
            RoundBottomRightProperty = DependencyProperty.Register
                ("RoundBottomRight", typeof(bool), typeof(PartiallyRoundedRectangle));
        }

        public PartiallyRoundedRectangle()
        {
        }

        protected override Geometry DefiningGeometry
        {
            get
            {
                Geometry result = new RectangleGeometry
                (new Rect(0, 0, base.Width, base.Height), RadiusX, RadiusY);
                double halfWidth = base.Width / 2;
                double halfHeight = base.Height / 2;

                if (!RoundTopLeft)
                    result = new CombinedGeometry
                (GeometryCombineMode.Union, result, new RectangleGeometry
                (new Rect(0, 0, halfWidth, halfHeight)));
                if (!RoundTopRight)
                    result = new CombinedGeometry
                (GeometryCombineMode.Union, result, new RectangleGeometry
                (new Rect(halfWidth, 0, halfWidth, halfHeight)));
                if (!RoundBottomLeft)
                    result = new CombinedGeometry
                (GeometryCombineMode.Union, result, new RectangleGeometry
                (new Rect(0, halfHeight, halfWidth, halfHeight)));
                if (!RoundBottomRight)
                    result = new CombinedGeometry
                (GeometryCombineMode.Union, result, new RectangleGeometry
                (new Rect(halfWidth, halfHeight, halfWidth, halfHeight)));

                return result;
            }
        }
    }
}
