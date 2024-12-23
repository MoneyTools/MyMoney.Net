﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Walkabout.Charts;
using Walkabout.Utilities;

namespace LovettSoftware.Charts
{
    /// <summary>
    /// Presents a pie chart view of the ChartDataSeries where any negative
    /// values are turned into positive values for the purposes of space allocation,
    /// but the true negative value is shown in the Hover tooltips.
    /// </summary>
    public partial class AnimatingPieChart : UserControl
    {
        private readonly DelayedActions actions = new DelayedActions();
        private readonly List<PieSlice> slices = new List<PieSlice>();
        private Point movePos;
        private PieSlice inside;
        private Size previousArrangeBounds = Size.Empty;
        private bool mouseOverAnimationCompleted = false;
        private readonly Random rand = new Random(Environment.TickCount);

        public AnimatingPieChart()
        {
            this.InitializeComponent();

            this.HoverDelayMilliseconds = 250;
            this.AnimationGrowthMilliseconds = 250;
            this.AnimationColorMilliseconds = 250;
            IsVisibleChanged += this.OnVisibleChanged;
        }

        public int HoverDelayMilliseconds { get; set; }

        /// <summary>
        /// Time to animate growth of the columns.
        /// </summary>
        public int AnimationGrowthMilliseconds { get; set; }

        /// <summary>
        /// Time to animate the slice color.
        /// </summary>
        public int AnimationColorMilliseconds { get; set; }


        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            if (this.previousArrangeBounds != arrangeBounds)
            {
                this.OnDelayedUpdate();
                this.previousArrangeBounds = arrangeBounds;
            }
            return base.ArrangeOverride(arrangeBounds);
        }


