using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace UnoApp1.Presentation;

public delegate UIElement ToolTipGenerator(ChartDataValue value);

public sealed partial class AnimatingBarChart : UserControl
{

    private readonly DelayedActions actions = new DelayedActions();
    private ChartDataValue tipColumn;
    private Point movePos;
    private ColumnInfo inside;
    private bool mouseOverAnimationCompleted = false;
    private readonly Random rand = new Random(Environment.TickCount);


    private class ColumnInfo
    {
        public TextBlock Label;
        public Rect Bounds;
        public StackedBar Shape;
        public int ColumnGroup;
    }

    // this is maintained for hit testing only since the mouse events don't seem to be 
    // working on the animated Rectangles.
    private readonly List<ColumnInfo> bars = new List<ColumnInfo>();
    private readonly List<Polygon> axisLines = new List<Polygon>();
    private readonly List<TextBlock> axisLabels = new List<TextBlock>();

    public AnimatingBarChart()
    {
        this.InitializeComponent();
        this.HoverDelayMilliseconds = 250;
        this.AnimationGrowthMilliseconds = 250;
        this.AnimationRippleMilliseconds = 20;
        this.AnimationColorMilliseconds = 120;
        //IsVisibleChanged += this.OnVisibleChanged;
        RegisterPropertyChangedCallback(
            VisibilityProperty,
            new DependencyPropertyChangedCallback((s, e) =>
            {
                this.OnVisibleChanged();
            }));

        // useful for debugging layout!
        // this.BorderBrush = Brushes.Yellow;
        // this.BorderThickness = new Thickness(1.0);
        this.BorderThickness = new Thickness(0);
    }

    private void OnVisibleChanged()
    {
        if (this.Visibility != Visibility.Visible)
        {
            this.HideToolTip();
        }
        this.OnDelayedUpdate();
    }

    private void HideToolTip()
    {
        ToolTipService.SetToolTip(this, null);
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
        get { return (Brush)this.GetValue(LineBrushProperty); }
        set { this.SetValue(LineBrushProperty, value); }
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
        get { return (double)this.GetValue(LineThicknessProperty); }
        set { this.SetValue(LineThicknessProperty, value); }
    }

    public bool Stacked
    {
        get { return (bool)this.GetValue(StackedProperty); }
        set { this.SetValue(StackedProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Stacked.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty StackedProperty =
        DependencyProperty.Register("Stacked", typeof(bool), typeof(AnimatingBarChart), new PropertyMetadata(false, OnStackedChanged));

    private static void OnStackedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AnimatingBarChart)d).OnDelayedUpdate();
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
        get { return (Orientation)this.GetValue(OrientationProperty); }
        set { this.SetValue(OrientationProperty, value); }
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
        get { return (ChartData)this.GetValue(ChartDataProperty); }
        set { this.SetValue(ChartDataProperty, value); }
    }

