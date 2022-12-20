//-----------------------------------------------------------------------
// <copyright file="ChartControl.xaml.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Listener;

namespace Microsoft.VisualStudio.PerformanceGraph
{
    /// <summary>
    /// Interaction logic for ChartControl.xaml
    /// </summary>
    public partial class ChartControl : UserControl
    {
        private double min, max;
        private List<PerformanceEventArrivedEventArgs> data;
        private List<Point> points;
        private Rect bounds;
        private string units;
        private HoverGesture hover;
        private double unitConversion;

        public ChartControl()
        {
            this.InitializeComponent();
            this.hover = new HoverGesture(this);
            this.hover.Hover += new MouseEventHandler(this.OnHover);
            this.Background = Brushes.White;
        }

        public Legend Legend { get; set; }

        public bool RemoveSpikes { get; set; }

        public double PerformanceFrequency { get; set; }

        public double UnitConversion { get { return this.unitConversion; } }

        public string Units
        {
            get
            {
                return this.units;
            }
            set
            {
                this.units = value;
                this.UpdateLabels();
            }
        }

        double zoom = 1;
        public double Zoom
        {
            get { return this.zoom; }
            set { this.zoom = value; this.zoomToFit = false; this.InvalidateMeasure(); }
        }

        bool zoomToFit = true;
        public bool ZoomToFit
        {
            get { return this.zoomToFit; }
            set { this.zoomToFit = value; this.InvalidateMeasure(); }
        }

        public bool ShowTrendLine
        {
            get { return this.TrendLine.Visibility == System.Windows.Visibility.Visible; }
            set
            {
                this.TrendLine.Visibility = value ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
                this.LineLabel.Visibility = this.TrendLine.Visibility;
            }
        }

        public List<PerformanceEventArrivedEventArgs> Data
        {
            get
            {
                return this.data;
            }
            set
            {
                this.SetLineData(value);
            }
        }

        private void UpdateLabels()
        {
            if (this.data.Count == 0)
            {
                this.Max.Text = string.Empty;
                this.Min.Text = string.Empty;
            }
            else
            {
                this.Max.Text = this.max.ToString("N0") + " " + this.Units;
                this.Min.Text = this.min.ToString("N0");
            }
            this.XMax.Text = this.data.Count.ToString("N0");
        }

        private PathGeometry CreateGeometry(List<Point> data)
        {
            var geometry = new PathGeometry();
            if (data.Count > 0)
            {
                var figure = new PathFigure();
                figure.StartPoint = new Point(data[0].X, data[0].Y);
                foreach (Point p in data.Skip(1))
                {
                    figure.Segments.Add(new LineSegment(new Point(p.X, p.Y), true));
                }
                geometry.Figures.Add(figure);
            }
            return geometry;
        }

        Size renderSize;

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            this.renderSize = sizeInfo.NewSize;
            this.InvalidateArrange();
            base.OnRenderSizeChanged(sizeInfo);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            base.MeasureOverride(constraint);
            if (this.zoomToFit)
            {
                return new Size(0, 0);
            }
            else
            {
                double w = this.bounds.Width;
                if (double.IsNaN(w) || double.IsInfinity(w))
                {
                    w = 0;
                }
                return new Size((w * this.Zoom) + this.YAxis.RenderSize.Width + 20, this.renderSize.Height);
            }
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            this.LayoutGraph(arrangeBounds);
            return base.ArrangeOverride(arrangeBounds);
        }

        List<Point> scaled;
        const double LeftMargin = 10;
        const double TopMargin = 40;

