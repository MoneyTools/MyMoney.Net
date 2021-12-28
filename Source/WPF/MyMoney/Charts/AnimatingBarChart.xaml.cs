using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Walkabout.Utilities;

namespace Walkabout.Charts
{
    public class BarChartDataValue
    {
        public string Label;
        public double Value;
        public object UserData;
    }

    public delegate UIElement ToolTipGenerator(BarChartDataValue value);

    /// <summary>
    /// Interaction logic for AnimatingBarChart.xaml
    /// </summary>
    public partial class AnimatingBarChart : UserControl
    {
        DelayedActions actions = new DelayedActions();
        Color fill;
        Color mouseOverColor;
        int tipColumn = -1;
        Point movePos;
        Polygon inside;
        bool mouseOverAnimationCompleted = false;

        class ColumnInfo
        {
            public TextBlock Label;
            public Rect Bounds;
            public Polygon Shape;
        }

        // this is maintained for hit testing only since the mouse events don't seem to be 
        // working on the animated Rectangles.
        List<ColumnInfo> bars = new List<ColumnInfo>();

        List<Polygon> axisLines = new List<Polygon>();
        List<TextBlock> axisLabels = new List<TextBlock>();


        public AnimatingBarChart()
        {
            InitializeComponent();
            HoverDelayMilliseconds = 250;
            this.AnimationGrowthMilliseconds = 250;
            this.AnimationRippleMilliseconds = 20;
            this.AnimationColorMilliseconds = 120;
        }

        public Color FillColor
        {
            get => fill;
            set { fill = value; OnColorChanged(); }
        }

        /// <summary>
        /// Time to animate growth of the columns.
        /// </summary>
        public int AnimationGrowthMilliseconds { get; set; }

        /// <summary>
        /// Delay from column to column creates a nice ripple effect.
        /// </summary>
        public int AnimationRippleMilliseconds { get; set; }

        /// <summary>
        /// Time to animate the column color.
        /// </summary>
        public int AnimationColorMilliseconds { get; set; }

        private void OnColorChanged()
        {
            var hls = new HlsColor(fill);
            hls.Lighten(0.25f);
            mouseOverColor = hls.Color;
        }

        public int HoverDelayMilliseconds { get; set; }

        public ToolTipGenerator ToolTipGenerator { get; set; }



        public Brush LineBrush
        {
            get { return (Brush)GetValue(LineBrushProperty); }
            set { SetValue(LineBrushProperty, value); }
        }

        // Using a DependencyProperty as the backing store for LineBrush.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LineBrushProperty =
            DependencyProperty.Register("LineBrush", typeof(Brush), typeof(AnimatingBarChart), new PropertyMetadata(null, new PropertyChangedCallback(OnLineBrushChanged)));

