using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Flow.Launcher.Helper
{
    internal static class ScrollViewerHelperEx
    {
        #region IsSmoothScrollingEnabled

        public static readonly DependencyProperty IsSmoothScrollingEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsSmoothScrollingEnabled",
                typeof(bool),
                typeof(ScrollViewerHelperEx),
                new PropertyMetadata(false, OnIsSmoothScrollingEnabledChanged));

        public static bool GetIsSmoothScrollingEnabled(ScrollViewer scrollViewer)
        {
            return (bool)scrollViewer.GetValue(IsSmoothScrollingEnabledProperty);
        }

        public static void SetIsSmoothScrollingEnabled(ScrollViewer scrollViewer, bool value)
        {
            scrollViewer.SetValue(IsSmoothScrollingEnabledProperty, value);
        }

        private static void OnIsSmoothScrollingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var scrollViewer = (ScrollViewer)d;
            var newValue = (bool)e.NewValue;

            if (newValue)
            {
                scrollViewer.PreviewMouseWheel += OnMouseWheel;
                SetCurrentVerticalOffset(scrollViewer, scrollViewer.VerticalOffset);
            }
            else
            {
                scrollViewer.PreviewMouseWheel -= OnMouseWheel;
            }
        }

        #endregion

        #region IsAnimating

        private static readonly DependencyProperty IsAnimatingProperty =
            DependencyProperty.RegisterAttached(
                "IsAnimating",
                typeof(bool),
                typeof(ScrollViewerHelperEx),
                new PropertyMetadata(false));

        private static bool GetIsAnimating(ScrollViewer scrollViewer)
        {
            return (bool)scrollViewer.GetValue(IsAnimatingProperty);
        }

        private static void SetIsAnimating(ScrollViewer scrollViewer, bool value)
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

        private static void OnMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            e.Handled = true;

            if (!GetIsAnimating(scrollViewer))
            {
                SetCurrentVerticalOffset(scrollViewer, scrollViewer.VerticalOffset);
            }

            double _totalVerticalOffset = Math.Min(Math.Max(0, scrollViewer.VerticalOffset - e.Delta), scrollViewer.ScrollableHeight);
            ScrollToVerticalOffsetInternal(scrollViewer, _totalVerticalOffset);
        }

        private static void ScrollToVerticalOffsetInternal(ScrollViewer scrollViewer, double offset)
        {
            var animation = new DoubleAnimation(offset, TimeSpan.FromMilliseconds(500));
            animation.EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            };
            animation.FillBehavior = FillBehavior.Stop;
            animation.Completed += (s, e1) =>
            {
                SetCurrentVerticalOffset(scrollViewer, offset);
                SetIsAnimating(scrollViewer, false);
            };
            SetIsAnimating(scrollViewer, true);

            scrollViewer.BeginAnimation(CurrentVerticalOffsetProperty, animation, HandoffBehavior.Compose);
        }
    }
}