        private void LayoutGraph(Size size)
        {
            PathGeometry path = this.LineGraph.Data as PathGeometry;
            if (path != null && this.data != null && this.points != null)
            {
                Rect bounds = this.bounds;
                double yw = this.YAxis.RenderSize.Width;
                var w = size.Width - yw - 20;
                var h = size.Height - 80;
                if (this.zoomToFit)
                {
                    this.zoom = w / bounds.Width;
                }
                var ymargin = TopMargin;

                if (bounds != Rect.Empty && bounds.Height != 0)
                {
                    TransformGroup g = new TransformGroup();
                    g.Children.Add(new TranslateTransform(0, -bounds.Top));
                    g.Children.Add(new ScaleTransform(this.zoom, -h / bounds.Height));
                    g.Children.Add(new TranslateTransform(LeftMargin, h + ymargin));

                    List<Point> pts = new List<Point>();
                    foreach (Point p in this.points)
                    {
                        Point t = g.Transform(p);
                        pts.Add(t);
                    }
                    this.scaled = pts;

                    this.LineGraph.Data = this.CreateGeometry(pts);

                    Rect newBounds = g.TransformBounds(bounds);
                    this.Border.Width = newBounds.Width;
                    this.Border.Height = newBounds.Height;

                    double xMean = this.Mean(from p in pts select p.X);
                    double yMean = this.Mean(from p in pts select p.Y);
                    double xVariance = this.Variance(from p in pts select p.X);
                    double yVariance = this.Variance(from p in pts select p.Y);
                    double covariance = this.Covariance(pts);

                    double b = covariance / xVariance;
                    double a = yMean - (b * xMean);
                    this.TrendLine.X1 = 0;
                    this.TrendLine.X2 = w;
                    this.TrendLine.Y1 = a;
                    this.TrendLine.Y2 = a + (b * w);

                    // now run on the raw data
                    xMean = this.Mean(from p in this.points select p.X);
                    yMean = this.Mean(from p in this.points select p.Y);
                    xVariance = this.Variance(from p in this.points select p.X);
                    yVariance = this.Variance(from p in this.points select p.Y);
                    covariance = this.Covariance(this.points);

                    double realb = covariance / xVariance;
                    double reala = yMean - (b * xMean);
                    this.LineLabel.Content = string.Format("y = {0} + {1}x", reala.ToString("N0"), realb.ToString("N0"));

                    g = new TransformGroup();
                    g.Children.Add(new RotateTransform(Math.Atan((this.TrendLine.Y2 - this.TrendLine.Y1) / (this.TrendLine.X2 - this.TrendLine.X2))));
                    g.Children.Add(new TranslateTransform(0, a - 20));
                    this.LineLabel.RenderTransform = g;
                }
            }
        }

        public double Variance(IEnumerable<double> values)
        {
            double mean = this.Mean(values);
            double variance = 0;
            foreach (double d in values)
            {
                double diff = d - mean;
                variance += diff * diff;
            }
            return variance;
        }

        public double Mean(IEnumerable<double> values)
        {
            double sum = 0;
            double count = 0;
            foreach (double d in values)
            {
                sum += d;
                count++;
            }
            if (count == 0)
            {
                return 0;
            }
            return sum / count;
        }

        public double Covariance(IEnumerable<Point> pts)
        {
            double xsum = 0;
            double ysum = 0;
            double count = 0;
            foreach (Point d in pts)
            {
                xsum += d.X;
                ysum += d.Y;
                count++;
            }
            if (count == 0)
            {
                return 0;
            }
            double xMean = xsum / count;
            double yMean = ysum / count;
            double covariance = 0;
            foreach (Point d in pts)
            {
                covariance += (d.X - xMean) * (d.Y - yMean);
            }
            return covariance;
        }