        private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool b && !b)
            {
                this.HideToolTip();
            }
            this.OnDelayedUpdate();
        }

        private void HideToolTip()
        {
            var tip = this.ToolTip as ToolTip;
            if (tip != null)
            {
                tip.IsOpen = false;
                this.ToolTip = null;
            }
        }

        public void Update()
        {
            this.UpdateChart();
        }

        public ChartDataSeries Series
        {
            get { return (ChartDataSeries)this.GetValue(PieSeriesProperty); }
            set { this.SetValue(PieSeriesProperty, value); }
        }

        public static readonly DependencyProperty PieSeriesProperty =
            DependencyProperty.Register("PieSeries", typeof(ChartDataSeries), typeof(AnimatingPieChart), new PropertyMetadata(null, OnSeriesChanged));

        private static void OnSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatingPieChart)d).OnSeriesChanged(e.NewValue);
        }

        private void OnSeriesChanged(object newValue)
        {
            this.HideToolTip();
            if (newValue == null)
            {
                this.ResetVisuals();
            }
            else
            {
                foreach (var dv in this.Series.Values)
                {
                    if (!dv.Color.HasValue || dv.Color.Value == Colors.Transparent)
                    {
                        dv.Color = this.GetRandomColor();
                    }
                }
                this.OnDelayedUpdate();
            }
        }

        private void ResetVisuals()
        {
            this.ChartCanvas.Children.Clear();
            this.slices.Clear();
            this.inside = null;
            this.mouseOverAnimationCompleted = false;
        }

        private void OnDelayedUpdate()
        {
            this.actions.StartDelayedAction("update", this.UpdateChart, TimeSpan.FromMilliseconds(10));
        }

        public event EventHandler<ChartDataValue> PieSliceHover;
        public event EventHandler<ChartDataValue> PieSliceClicked;

        private void UpdateChart()
        {
            if (this.Series == null || this.Series.Values.Count == 0 || double.IsNaN(this.ActualWidth))
            {
                this.ResetVisuals();
                return;
            }

            double w = this.ActualWidth - (this.Margin.Left + this.Margin.Right);
            double h = this.ActualHeight - (this.Margin.Top + this.Margin.Bottom);
            if (w < 0 || h < 0 || this.Visibility != Visibility.Visible)
            {
                return;
            }

            double m = Math.Min(w, h);
            double c = Math.Floor(m / 2);

            double total = (from d in this.Series.Values where !d.Hidden select Math.Abs(d.Value)).Sum();
            Point center = new Point(c + ((w - m) / 2), c + ((h - m) / 2));

            switch (this.HorizontalContentAlignment)
            {
                case HorizontalAlignment.Left:
                    center = new Point(this.Margin.Left + c, c + ((h - m) / 2));
                    break;
                case HorizontalAlignment.Right:
                    center = new Point(this.Width - this.Margin.Left - c, c + ((h - m) / 2));
                    break;
                case HorizontalAlignment.Center:
                case HorizontalAlignment.Stretch: // not supported.
                default:
                    center = new Point(c + ((w - m) / 2), c + ((h - m) / 2));
                    break;
            }

            Size size = new Size(c, c);
            Storyboard sb = new Storyboard();

            double sum = 0;
            int i = 0;
            double oldStart = 0;
            double oldEnd = 0;
            foreach (var item in this.Series.Values)
            {
                PieSlice slice = null;
                if (i < this.slices.Count)
                {
                    slice = this.slices[i];
                    oldStart = slice.StartAngle;
                    oldEnd = slice.EndAngle;
                    slice.Center = center;
                    slice.Size = size;
                }
                else
                {
                    slice = new PieSlice(this.ChartCanvas, Colors.Transparent, center, size);
                    this.slices.Add(slice);
                }

                if (item.Hidden)
                {
                    // we need the slice to be added, but it needs to be hidden.
                    slice.Visibility = Visibility.Collapsed;
                    continue;
                }
                else
                {
                    slice.Visibility = Visibility.Visible;
                }

                var value = Math.Abs(item.Value); // can't show negatives, but we will in the hover tooltip!

                slice.Data = item;
                slice.Color = item.Color.Value;
                double start = total == 0 ? 0 : sum * 360 / total;
                double end = total == 0 ? 360 : (sum + value) * 360 / total;
                if (end == 360)
                {
                    end = 359.99;
                }
                this.AnimateSlice(sb, slice, oldStart, start, oldEnd, end, item.Color.Value);
                sum += value;
                i++;
            }

            while (this.slices.Count > i)
            {
                this.ChartCanvas.Children.Remove(this.slices[i].Path);
                this.slices.RemoveAt(i);
            }

            this.BeginStoryboard(sb);
        }

        private void AnimateSlice(Storyboard sb, PieSlice slice, double a1, double a2, double a3, double a4, Color c)
        {
            var duration1 = new Duration(TimeSpan.FromMilliseconds(this.AnimationGrowthMilliseconds));

            var animation1 = new DoubleAnimation()
            {
                From = a1,
                To = a2,
                Duration = duration1,
                EasingFunction = new ExponentialEase() { Exponent = 0.6, EasingMode = EasingMode.EaseIn }
            };
            sb.Children.Add(animation1);
            Storyboard.SetTarget(animation1, slice);
            Storyboard.SetTargetProperty(animation1, new PropertyPath(PieSlice.StartAngleProperty)); // start of the slice

            var animation2 = new DoubleAnimation()
            {
                From = a3,
                To = a4,
                Duration = duration1,
                EasingFunction = new ExponentialEase() { Exponent = 0.6, EasingMode = EasingMode.EaseIn }
            };
            sb.Children.Add(animation2);
            Storyboard.SetTarget(animation2, slice);
            Storyboard.SetTargetProperty(animation2, new PropertyPath(PieSlice.EndAngleProperty)); // end of the slice.

            var duration2 = new Duration(TimeSpan.FromMilliseconds(this.AnimationColorMilliseconds));
            SolidColorBrush stroke = (SolidColorBrush)slice.Path.Stroke;
            SolidColorBrush fill = (SolidColorBrush)slice.Path.Fill;
            stroke.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(c, duration2));
            fill.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(c, duration2));
        }

        private Color GetMouseOverColor(Color c)
        {
            var hls = new HlsColor(c);
            hls.Lighten(0.25f);
            return hls.Color;
        }

        public ToolTipGenerator ToolTipGenerator { get; set; }


        protected override void OnMouseLeave(MouseEventArgs e)
        {
            this.actions.CancelDelayedAction("hover");
            this.OnExitSlice();
            base.OnMouseLeave(e);
        }

        private void OnHover()
        {
            var slice = this.inside;
            if (slice == null)
            {
                return;
            }

            ChartDataValue value = slice.Data;
            var tip = this.ToolTip as ToolTip;
            var content = this.ToolTipGenerator != null ? this.ToolTipGenerator(value) : new TextBlock() { Text = value.Label + "\r\n" + value.Value };
            if (tip == null)
            {
                tip = new ToolTip()
                {
                    Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
                    Content = content,
                    IsOpen = true
                };
                this.ToolTip = tip;
            }
            else
            {
                tip.Content = content;
                tip.IsOpen = true;
            }
            tip.Measure(new Size(100, 100));
            tip.HorizontalOffset = 0;
            tip.VerticalOffset = -tip.ActualHeight;

            // notify any interested listeners
            var h = PieSliceHover;
            if (h != null)
            {
                h(this, value);
            }

        }

        private PieSlice FindSlice(Point pos)
        {
            for (int i = 0, n = this.slices.Count; i < n; i++)
            {
                var slice = this.slices[i];
                var p = slice.Path;
                if (p.Data.FillContains(pos) && p.Visibility == Visibility.Visible)
                {
                    return slice;
                }
            }
            return null;
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            var pos = e.GetPosition(this.ChartCanvas);
            var slice = this.FindSlice(pos);
            if (slice != null)
            {
                this.OnEnterSlice(slice);
                this.HideToolTip();
                this.movePos = pos;
                this.actions.StartDelayedAction("hover", () =>
                {
                    this.OnHover();
                }, TimeSpan.FromMilliseconds(this.HoverDelayMilliseconds));
            }
            else
            {
                this.OnExitSlice();
            }
            base.OnPreviewMouseMove(e);
        }

        private void OnEnterSlice(PieSlice slice)
        {
            if (slice != null)
            {
                var brush = (SolidColorBrush)slice.Path.Fill;
                var color = slice.Color;
                if (this.inside == null || slice != this.inside)
                {
                    if (this.inside != null)
                    {
                        this.OnExitSlice();
                    }

                    var duration = new Duration(TimeSpan.FromMilliseconds(this.AnimationColorMilliseconds));
                    var highlight = this.GetMouseOverColor(color);
                    var mouseOverAnimation = new ColorAnimation() { To = highlight, Duration = duration };
                    mouseOverAnimation.Completed += (s, e) =>
                    {
                        this.mouseOverAnimationCompleted = true;
                        if (slice != this.inside)
                        {
                            brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation() { To = color, Duration = duration });
                        }
                    };
                    this.mouseOverAnimationCompleted = false;
                    brush.BeginAnimation(SolidColorBrush.ColorProperty, mouseOverAnimation);
                    this.inside = slice;
                }
            }
        }

        private void OnExitSlice()
        {
            if (this.inside != null && this.mouseOverAnimationCompleted)
            {
                var duration = new Duration(TimeSpan.FromMilliseconds(this.AnimationColorMilliseconds));
                var brush = (SolidColorBrush)this.inside.Path.Fill;
                brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation() { To = this.inside.Color, Duration = duration });
            }
            this.inside = null;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this.ChartCanvas);
            var slice = this.FindSlice(pos);
            if (slice != null)
            {
                ChartDataValue value = slice.Data;
                if (PieSliceClicked != null)
                {
                    PieSliceClicked(this, value);
                }
            }
        }

        private class PieSlice : DependencyObject
        {
            private readonly Path path;
            private PathFigure figure;
            private LineSegment line1;
            private LineSegment line2;
            private ArcSegment arc;


            public PieSlice(Canvas owner, Color color, Point center, Size size)
            {
                this.Color = color;
                this.path = new Path();
                this.path.Stroke = new SolidColorBrush(color);
                this.path.Fill = new SolidColorBrush(color);
                this.path.StrokeThickness = 1;
                this.path.StrokeMiterLimit = 1; // stop narrow slices from creating long pointed line join.
                owner.Children.Add(this.path);
                this.Size = size;
                this.Center = center;
            }

            public Visibility Visibility
            {
                get => this.path.Visibility;
                set { this.path.Visibility = value; }
            }

            public Color Color { get; set; }

            public Path Path => this.path;

            public ChartDataValue Data { get; set; }


            public double StartAngle
            {
                get { return (double)this.GetValue(StartAngleProperty); }
                set { this.SetValue(StartAngleProperty, value); }
            }

            // Using a DependencyProperty as the backing store for StartAngle.  This enables animation, styling, binding, etc...
            public static readonly DependencyProperty StartAngleProperty =
                DependencyProperty.Register("StartAngle", typeof(double), typeof(PieSlice), new PropertyMetadata(0.0, new PropertyChangedCallback(OnPropertyChanged)));

            private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                ((PieSlice)d).OnPropertyChanged();
            }

            private void OnPropertyChanged()
            {
                var center = this.Center;
                var size = this.Size;
                double x1 = center.X + (Math.Cos(this.StartAngle * Math.PI / 180) * size.Width);
                double y1 = center.Y + (Math.Sin(this.StartAngle * Math.PI / 180) * size.Height);
                double x2 = center.X + (Math.Cos(this.EndAngle * Math.PI / 180) * size.Width);
                double y2 = center.Y + (Math.Sin(this.EndAngle * Math.PI / 180) * size.Height);
                var p1 = new Point(x1, y1);
                var p2 = new Point(x2, y2);

                if (this.line1 == null)
                {
                    PathGeometry g = new PathGeometry();
                    this.line1 = new LineSegment(p1, true);
                    this.arc = new ArcSegment(p2, size, 0, false, SweepDirection.Clockwise, true);
                    this.line2 = new LineSegment(center, true);
                    this.figure = new PathFigure(center, new PathSegment[] {
                        this.line1,
                        this.arc,
                        this.line2
                    }, true);
                    g.Figures.Add(this.figure);
                    this.path.Data = g;
                }
                else
                {
                    this.figure.StartPoint = center;
                    this.line1.Point = p1;
                    this.line2.Point = center;
                    this.arc.Point = p2;
                }

                this.arc.Size = size;
                this.arc.IsLargeArc = (this.EndAngle - this.StartAngle) > 180;
            }

            public double EndAngle
            {
                get { return (double)this.GetValue(EndAngleProperty); }
                set { this.SetValue(EndAngleProperty, value); }
            }

            public static readonly DependencyProperty EndAngleProperty =
                DependencyProperty.Register("EndAngle", typeof(double), typeof(PieSlice), new PropertyMetadata(0.0, new PropertyChangedCallback(OnPropertyChanged)));

            public Point Center
            {
                get { return (Point)this.GetValue(CenterProperty); }
                set { this.SetValue(CenterProperty, value); }
            }

            public static readonly DependencyProperty CenterProperty =
                DependencyProperty.Register("Center", typeof(Point), typeof(PieSlice), new PropertyMetadata(new Point(0, 0), new PropertyChangedCallback(OnPropertyChanged)));

            public Size Size
            {
                get { return (Size)this.GetValue(SizeProperty); }
                set { this.SetValue(SizeProperty, value); }
            }

            public static readonly DependencyProperty SizeProperty =
                DependencyProperty.Register("Size", typeof(Size), typeof(PieSlice), new PropertyMetadata(new Size(0, 0), new PropertyChangedCallback(OnPropertyChanged)));


        }

        private Color GetRandomColor()
        {
            return Color.FromRgb((byte)this.rand.Next(80, 200), (byte)this.rand.Next(80, 200), (byte)this.rand.Next(80, 200));
        }

    }
}