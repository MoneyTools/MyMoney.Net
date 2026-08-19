using Walkabout.Utilities;
using Windows.UI;
using System.Diagnostics;
using Windows.Foundation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

namespace UnoApp1.Presentation;

public sealed partial class MainPage : Page
{
    StackedBar bar;
    AnimatingBarChart chart;

    public MainPage()
    {
        UiDispatcher.CurrentDispatcher = this.Dispatcher;

        InitializeComponent();
    }

    static Color Green = Color.FromArgb(0xff, 0x00, 0x80, 0x00);
    static Color LightGreen = Color.FromArgb(0xff, 0x90, 0xEE, 0x90);
    static Color SeaGreen = Color.FromArgb(0xFF, 0x2E, 0x8B, 0x57);

    static Color LightGray = Color.FromArgb(0xFF, 0xD3, 0xD3, 0xD3);

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
                Green,
                LightGreen,
                SeaGreen
            };

        var series = new List<ChartDataSeries>();

        CsvDocument doc;
        var data = GetEmbeddedResource("UnoApp1.ChartData.csv");
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
        chart.LineBrush = new SolidColorBrush(LightGray);
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
        using (Stream s = typeof(MainPage).Assembly.GetManifestResourceStream(name))
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
        this.bar.AddSegment(10, Green, 10);
        this.bar.AddSegment(20, LightGreen, 20);
        this.bar.AddSegment(50, SeaGreen, 30);
        this.bar.HorizontalAlignment = HorizontalAlignment.Left;
        this.bar.VerticalAlignment = VerticalAlignment.Bottom;
        this.bar.Width = 30;
        this.bar.Height = height;
        RootGrid.Children.Add(this.bar);

        var duration = new Duration(TimeSpan.FromSeconds(1));
        var start = TimeSpan.FromSeconds(0);
        this.bar.AnimateBar(finalPoints, duration, start);

        //this.bar.MouseLeftButtonDown += OnBarMouseLeftButtonDown;
        this.bar.PointerPressed += OnBarPointerPressed;

        this.bar.BeginAnimation(new DoubleAnimation()
        {
            Duration = new Duration(TimeSpan.FromSeconds(1)),
            From = 0.0,
            To = 1.0,
        }, "Opacity");
    }

    private void OnBarPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(this.bar);
        var data = this.bar.HitBarSegment(pos.Position);
        Debug.WriteLine($"Hit found data: {data}");
    }

}
