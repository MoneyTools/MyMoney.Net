using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Walkabout.Utilities;

namespace Walkabout.Controls
{
    public class StackedBar : Shape
    {
        private readonly List<BarInfo> infos = new List<BarInfo>();
        private PathGeometry outline = new PathGeometry();
        private bool mouseOver;
        private bool mouseOverAnimationCompleted;

        public StackedBar()
        {
            this.AnimationColorMilliseconds = 120;
        }

        /// <summary>
        /// Time to animate the column color.
        /// </summary>
        public int AnimationColorMilliseconds { get; set; }

        protected override Geometry DefiningGeometry => this.outline;

        public PointCollection Points
        {
            get { return (PointCollection)this.GetValue(PointsProperty); }
            set {
                this.SetValue(PointsProperty, value);
            }
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            this.mouseOver = true;
            var duration = new Duration(TimeSpan.FromMilliseconds(this.AnimationColorMilliseconds));
            var mouseOverAnimation = new DoubleAnimation() { To = 1.0, Duration = duration };
            mouseOverAnimation.Completed += (s, e) =>
            {
                this.mouseOverAnimationCompleted = true;
                if (!this.mouseOver)
                {
                    this.BeginAnimation(MouseOverBlendProperty, new DoubleAnimation() { To = 0.0, Duration = duration });
                }
            };
            this.mouseOverAnimationCompleted = false;
            this.BeginAnimation(MouseOverBlendProperty, mouseOverAnimation);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            if (this.mouseOver && this.mouseOverAnimationCompleted)
            {
                var duration = new Duration(TimeSpan.FromMilliseconds(this.AnimationColorMilliseconds));
                this.BeginAnimation(MouseOverBlendProperty, new DoubleAnimation() { To = 0.0, Duration = duration });
            }
            this.mouseOver = false;
            this.InvalidateVisual();
        }

        // Using a DependencyProperty as the backing store for Points.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PointsProperty =
            DependencyProperty.Register("Points", typeof(PointCollection), typeof(StackedBar), 
                new PropertyMetadata(new PointCollection(), OnPointsChanged));

        private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            StackedBar bar = (StackedBar)d;
            bar.CreateOutlineGeometry();
        }

        public double MouseOverBlend
        {
            get { return (double)this.GetValue(MouseOverBlendProperty); }
            set { this.SetValue(MouseOverBlendProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MouseOverBlend.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MouseOverBlendProperty =
            DependencyProperty.Register("MouseOverBlend", typeof(double), typeof(StackedBar), new PropertyMetadata(0.0, OnMouseOverBlendChanged));

        private static void OnMouseOverBlendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            StackedBar bar = (StackedBar)d;
            bar.InvalidateVisual();
        }

        private void CreateOutlineGeometry() 
        {
            var points = this.Points;
            var figure = new PathFigure() { IsClosed = true };
            figure.StartPoint = points.FirstOrDefault();
            for (int i = 1; i < points.Count; i++)
            {
                var p = points[i];
                figure.Segments.Add(new LineSegment(p, true));
            }
            this.outline = new PathGeometry();
            this.outline.Figures.Add(figure);
            this.InvalidateVisual();
        }

        class BarInfo
        {
            public double Length;
            public Color Color;
            public Color MouseOverColor;
            public Brush Brush;
            public object UserData;
            public Rect Bounds;
        }

        public void AddSegment(double length, Color color, object userData)
        {
            var moc = this.GetMouseOverColor(color);
            this.infos.Add(new BarInfo { 
                Length = length, 
                Color = color,
                MouseOverColor = moc,
                Brush = new SolidColorBrush(color),
                UserData = userData 
            });
        }

        public object HitBarSegment(Point pos)
        {
            foreach (var i in this.infos)
            {
                if (i.Bounds.Contains(pos))
                {
                    return i.UserData;
                }
            }
            return null;
        }

        private Color GetMouseOverColor(Color c)
        {
            var hls = new HlsColor(c);
            hls.Lighten(0.25f);
            return hls.Color;
        }

        public Orientation Orientation
        {
            get { return (Orientation)this.GetValue(OrientationProperty); }
            set { this.SetValue(OrientationProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MyProperty.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register("OrientationProperty", typeof(Orientation), typeof(StackedBar), new PropertyMetadata(Orientation.Vertical));

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (!this.IsVisible || this.infos.Count == 0 || this.outline.Bounds == Rect.Empty) return;

            this.LayoutSegments();

            double totalLength = (from i in this.infos select i.Length).Sum();
            if (totalLength == 0) return;

            foreach (var i in infos)
            {
                var seg = i.Bounds;
                Brush brush = i.Brush;
                if (this.MouseOverBlend > 0)
                {
                    brush = this.GetBlendBrush(i.Color, i.MouseOverColor, this.MouseOverBlend);
                }
                drawingContext.DrawRectangle(brush, null, seg);
            }
        }

        Brush GetBlendBrush(Color color1, Color color2, double amount)
        {
            var blend = Color.FromRgb(
                (byte)(color1.R + (this.MouseOverBlend * (color2.R - color1.R))),
                (byte)(color1.G + (this.MouseOverBlend * (color2.G - color1.G))),
                (byte)(color1.B + (this.MouseOverBlend * (color2.B - color1.B))));
            return new SolidColorBrush(blend);
        }

        public double GetSegmentLength()
        {
            return (from i in this.infos select i.Length).Sum();
        }


        private void LayoutSegments()
        {
            double totalLength = this.GetSegmentLength();
            if (totalLength == 0) return;

            if (this.Orientation == Orientation.Vertical)
            {
                Rect bounds = this.outline.Bounds;
                double width = bounds.Width;
                double x = bounds.Left;
                double y = bounds.Top;
                foreach (var i in infos)
                {
                    double height = Math.Round((i.Length / totalLength) * bounds.Height);
                    if (height < 0) height = 0;
                    i.Bounds = new Rect(x, y, width, height);
                    y += height;
                }
            }
            else
            {
                Rect bounds = this.outline.Bounds;
                double height = bounds.Height;
                double x = bounds.Left;
                double y = bounds.Top;
                foreach (var i in infos)
                {
                    double width = Math.Round((i.Length / totalLength) * bounds.Width);
                    if (width < 0) width = 0;
                    i.Bounds = new Rect(x, y, width, height);
                    x += width;
                }
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            this.InvalidateVisual();
            base.OnRenderSizeChanged(sizeInfo);
        }

        internal void ClearSegments()
        {
            infos.Clear();
            this.InvalidateVisual();
        }
    }
}