        private static void OnLineBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatingBarChart)d).OnDelayedUpdate();
        }

        public Orientation Orientation
        {
            get { return (Orientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        // Using a DependencyProperty as the backing store for AxisOrientation.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register("AxisOrientation", typeof(Orientation), typeof(AnimatingBarChart), new PropertyMetadata(Orientation.Horizontal, OnOrientationChanged));

        private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatingBarChart)d).OnDelayedUpdate();
        }

        public List<BarChartDataValue> Series
        {
            get { return (List<BarChartDataValue>)GetValue(PointsProperty); }
            set { SetValue(PointsProperty, value); }
        }

        public static readonly DependencyProperty PointsProperty =
            DependencyProperty.Register("Series", typeof(List<BarChartDataValue>), typeof(AnimatingBarChart), new PropertyMetadata(null, OnSeriesChanged));

        private static void OnSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatingBarChart)d).OnSeriesChanged(e.NewValue);
        }

        private void OnSeriesChanged(object newValue)
        {
            if (newValue == null) {
                ResetVisuals();
            }
            else
            {
                OnDelayedUpdate();
            }
        }

        void ResetVisuals()
        {
            ChartCanvas.Children.Clear();
            bars.Clear();
            tipColumn = -1;
            inside = null;
            mouseOverAnimationCompleted = false;
        }

        private void OnDelayedUpdate()
        {
            actions.StartDelayedAction("update", UpdateChart, TimeSpan.FromMilliseconds(10));
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            actions.StartDelayedAction("update", UpdateChart, TimeSpan.FromMilliseconds(10));
            return base.ArrangeOverride(arrangeBounds);
        }

        public event EventHandler<BarChartDataValue> ColumnHover;
        public event EventHandler<BarChartDataValue> ColumnClicked;


        private void UpdateChart()
        {
            double w = this.ActualWidth;
            double h = this.ActualHeight;
            if (Series == null || Series.Count == 0 || w == 0 || h == 0)
            {
                ResetVisuals();
            }
            else
            {
                if (this.Orientation == Orientation.Horizontal)
                {
                    HorizontalLayout();
                }
                else
                {
                    VerticalLayout();
                }
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            actions.CancelDelayedAction("hover");
            tipColumn = -1;
            OnExitColumn();
            base.OnMouseLeave(e);
        }

        private void OnHover()
        {
            var i = this.tipColumn;
            if (i < 0 || i >= Series.Count)
            {
                return;
            }

            BarChartDataValue value = Series[i];
            var s = this.PointToScreen(this.movePos);
            var tip = this.ToolTip as ToolTip;
            var content = ToolTipGenerator != null ? ToolTipGenerator(value) : new TextBlock() { Text = value.Label + "\r\n" + value.Value };
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
            tip.VerticalOffset = -tip.DesiredSize.Height;

            // notify any interested listeners
            var h = this.ColumnHover;
            if (h != null)
            {
                h(this, value);
            }

        }

        int FindColumn(Point pos)
        {
            for (int i = 0, n = bars.Count; i < n; i++)
            {
                var r = this.bars[i].Bounds;
                if (pos.X >= r.Left && pos.X <= r.Right)
                {
                    if (pos.Y >= r.Top && pos.Y <= r.Bottom)
                    {
                        // found it!
                        return i;
                    }
                }
            }
            return -1;
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            var pos = e.GetPosition(this);
            var i = FindColumn(pos);
            if (i >= 0 && i < Series.Count) 
            {
                OnEnterColumn(i);
                var tip = this.ToolTip as ToolTip;
                if (tip != null)
                {
                    tip.IsOpen = false;
                    this.ToolTip = null;
                }
                this.movePos = pos;
                this.tipColumn = i;
                actions.StartDelayedAction("hover", () =>
                {
                    OnHover();
                }, TimeSpan.FromMilliseconds(HoverDelayMilliseconds));
            }
            else
            {
                this.tipColumn = -1;
                OnExitColumn();
            }
            base.OnPreviewMouseMove(e);
        }

        private void OnEnterColumn(int i)
        {
            if (i < bars.Count)
            {
                Polygon r = bars[i].Shape;
                if (r != inside)
                {
                    if (inside != null)
                    {
                        OnExitColumn();
                    }
                    var duration = new Duration(TimeSpan.FromMilliseconds(AnimationColorMilliseconds));
                    var brush = r.Fill as SolidColorBrush;
                    var mouseOverAnimation = new ColorAnimation() { To = this.mouseOverColor, Duration = duration };
                    mouseOverAnimation.Completed += (s, e) =>
                    {
                        this.mouseOverAnimationCompleted = true;
                        if (r != inside)
                        {
                            brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation() { To = this.fill, Duration = duration });
                        }
                    };
                    this.mouseOverAnimationCompleted = false;
                    brush.BeginAnimation(SolidColorBrush.ColorProperty, mouseOverAnimation );
                    inside = r;
                }
            }
        }

        void OnExitColumn()
        {
            if (inside != null && this.mouseOverAnimationCompleted)
            {
                var duration = new Duration(TimeSpan.FromMilliseconds(AnimationColorMilliseconds));
                var brush = inside.Fill as SolidColorBrush;
                brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation() { To = this.fill, Duration = duration });
            }
            inside = null;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this);
            var i = FindColumn(pos);
            if (i >= 0 && i < Series.Count)
            {
                BarChartDataValue value = Series[i];
                if (ColumnClicked != null)
                {
                    ColumnClicked(this, value);
                }
            }
        }

        private Size CreateColumnInfos()
        {
            int index = 0;
            Size minMax = new Size();
            foreach (var item in Series)
            {
                ColumnInfo info = null;
                if (index < bars.Count)
                {
                    info = bars[index];
                }
                else
                {
                    info = new ColumnInfo();
                    bars.Add(info);
                }

                if (!string.IsNullOrEmpty(item.Label))
                {
                    var block = info.Label;
                    if (block == null)
                    {
                        block = new TextBlock() { Foreground = this.Foreground };
                        info.Label = block;
                    }
                    block.Text = item.Label;
                    block.BeginAnimation(TextBlock.OpacityProperty, null);
                    block.Opacity = 0;
                    ChartCanvas.Children.Add(block); // so it measures properly.
                    block.Measure(new Size(100, 100));
                    ChartCanvas.Children.Remove(block);
                    var size = block.DesiredSize;
                    minMax.Width = Math.Max(minMax.Width, size.Width);
                    minMax.Height = Math.Max(minMax.Height, size.Height);
                }
                index++;
            }

            bars.RemoveRange(index, bars.Count - index);

            return minMax;
        }

        private Size AddAxisLabels(out NiceScale scale)
        {
            double maxValue = 0;
            double minValue = 0;
            foreach (var item in Series)
            {
                var v = item.Value;
                maxValue = Math.Max(maxValue, v);
                minValue = Math.Min(minValue, v);
            }

            Size minMax = new Size();
            scale = new NiceScale(minValue, maxValue);
            var spacing = scale.GetTickSpacing();
            var min = scale.GetNiceMin();
            var max = scale.GetNiceMax();
            var labels = new List<TextBlock>();
            int i = 0;
            for (var r = min; r <= max; r += spacing)
            {
                TextBlock label = null;
                Polygon line = null;
                if (i < axisLabels.Count)
                {
                    label = axisLabels[i];
                    line = axisLines[i];
                }
                else
                {
                    label = new TextBlock() { Foreground = this.Foreground };
                    axisLabels.Add(label);
                    line = new Polygon() { Stroke = this.LineBrush, StrokeThickness = 1, Points = new PointCollection() };
                    axisLines.Add(line);
                }
                ChartCanvas.Children.Add(line);
                label.Text = r.ToString("N0");
                ChartCanvas.Children.Add(label);
                label.Measure(new Size(100, 100));
                minMax.Width = Math.Max(minMax.Width, label.DesiredSize.Width);
                minMax.Height = Math.Max(minMax.Height, label.DesiredSize.Height);
                i++;
            }

            axisLabels.RemoveRange(i, axisLabels.Count - i);
            axisLines.RemoveRange(i, axisLines.Count - i);

            return minMax;
        }

        private void VerticalLayout()
        {
            ChartCanvas.Children.Clear();

            var duration = new Duration(TimeSpan.FromMilliseconds(this.AnimationGrowthMilliseconds));
            double steps = Series.Count;
            double w = this.ActualWidth;
            double h = this.ActualHeight;

            Size labelSize = CreateColumnInfos();

            Size axisLabelSize = AddAxisLabels(out NiceScale scale);

            var min = scale.GetNiceMin();
            var max = scale.GetNiceMax();
            var spacing = scale.GetTickSpacing();

            double labelGap = 10;
            double labelMargin = labelSize.Width + labelGap + labelGap;
            if (-min > labelMargin)
            {
                labelMargin = 0;
            }
            w -= labelMargin; // allocate space at the left column labels.
            h -= axisLabelSize.Height + labelGap + labelGap;

            double columnHeight = h / steps;
            // make the gap between columns 1/3 of the column width.
            double gap = columnHeight / 3;
            columnHeight -= gap;
            double range = (max - min);
            double zero = 0;
            if (min < 0)
            {
                zero = Math.Abs(min) * w / range;
            }

            // layout the axis labels and lines
            int i = 0;
            for (var r = min; r <= max; r += spacing)
            {
                double xpos = labelMargin + zero + (r * w / range);
                var label = axisLabels[i];
                var line = axisLines[i];
                var mid = label.DesiredSize.Width / 2;
                Canvas.SetLeft(label, xpos > mid ? xpos - mid : xpos + labelGap);
                Canvas.SetTop(label, h + labelGap);

                PointCollection poly = new PointCollection();
                poly.Add(new Point() { X = xpos, Y = 0 });
                poly.Add(new Point() { X = xpos, Y = h });
                line.BeginAnimation(Polygon.PointsProperty, new PointCollectionAnimation() { To = poly, Duration = duration });

                label.BeginAnimation(TextBlock.OpacityProperty, new DoubleAnimation()
                {
                    From = 0,
                    To = 1,
                    Duration = duration
                });
                i++;
            }

            double y = 0;
            double x = labelMargin + zero;
            int index = 0;
            Rect previousLabel = new Rect() { X = -1000, Y = 0, Width = 0, Height = 0 };
            // layout the columns.
            foreach (var item in Series)
            {
                double s = (item.Value * w / range);
                
                ColumnInfo info = bars[index];
                Polygon polygon = info.Shape;
                SolidColorBrush brush = null;
                if (polygon != null)
                {
                    brush = polygon.Fill as SolidColorBrush;
                }
                else
                {
                    // make initial bars grow from zero.
                    PointCollection initial = new PointCollection();
                    initial.Add(new Point() { X = x, Y = y });
                    initial.Add(new Point() { X = x, Y = y, });
                    initial.Add(new Point() { X = x, Y = y + columnHeight });
                    initial.Add(new Point() { X = x, Y = y + columnHeight });
                    brush = new SolidColorBrush() { Color = Colors.Transparent };
                    polygon = new Polygon() { Fill = brush, Points = initial };
                    info.Shape = polygon;
                }

                var start = TimeSpan.FromMilliseconds(index * AnimationRippleMilliseconds);

                if (info.Label != null)
                {
                    var block = info.Label;
                    var size = block.DesiredSize;
                    double xpos = 0;
                    if (s < 0)
                    {
                        // right of the negative sized column
                        xpos = x + labelGap;
                    }
                    else
                    {
                        xpos = x - labelGap - size.Width;
                    }

                    Rect bounds = new Rect() { X = xpos, Y = y + (columnHeight - size.Height) / 2, Width = size.Width, Height = size.Height };
                    Rect inflated = bounds;
                    inflated.Inflate(this.FontSize / 2, 0);
                    if (inflated.IntersectsWith(previousLabel))
                    {
                        // skip it!
                    }
                    else
                    {
                        previousLabel = inflated;
                        Canvas.SetLeft(block, bounds.X);
                        Canvas.SetTop(block, bounds.Y);

                        block.BeginAnimation(TextBlock.OpacityProperty, new DoubleAnimation()
                        {
                            From = 0,
                            To = 1,
                            Duration = duration,
                            BeginTime = start
                        });

                        ChartCanvas.Children.Add(block);
                    }
                }
                if (s < 0)
                {
                    info.Bounds = new Rect() { X = x + s, Y = y, Width = -s, Height = columnHeight };
                }
                else
                {
                    info.Bounds = new Rect() { X = x, Y = y, Width = s, Height = columnHeight };
                }

                PointCollection poly = new PointCollection();
                poly.Add(new Point() { X = x, Y = y });
                poly.Add(new Point() { X = x + s, Y = y, });
                y += columnHeight;
                poly.Add(new Point() { X = x + s, Y = y });
                poly.Add(new Point() { X = x, Y = y, });
                y += gap;
                ChartCanvas.Children.Add(polygon);

                polygon.BeginAnimation(Polygon.PointsProperty, new PointCollectionAnimation() { To = poly, Duration = duration, BeginTime = start });
                brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation() { To = this.FillColor, Duration = duration, BeginTime = start });
                index++;
            }
        }

        private void HorizontalLayout()
        {
            ChartCanvas.Children.Clear();

            var duration = new Duration(TimeSpan.FromMilliseconds(this.AnimationGrowthMilliseconds));
            double steps = Series.Count;
            double w = this.ActualWidth;
            double h = this.ActualHeight;

            Size axisLabelSize = AddAxisLabels(out NiceScale scale);

            var min = scale.GetNiceMin();
            var max = scale.GetNiceMax();
            var spacing = scale.GetTickSpacing();

            Size labelSize = CreateColumnInfos();

            double labelGap = this.FontSize / 3;
            double labelMargin = labelSize.Height + labelGap + labelGap;
            if (-min > labelMargin)
            {
                labelMargin = 0;
            }
            h -= labelMargin; // allocate space at the bottom for column labels.
            double axisLabelGap = axisLabelSize.Width + labelGap + labelGap;
            w -= axisLabelGap; // allocate space for axis labels.

            double columnWidth = w / steps;
            // make the gap between columns 1/3 of the column width.
            double gap = columnWidth / 3;
            columnWidth -= gap;

            double range = (max - min);
            double zero = 0;
            if (min < 0)
            {
                zero = (Math.Abs(min) * h / range);
            }

            // layout the axis labels and lines
            int i = 0;
            for (var r = min; r <= max; r += spacing)
            {
                double ypos = (h - (r * h / range)) + labelMargin;
                var label = axisLabels[i];
                var line = axisLines[i];
                var mid = label.DesiredSize.Height / 2;
                Canvas.SetLeft(label, labelGap);
                Canvas.SetTop(label, ypos > mid ? ypos - mid : ypos + labelGap);

                PointCollection poly = new PointCollection();
                poly.Add(new Point() { X = axisLabelGap, Y = ypos });
                poly.Add(new Point() { X = w, Y = ypos });
                line.BeginAnimation(Polygon.PointsProperty, new PointCollectionAnimation() { To = poly, Duration = duration });

                label.BeginAnimation(TextBlock.OpacityProperty, new DoubleAnimation()
                {
                    From = 0,
                    To = 1,
                    Duration = duration
                });

                i++;
            }

            Rect previousLabel = new Rect() { X = -1000, Y = 0, Width = 0, Height = 0 };
            int index = 0;
            double x = axisLabelGap;
            double y = h - zero;
            foreach (var item in Series)
            {
                double s = (item.Value * h / range);
                var start = TimeSpan.FromMilliseconds(index * AnimationRippleMilliseconds);
                ColumnInfo info = this.bars[index];
                Polygon polygon = info.Shape;
                SolidColorBrush brush = null;
                if (polygon != null)
                {
                    brush = polygon.Fill as SolidColorBrush;
                }
                else
                {
                    // make initial bars grow from zero.
                    PointCollection initial = new PointCollection();
                    initial.Add(new Point() { X = x, Y = y });
                    initial.Add(new Point() { X = x, Y = y, });
                    initial.Add(new Point() { X = x + columnWidth, Y = y });
                    initial.Add(new Point() { X = x + columnWidth, Y = y });
                    brush = new SolidColorBrush() { Color = Colors.Transparent };
                    polygon = new Polygon() { Fill = brush, Points = initial };
                    info.Shape = polygon;
                }

                if (info.Label != null)
                {
                    var block = info.Label;
                    var size = block.DesiredSize;
                    double ypos = 0;
                    if (s < 0)
                    {
                        // above the downward pointing column then.
                        ypos = y - labelGap - size.Height;
                    }
                    else
                    {
                        ypos = y + labelGap;
                    }

                    Rect bounds = new Rect() { X = x + (columnWidth - size.Width) / 2, Y = ypos, Width = size.Width, Height = size.Height };
                    Rect inflated = bounds;
                    inflated.Inflate(this.FontSize / 2, 0);
                    if (inflated.IntersectsWith(previousLabel))
                    {
                        // skip it!
                    }
                    else
                    {
                        previousLabel = inflated;
                        Canvas.SetLeft(block, bounds.X);
                        Canvas.SetTop(block, bounds.Y);

                        block.BeginAnimation(TextBlock.OpacityProperty, new DoubleAnimation()
                        {
                            From = 0,
                            To = 1,
                            Duration = duration,
                            BeginTime = start
                        });
                        ChartCanvas.Children.Add(block);
                    }
                }

                if (s < 0)
                {
                    info.Bounds = new Rect() { X = x, Y = y, Width = columnWidth, Height = -s };
                }
                else
                {
                    info.Bounds = new Rect() { X = x, Y = y - s, Width = columnWidth, Height = s };
                }

                PointCollection poly = new PointCollection();
                poly.Add(new Point() { X = x, Y = y });
                poly.Add(new Point() { X = x, Y = y - s });
                x += columnWidth;
                poly.Add(new Point() { X = x, Y = y - s});
                poly.Add(new Point() { X = x, Y = y });
                x += gap;

                ChartCanvas.Children.Add(polygon);
                polygon.BeginAnimation(Polygon.PointsProperty, new PointCollectionAnimation() { To = poly, Duration = duration, BeginTime = start });
                brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation() { To = this.FillColor, Duration = duration, BeginTime = start });

                index++;
            }

            while (index < bars.Count)
            {
                var item = bars[index];
                if (item.Shape != null) ChartCanvas.Children.Remove(item.Shape);
                if (item.Label != null) ChartCanvas.Children.Remove(item.Label);
                bars.RemoveAt(index);
            }
        }
    }

    class NiceScale
    {
        private double minPoint;
        private double maxPoint;
        private double maxTicks = 10;
        private double tickSpacing;
        private double range;
        private double niceMin;
        private double niceMax;

        /**
        * Instantiates a new instance of the NiceScale class.
        *
        * @param min the minimum data point on the axis
        * @param max the maximum data point on the axis
*/
        public NiceScale(double min, double max)
        {
            this.minPoint = min;
            this.maxPoint = max;
            Calculate();
        }

        /**
        * Calculate and update values for tick spacing and nice
        * minimum and maximum data points on the axis.
*/
        private void Calculate()
        {
            this.range = GetNiceNum(maxPoint - minPoint, false);
            this.tickSpacing = GetNiceNum(range / (maxTicks - 1), true);
            this.niceMin = Math.Floor(minPoint / tickSpacing) * tickSpacing;
            this.niceMax = Math.Ceiling(maxPoint / tickSpacing) * tickSpacing;
        }

        /**
        * Returns a "nice" number approximately equal to range Rounds
        * the number if round = true Takes the ceiling if round = false.
        *
        * @param range the data range
        * @param round whether to round the result
        * @return a "nice" number to be used for the data range
*/
        private double GetNiceNum(double range, bool round)
        {
            double exponent; /** exponent of range */
            double fraction; /** fractional part of range */
            double niceFraction; /** nice, rounded fraction */

            exponent = Math.Floor(Math.Log10(range));
            fraction = range / Math.Pow(10, exponent);

            if (round)
            {
                if (fraction < 1.5)
                    niceFraction = 1;
                else if (fraction < 3)
                    niceFraction = 2;
                else if (fraction < 7)
                    niceFraction = 5;
                else
                    niceFraction = 10;
            }
            else
            {
                if (fraction <= 1)
                    niceFraction = 1;
                else if (fraction <= 2)
                    niceFraction = 2;
                else if (fraction <= 5)
                    niceFraction = 5;
                else
                    niceFraction = 10;
            }

            return niceFraction * Math.Pow(10, exponent);
        }

        /**
        * Sets the minimum and maximum data points for the axis.
        *
        * @param minPoint the minimum data point on the axis
        * @param maxPoint the maximum data point on the axis
*/
        public void SetMinMaxPoints(double minPoint, double maxPoint)
        {
            this.minPoint = minPoint;
            this.maxPoint = maxPoint;
            Calculate();
        }

        /**
        * Sets maximum number of tick marks we're comfortable with
        *
        * @param maxTicks the maximum number of tick marks for the axis
*/
        public void SetMaxTicks(double maxTicks)
        {
            this.maxTicks = maxTicks;
            Calculate();
        }

        /**
        * Gets the tick spacing.
        *
        * @return the tick spacing
*/
        public double GetTickSpacing()
        {
            return tickSpacing;
        }

        /**
        * Gets the "nice" minimum data point.
        *
        * @return the new minimum data point for the axis scale
*/
        public double GetNiceMin()
        {
            return niceMin;
        }

        /**
        * Gets the "nice" maximum data point.
        *
        * @return the new maximum data point for the axis scale
*/
        public double GetNiceMax()
        {
            return niceMax;
        }
    }
}

