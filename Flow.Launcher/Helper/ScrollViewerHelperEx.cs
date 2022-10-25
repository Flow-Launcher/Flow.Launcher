using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Input;

namespace Flow.Launcher.Helper
{
    internal static class ScrollViewerHelperEx
    {
        #region IsAnimating

        internal static readonly DependencyProperty IsAnimatingProperty =
            DependencyProperty.RegisterAttached(
                "IsAnimating",
                typeof(bool),
                typeof(ScrollViewerHelperEx),
                new PropertyMetadata(false));

        internal static bool GetIsAnimating(ScrollViewer scrollViewer)
        {
            return (bool)scrollViewer.GetValue(IsAnimatingProperty);
        }

        internal static void SetIsAnimating(ScrollViewer scrollViewer, bool value)
        {
            scrollViewer.SetValue(IsAnimatingProperty, value);
        }

        #endregion

        #region CurrentVerticalOffset

        internal static readonly DependencyProperty CurrentVerticalOffsetProperty =
            DependencyProperty.RegisterAttached("CurrentVerticalOffset",
                typeof(double),
                typeof(ScrollViewerHelperEx),
                new PropertyMetadata(0.0, OnCurrentVerticalOffsetChanged));

        private static double GetCurrentVerticalOffset(ScrollViewer scrollViewer)
        {
            return (double)scrollViewer.GetValue(CurrentVerticalOffsetProperty);
        }

        private static void SetCurrentVerticalOffset(ScrollViewer scrollViewer, double value)
        {
            scrollViewer.SetValue(CurrentVerticalOffsetProperty, value);
        }

        private static void OnCurrentVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer ctl && e.NewValue is double v)
            {
                ctl.ScrollToVerticalOffset(v);
            }
        }

        #endregion

        #region CurrentHorizontalOffset

        internal static readonly DependencyProperty CurrentHorizontalOffsetProperty =
            DependencyProperty.RegisterAttached("CurrentHorizontalOffset",
                typeof(double),
                typeof(ScrollViewerHelperEx),
                new PropertyMetadata(0.0, OnCurrentHorizontalOffsetChanged));

        private static double GetCurrentHorizontalOffset(ScrollViewer scrollViewer)
        {
            return (double)scrollViewer.GetValue(CurrentHorizontalOffsetProperty);
        }

        private static void SetCurrentHorizontalOffset(ScrollViewer scrollViewer, double value)
        {
            scrollViewer.SetValue(CurrentHorizontalOffsetProperty, value);
        }

        private static void OnCurrentHorizontalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer ctl && e.NewValue is double v)
            {
                ctl.ScrollToHorizontalOffset(v);
            }
        }

        #endregion

        internal static void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;

            bool isHorizontal = Keyboard.Modifiers == ModifierKeys.Shift;

            if (!isHorizontal)
            {
                if (!GetIsAnimating(scrollViewer))
                {
                    SetCurrentVerticalOffset(scrollViewer, scrollViewer.VerticalOffset);
                }

                double _totalVerticalOffset = Math.Min(Math.Max(0, scrollViewer.VerticalOffset - e.Delta), scrollViewer.ScrollableHeight);
                ScrollToVerticalOffset(scrollViewer, _totalVerticalOffset);
            }
            else
            {
                if (!GetIsAnimating(scrollViewer))
                {
                    SetCurrentHorizontalOffset(scrollViewer, scrollViewer.HorizontalOffset);
                }

                double _totalHorizontalOffset = Math.Min(Math.Max(0, scrollViewer.HorizontalOffset - e.Delta), scrollViewer.ScrollableWidth);
                ScrollToHorizontalOffset(scrollViewer, _totalHorizontalOffset);
            }
        }

        public static void ScrollToOffset(ScrollViewer scrollViewer, Orientation orientation, double offset, double duration = 500, IEasingFunction easingFunction = null)
        {
            var animation = new DoubleAnimation(offset, TimeSpan.FromMilliseconds(duration));
            easingFunction ??= new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            };
            animation.EasingFunction = easingFunction;
            animation.FillBehavior = FillBehavior.Stop;
            animation.Completed += (s, e1) =>
            {
                if (orientation == Orientation.Vertical)
                {
                    SetCurrentVerticalOffset(scrollViewer, offset);
                }
                else
                {
                    SetCurrentHorizontalOffset(scrollViewer, offset);
                }
                SetIsAnimating(scrollViewer, false);
            };
            SetIsAnimating(scrollViewer, true);

            scrollViewer.BeginAnimation(orientation == Orientation.Vertical ? CurrentVerticalOffsetProperty : CurrentHorizontalOffsetProperty, animation, HandoffBehavior.Compose);
        }

        public static void ScrollToVerticalOffset(ScrollViewer scrollViewer, double offset, double duration = 500, IEasingFunction easingFunction = null)
        {
            ScrollToOffset(scrollViewer, Orientation.Vertical, offset, duration, easingFunction);
        }

        public static void ScrollToHorizontalOffset(ScrollViewer scrollViewer, double offset, double duration = 500, IEasingFunction easingFunction = null)
        {
            ScrollToOffset(scrollViewer, Orientation.Horizontal, offset, duration, easingFunction);
        }
    }
}
