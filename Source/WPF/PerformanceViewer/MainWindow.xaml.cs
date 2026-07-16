using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Walkabout.PerformanceProvider;
using Walkabout.Utilities;

namespace PerformanceViewer
{
    public partial class MainWindow : Window
    {
        class Segment
        {
            public ComponentId Component;
            public CategoryId Category;
            public MeasurementId Measurement;
            public long Start;
            public long End;
            public ulong Size;
            public double Rate;
        }

        List<Segment> segments = new List<Segment>();
        Dictionary<(int component, int category, int measurement), Stack<long>> beginStacks = new Dictionary<(int, int, int), Stack<long>>();
        Dictionary<int, SolidColorBrush> colorByMeasurement = new Dictionary<int, SolidColorBrush>();
        bool isPanning;
        double rulerHeight = 20;
        double leftLabelWidth = 250;
        double pixelsPerMillisecond = 1;
        Point panStartPoint;
        double panStartOffset;
        PerformanceServer server;
        DelayedActions actions = new DelayedActions();

        public MainWindow()
        {
            UiDispatcher.CurrentDispatcher = this.Dispatcher;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            segments.Clear();
            beginStacks.Clear();
            colorByMeasurement.Clear();
            Redraw();
        }

        internal void OnEventCaptured(int eventId, int component, int category, int measurement, long ticks, ulong size, double rate, DateTime timestamp)
        {
            // called from listener on UI thread
            // payload: eventId 1=Begin,2=End,3=Step,4=Mark
            var key = (component, category, measurement);
            if (eventId == 1) // Begin
            {
                if (!beginStacks.TryGetValue(key, out var stack))
                {
                    stack = new Stack<long>();
                    beginStacks[key] = stack;
                }
                stack.Push(ticks);
            }
            else if (eventId == 2) // End
            {
                if (beginStacks.TryGetValue(key, out var stack) && stack.Count > 0)
                {
                    var start = stack.Pop();
                    var end = ticks;
                    var s = new Segment { Component = (ComponentId)component, Category = (CategoryId)category, Measurement = (MeasurementId)measurement, Start = start, End = end, Size = size, Rate = rate };
                    segments.Add(s);
                    EnsureColor(measurement);
                    RedrawAndAutoScroll();
                }
            }
            else if (eventId == 4) // Mark
            {
                var s = new Segment { Component = (ComponentId)component, Category = (CategoryId)category, Measurement = (MeasurementId)measurement, Start = ticks, End = ticks, Size = size, Rate = rate };
                segments.Add(s);
                EnsureColor(measurement);
                RedrawAndAutoScroll();
            }
        }

        private void EnsureColor(int measurement)
        {
            if (!colorByMeasurement.ContainsKey(measurement))
            {
                var rnd = new Random(measurement ^ 0x123456);
                var color = Color.FromRgb((byte)rnd.Next(64, 230), (byte)rnd.Next(64, 230), (byte)rnd.Next(64, 230));
                colorByMeasurement[measurement] = new SolidColorBrush(color);
            }
        }

        private void RedrawAndAutoScroll()
        {
            actions.StartDelayedAction("Redraw", () =>
            {
                Redraw();
                // auto-scroll to end
                scrollViewer.UpdateLayout();
                double right = Math.Max(0, timelineCanvas.Width - scrollViewer.ViewportWidth);
                scrollViewer.ScrollToHorizontalOffset(right);
            }, TimeSpan.FromMilliseconds(100));
        }

        internal void UpdateServerStatus(bool running, int port)
        {
            txtServerStatus.Text = running ? $"Server listening on {port}" : "Server stopped";
        }

        private void Redraw()
        {
            timelineCanvas.Children.Clear();
            if (segments.Count == 0)
            {
                timelineCanvas.Width = Math.Max(800, this.ActualWidth - 50);
                timelineCanvas.Height = rulerHeight + 80;
                return;
            }

            var groups = segments.GroupBy(s => s.Measurement).OrderBy(g => g.Key).ToList();
            int rows = groups.Count;
            double rowHeight = 24;
            double spacing = 6;

            long min = segments.Min(s => s.Start);
            long max = segments.Max(s => s.End);
            long totalMs = Math.Max(1, (max - min));

            // compute canvas size including ruler and left label area
            timelineCanvas.Height = rulerHeight + rows * (rowHeight + spacing) + 20;
            double contentWidth = leftLabelWidth + totalMs * pixelsPerMillisecond + 200;
            timelineCanvas.Width = Math.Max(this.ActualWidth, contentWidth);

            // draw time ruler
            DrawRuler(min, totalMs);

            for (int row = 0; row < groups.Count; row++)
            {
                var g = groups[row];
                double y = rulerHeight + row * (rowHeight + spacing) + 6;
                // label
                var lbl = new TextBlock { Text = $"Measurement {g.Key}", Foreground = Brushes.Black };
                Canvas.SetLeft(lbl, 4);
                Canvas.SetTop(lbl, y + (rowHeight - 14) / 2);
                timelineCanvas.Children.Add(lbl);

                foreach (var seg in g)
                {
                    double left = leftLabelWidth + (seg.Start - min) * pixelsPerMillisecond;
                    double width = Math.Max(2, (seg.End - seg.Start) * pixelsPerMillisecond);

                    long s = seg.Start - min;
                    long e = seg.End - min;
                    var rect = new Rectangle
                    {
                        Height = rowHeight,
                        Width = width,
                        Fill = colorByMeasurement[(int)seg.Measurement],
                        ToolTip = $"Component={seg.Component}, Category={seg.Category}, Measurement={seg.Measurement}\nStart={s}\nEnd={e}\nDuration={(e - s):F2} ms\nSize={seg.Size}"
                    };
                    Canvas.SetLeft(rect, left);
                    Canvas.SetTop(rect, y);
                    timelineCanvas.Children.Add(rect);
                }
            }
        }

