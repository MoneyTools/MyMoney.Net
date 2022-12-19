using System;
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

    public delegate UIElement ToolTipGenerator(ChartDataValue value);

    /// <summary>
    /// Interaction logic for AnimatingBarChart.xaml
    /// </summary>
    public partial class AnimatingBarChart : UserControl
    {
        DelayedActions actions = new DelayedActions();
        ColumnInfo tipColumn;
        Point movePos;
        ColumnInfo inside;
        bool mouseOverAnimationCompleted = false;
        Random rand = new Random(Environment.TickCount);

        class ColumnInfo
        {
            public TextBlock Label;
            public Rect Bounds;
            public Polygon Shape;
            public Color Color;
            public ChartDataValue Data;
        }

        // this is maintained for hit testing only since the mouse events don't seem to be 
        // working on the animated Rectangles.
        List<ColumnInfo> bars = new List<ColumnInfo>();

        List<Polygon> axisLines = new List<Polygon>();
        List<TextBlock> axisLabels = new List<TextBlock>();

        public AnimatingBarChart()
        {
            InitializeComponent();
            this.HoverDelayMilliseconds = 250;
            this.AnimationGrowthMilliseconds = 250;
            this.AnimationRippleMilliseconds = 20;
            this.AnimationColorMilliseconds = 120;
            this.IsVisibleChanged += OnVisibleChanged;
        }

        private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool b && !b)
            {
                HideToolTip();
            }
            OnDelayedUpdate();
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

        public int HoverDelayMilliseconds { get; set; }

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

        private Color GetMouseOverColor(Color c)
        {
            var hls = new HlsColor(c);
            hls.Lighten(0.25f);
            return hls.Color;
        }

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

        public double LineThickness
        {
            get { return (double)GetValue(LineThicknessProperty); }
            set { SetValue(LineThicknessProperty, value); }
        }

        // Using a DependencyProperty as the backing store for LineThickness.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LineThicknessProperty =
            DependencyProperty.Register("LineThickness", typeof(double), typeof(AnimatingBarChart), new PropertyMetadata(0.5, new PropertyChangedCallback(OnLineThicknessChanged)));

        private static void OnLineThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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

        /// <summary>
        /// Note that if there are multiple ChartSeries we assume the X-axis labels are the same across all series.
        /// </summary>
        public ChartData Data
        {
            get { return (ChartData)GetValue(ChartDataProperty); }
            set { SetValue(ChartDataProperty, value); }
        }

        public static readonly DependencyProperty ChartDataProperty =
            DependencyProperty.Register("ChartData", typeof(ChartData), typeof(AnimatingBarChart), new PropertyMetadata(null, OnDataChanged));

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatingBarChart)d).OnDataChanged(e.NewValue);
        }

        private void OnDataChanged(object newValue)
        {
            HideToolTip();
            if (newValue == null)
            {
                ResetVisuals();
            }
            else if (newValue is ChartData data)
            {
                var s = data.Series;
                if (s.Count > 0)
                {
                    var first = s[0].Values;
                    int cols = first.Count;
                    foreach (var series in s)
                    {
                        var seriesDefaultColor = GetRandomColor();
                        if (series.Values.Count != cols)
                        {
                            throw new Exception("All series must have the same number of columns");
                        }
                        for (int i = 0; i < series.Values.Count; i++)
                        {
                            var d = series.Values[i];
                            if (!d.Color.HasValue)
                            {
                                d.Color = seriesDefaultColor;
                            }
                            if (d.Label != first[i].Label)
                            {
                                throw new Exception("All series must have the same label on each column");
                            }
                            if (d.Hidden)
                            {
                                // then we must hide all columns at this index
                                foreach (var t in s)
                                {
                                    t.Values[i].Hidden = true;
                                }
                            }
                        }
                    }
                }
                OnDelayedUpdate();
            }
        }

        void ResetVisuals()
        {
            ChartCanvas.Children.Clear();
            bars.Clear();
            tipColumn = null;
            inside = null;
            mouseOverAnimationCompleted = false;
        }

        private void OnDelayedUpdate()
        {
            actions.StartDelayedAction("update", UpdateChart, TimeSpan.FromMilliseconds(10));
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            OnDelayedUpdate();
            return base.ArrangeOverride(arrangeBounds);
        }

        public event EventHandler<ChartDataValue> ColumnHover;
        public event EventHandler<ChartDataValue> ColumnClicked;


        private void UpdateChart()
        {
            double w = this.ActualWidth;
            double h = this.ActualHeight;
            if (Data == null || Data.Series.Count == 0 || w == 0 || h == 0)
            {
                ResetVisuals();
            }
            else if (this.Visibility == Visibility.Visible)
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
            tipColumn = null;
            OnExitColumn();
            base.OnMouseLeave(e);
        }

        private void OnHover()
        {
            var info = this.tipColumn;
            if (info == null)
            {
                return;
            }

            ChartDataValue value = info.Data;
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
            tip.VerticalOffset = -tip.ActualHeight;

            // notify any interested listeners
            var h = this.ColumnHover;
            if (h != null)
            {
                h(this, value);
            }

        }

        ColumnInfo FindColumn(Point pos)
        {
            for (int i = 0, n = bars.Count; i < n; i++)
            {
                var info = this.bars[i];
                var r = info.Bounds;
                if (pos.X >= r.Left && pos.X <= r.Right)
                {
                    if (pos.Y >= r.Top && pos.Y <= r.Bottom)
                    {
                        // found it!
                        return info;
                    }
                }
            }
            return null;
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            var pos = e.GetPosition(this);
            var info = FindColumn(pos);
            if (info != null)
            {
                OnEnterColumn(info);
                HideToolTip();
                this.movePos = pos;
                this.tipColumn = info;
                actions.StartDelayedAction("hover", () =>
                {
                    OnHover();
                }, TimeSpan.FromMilliseconds(HoverDelayMilliseconds));
            }
            else
            {
                this.tipColumn = null;
                OnExitColumn();
            }
            base.OnPreviewMouseMove(e);
        }

        private void OnEnterColumn(ColumnInfo info)
        {
            if (info != null)
            {
                var color = info.Color;
                Polygon r = info.Shape;
                if (inside == null || r != inside.Shape)
                {
                    if (inside != null)
                    {
                        OnExitColumn();
                    }

                    var duration = new Duration(TimeSpan.FromMilliseconds(AnimationColorMilliseconds));
                    var brush = r.Fill as SolidColorBrush;
                    var highlight = GetMouseOverColor(color);
                    var mouseOverAnimation = new ColorAnimation() { To = highlight, Duration = duration };
                    mouseOverAnimation.Completed += (s, e) =>
                    {
                        this.mouseOverAnimationCompleted = true;
                        if (info != inside)
                        {
                            brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation() { To = color, Duration = duration });
                        }
                    };
                    this.mouseOverAnimationCompleted = false;
                    brush.BeginAnimation(SolidColorBrush.ColorProperty, mouseOverAnimation);
                    inside = info;
                }
            }
        }

        void OnExitColumn()
        {
            if (inside != null && this.mouseOverAnimationCompleted)
            {
                var duration = new Duration(TimeSpan.FromMilliseconds(AnimationColorMilliseconds));
                var brush = inside.Shape.Fill as SolidColorBrush;
                brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation() { To = inside.Color, Duration = duration });
            }
            inside = null;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this);
            var info = FindColumn(pos);
            if (info != null)
            {
                ChartDataValue value = info.Data;
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
            bool firstSeries = true;
            foreach (var series in Data.Series)
            {
                foreach (var item in series.Values)
                {
                    if (item.Hidden)
                    {
                        continue;
                    }
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
                    info.Data = item;
                    info.Color = item.Color.Value;

                    if (firstSeries && !string.IsNullOrEmpty(item.Label))
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
                    else
                    {
                        info.Label = null;
                    }
                    index++;
                }
                firstSeries = false;
            }

            bars.RemoveRange(index, bars.Count - index);

            return minMax;
        }

        /// <summary>
        /// Add the range axis labels.
        /// </summary>
        private Size AddAxisLabels(out AxisTickSpacer scale)
        {
            double maxValue = 0;
            double minValue = 0;
            foreach (var series in Data.Series)
            {
                foreach (var item in series.Values)
                {
                    if (item.Hidden)
                    {
                        continue;
                    }
                    var v = item.Value;
                    maxValue = Math.Max(maxValue, v);
                    minValue = Math.Min(minValue, v);
                }
            }

            Size minMax = new Size();
            scale = new AxisTickSpacer(minValue, maxValue);
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
                    line = new Polygon() { Stroke = this.LineBrush, StrokeThickness = this.LineThickness, Points = new PointCollection() };
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

            int columns = GetVisibleColumns();
            double w = this.ActualWidth;
            double h = this.ActualHeight;

            Size axisLabelSize = AddAxisLabels(out AxisTickSpacer scale);

            var min = scale.GetNiceMin();
            var max = scale.GetNiceMax();
            var spacing = scale.GetTickSpacing();

            Size labelSize = CreateColumnInfos();

            double labelGap = 10;
            double labelMargin = labelSize.Width + labelGap + labelGap;
            if (-min > labelMargin)
            {
                labelMargin = 0;
            }
            w -= labelMargin; // allocate space at the left column labels.
            h -= axisLabelSize.Height + labelGap + labelGap;

            int numSeries = Data.Series.Count;
            double seriesHeight = h / columns;
            double innerGap = numSeries > 1 ? 2 : 0; // gap between columns in a series
            double seriesGap = seriesHeight / (3 * numSeries); // gap between series
            seriesHeight -= seriesGap;

            double columnHeight = seriesHeight / numSeries;
            columnHeight -= innerGap;

            double range = (max - min);
            double zero = 0;
            if (min < 0)
            {
                zero = Math.Abs(min) * w / range;
            }

            // layout the range axis labels and lines
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
            Rect previousLabel = new Rect() { X = -1000, Y = 0, Width = 0, Height = 0 };

            // layout the columns.
            for (int col = 0; col < columns; col++)
            {
                int index = 0;
                foreach (var series in this.Data.Series)
                {
                    var dataValue = series.Values[col];
                    if (dataValue.Hidden)
                    {
                        continue;
                    }
                    double s = (dataValue.Value * w / range);
                    Color color = dataValue.Color.Value;

                    ColumnInfo info = this.bars[col + (index * columns)];
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

                        Rect bounds = new Rect() { X = xpos, Y = y + (seriesHeight - size.Height) / 2, Width = size.Width, Height = size.Height };
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
                    ChartCanvas.Children.Add(polygon);

                    polygon.BeginAnimation(Polygon.PointsProperty, new PointCollectionAnimation() { To = poly, Duration = duration, BeginTime = start });
                    brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation() { To = color, Duration = duration, BeginTime = start });
                    index++;
                    y += innerGap;
                }
                y += seriesGap;
            }
        }

        private int GetVisibleColumns()
        {
            int count = 0;
            if (Data.Series.Count > 0)
            {
                count = (from i in Data.Series[0].Values where !i.Hidden select i).Count();
            }
            return count;
        }

        private void HorizontalLayout()
        {
            ChartCanvas.Children.Clear();

            var duration = new Duration(TimeSpan.FromMilliseconds(this.AnimationGrowthMilliseconds));

            int columns = GetVisibleColumns();
            double w = this.ActualWidth;
            double h = this.ActualHeight;

            Size axisLabelSize = AddAxisLabels(out AxisTickSpacer scale);

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

            int numSeries = Data.Series.Count;
            double seriesWidth = w / columns;
            double innerGap = numSeries > 1 ? 2 : 0; // gap between columns in a series
            double seriesGap = seriesWidth / (3 * numSeries); // gap between series
            seriesWidth -= seriesGap;

            double columnWidth = seriesWidth / numSeries;
            columnWidth -= innerGap;

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
                double ypos = (h - zero) - (r * h / range);
                var label = axisLabels[i];
                var line = axisLines[i];
                var mid = label.DesiredSize.Height / 2;
                Canvas.SetLeft(label, labelGap);
                Canvas.SetTop(label, ypos - mid);

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
            double x = axisLabelGap;
            double y = h - zero;

            // layout the columns.
            for (int col = 0; col < columns; col++)
            {
                int index = 0;
                foreach (var series in this.Data.Series)
                {
                    var dataValue = series.Values[col];
                    if (dataValue.Hidden)
                    {
                        continue;
                    }
                    double s = (dataValue.Value * h / range);
                    Color color = dataValue.Color.Value;

                    var start = TimeSpan.FromMilliseconds(index * AnimationRippleMilliseconds);
                    ColumnInfo info = this.bars[col + (index * columns)];
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

                        Rect bounds = new Rect() { X = x + (seriesWidth - size.Width) / 2, Y = ypos, Width = size.Width, Height = size.Height };
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
                    poly.Add(new Point() { X = x, Y = y - s });
                    poly.Add(new Point() { X = x, Y = y });

                    ChartCanvas.Children.Add(polygon);
                    polygon.BeginAnimation(Polygon.PointsProperty, new PointCollectionAnimation() { To = poly, Duration = duration, BeginTime = start });
                    brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation() { To = color, Duration = duration, BeginTime = start });
                    index++;
                    x += innerGap;
                }

                x += seriesGap;
            }
        }

        private Color GetRandomColor()
        {
            return Color.FromRgb((byte)rand.Next(80, 200), (byte)rand.Next(80, 200), (byte)rand.Next(80, 200));
        }

    }
}

