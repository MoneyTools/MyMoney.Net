using LovettSoftware.Charts;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Walkabout.Charts;
using Walkabout.Utilities;
using Walkabout.Controls;
using System;
using System.Diagnostics;

namespace Walkabout.Tests
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        StackedBar bar;
        AnimatingBarChart chart;

        public MainWindow()
        {
            UiDispatcher.CurrentDispatcher = this.Dispatcher;

            InitializeComponent();
        }

        private void Clear()
        {
            this.RootGrid.Children.Clear();
            this.bar = null;
            this.chart = null;
        }

        private void OnTestChart(object sender, RoutedEventArgs e)
        {
            this.Clear();
            var colors = new Color[] {
                Colors.Green,
                Colors.LightGreen,
                Colors.SeaGreen
            };

            var series = new List<ChartDataSeries>();

            CsvDocument doc;
            var data = GetEmbeddedResource("Walkabout.Tests.ChartData.csv");
            using (var reader = new StringReader(data))
            {
                doc = CsvDocument.Read(reader);
            }
            foreach (var head in doc.Headers)
            {
                series.Add(new ChartDataSeries() { Name = head });
            }
            var rowIndex = 0;
            foreach (var columns in doc.Rows)
            {
                rowIndex++;
                for (int i = 0; i < columns.Count; i++)
                {
                    var value = columns[i];
                    double x = 0;
                    double.TryParse(value, out x);
                    var color = colors[i];
                    var label = rowIndex.ToString();
                    series[i].Add(new ChartDataValue(label, x, null) { Color = color });
                }
            }

            chart = new AnimatingBarChart();
            chart.LineBrush = Brushes.LightGray;
            chart.HorizontalContentAlignment = HorizontalAlignment.Left;
            chart.Padding = new Thickness(20, 0, 100, 0);
            chart.BorderThickness = new Thickness(0);
            chart.VerticalAlignment = VerticalAlignment.Top;
            chart.HorizontalAlignment = HorizontalAlignment.Left;

            ChartData chartData = new ChartData();
            foreach (var seriesData in series)
            {
                chartData.AddSeries(seriesData);
            }
            chart.Data = chartData;

            this.RootGrid.Children.Add(chart);

            this.RootGrid.SizeChanged += (sender, args) =>
            {
                var s = args.NewSize;
                s.Width -= this.RootGrid.Margin.Left - this.RootGrid.Margin.Right;
                s.Height -= this.RootGrid.Margin.Top - this.RootGrid.Margin.Bottom;
                chart.Width = s.Width;
                chart.Height = s.Height;
            };

            chart.Width = this.RootGrid.ActualWidth;
            chart.Height = this.RootGrid.ActualHeight;
        }

        private void OnToggleLayout(object sender, RoutedEventArgs e)
        {
            if (this.chart != null)
            {
                this.chart.Orientation = this.chart.Orientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal;
            }
            if (this.bar != null)
            {
                this.bar.Orientation = this.bar.Orientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal;
            }
        }

        private void OnToggleStacked(object sender, RoutedEventArgs e)
        {
            if (this.chart != null)
            {
                this.chart.Stacked = !this.chart.Stacked;
            }
        }

        private string GetEmbeddedResource(string name)
        {
            using (Stream s = typeof(MainWindow).Assembly.GetManifestResourceStream(name))
            {
                StreamReader reader = new StreamReader(s);
                return reader.ReadToEnd();
            }
        }

        private void OnTestBar(object sender, RoutedEventArgs e)
        {
            this.Clear();
            this.bar = new StackedBar();

            var height = this.RootGrid.ActualHeight - this.RootGrid.Margin.Top - this.RootGrid.Margin.Bottom;
            var finalPoints = new PointCollection()
            {
                new Point(0,0),
                new Point(30,0),
                new Point(0,height),
                new Point(30,height),
            };
            this.bar.AddSegment(10, Colors.Green, 10);
            this.bar.AddSegment(20, Colors.LightGreen, 20);
            this.bar.AddSegment(50, Colors.SeaGreen, 30);
            RootGrid.Children.Add(this.bar);

            var duration = new Duration(TimeSpan.FromSeconds(1));
            var start = TimeSpan.FromSeconds(0);
            this.bar.BeginAnimation(StackedBar.PointsProperty, new PointCollectionAnimation() { To = finalPoints, Duration = duration, BeginTime = start });

            this.bar.MouseLeftButtonDown += OnBarMouseLeftButtonDown;
        }

        private void OnBarMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this.bar);
            var data = this.bar.HitBarSegment(pos);
            Debug.WriteLine($"Hit found data: {data}");
        }
    }
}