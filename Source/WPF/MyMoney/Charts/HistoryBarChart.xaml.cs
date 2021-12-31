using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Data;
using Walkabout.Utilities;
using Walkabout.Views;
using LovettSoftware.Charts;


namespace Walkabout.Charts
{
    public enum HistoryRange
    {
        All,
        Year,
        Month,
        Day
    }

    public class ColumnLabel
    {
        string label;
        HistoryChartColumn data;

        public HistoryChartColumn Data
        {
            get { return data; }
            set { data = value; }
        }

        public ColumnLabel(string label)
        {
            this.label = label;
        }
        public override string ToString()
        {
            return label;
        }
    }

    public class HistoryDataValue
    {
        public DateTime Date { get; set; }
        public decimal Value { get; set; }
        public object UserData { get; set; }
    }

    public class HistoryChartColumn
    {
        public Brush Brush { get; set; }
        public decimal Amount { get; set; }
        public ColumnLabel Label { get; set; }
        public decimal Average { get; set; }
        public bool Partial { get; set; } // whether this column has partial data (doesn't seem to be a full set of transactions in this date range).
        public DateTime EndDate
        {
            get
            {
                HistoryDataValue last = Values != null ? Values.LastOrDefault() : null;
                return (last != null) ? last.Date : DateTime.Now;
            }
        }
        public HistoryRange Range { get; set; }
        public IEnumerable<HistoryDataValue> Values { get; set; }
    };


    /// <summary>
    /// Interaction logic for HistoryBarChart.xaml
    /// </summary>
    public partial class HistoryBarChart : UserControl
    {
        ObservableCollection<HistoryChartColumn> collection = new ObservableCollection<HistoryChartColumn>();

        bool invert;
        HistoryChartColumn selection;

        public HistoryBarChart()
        {
            InitializeComponent();

            RangeCombo.Items.Add(HistoryRange.All);
            RangeCombo.Items.Add(HistoryRange.Year);
            RangeCombo.Items.Add(HistoryRange.Month);
            RangeCombo.Items.Add(HistoryRange.Day);
            RangeCombo.SelectedIndex = 0;
            RangeCombo.SelectionChanged += new SelectionChangedEventHandler(RangeCombo_SelectionChanged);
            // BarChart.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(BarChart_PreviewMouseLeftButtonDown);

            this.IsVisibleChanged += new DependencyPropertyChangedEventHandler(HistoryBarChart_IsVisibleChanged);

            Chart.ToolTipGenerator = OnGenerateTip;
        }

        private UIElement OnGenerateTip(ChartDataValue value)
        {
            var tip = new StackPanel() { Orientation = Orientation.Vertical };
            tip.Children.Add(new TextBlock() { Text = value.Label, FontWeight = FontWeights.Bold });
            tip.Children.Add(new TextBlock() { Text = value.Value.ToString("C0") });
            return tip;
        }