        private void DrawRuler(long min, long totalMs)
        {
            // horizontal baseline
            var baseLine = new Line { X1 = 0, Y1 = rulerHeight - 1, X2 = timelineCanvas.Width, Y2 = rulerHeight - 1, Stroke = Brushes.Gray, StrokeThickness = 1 };
            timelineCanvas.Children.Add(baseLine);

            double desiredPixels = 100; // desired distance between major ticks
            double msStep = GetNiceMsStep(pixelsPerMillisecond, desiredPixels);

            // draw ticks from 0..totalMs
            for (double t = 0; t <= totalMs + msStep; t += msStep)
            {
                double x = leftLabelWidth + t * pixelsPerMillisecond;
                if (x < leftLabelWidth - 1) continue;
                if (x > timelineCanvas.Width) break;
                var tick = new Line { X1 = x, Y1 = rulerHeight - 6, X2 = x, Y2 = rulerHeight - 1, Stroke = Brushes.Black, StrokeThickness = 1 };
                timelineCanvas.Children.Add(tick);

                // label
                var labelTime = TimeSpan.FromMilliseconds(t);
                string label = FormatTimeLabel(labelTime, totalMs);
                var tb = new TextBlock { Text = label, Foreground = Brushes.Black, FontSize = 11 };
                Canvas.SetLeft(tb, x + 2);
                Canvas.SetTop(tb, 2);
                timelineCanvas.Children.Add(tb);
            }
        }

        private string FormatTimeLabel(TimeSpan ts, double totalMs)
        {
            if (totalMs >= 60000)
            {
                // show mm:ss
                return string.Format("{0:D2}:{1:D2}", ts.Minutes + ts.Hours * 60, ts.Seconds);
            }
            else if (totalMs >= 1000)
            {
                return string.Format("{0:F1}s", ts.TotalSeconds);
            }
            else
            {
                return string.Format("{0}ms", (int)ts.TotalMilliseconds);
            }
        }

        private double GetNiceMsStep(double pixelsPerMs, double desiredPixels)
        {
            double desiredMs = Math.Max(1.0, desiredPixels / Math.Max(1e-6, pixelsPerMs));
            double pow = Math.Pow(10, Math.Floor(Math.Log10(desiredMs)));
            double[] bases = { 1, 2, 5 };
            foreach (var b in bases)
            {
                double cand = b * pow;
                if (cand >= desiredMs) return cand;
            }
            return 10 * pow;
        }

        // Panning handlers
        private void TimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isPanning = true;
            panStartPoint = e.GetPosition(scrollViewer);
            panStartOffset = scrollViewer.HorizontalOffset;
            timelineCanvas.CaptureMouse();
            this.Cursor = Cursors.Hand;
            e.Handled = true;
        }

        private void TimelineCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isPanning) return;
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                // release if button released outside
                TimelineCanvas_MouseLeftButtonUp(sender, null);
                return;
            }
            var pos = e.GetPosition(scrollViewer);
            double dx = pos.X - panStartPoint.X;
            double newOffset = panStartOffset - dx;
            double maxOffset = Math.Max(0, timelineCanvas.Width - scrollViewer.ViewportWidth);
            if (double.IsNaN(maxOffset) || double.IsInfinity(maxOffset)) maxOffset = 0;
            newOffset = Math.Max(0, Math.Min(newOffset, maxOffset));
            scrollViewer.ScrollToHorizontalOffset(newOffset);
        }

        private void TimelineCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs? e)
        {
            if (!isPanning) return;
            isPanning = false;
            try { timelineCanvas.ReleaseMouseCapture(); } catch { }
            this.Cursor = Cursors.Arrow;
            if (e != null) e.Handled = true;
        }

        private void TimelineCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (isPanning && e.LeftButton != MouseButtonState.Pressed)
            {
                TimelineCanvas_MouseLeftButtonUp(sender, null);
            }
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CanvasScale != null)
            {
                CanvasScale.ScaleX = e.NewValue;
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            server = new PerformanceServer(this);
            server.Start();
        }
    }
}
