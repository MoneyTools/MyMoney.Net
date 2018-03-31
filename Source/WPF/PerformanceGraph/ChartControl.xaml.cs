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
            InitializeComponent();
            hover = new HoverGesture(this);
            hover.Hover += new MouseEventHandler(OnHover);
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
                return units;
            }
            set
            {
                units = value;
                UpdateLabels();
            }
        }

        double zoom = 1; 
        public double Zoom
        {
            get { return zoom; }
            set { zoom = value; zoomToFit = false;  InvalidateMeasure(); }
        }

        bool zoomToFit = true;
        public bool ZoomToFit
        {
            get { return zoomToFit; }
            set { zoomToFit = value; InvalidateMeasure(); }
        }

        public bool ShowTrendLine
        {
            get { return TrendLine.Visibility == System.Windows.Visibility.Visible; }
            set
            {
                TrendLine.Visibility = value ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
                LineLabel.Visibility = TrendLine.Visibility;
            }
        }

        public List<PerformanceEventArrivedEventArgs> Data
        {
            get
            {
                return data;
            }
            set
            {
                SetLineData(value);                
            }
        }

        private void UpdateLabels()
        {
            if (data.Count == 0)
            {
                Max.Text = string.Empty;
                Min.Text = string.Empty;
            }
            else
            {
                Max.Text = max.ToString("N0") + " " + Units;
                Min.Text = min.ToString("N0");
            }
            XMax.Text = data.Count.ToString("N0");
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
            renderSize = sizeInfo.NewSize;
            InvalidateArrange();
            base.OnRenderSizeChanged(sizeInfo);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            base.MeasureOverride(constraint);
            if (zoomToFit)
            {
                return new Size(0,0);
            }
            else
            {
                double w = this.bounds.Width;
                if (double.IsNaN(w) || double.IsInfinity(w))
                {
                    w = 0;
                }
                return new Size((w * Zoom) + YAxis.RenderSize.Width + 20, renderSize.Height);
            }
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            LayoutGraph(arrangeBounds);
            return base.ArrangeOverride(arrangeBounds);
        }

        List<Point> scaled;
        const double LeftMargin = 10;
        const double TopMargin = 40;

        private void LayoutGraph(Size size)
        {
            PathGeometry path = LineGraph.Data as PathGeometry;
            if (path != null && data != null && points != null)
            {
                Rect bounds = this.bounds;
                double yw = YAxis.RenderSize.Width;
                var w = size.Width - yw - 20;
                var h = size.Height - 80;
                if (zoomToFit)
                {
                    zoom = w / bounds.Width;
                }
                var ymargin = TopMargin;

                if (bounds != Rect.Empty && bounds.Height != 0)
                {
                    TransformGroup g = new TransformGroup();
                    g.Children.Add(new TranslateTransform(0, -bounds.Top));
                    g.Children.Add(new ScaleTransform(zoom, -h / bounds.Height));
                    g.Children.Add(new TranslateTransform(LeftMargin, h + ymargin));

                    List<Point> pts = new List<Point>();
                    foreach (Point p in points)
                    {
                        Point t = g.Transform(p);
                        pts.Add(t);
                    }
                    scaled = pts;

                    LineGraph.Data = CreateGeometry(pts);

                    Rect newBounds = g.TransformBounds(bounds);
                    Border.Width = newBounds.Width;
                    Border.Height = newBounds.Height;

                    double xMean = Mean(from p in pts select p.X);
                    double yMean = Mean(from p in pts select p.Y);
                    double xVariance = Variance(from p in pts select p.X);
                    double yVariance = Variance(from p in pts select p.Y);
                    double covariance = Covariance(pts);

                    double b = covariance / xVariance;
                    double a = yMean - (b * xMean);
                    TrendLine.X1 = 0;
                    TrendLine.X2 = w;
                    TrendLine.Y1 = a;
                    TrendLine.Y2 = a + (b * w);

                    // now run on the raw data
                    xMean = Mean(from p in points select p.X);
                    yMean = Mean(from p in points select p.Y);
                    xVariance = Variance(from p in points select p.X);
                    yVariance = Variance(from p in points select p.Y);
                    covariance = Covariance(points);

                    double realb = covariance / xVariance;
                    double reala = yMean - (b * xMean);
                    LineLabel.Content = string.Format("y = {0} + {1}x", reala.ToString("N0"), realb.ToString("N0"));

                    g = new TransformGroup();
                    g.Children.Add(new RotateTransform(Math.Atan((TrendLine.Y2 - TrendLine.Y1) / (TrendLine.X2 - TrendLine.X2))));
                    g.Children.Add(new TranslateTransform(0, a - 20));
                    LineLabel.RenderTransform = g;
                }
            }
        }

        public double Variance(IEnumerable<double> values)
        {
            double mean = Mean(values);
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
                        data.Add(e);
                        min = Math.Min(e.Ticks, min);
                        max = Math.Max(e.Ticks, max);
                        pts.Add(new Point(x++, (double)e.Ticks));
                        sum += (double)e.Ticks;
                    }
                }
            }

            if (RemoveSpikes && x > 0)
            {
                min = 0;
                max = 0;
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
                max = 0;
                List<Point> flattened = new List<Point>();
                for (int i = 0; i < data.Count; i++)
                {
                    Point p = pts[i];
                    double dev = Math.Abs(p.Y - avg);
                    if (dev < spike)
                    {
                        var e = data[i];
                        min = Math.Min(e.Ticks, min);
                        max = Math.Max(e.Ticks, max);
                        flattened.Add(p);
                        filtered.Add(e);
                        max = Math.Max(p.Y, max);
                    }
                }
                pts = flattened;
                this.data = filtered;
            }

            if (max > 0)
            {
                // now do some auto-scaling
                Units = "Ticks";
                unitConversion = 1;
                double ticksPerMicrosecond = (double)Stopwatch.Frequency / (double)1000000;
                if (max > ticksPerMicrosecond)
                {
                    unitConversion = ticksPerMicrosecond;
                    Units = "μs";
                }
                double ticksPerMillisecond = (double)Stopwatch.Frequency / (double)1000;
                if (max > ticksPerMillisecond)
                {
                    unitConversion = ticksPerMillisecond;
                    Units = "ms";
                }

                double ticksPerSecond = (double)Stopwatch.Frequency;
                if (max > ticksPerSecond)
                {
                    unitConversion = ticksPerSecond;
                    Units = "s";
                }

                for (int i = 0; i < pts.Count; i++)
                {
                    Point p = pts[i];
                    p.Y = p.Y / unitConversion;
                    pts[i] = p;
                }
            }

            this.points = pts;
            if (points != null)
            {
                min = double.MaxValue;
                max = double.MinValue;
                foreach (Point p in points)
                {
                    double v = p.Y;
                    if (v < min)
                    {
                        min = v;
                    }
                    if (v > max)
                    {
                        max = v;
                    }
                }

                var geometry = CreateGeometry(points);
                LineGraph.Data = geometry;
                bounds = geometry.Bounds;
                UpdateLabels();
                InvalidateArrange();
            } 
        }

        static Point CursorHotSpot = new Point(-26, 0);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Connector.Visibility = System.Windows.Visibility.Hidden;
            Dot.Visibility = System.Windows.Visibility.Hidden;
            int closest = FindClosestPoint(e);
            if (closest >= 0)
            {
                Point p = scaled[closest];
                Canvas.SetLeft(Dot, p.X - Dot.Width / 2);
                Canvas.SetTop(Dot, p.Y - Dot.Height / 2);
                Dot.Visibility = System.Windows.Visibility.Visible;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            Connector.Visibility = System.Windows.Visibility.Hidden;
            Dot.Visibility = System.Windows.Visibility.Hidden;
            base.OnMouseLeave(e);
        }

        #region Hover Tips

        int FindClosestPoint(MouseEventArgs e)
        {
            int closest = -1;
            if (scaled != null)
            {
                Point pos = e.GetPosition(this);
                pos.X += CursorHotSpot.X;
                pos.Y += CursorHotSpot.Y;
                double length = double.MaxValue;
                for (int i = 0; i < scaled.Count; i++)
                {
                    Point p = scaled[i];
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
            Connector.Visibility = System.Windows.Visibility.Hidden;
            Dot.Visibility = System.Windows.Visibility.Hidden;
            if (scaled != null)
            {
                int closest = FindClosestPoint(e);
                if (closest >= 0)
                {
                    Grid grid = GetPopupContent(closest);
                    if (grid != null)
                    {
                        Popup popup = hover.ShowPopup(grid);
                        Dispatcher.BeginInvoke(new Action(() => {
                            try
                            {
                                Point p = scaled[closest];
                                Point topleft = new Point(0, 0);
                                topleft = popup.Child.PointToScreen(topleft);
                                topleft = this.PointFromScreen(topleft);

                                Connector.X1 = topleft.X;
                                Connector.Y1 = topleft.Y;
                                Connector.X2 = p.X;
                                Connector.Y2 = p.Y;
                                Connector.Visibility = System.Windows.Visibility.Visible;

                                Canvas.SetLeft(Dot, p.X - Dot.Width / 2);
                                Canvas.SetTop(Dot, p.Y - Dot.Height / 2);
                                Dot.Visibility = System.Windows.Visibility.Visible;
                            }
                            catch
                            {
                            }
                        }), System.Windows.Threading.DispatcherPriority.Background);
                        
                    }
                }
            }
        }


        Grid GetPopupContent(int index)
        {
            Grid content = null;

            if (index < data.Count)
            {
                PerformanceEventArrivedEventArgs args = data[index];

                content = new Grid();
                content.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
                content.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });

                ulong span = args.Ticks;
                double seconds = (double)span / (double)PerformanceFrequency;

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

                AddRow(content, "Component", args.ComponentName);
                AddRow(content, "Category", args.CategoryName);
                AddRow(content, "Measurement", args.MeasurementName);
                AddRow(content, "Time", seconds.ToString("N3") + units);
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