        void HistoryBarChart_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateChart(false);
            }));
        }

        /// <summary>
        /// The selected column in the chart
        /// </summary>
        public HistoryChartColumn Selection
        {
            get { return selection; }
            set
            {
                selection = value;
                UpdateChart(false);
            }
        }

        public event EventHandler SelectionChanged;

        void OnSelectionChanged()
        {
            if (SelectionChanged != null)
            {
                SelectionChanged(this, EventArgs.Empty);
            }
        }

        HistoryRange range = HistoryRange.All;

        public HistoryRange SelectedRange
        {
            get { return this.range; }
            set { this.range = value; OnRangeChanged(); }
        }

        public event EventHandler RangeChanged;

        private void OnRangeChanged()
        {
            if (RangeChanged != null)
            {
                RangeChanged(this, EventArgs.Empty);
            }
        }

        private void OnColumnHover(object sender, ChartDataValue e)
        {

        }

        private void OnColumnClicked(object sender, ChartDataValue e)
        {
            if (e.UserData is HistoryChartColumn data)
            {
                this.selection = data;
                OnSelectionChanged();
            }
            else if (e.UserData is ColumnLabel label)
            {
                this.selection = label.Data;
                OnSelectionChanged();
            }
        }

        void RangeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateChart(true);
        }

        bool updating;

        public void UpdateChart(bool rangeFixed)
        {
            if (updating)
            {
                return;
            }
            updating = true;

            try
            {
                collection.Clear();

                if (this.Selection == null)
                {
                    return;
                }

                IEnumerable<HistoryDataValue> rows = this.Selection.Values;

                if (rows == null || !this.IsVisible)
                {
                    return;
                }

                HistoryRange range = HistoryRange.Year;
                if (this.RangeCombo.SelectedItem != null)
                {
                    string value = this.RangeCombo.SelectedItem.ToString();
                    Enum.TryParse<HistoryRange>(value, out range);
                }

                Brush brush = this.Selection.Brush;
                if (brush == null)
                {
                    brush = Brushes.DarkSlateBlue;
                }

                TimeSpan dateRange = ComputeChartParameters(rows);
                if (!rangeFixed)
                {
                    if (dateRange <= TimeSpan.FromDays(31))
                    {
                        range = HistoryRange.Day;
                        this.RangeCombo.SelectedItem = range;
                    }
                    else if (dateRange <= TimeSpan.FromDays(365))
                    {
                        range = HistoryRange.Month;
                        this.RangeCombo.SelectedItem = range;
                    }
                    else
                    {
                        range = HistoryRange.Year;
                        this.RangeCombo.SelectedItem = range;
                    }
                }

                DateTime startDate = DateTime.Now;
                int maxColumns = 20;
                if (range == HistoryRange.Month)
                {
                    maxColumns = 24;
                    startDate = this.selection.EndDate.AddMonths(-maxColumns);
                }
                else if (range == HistoryRange.Day)
                {
                    maxColumns = 31;
                    startDate = this.selection.EndDate.AddDays(-maxColumns);
                }
                else 
                {
                    maxColumns = 20;
                    startDate = this.selection.EndDate.AddYears(-maxColumns);
                }

                decimal total = 0;
                DateTime start = DateTime.MinValue;
                // the current column fills this bucket until the next column date boundary is reached
                List<HistoryDataValue> bucket = new List<HistoryDataValue>();

                foreach (HistoryDataValue t in rows)
                {
                    decimal amount = t.Value;
                    if (invert)
                    {
                        amount = -amount;
                    }
                    DateTime td = t.Date;
                    while (start == DateTime.MinValue || start.Year < td.Year || 
                        (range == HistoryRange.Month && start.Month < td.Month) ||
                        (range == HistoryRange.Day && start.Day < td.Day ))
                    {
                        if (start != DateTime.MinValue)
                        {
                            AddColumn(start, range, total, bucket, brush);
                        }
                        if (start == DateTime.MinValue)
                        {
                            start = new DateTime(td.Year, (range == HistoryRange.Month || range == HistoryRange.Day) ? td.Month : 1,
                                range == HistoryRange.Day ? td.Day : 1);
                        }
                        else
                        {
                            switch (range)
                            {
                                case HistoryRange.All:
                                case HistoryRange.Year:
                                    start = start.AddYears(1);
                                    break;
                                case HistoryRange.Month:
                                    start = start.AddMonths(1);
                                    break;
                                case HistoryRange.Day:
                                    start = start.AddDays(1);
                                    break;
                            }
                        }
                        total = 0;
                        bucket = new List<HistoryDataValue>();
                    }
                    if (t.Date < start)
                    {
                        continue;
                    }
                    total += amount;
                    bucket.Add(t);
                }
                if (total != 0)
                {
                    AddColumn(start, range, total, bucket, brush);
                }
                while (collection.Count > maxColumns)
                {
                    collection.RemoveAt(0);
                }

                ComputeLinearRegression();

                Color c = Colors.Black;
                if (brush is SolidColorBrush sc)
                {
                    c = sc.Color;
                }
                
                if (c == Colors.Transparent || c == Colors.White)
                {
                    // not defined on the category, so use gray.
                    c = Colors.Gray;
                }

                var series = new ChartDataSeries() { Name = "History" };
                foreach(var column in this.collection)
                {
                    series.Data.Add(new ChartDataValue() { Label = column.Label.ToString(), Value = (double)column.Amount, UserData = column, Color = c });
                }

                Chart.Series = new List<ChartDataSeries>() { series };
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }
            finally
            {
                updating = false;
            }
        }

        private void ComputeLinearRegression()
        {
            // Compute linear regression
            int count = collection.Count;
            if (count == 0)
            {
                return;
            }

            // skip the first and/or last column if they don't seem to have enough data (they may have incomplete year/month).
            double sum = (from c in collection select c.Values.Count()).Sum();
            double avg = sum / count;

            HistoryChartColumn first = collection[0];
            HistoryChartColumn last = collection[count - 1];

            double x = 0;
            List<Point> points = new List<Point>();
            foreach (HistoryChartColumn c in collection)
            {
                if ((c == last || c == last) && c.Values.Count() < (avg / 2))
                {
                    // skip it.
                    continue;
                }

                points.Add(new Point(x++, (double)c.Amount));
            }

            double a, b;    //  y = a + b.x
            MathHelpers.LinearRegression(points, out a, out b);

            // create "Average" points that represent this line.
            x = 0;
            foreach (HistoryChartColumn c in collection)
            {
                double y = a + (b * x);
                c.Average = (decimal)y;
                x++;
            }
        }

        private TimeSpan ComputeChartParameters(IEnumerable<HistoryDataValue> rows)
        {
            int count = 0;
            int negatives = 0;
            DateTime startDate = DateTime.MinValue;
            DateTime endDate = DateTime.MinValue;
            foreach (HistoryDataValue t in rows)
            {
                if (t.Value == 0)
                {
                    continue;
                }
                if (startDate == DateTime.MinValue)
                {
                    startDate = t.Date;
                }
                else
                {
                    endDate = t.Date;
                }
                count++;
                if (t.Value < 0)
                {
                    negatives++;
                }
            }
            invert = false;
            if (negatives > count / 2)
            {
                invert = true;
            }
            return (endDate == startDate) ? TimeSpan.FromDays(0) : endDate - startDate;
        }

        private void AddColumn(DateTime start, HistoryRange range, decimal total, List<HistoryDataValue> bucket, Brush brush)
        {
            int century = start.Year / 100;
            int year = start.Year;

            string label = null;
            switch (range)
            {
                case HistoryRange.All:
                case HistoryRange.Year:
                    label = start.Year.ToString();
                    break;
                case HistoryRange.Month:
                    year = year - (100 * century);
                    label = string.Format("{0:00}/{1:00}", start.Month, year);
                    break;
                case HistoryRange.Day:
                    label = string.Format("{0:00}", start.Day);
                    break;
            }
            ColumnLabel clabel = new Charts.ColumnLabel(label);

            HistoryChartColumn column = new HistoryChartColumn() { Amount = total, Label = clabel, Values = bucket, Brush = brush };
            clabel.Data = column;
            collection.Add(column);
        }

        private void OnExport(object sender, RoutedEventArgs e)
        {
            string name = System.IO.Path.GetTempFileName() + ".csv";
            TempFilesManager.AddTempFile(name);
            using (StreamWriter writer = new StreamWriter(name))
            {
                writer.WriteLine("Label, Amount");
                foreach (var column in collection)
                {
                    writer.WriteLine("{0}, {1}", column.Label, column.Amount);
                }
            }
            int SW_SHOWNORMAL = 1;
            NativeMethods.ShellExecute(IntPtr.Zero, "Open", name, "", "", SW_SHOWNORMAL);
        }

        private void Rotate(object sender, RoutedEventArgs e)
        {
            this.Chart.Orientation = this.Chart.Orientation == Orientation.Vertical ? Orientation.Horizontal : Orientation.Vertical;
        }
    }
}
