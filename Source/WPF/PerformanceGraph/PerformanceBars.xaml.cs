using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
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
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider.Listener;
using System.Windows.Threading;

namespace Microsoft.VisualStudio.PerformanceGraph
{
    /// <summary>
    /// Interaction logic for PerformanceBars.xaml
    /// </summary>
    public partial class PerformanceBars : UserControl
    {
        double pixelsPerLabel = 50; // 50 pixels per label on the X-Axis.
        long frequency;
        // one tick mark is 1 second or 'frequency' ticks       
        // zoom=2 means twice as many ticks per tick mark (which means we zoomed out).
        // zoom=0.5 means half as many (which means we zoomed in).
        double zoom = 1;
        HoverGesture gesture;

        public PerformanceBars()
        {
            InitializeComponent();
            gesture = new HoverGesture(this);
            gesture.Hover += new MouseEventHandler(OnHover);
        }

        public Legend Legend { get; set; }

        class BeginEndEvent
        {
            public PerformanceEventArrivedEventArgs Begin;
            public PerformanceEventArrivedEventArgs End;
            public Rect Bounds;
        }

        public long PerformanceFrequency
        {
            get
            {
                if (this.frequency == 0) return System.Diagnostics.Stopwatch.Frequency;
                return this.frequency;
            }
            set
            {
                if (this.frequency != value)
                {
                    this.frequency = value;
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        const int BeginEvent = 1;
        const int EndEvent = 2;

        List<PerformanceEventArrivedEventArgs> rawdata;
        List<BeginEndEvent> data;
        long start; // earliest timestamp
        long end; // last timestamp + duration of event.

        public IEnumerable<PerformanceEventArrivedEventArgs> Data
        {
            get { return rawdata; }
            set
            {
                gesture.HidePopup();
                if (value == null)
                {
                    rawdata = new List<PerformanceEventArrivedEventArgs>();
                }
                else
                {
                    rawdata = new List<PerformanceEventArrivedEventArgs>(value);
                }
                FindMatchingPairs();
                PrepareLegend();
                InvalidateMeasure();
                InvalidateArrange();
                InvalidateVisual();
            }
        }

        void FindMatchingPairs()
        {
            data = new List<BeginEndEvent>();
            Dictionary<PerformanceEventArrivedEventArgsKey, PerformanceEventArrivedEventArgs> open = new Dictionary<PerformanceEventArrivedEventArgsKey, PerformanceEventArrivedEventArgs>();
            start = long.MaxValue;
            end = 0;
            foreach (PerformanceEventArrivedEventArgs e in rawdata)
            {
                PerformanceEventArrivedEventArgsKey key = new PerformanceEventArrivedEventArgsKey(e);
                if (e.EventId == BeginEvent)
                {
                    open[key] = e;
                }
                else if (e.EventId == EndEvent)
                {
                    PerformanceEventArrivedEventArgs s;
                    if (open.TryGetValue(key, out s))
                    {
                        if (s.Timestamp < start)
                        {
                            start = s.Timestamp;
                        }
                        if (e.Timestamp > end)
                        {
                            end = e.Timestamp;
                        }                        
                        data.Add(new BeginEndEvent() { Begin = s, End = e });
                    }
                }
            }
        }

        const double HorizontalMargin = 10;
        Size renderSize;

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            renderSize = sizeInfo.NewSize;
            base.OnRenderSizeChanged(sizeInfo);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            base.MeasureOverride(constraint);
            if (ZoomToFit)
            {
                return new Size(0, 0);
            }
            else
            {
                if (data == null || data.Count == 0) return new Size(0, 0);
                
                double ticksPerLabel = (zoom * frequency);
                double scale = pixelsPerLabel / ticksPerLabel;

                double width = (double)(end - start) * scale;
                return new Size((2 * HorizontalMargin) + width, renderSize.Height);
            }
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            InvalidateVisual();
            return base.ArrangeOverride(arrangeBounds);
        }

        public void ZoomIn()
        {
            this.Zoom = ZoomIn(this.zoom);
        }

        private double ZoomIn(double z)
        {
            int digits = GetDigits(z);
            return Math.Round(z / 2, digits, MidpointRounding.ToEven);
        }

        public void ZoomOut()
        {
            this.Zoom = ZoomOut(this.zoom);
        }

        private double ZoomOut(double z)
        {
            string s = (z * 2).ToString();
            s = s.Replace("4", "5");
            return double.Parse(s);
        }

        public double Zoom
        {
            get { return zoom; }
            set
            {
                zoom = value;
                zoomToFit = false;
                InvalidateMeasure();
                InvalidateArrange();
                InvalidateVisual();
            }
        }

        bool zoomToFit = true;
        public bool ZoomToFit
        {
            get { return zoomToFit; }
            set { zoomToFit = value; InvalidateMeasure(); }
        }

        List<Color> colors = new List<Color>(new Color[] {
            Colors.Green, 
            Colors.Blue,
            Colors.Red,
            Colors.Navy,
            Colors.Teal,
            Colors.Violet });

        Random randColors = new Random();

        Color GetColor(int i)
        {
            while (i >= colors.Count)
            {
                colors.Add(Color.FromRgb((byte)randColors.Next(0, 200), (byte)randColors.Next(0, 200), (byte)randColors.Next(0, 200)));
            }
            return colors[i];
        }

        Rect extent = new Rect(0, 0, 0, 0);

        Dictionary<PerformanceEventArrivedEventArgsKey, Tuple<int, Color>> legendColors = new Dictionary<PerformanceEventArrivedEventArgsKey, Tuple<int, Color>>();

        void PrepareLegend()
        {
            Legend.Clear();
            legendColors = new Dictionary<PerformanceEventArrivedEventArgsKey, Tuple<int, Color>>();

            foreach (BeginEndEvent record in data)
            {
                PerformanceEventArrivedEventArgs begin = record.Begin;
                PerformanceEventArrivedEventArgs e = record.End;

                Tuple<int, Color> style;
                PerformanceEventArrivedEventArgsKey key = new PerformanceEventArrivedEventArgsKey(begin);
                if (!legendColors.TryGetValue(key, out style))
                {
                    int i = legendColors.Count;
                    Color color = GetColor(i);
                    legendColors[key] = style = new Tuple<int, Color>(i, color);
                    Legend.AddItem(color, key.Label);
                }
            }
        }


        protected override void OnRender(DrawingContext drawingContext)
        {
            drawingContext.DrawRectangle(Brushes.White, null, new Rect(new Point(0, 0), this.RenderSize));
            
            gesture.HidePopup();
            if (data == null || data.Count == 0) return;

            long span = end - start; // ticks

            double ticksPerLabel = (zoom * frequency);
            double scale = pixelsPerLabel / ticksPerLabel;

            if (ZoomToFit)
            {
                //double labels = (int)(this.ActualWidth / pixelsPerLabel);
                //while (ticksPerLabel * labels > span)
                //{
                //    zoom = ZoomIn(zoom);
                //    actualZoom = 1 / (zoom * frequency);
                //    ticksPerLabel = (pixelsPerLabel * actualZoom);
                //}
                //while (ticksPerLabel * labels < span)
                //{
                //    zoom = ZoomOut(zoom);
                //    actualZoom = 1 / (zoom * frequency);
                //    ticksPerLabel = (pixelsPerLabel * actualZoom);
                //}                
            }

            extent = Rect.Empty;

            double maxy = 0;
            double maxx = 0;

            foreach (BeginEndEvent record in data)
            {
                PerformanceEventArrivedEventArgs begin = record.Begin;
                PerformanceEventArrivedEventArgs e = record.End;

                Brush color = null;
                PerformanceEventArrivedEventArgsKey key = new PerformanceEventArrivedEventArgsKey(begin);
                Tuple<int, Color> style = legendColors[key];
                color = new SolidColorBrush(style.Item2);

                double y = 20 + style.Item1 * 22;
                double x = HorizontalMargin + (double)(begin.Timestamp - start) * (double)scale;
                double w = (double)(e.Timestamp - begin.Timestamp) * (double)scale;
                Rect bounds = new Rect(x, y, w, 20);
                maxy = Math.Max(maxy, y + 20);
                maxx = Math.Max(maxx, x + w);
                drawingContext.DrawRectangle(color, new Pen(color, 1), bounds);
                record.Bounds = bounds;
                extent = Rect.Union(extent, bounds);
            }

            // Draw scale
            Typeface typeface = new Typeface("Segoe UI");
            maxy += 20;
            Pen pen = new Pen(Brushes.Black, 1);
            drawingContext.DrawLine(pen, new Point(HorizontalMargin, maxy - 5), new Point(HorizontalMargin, maxy + 5));
            drawingContext.DrawLine(pen, new Point(maxx, maxy - 5), new Point(maxx, maxy + 5));
            drawingContext.DrawLine(pen, new Point(HorizontalMargin, maxy), new Point(maxx, maxy));

            double unit = 0;
            double unitStep = zoom; // zoom is inverse .
            string units = "s"; // seconds;
            if (unitStep.ToString("N").StartsWith("0.0"))
            {
                unitStep *= 1000;
                units = "ms";
            }
            if (unitStep.ToString("N").StartsWith("0.0"))
            {
                unitStep *= 1000;
                units = "μs";
            }

            int digits = GetDigits(zoom);

            for (double i = 0; i < span; i += ticksPerLabel)
            {
                double x = HorizontalMargin + (i * scale);
                drawingContext.DrawLine(pen, new Point(x, maxy - 5), new Point(x, maxy + 5));
                string label = Math.Round(unit, digits, MidpointRounding.ToEven).ToString();
                if (i == 0)
                {
                    label = units;
                }
                FormattedText ft = new FormattedText(label,
                        CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight,
                        typeface, 10, Brushes.Black);
                drawingContext.DrawText(ft, new Point(x - (ft.Width / 2), maxy + 10));
                unit += unitStep;
            }

        }

        void OnHover(object sender, MouseEventArgs e)
        {
            Point pos = e.GetPosition(this);
            foreach (double inflation in new double[] { 0, 2, 5, 10, 20 })
            {
                foreach (BeginEndEvent record in data)
                {
                    Rect bounds = record.Bounds;
                    bounds.Inflate(inflation, inflation);
                    if (bounds.Contains(pos))
                    {
                        Popup popup = gesture.CreatePopup(GetPopupContent(record));
                        popup.IsOpen = true;
                        return;
                    }
                }
            }
        }

        Grid GetPopupContent(BeginEndEvent evt)
        {
            Grid content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            content.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });

            long span = evt.End.Timestamp - evt.Begin.Timestamp;
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

            AddRow(content, "Component", evt.Begin.ComponentName);
            AddRow(content, "Category", evt.Begin.CategoryName);
            AddRow(content, "Measurement", evt.Begin.MeasurementName);
            AddRow(content, "Time", seconds.ToString("N3") + units);

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

        private static int GetDigits(double z)
        {
            int digits = (int)Math.Log10(10 / z);
            digits = Math.Max(0, digits);
            return digits;
        }
    }
}