        private void SetLineData(List<PerformanceEventArrivedEventArgs> raw)
        {
            List<Point> pts = new List<Point>();
            double x = 0;
            this.min = 0;
            this.max = 0;
            double sum = 0;
            this.data = new List<PerformanceEventArrivedEventArgs>();

            if (raw != null)
            {
                foreach (PerformanceEventArrivedEventArgs e in raw)
                {
                    if (e.EventId == PerformanceData.EndEvent)
                    {
                        this.data.Add(e);
                        this.min = Math.Min(e.Ticks, this.min);
                        this.max = Math.Max(e.Ticks, this.max);
                        pts.Add(new Point(x++, (double)e.Ticks));
                        sum += (double)e.Ticks;
                    }
                }
            }

            if (this.RemoveSpikes && x > 0)
            {
                this.min = 0;
                this.max = 0;
                var filtered = new List<PerformanceEventArrivedEventArgs>();
                double avg = sum / x;
                double stddev = 0;
                double maxdev = 0;
                foreach (Point p in pts)
                {
                    double dev = Math.Abs(p.Y - avg);
                    stddev += dev;
                    maxdev = Math.Max(maxdev, dev);
                }
                stddev /= x;
                x = 0;
                // remove all spikes greater than 3 times the standard deviation.
                double spike = stddev * 3;
                this.max = 0;
                List<Point> flattened = new List<Point>();
                for (int i = 0; i < this.data.Count; i++)
                {
                    Point p = pts[i];
                    double dev = Math.Abs(p.Y - avg);
                    if (dev < spike)
                    {
                        var e = this.data[i];
                        this.min = Math.Min(e.Ticks, this.min);
                        this.max = Math.Max(e.Ticks, this.max);
                        flattened.Add(p);
                        filtered.Add(e);
                        this.max = Math.Max(p.Y, this.max);
                    }
                }
                pts = flattened;
                this.data = filtered;
            }

            if (this.max > 0)
            {
                // now do some auto-scaling
                this.Units = "Ticks";
                this.unitConversion = 1;
                double ticksPerMicrosecond = (double)Stopwatch.Frequency / (double)1000000;
                if (this.max > ticksPerMicrosecond)
                {
                    this.unitConversion = ticksPerMicrosecond;
                    this.Units = "μs";
                }
                double ticksPerMillisecond = (double)Stopwatch.Frequency / (double)1000;
                if (this.max > ticksPerMillisecond)
                {
                    this.unitConversion = ticksPerMillisecond;
                    this.Units = "ms";
                }

                double ticksPerSecond = (double)Stopwatch.Frequency;
                if (this.max > ticksPerSecond)
                {
                    this.unitConversion = ticksPerSecond;
                    this.Units = "s";
                }

                for (int i = 0; i < pts.Count; i++)
                {
                    Point p = pts[i];
                    p.Y = p.Y / this.unitConversion;
                    pts[i] = p;
                }
            }

            this.points = pts;
            if (this.points != null)
            {
                this.min = double.MaxValue;
                this.max = double.MinValue;
                foreach (Point p in this.points)
                {
                    double v = p.Y;
                    if (v < this.min)
                    {
                        this.min = v;
                    }
                    if (v > this.max)
                    {
                        this.max = v;
                    }
                }

                var geometry = this.CreateGeometry(this.points);
                this.LineGraph.Data = geometry;
                this.bounds = geometry.Bounds;
                this.UpdateLabels();
                this.InvalidateArrange();
            }
        }

        static Point CursorHotSpot = new Point(-26, 0);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            this.Connector.Visibility = System.Windows.Visibility.Hidden;
            this.Dot.Visibility = System.Windows.Visibility.Hidden;
            int closest = this.FindClosestPoint(e);
            if (closest >= 0)
            {
                Point p = this.scaled[closest];
                Canvas.SetLeft(this.Dot, p.X - this.Dot.Width / 2);
                Canvas.SetTop(this.Dot, p.Y - this.Dot.Height / 2);
                this.Dot.Visibility = System.Windows.Visibility.Visible;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            this.Connector.Visibility = System.Windows.Visibility.Hidden;
            this.Dot.Visibility = System.Windows.Visibility.Hidden;
            base.OnMouseLeave(e);
        }

        #region Hover Tips

