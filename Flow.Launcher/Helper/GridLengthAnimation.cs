using System;
using System.Windows;
using System.Windows.Media.Animation;


namespace Flow.Launcher.Helper
{
    internal class GridLengthAnimation : AnimationTimeline
    {
        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register(nameof(From),
                typeof(GridLength),
                typeof(GridLengthAnimation));

        public GridLength From
        {
            get => (GridLength)GetValue(FromProperty);
            set => SetValue(FromProperty, value);
        }

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register(nameof(To),
                typeof(GridLength),
                typeof(GridLengthAnimation));

        public GridLength To
        {
            get => (GridLength)GetValue(ToProperty);
            set => SetValue(ToProperty, value);
        }

        public static readonly DependencyProperty EasingFunctionProperty =
            DependencyProperty.Register(nameof(EasingFunction),
                typeof(IEasingFunction),
                typeof(GridLengthAnimation));

        public IEasingFunction EasingFunction
        {
            get => (IEasingFunction)GetValue(EasingFunctionProperty);
            set => SetValue(EasingFunctionProperty, value);
        }

        public override Type TargetPropertyType => typeof(GridLength);

        protected override Freezable CreateInstanceCore()
        {
            return new GridLengthAnimation();
        }

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
        {
            double fromVal = ((GridLength)GetValue(FromProperty)).Value;
            double toVal = ((GridLength)GetValue(ToProperty)).Value;
            double progress = animationClock.CurrentProgress.Value;

            if (EasingFunction != null)
            {
                progress = EasingFunction.Ease(progress);
            }

            if (fromVal > toVal)
            {
                return new GridLength((1 - progress) * (fromVal - toVal) + toVal, GridUnitType.Pixel);
            }
            else
            {
                return new GridLength(progress * (toVal - fromVal) + fromVal, GridUnitType.Pixel);
            }
        }
    }
}
