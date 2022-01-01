using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Walkabout.Utilities
{

    public enum EdgeBehavior
    {
        EaseIn, EaseOut, EaseInOut
    }

    public class ExponentialDoubleAnimation : DoubleAnimation
    {

        public static readonly DependencyProperty EdgeBehaviorProperty =
            DependencyProperty.Register("EdgeBehavior", typeof(EdgeBehavior), typeof(ExponentialDoubleAnimation), new PropertyMetadata(EdgeBehavior.EaseIn));

        public static readonly DependencyProperty PowerProperty =
            DependencyProperty.Register("Power", typeof(double), typeof(ExponentialDoubleAnimation), new PropertyMetadata(2.0));

        public ExponentialDoubleAnimation()
        {
        }

        public ExponentialDoubleAnimation(double from, double to, double power, EdgeBehavior behavior, Duration duration)
        {
            this.EdgeBehavior = behavior;
            this.Duration = duration;
            this.Power = power;
            this.From = from;
            this.To = to;
        }

        /// <summary>
        /// which side gets the effect
        /// </summary>
        public EdgeBehavior EdgeBehavior
        {
            get
            {
                return (EdgeBehavior)GetValue(EdgeBehaviorProperty);
            }
            set
            {
                SetValue(EdgeBehaviorProperty, value);
            }
        }

        /// <summary>
        /// exponential rate of growth
        /// </summary>
        public double Power
        {
            get
            {
                return (double)GetValue(PowerProperty);
            }
            set
            {
                if (value > 0.0)
                {
                    SetValue(PowerProperty, value);
                }
                else
                {
                    throw new ArgumentException("cannot set power to less than 0.0. Value: " + value);
                }
            }
        }

        protected override double GetCurrentValueCore(double defaultOriginValue, double defaultDestinationValue, AnimationClock clock)
        {
            double returnValue;
            double start = (double)From;
            double delta = (double)To - start;

            switch (this.EdgeBehavior)
            {
                case EdgeBehavior.EaseIn:
                    returnValue = easeIn(clock.CurrentProgress.Value, start, delta, Power);
                    break;
                case EdgeBehavior.EaseOut:
                    returnValue = easeOut(clock.CurrentProgress.Value, start, delta, Power);
                    break;
                case EdgeBehavior.EaseInOut:
                default:
                    returnValue = easeInOut(clock.CurrentProgress.Value, start, delta, Power);
                    break;
            }
            return returnValue;
        }

        protected override Freezable CreateInstanceCore()
        {
            return new ExponentialDoubleAnimation();
        }

        private static double easeIn(double timeFraction, double start, double delta, double power)
        {
            double returnValue = 0.0;

            // math magic: simple exponential growth
            returnValue = Math.Pow(timeFraction, power);
            returnValue *= delta;
            returnValue = returnValue + start;
            return returnValue;
        }
        private static double easeOut(double timeFraction, double start, double delta, double power)
        {
            double returnValue = 0.0;

            // math magic: simple exponential decay
            returnValue = Math.Pow(timeFraction, 1 / power);
            returnValue *= delta;
            returnValue = returnValue + start;
            return returnValue;
        }
        private static double easeInOut(double timeFraction, double start, double delta, double power)
        {
            double returnValue = 0.0;

            // we cut each effect in half by multiplying the time fraction by two and halving the distance.
            if (timeFraction <= 0.5)
            {
                returnValue = easeOut(timeFraction * 2, start, delta / 2, power);
            }
            else
            {
                returnValue = easeIn((timeFraction - 0.5) * 2, start, delta / 2, power);
                returnValue += (delta / 2);
            }
            return returnValue;
        }
    }
}