        int FindClosestPoint(MouseEventArgs e)
        {
            int closest = -1;
            if (this.scaled != null)
            {
                Point pos = e.GetPosition(this);
                pos.X += CursorHotSpot.X;
                pos.Y += CursorHotSpot.Y;
                double length = double.MaxValue;
                for (int i = 0; i < this.scaled.Count; i++)
                {
                    Point p = this.scaled[i];
                    Vector v = pos - p;
                    double l = v.Length;
                    if (l < length)
                    {
                        length = l;
                        closest = i;
                    }
                }
            }
            return closest;
        }

        void OnHover(object sender, MouseEventArgs e)
        {
            this.Connector.Visibility = System.Windows.Visibility.Hidden;
            this.Dot.Visibility = System.Windows.Visibility.Hidden;
            if (this.scaled != null)
            {
                int closest = this.FindClosestPoint(e);
                if (closest >= 0)
                {
                    Grid grid = this.GetPopupContent(closest);
                    if (grid != null)
                    {
                        Popup popup = this.hover.CreatePopup(grid);
                        popup.Opened += (sender2, args) =>
                        {
                            try
                            {
                                Point p = this.scaled[closest];
                                Point topleft = new Point(0, 0);
                                topleft = popup.Child.PointToScreen(topleft);
                                topleft = this.PointFromScreen(topleft);

                                this.Connector.X1 = topleft.X;
                                this.Connector.Y1 = topleft.Y;
                                this.Connector.X2 = p.X;
                                this.Connector.Y2 = p.Y;
                                this.Connector.Visibility = System.Windows.Visibility.Visible;

                                Canvas.SetLeft(this.Dot, p.X - this.Dot.Width / 2);
                                Canvas.SetTop(this.Dot, p.Y - this.Dot.Height / 2);
                                this.Dot.Visibility = System.Windows.Visibility.Visible;
                            }
                            catch
                            {
                            }
                        };

                        popup.IsOpen = true;

                    }
                }
            }
        }

        private void Popup_Loaded(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        Grid GetPopupContent(int index)
        {
            Grid content = null;

            if (index < this.data.Count)
            {
                PerformanceEventArrivedEventArgs args = this.data[index];

                content = new Grid();
                content.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
                content.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });

                ulong span = args.Ticks;
                double seconds = (double)span / (double)this.PerformanceFrequency;

                string units = "s"; // seconds;
                if (seconds.ToString("N").StartsWith("0.0"))
                {
                    seconds *= 1000;
                    units = "ms";
                }
                if (seconds.ToString("N").StartsWith("0.0"))
                {
                    seconds *= 1000;
                    units = "μs";
                }

                this.AddRow(content, "Component", args.ComponentName);
                this.AddRow(content, "Category", args.CategoryName);
                this.AddRow(content, "Measurement", args.MeasurementName);
                this.AddRow(content, "Time", seconds.ToString("N3") + units);
            }
            return content;
        }

        void AddRow(Grid grid, string label, string value)
        {
            int row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition());

            int top = row == 0 ? 1 : 0;

            Border b1 = new Border() { BorderBrush = Brushes.LightCoral, BorderThickness = new Thickness(top, 1, 1, 1), Padding = new Thickness(2, 2, 5, 2) };
            Grid.SetRow(b1, row);
            Grid.SetColumn(b1, 0);
            TextBlock col1 = new TextBlock() { Text = label, Margin = new Thickness(0, 0, 10, 0), FontWeight = FontWeights.SemiBold };
            b1.Child = col1;
            grid.Children.Add(b1);

            Border b2 = new Border() { BorderBrush = Brushes.LightCoral, BorderThickness = new Thickness(0, top, 1, 1), Padding = new Thickness(5, 2, 2, 2) };
            Grid.SetRow(b2, row);
            Grid.SetColumn(b2, 1);
            TextBlock col2 = new TextBlock() { Text = value };
            b2.Child = col2;
            grid.Children.Add(b2);
        }
        #endregion
    }
}
