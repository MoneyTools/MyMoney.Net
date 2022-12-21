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
            return this.GetCurrentValueCore((PointCollection)defaultOriginValue, (PointCollection)defaultDestinationValue, animationClock);
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
            get { return (PointCollection)this.GetValue(FromProperty); }
            set { this.SetValue(FromProperty, value); }
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
            PointCollection ptsFrom = this.From ?? new PointCollection();

            // Set ptsTo from To, By, or a default value.
            this.ptsTo.Clear();

            if (this.To != null)
            {
                foreach (Point pt in this.To)
                {
                    this.ptsTo.Add(pt);
                }
            }
            else if (this.By != null)
            {
                var count = Math.Min(ptsFrom.Count, this.By.Count);
                for (int i = 0; i < this.By.Count; i++)
                {
                    double fromX = (i < ptsFrom.Count) ? ptsFrom[i].X : 0;
                    double fromY = (i < ptsFrom.Count) ? ptsFrom[i].Y : 0;
                    this.ptsTo.Add(new Point(fromX + this.By[i].X, fromY + this.By[i].Y));
                }
            }
        }

        public PointCollection To
        {
            get { return (PointCollection)this.GetValue(ToProperty); }
            set { this.SetValue(ToProperty, value); }
        }

        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register("To", typeof(PointCollection), typeof(PointCollectionAnimation), new PropertyMetadata(null, OnToChanged));

        private static void OnToChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PointCollectionAnimation)d).OnPointsChanged();
        }

        public PointCollection By
        {
            get { return (PointCollection)this.GetValue(ByProperty); }
            set { this.SetValue(ByProperty, value); }
        }

        public static readonly DependencyProperty ByProperty =
            DependencyProperty.Register("By", typeof(PointCollection), typeof(PointCollectionAnimation), new PropertyMetadata(null, OnByChanged));

        private static void OnByChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PointCollectionAnimation)d).OnPointsChanged();
        }

        public bool IsAdditive
        {
            get { return (bool)this.GetValue(IsAdditiveProperty); }
        }

        public bool IsCumulative
        {
            get { return (bool)this.GetValue(IsCumulativeProperty); }
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
            if (animationClock.CurrentProgress == null)
            {
                return null;
            }

            double progress = animationClock.CurrentProgress.Value;
            int count;

            // Set ptsFrom from From or defaultOriginValue
            PointCollection ptsFrom = this.From ?? defaultOriginValue;

            if (this.To == null && this.By == null)
            {
                this.ptsTo.Clear();
                foreach (Point pt in defaultDestinationValue)
                {
                    this.ptsTo.Add(pt);
                }
            }

            // Choose which destination collection to use
            PointCollection ptsDst = this.flip ? this.ptsDst1 : this.ptsDst2;
            this.flip = !this.flip;
            ptsDst.Clear();

            // Interpolate the points, but in a left to right sweeping motion
            // where column growth happens in 1/10th of the allocated duration (0.1 on our 0-1 clock scale).
            double end = this.ptsTo.Count;

            for (int i = 0; i < this.ptsTo.Count; i++)
            {
                double fromX = (i < ptsFrom.Count) ? ptsFrom[i].X : 0;
                double fromY = (i < ptsFrom.Count) ? ptsFrom[i].Y : 0;
                ptsDst.Add(new Point((1 - progress) * fromX + progress * this.ptsTo[i].X,
                                     (1 - progress) * fromY + progress * this.ptsTo[i].Y));
            }

            // If IsAdditive, add the base values to ptsDst
            if (this.IsAdditive && this.From != null && (this.To != null || this.By != null))
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
            if (this.IsCumulative && animationClock.CurrentIteration != null)
            {
                int iter = animationClock.CurrentIteration.Value;

                for (int i = 0; i < ptsDst.Count; i++)
                {
                    Point pt = ptsDst[i];
                    pt.X += (iter - 1) * (this.ptsTo[i].X - ptsFrom[i].X);
                    pt.Y += (iter - 1) * (this.ptsTo[i].Y - ptsFrom[i].Y);
                    ptsDst[i] = pt;
                }
            }

            // Return the PointCollection
            return ptsDst;
        }
    }
}
