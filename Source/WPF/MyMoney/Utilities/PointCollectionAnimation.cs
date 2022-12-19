using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Walkabout.Utilities
{
    public abstract class PointCollectionAnimationBase : AnimationTimeline
    {
        public override Type TargetPropertyType => typeof(PointCollection);

        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
        {
            return GetCurrentValueCore((PointCollection)defaultOriginValue, (PointCollection)defaultDestinationValue, animationClock);
        }

        protected abstract PointCollection GetCurrentValueCore(
            PointCollection defaultOriginValue,
            PointCollection defaultDestinationValue,
            AnimationClock animationClock);
    }

    public class PointCollectionAnimation : PointCollectionAnimationBase
    {
        protected override Freezable CreateInstanceCore()
        {
            return new PointCollectionAnimation();
        }

        public PointCollection From
        {
            get { return (PointCollection)GetValue(FromProperty); }
            set { SetValue(FromProperty, value); }
        }

        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register("From", typeof(PointCollection), typeof(PointCollectionAnimation), new PropertyMetadata(null, OnFromChanged));

        private static void OnFromChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PointCollectionAnimation)d).OnPointsChanged();
        }

        private void OnPointsChanged()
        {
            // Set ptsFrom from From 
            PointCollection ptsFrom = From ?? new PointCollection();

            // Set ptsTo from To, By, or a default value.
            ptsTo.Clear();

            if (To != null)
            {
                foreach (Point pt in To)
                    ptsTo.Add(pt);
            }
            else if (By != null)
            {
                var count = Math.Min(ptsFrom.Count, By.Count);
                for (int i = 0; i < By.Count; i++)
                {
                    double fromX = (i < ptsFrom.Count) ? ptsFrom[i].X : 0;
                    double fromY = (i < ptsFrom.Count) ? ptsFrom[i].Y : 0;
                    ptsTo.Add(new Point(fromX + By[i].X, fromY + By[i].Y));
                }
            }
        }

        public PointCollection To
        {
            get { return (PointCollection)GetValue(ToProperty); }
            set { SetValue(ToProperty, value); }
        }

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register("To", typeof(PointCollection), typeof(PointCollectionAnimation), new PropertyMetadata(null, OnToChanged));

        private static void OnToChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PointCollectionAnimation)d).OnPointsChanged();
        }

        public PointCollection By
        {
            get { return (PointCollection)GetValue(ByProperty); }
            set { SetValue(ByProperty, value); }
        }

        public static readonly DependencyProperty ByProperty =
            DependencyProperty.Register("By", typeof(PointCollection), typeof(PointCollectionAnimation), new PropertyMetadata(null, OnByChanged));

        private static void OnByChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PointCollectionAnimation)d).OnPointsChanged();
        }

        public bool IsAdditive
        {
            get { return (bool)GetValue(IsAdditiveProperty); }
        }

        public bool IsCumulative
        {
            get { return (bool)GetValue(IsCumulativeProperty); }
        }

        PointCollection ptsDst1 = new PointCollection();
        PointCollection ptsDst2 = new PointCollection();
        PointCollection ptsTo = new PointCollection();
        bool flip;


        protected override PointCollection GetCurrentValueCore(
            PointCollection defaultOriginValue,
            PointCollection defaultDestinationValue,
            AnimationClock animationClock)
        {
            // Let’s hope this doesn’t happen too often
            if (animationClock.CurrentProgress == null) return null;

            double progress = animationClock.CurrentProgress.Value;
            int count;

            // Set ptsFrom from From or defaultOriginValue
            PointCollection ptsFrom = From ?? defaultOriginValue;

            if (To == null && By == null)
            {
                ptsTo.Clear();
                foreach (Point pt in defaultDestinationValue) ptsTo.Add(pt);
            }

            // Choose which destination collection to use
            PointCollection ptsDst = flip ? ptsDst1 : ptsDst2;
            flip = !flip;
            ptsDst.Clear();

            // Interpolate the points, but in a left to right sweeping motion
            // where column growth happens in 1/10th of the allocated duration (0.1 on our 0-1 clock scale).
            double end = (double)ptsTo.Count;

            for (int i = 0; i < ptsTo.Count; i++)
            {
                double fromX = (i < ptsFrom.Count) ? ptsFrom[i].X : 0;
                double fromY = (i < ptsFrom.Count) ? ptsFrom[i].Y : 0;
                ptsDst.Add(new Point((1 - progress) * fromX + progress * ptsTo[i].X,
                                     (1 - progress) * fromY + progress * ptsTo[i].Y));
            }

            // If IsAdditive, add the base values to ptsDst
            if (IsAdditive && From != null && (To != null || By != null))
            {
                count = Math.Min(ptsDst.Count, defaultOriginValue.Count);

                for (int i = 0; i < count; i++)
                {
                    Point pt = ptsDst[i];
                    pt.X += defaultOriginValue[i].X;
                    pt.Y += defaultOriginValue[i].Y;
                    ptsDst[i] = pt;
                }
            }

            // Take account of IsCumulative
            if (IsCumulative && animationClock.CurrentIteration != null)
            {
                int iter = animationClock.CurrentIteration.Value;

                for (int i = 0; i < ptsDst.Count; i++)
                {
                    Point pt = ptsDst[i];
                    pt.X += (iter - 1) * (ptsTo[i].X - ptsFrom[i].X);
                    pt.Y += (iter - 1) * (ptsTo[i].Y - ptsFrom[i].Y);
                    ptsDst[i] = pt;
                }
            }

            // Return the PointCollection
            return ptsDst;
        }
    }
}