    public static readonly DependencyProperty ChartDataProperty =
        DependencyProperty.Register("ChartData", typeof(ChartData), typeof(AnimatingBarChart), new PropertyMetadata(null, OnDataChanged));

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AnimatingBarChart)d).OnDataChanged(e.NewValue);
    }

    private void OnDataChanged(object newValue)
    {
        this.HideToolTip();
        if (newValue == null)
        {
            this.ResetVisuals();
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
                    var seriesDefaultColor = this.GetRandomColor();
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
            this.OnDelayedUpdate();
        }
    }

    private void ResetVisuals()
    {
        this.ChartCanvas.Children.Clear();
        this.bars.Clear();
        this.tipColumn = null;
        this.inside = null;
        this.mouseOverAnimationCompleted = false;
    }

    internal void OnDelayedUpdate()
    {
        this.actions.StartDelayedAction("update", this.UpdateChart, TimeSpan.FromMilliseconds(10));
    }

    Size previousBounds;

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        if (this.previousBounds != arrangeBounds)
        {
            this.previousBounds = arrangeBounds;
            this.OnDelayedUpdate();
        }
        return base.ArrangeOverride(arrangeBounds);
    }

    public event EventHandler<ChartDataValue> ColumnHover;
    public event EventHandler<ChartDataValue> ColumnClicked;


    private void UpdateChart()
    {
        double w = this.ActualWidth;
        double h = this.ActualHeight;
        if (this.Data == null || this.Data.Series.Count == 0 || w == 0 || h == 0)
        {
            this.ResetVisuals();
        }
        else if (this.Visibility == Visibility.Visible)
        {
            if (this.Orientation == Orientation.Horizontal)
            {
                this.HorizontalLayout();
            }
            else
            {
                this.VerticalLayout();
            }
        }
    }

    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        this.HideToolTip();
        this.actions.CancelDelayedAction("hover");
        this.tipColumn = null;
        base.OnPointerExited(e);
    }

    private void OnHover()
    {
        ChartDataValue value = this.tipColumn;
        if (value == null)
        {
            return;
        }

        var content = this.ToolTipGenerator != null ? this.ToolTipGenerator(value) : new TextBlock() { Text = value.Label + "\r\n" + value.Value };
        var tip = new ToolTip()
        {
            Placement = PlacementMode.Mouse,
            Content = content,
            IsOpen = true
        };

        tip.Measure(new Size(100, 100));
        tip.HorizontalOffset = 0;
        tip.VerticalOffset = -tip.ActualHeight;

        ToolTipService.SetToolTip(this, tip);
        ToolTipService.SetPlacement(tip, PlacementMode.Mouse);        

        // notify any interested listeners
        var h = ColumnHover;
        if (h != null)
        {
            h(this, value);
        }
    }

    private ColumnInfo FindColumn(Point pos)
    {
        pos.X -= this.Padding.Left;
        pos.Y -= this.Padding.Top;
        for (int i = 0, n = this.bars.Count; i < n; i++)
        {
            var info = this.bars[i];
            var r = info.Bounds;
            if (r.Contains(pos))
            {
                // found it!
                return info;
            }
        }
        return null;
    }

    protected override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(this).Position;
        var info = this.FindColumn(pos);
        if (info != null)
        {
            this.HideToolTip();
            this.movePos = pos;
            var userData = (ChartDataValue)info.Shape.HitBarSegment(e.GetCurrentPoint(info.Shape).Position);
            this.tipColumn = userData;
            this.actions.StartDelayedAction("hover", () =>
            {
                this.OnHover();
            }, TimeSpan.FromMilliseconds(this.HoverDelayMilliseconds));
        }
        else
        {
            this.tipColumn = null;
        }
        base.OnPointerMoved(e);
    }

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(this).Position;
        var info = this.FindColumn(pos);
        if (info != null)
        {
            var data = (ChartDataValue)info.Shape.HitBarSegment(e.GetCurrentPoint(info.Shape).Position);
            if (ColumnClicked != null && data != null)
            {
                ColumnClicked(this, data);
            }
        }
        base.OnPointerPressed(e);
    }

    private Size CreateColumnInfos()
    {
        Size minMax = new Size();
        bool firstSeries = true;
        int numSeries = this.Data.Series.Count;

        // compute how many bars are needed (depends on this.Stacked).
        int barCount = 0;
        foreach (var series in this.Data.Series)
        {
            lock (series.Values)
            {
                int count = 0;
                foreach (var item in series.Values)
                {
                    if (item.Hidden)
                    {
                        continue;
                    }
                    if (item.Series == null)
                    {
                        item.Series = series;
                    }
                    count++;
                    if (!this.Stacked)
                    {
                        barCount++;
                    }
                }

                if (this.Stacked)
                {
                    barCount = Math.Max(barCount, count);
                }
            }
        }

        // Create or reset ColumnInfo for the bars.
        for (int i = 0; i < barCount; i++)
        {
            // Make sure bar exists
            ColumnInfo info = null;
            if (i < this.bars.Count)
            {
                info = this.bars[i];
                info.Shape.ClearSegments();
            }
            else
            {
                info = new ColumnInfo();
                info.Shape = new StackedBar();
                this.bars.Add(info);
            }
        }

        // incremental update, remove any excess bars.
        this.bars.RemoveRange(barCount, this.bars.Count - barCount);

        int seriesCount = this.Data.Series.Count;
        int seriesIndex = 0;
        foreach (var series in this.Data.Series)
        {
            lock (series.Values)
            {
                int columnIndex = 0;
                foreach (var item in series.Values)
                {
                    if (item.Hidden)
                    {
                        continue;
                    }

                    int index = this.Stacked ? columnIndex : seriesIndex + seriesCount * columnIndex;
                    ColumnInfo info = this.bars[index];
                    info.ColumnGroup = columnIndex;
                    info.Shape.AddSegment(item.Value, item.Color.Value, item);

                    if (firstSeries)
                    {
                        var block = info.Label;
                        if (block == null)
                        {
                            block = new TextBlock();
                            info.Label = block;
                        }
                        block.Foreground = this.Foreground;
                        block.Text = "" + item.Label;
                        block.ClearAnimation("Opacity");
                        block.Opacity = 0;
                        this.ChartCanvas.Children.Add(block); // so it measures properly.
                        block.Measure(new Size(100, 100));
                        this.ChartCanvas.Children.Remove(block);
                        var size = block.DesiredSize;
                        minMax.Width = Math.Max(minMax.Width, size.Width);
                        minMax.Height = Math.Max(minMax.Height, size.Height);
                    }
                    else if (!this.Stacked)
                    {
                        info.Label = null;
                    }
                    columnIndex++;
                }
            }
            firstSeries = false;
            seriesIndex++;
        }

        return minMax;
    }

    /// <summary>
    /// Add the range axis labels.
    /// </summary>
    private Size AddAxisLabels(out AxisTickSpacer scale)
    {
        double maxValue = 0;
        double minValue = 0;

        foreach (var info in this.bars)
        {
            var v = info.Shape.GetSegmentLength();
            maxValue = Math.Max(maxValue, v);
            minValue = Math.Min(minValue, v);
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
            if (i < this.axisLabels.Count)
            {
                label = this.axisLabels[i];
                line = this.axisLines[i];
            }
            else
            {
                label = new TextBlock();
                this.axisLabels.Add(label);
                line = new Polygon() { Stroke = this.LineBrush, StrokeThickness = this.LineThickness, Points = new PointCollection() };
                this.axisLines.Add(line);
            }
            this.ChartCanvas.Children.Add(line);

            label.Foreground = this.Foreground;
            label.Text = r.ToString("N0");
            this.ChartCanvas.Children.Add(label);
            label.Measure(new Size(100, 100));
            minMax.Width = Math.Max(minMax.Width, label.DesiredSize.Width);
            minMax.Height = Math.Max(minMax.Height, label.DesiredSize.Height);
            i++;
        }

        this.axisLabels.RemoveRange(i, this.axisLabels.Count - i);
        this.axisLines.RemoveRange(i, this.axisLines.Count - i);

        return minMax;
    }

    private void VerticalLayout()
    {
        this.ChartCanvas.Children.Clear();

        var duration = new Duration(TimeSpan.FromMilliseconds(this.AnimationGrowthMilliseconds));

        double w = this.ActualWidth;
        double h = this.ActualHeight;

        Size labelSize = this.CreateColumnInfos();
        int columns = this.GetVisibleColumns();

        Size axisLabelSize = this.AddAxisLabels(out AxisTickSpacer scale);
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

        int numSeries = this.Data.Series.Count;
        double seriesHeight = h / columns;
        double innerGap = numSeries > 1 ? 2 : 0; // gap between columns in a series
        double seriesGap = seriesHeight / (3 * numSeries); // gap between series
        seriesHeight -= seriesGap;

        double columnHeight = seriesHeight / numSeries;
        columnHeight -= innerGap;

        if (this.Stacked)
        {
            seriesHeight = h / columns;
            columnHeight = seriesHeight;
            seriesGap = columnHeight / 4;
            seriesHeight -= seriesGap;
            innerGap = 2;
            columnHeight -= seriesGap;
        }

        double range = max - min;
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
            var label = this.axisLabels[i];
            var line = this.axisLines[i];
            var mid = label.DesiredSize.Width / 2;
            Canvas.SetLeft(label, xpos > mid ? xpos - mid : xpos + labelGap);
            Canvas.SetTop(label, h + labelGap);

            PointCollection poly = new PointCollection();
            poly.Add(new Point() { X = xpos, Y = 0 });
            poly.Add(new Point() { X = xpos, Y = h });

            Canvas.SetLeft(line, xpos);
            Canvas.SetTop(line, 0);
                
            var animation = new PointCollectionAnimation(line, "Points") { To = poly };
            animation.BeginAnimation(duration, TimeSpan.FromSeconds(0));
            line.Tag = animation;
            //line.BeginAnimation(Polygon.PointsProperty, new PointCollectionAnimation() { To = poly, Duration = duration });

            label.BeginAnimation(new DoubleAnimation()
            {
                From = 0,
                To = 1,
                Duration = duration
            }, "Opacity");
            i++;
        }

        int index = 0;
        double y = 0;
        double x = labelMargin + zero;
        Rect previousLabel = new Rect() { X = -1000, Y = 0, Width = 0, Height = 0 };

        // layout the columns.
        // layout the columns.
        foreach (var info in this.bars)
        {
            if (info.ColumnGroup != index)
            {
                index = info.ColumnGroup;
                y += seriesGap;
            }

            double length = info.Shape.GetSegmentLength();
            double s = length * w / range;

            var start = TimeSpan.FromMilliseconds(index * this.AnimationRippleMilliseconds);
            var bar = info.Shape;
            bar.Orientation = Orientation.Horizontal;

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

                Rect bounds = new Rect() { X = xpos, Y = y + ((seriesHeight - size.Height) / 2), Width = size.Width, Height = size.Height };
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

                    block.BeginAnimation(new DoubleAnimation()
                    {
                        From = 0,
                        To = 1,
                        Duration = duration,
                        BeginTime = start
                    }, "Opacity");

                    this.ChartCanvas.Children.Add(block);
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
            poly.Add(new Point() { X = 0, Y = 0 });
            poly.Add(new Point() { X = s, Y = 0, });
            poly.Add(new Point() { X = s, Y = columnHeight });
            poly.Add(new Point() { X = 0, Y = columnHeight, });
            this.ChartCanvas.Children.Add(bar);
            bar.Width = w;
            bar.Height = columnHeight;

            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, y);
            bar.AnimateBar(poly, duration, start);
            //bar.BeginAnimation(StackedBar.PointsProperty, new PointCollectionAnimation() { To = poly, Duration = duration, BeginTime = start });
            index++;
            y += columnHeight;
            y += innerGap;
        }
    }

    private int GetVisibleColumns()
    {
        // Get number of visible items in each series
        // which is the number of column groups really, not total number of bars.
        int count = 0;
        if (this.Data.Series.Count > 0)
        {
            count = (from i in this.Data.Series[0].Values where !i.Hidden select i).Count();
        }
        return count;
    }

    private void HorizontalLayout()
    {
        this.ChartCanvas.Children.Clear();

        var duration = new Duration(TimeSpan.FromMilliseconds(this.AnimationGrowthMilliseconds));

        double w = this.ActualWidth;
        double h = this.ActualHeight;

        Size labelSize = this.CreateColumnInfos();
        int columns = this.GetVisibleColumns();

        Size axisLabelSize = this.AddAxisLabels(out AxisTickSpacer scale);
        var min = scale.GetNiceMin();
        var max = scale.GetNiceMax();
        var spacing = scale.GetTickSpacing();

        double labelGap = this.FontSize / 3;
        double labelMargin = labelSize.Height + labelGap + labelGap;
        if (-min > labelMargin)
        {
            labelMargin = 0;
        }
        h -= labelMargin; // allocate space at the bottom for column labels.
        double axisLabelGap = axisLabelSize.Width + labelGap + labelGap;
        w -= axisLabelGap; // allocate space for axis labels.

        int numSeries = this.Data.Series.Count;
        double seriesWidth = w / columns;
        double innerGap = numSeries > 1 ? 2 : 0; // gap between columns in a series
        double seriesGap = seriesWidth / (4 * numSeries); // gap between series
        seriesWidth -= seriesGap;

        double columnWidth = seriesWidth / numSeries;
        if (this.Stacked)
        {
            seriesWidth = (w / columns);
            columnWidth = seriesWidth;
            seriesGap = columnWidth / 4;
            seriesWidth -= seriesGap;
            innerGap = 2;
            columnWidth -= seriesGap;
        }
        columnWidth -= innerGap;

        double range = max - min;
        double zero = 0;
        if (min < 0)
        {
            zero = Math.Abs(min) * h / range;
        }

        // layout the axis labels and lines
        int i = 0;
        for (var r = min; r <= max; r += spacing)
        {
            double ypos = h - zero - (r * h / range);
            var label = this.axisLabels[i];
            var line = this.axisLines[i];
            var mid = label.DesiredSize.Height / 2;
            Canvas.SetLeft(label, labelGap);
            Canvas.SetTop(label, ypos - mid);

            PointCollection poly = new PointCollection();
            poly.Add(new Point() { X = 0, Y = 0 });
            poly.Add(new Point() { X = w, Y = 0 });
            Canvas.SetLeft(line, axisLabelGap);
            Canvas.SetTop(line, ypos);
            var animation = new PointCollectionAnimation(line, "Points") { To = poly };
            animation.BeginAnimation(duration, TimeSpan.FromSeconds(0));
            line.Tag = animation;
            ///line.BeginAnimation(Polygon.PointsProperty, new PointCollectionAnimation() { To = poly, Duration = duration });

            label.BeginAnimation(new DoubleAnimation()
            {
                From = 0,
                To = 1,
                Duration = duration
            }, "Opacity");

            i++;
        }

        Rect previousLabel = new Rect() { X = -1000, Y = 0, Width = 0, Height = 0 };
        double x = axisLabelGap;
        double y = h - zero;

        int index = 0;

        // layout the columns.
        foreach (var info in this.bars)
        {
            if (info.ColumnGroup != index)
            {
                index = info.ColumnGroup;
                x += seriesGap;
            }

            var length = info.Shape.GetSegmentLength();
            double s = length * h / range;

            var start = TimeSpan.FromMilliseconds(index * this.AnimationRippleMilliseconds);
            var bar = info.Shape;
            bar.Orientation = Orientation.Vertical;
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

                Rect bounds = new Rect() { X = x + ((seriesWidth - size.Width) / 2), Y = ypos, Width = size.Width, Height = size.Height };
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

                    var animation = new DoubleAnimation()
                    {
                        From = 0,
                        To = 1,
                        Duration = duration,
                        BeginTime = start
                    };
                    block.BeginAnimation(animation, "Opacity");
                    this.ChartCanvas.Children.Add(block);
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
            poly.Add(new Point() { X = 0, Y = 0 });
            poly.Add(new Point() { X = 0, Y = s });
            poly.Add(new Point() { X = columnWidth, Y = s });
            poly.Add(new Point() { X = columnWidth, Y = 0 });
            bar.Width = columnWidth;
            bar.Height = h;
            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, y - s);
            this.ChartCanvas.Children.Add(bar);
            bar.AnimateBar(poly, duration, start);
            x += columnWidth;
            x += innerGap;
        }
    }

    private Color GetRandomColor()
    {
        return Extensions.FromRgb((byte)this.rand.Next(80, 200), (byte)this.rand.Next(80, 200), (byte)this.rand.Next(80, 200));
    }
}
