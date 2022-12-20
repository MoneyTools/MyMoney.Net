using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Walkabout.Utilities;


namespace Walkabout.Charts
{
    public enum HistoryRange
    {
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
            get { return this.data; }
            set { this.data = value; }
        }

        public ColumnLabel(string label)
        {
            this.label = label;
        }

        public override string ToString()
        {
            return this.label;
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
        public IEnumerable<HistoryDataValue> Values { get; set; }
        public HistoryRange Range { get; internal set; }
        public Brush Brush { get; set; }
        public decimal Amount { get; set; }
        public ColumnLabel Label { get; set; }
        public decimal Average { get; set; }
        public DateTime StartDate
        {
            get
            {
                HistoryDataValue first = this.Values != null ? this.Values.FirstOrDefault() : null;
                return (first != null) ? first.Date : DateTime.Now;
            }
        }
        public DateTime EndDate
        {
            get
            {
                HistoryDataValue last = this.Values != null ? this.Values.LastOrDefault() : null;
                return (last != null) ? last.Date : DateTime.Now;
            }
        }

    }

    /// <summary>
    /// Interaction logic for HistoryBarChart.xaml
    /// </summary>
    public partial class HistoryBarChart : UserControl
    {
        private DelayedActions delayedActions = new DelayedActions();
        ObservableCollection<HistoryChartColumn> collection = new ObservableCollection<HistoryChartColumn>();
        bool invert;
        int fiscalYearStart;
        HistoryChartColumn selection;

        public HistoryBarChart()
        {
            this.InitializeComponent();

            this.RangeCombo.Items.Add(HistoryRange.Year);
            this.RangeCombo.Items.Add(HistoryRange.Month);
            this.RangeCombo.Items.Add(HistoryRange.Day);
            this.RangeCombo.SelectedIndex = 0;
            this.RangeCombo.SelectionChanged += new SelectionChangedEventHandler(this.RangeCombo_SelectionChanged);

            IsVisibleChanged += new DependencyPropertyChangedEventHandler(this.HistoryBarChart_IsVisibleChanged);

            this.Chart.ToolTipGenerator = this.OnGenerateTip;
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
            this.DelayedUpdate();
        }

        void DelayedUpdate()
        {
            this.delayedActions.StartDelayedAction("update", this.UpdateChart, TimeSpan.FromMilliseconds(1));
        }

        /// <summary>
        /// The selected column in the chart
        /// </summary>
        public HistoryChartColumn Selection
        {
            get { return this.selection; }
            set
            {
                this.selection = value;
                this.DelayedUpdate();
            }
        }

        public int FiscalYearStart
        {
            get => this.fiscalYearStart;
            set
            {
                if (this.fiscalYearStart != value)
                {
                    this.fiscalYearStart = value;
                    this.DelayedUpdate();
                }
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

        private void OnColumnClicked(object sender, ChartDataValue e)
        {
            if (e.UserData is HistoryChartColumn data)
            {
                this.selection = data;
                this.OnSelectionChanged();
            }
            else if (e.UserData is ColumnLabel label)
            {
                this.selection = label.Data;
                this.OnSelectionChanged();
            }
        }

        void RangeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.selection != null && e.AddedItems.Count > 0 && e.AddedItems[0] is HistoryRange newRange)
            {
                this.selection.Range = newRange;
            }
            this.UpdateChart();
        }

        bool updating;

        public void UpdateChart()
        {
            if (this.updating)
            {
                return;
            }
            this.updating = true;

            try
            {
                this.collection.Clear();

                if (this.Selection == null)
                {
                    this.Chart.Data = null;
                    return;
                }

                this.RangeCombo.SelectedItem = this.Selection.Range;

                IEnumerable<HistoryDataValue> rows = this.Selection.Values;

                if (rows == null || !this.IsVisible || !rows.Any())
                {
                    this.Chart.Data = null;
                    return;
                }

                HistoryRange range = this.Selection.Range;

                Brush brush = this.Selection.Brush;
                if (brush == null)
                {
                    brush = Brushes.DarkSlateBlue;
                }

                this.ComputeInversion(rows);

                var lastItem = rows.LastOrDefault();
                DateTime startDate = DateTime.Now;
                DateTime endDate = startDate;
                int maxColumns = 20;
                if (range == HistoryRange.Month)
                {
                    maxColumns = 24;
                    startDate = this.selection.EndDate.AddMonths(-maxColumns);
                    endDate = startDate.AddMonths(1);
                }
                else if (range == HistoryRange.Day)
                {
                    maxColumns = 31;
                    startDate = this.selection.EndDate.AddDays(-maxColumns);
                    endDate = startDate.AddDays(1);
                }
                else
                {
                    maxColumns = 24;
                    DateTime yearStart = new DateTime(this.selection.EndDate.Year, this.fiscalYearStart + 1, 1);
                    if (yearStart > this.selection.EndDate)
                    {
                        yearStart = yearStart.AddYears(-1);
                    }
                    startDate = yearStart.AddYears(-maxColumns);
                    endDate = startDate.AddYears(1);
                }

                bool started = false;
                decimal total = 0;
                // the current column fills this bucket until the next column date boundary is reached
                List<HistoryDataValue> bucket = new List<HistoryDataValue>();
                foreach (HistoryDataValue t in rows)
                {
                    if (t.Date < startDate)
                    {
                        continue;
                    }
                    decimal amount = t.Value;
                    if (this.invert)
                    {
                        amount = -amount;
                    }
                    DateTime td = t.Date;
                    // This is a while loop because sometimes we don't have the requested
                    // number of years in the history, so this does a quick "catch up" to
                    // the year where the data actually starts.
                    while (t.Date >= endDate)
                    {
                        if (t == lastItem && t.Date < endDate)
                        {
                            bucket.Add(t);
                        }
                        if (bucket.Count > 0 || started)
                        {
                            started = true;
                            this.AddColumn(startDate, range, total, bucket, brush);
                        }
                        startDate = endDate;
                        switch (range)
                        {
                            case HistoryRange.Year:
                                endDate = endDate.AddYears(1);
                                break;
                            case HistoryRange.Month:
                                endDate = endDate.AddMonths(1);
                                break;
                            case HistoryRange.Day:
                                endDate = endDate.AddDays(1);
                                break;
                        }
                        total = 0;
                        bucket = new List<HistoryDataValue>();
                    }
                    if (t.Date < endDate)
                    {
                        total += amount;
                        bucket.Add(t);
                    }
                }

                if (bucket.Count > 0 || started)
                {
                    this.AddColumn(startDate, range, total, bucket, brush);
                }

                while (this.collection.Count > maxColumns)
                {
                    this.collection.RemoveAt(0);
                }

                this.ComputeLinearRegression();

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

                // Send data to the animating bar chart.
                var data = new ChartData();
                var series = new ChartDataSeries() { Name = "History" };
                foreach (var column in this.collection)
                {
                    series.Values.Add(new ChartDataValue() { Label = column.Label.ToString(), Value = (double)column.Amount, UserData = column, Color = c });
                }

                data.AddSeries(series);
                this.Chart.Data = data;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }
            finally
            {
                this.updating = false;
            }
        }

        private void ComputeLinearRegression()
        {
            // Compute linear regression
            int count = this.collection.Count;
            if (count == 0)
            {
                return;
            }

            // skip the first and/or last column if they don't seem to have enough data (they may have incomplete year/month).
            double sum = (from c in this.collection select c.Values.Count()).Sum();
            double avg = sum / count;

            HistoryChartColumn first = this.collection[0];
            HistoryChartColumn last = this.collection[count - 1];

            double x = 0;
            List<Point> points = new List<Point>();
            foreach (HistoryChartColumn c in this.collection)
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
            foreach (HistoryChartColumn c in this.collection)
            {
                double y = a + (b * x);
                c.Average = (decimal)y;
                x++;
            }
        }

        private void ComputeInversion(IEnumerable<HistoryDataValue> rows)
        {
            int count = 0;
            int negatives = 0;
            foreach (HistoryDataValue t in rows)
            {
                if (t.Value == 0)
                {
                    continue;
                }
                count++;
                if (t.Value < 0)
                {
                    negatives++;
                }
            }
            this.invert = false;
            if (negatives > count / 2)
            {
                this.invert = true;
            }
        }

        private void AddColumn(DateTime start, HistoryRange range, decimal total, List<HistoryDataValue> bucket, Brush brush)
        {
            int century = start.Year / 100;
            int year = start.Year;

            HistoryRange columnRange = HistoryRange.Year;
            string label = null;
            switch (range)
            {
                case HistoryRange.Year:
                    if (this.fiscalYearStart > 0)
                    {
                        label = "FY" + (start.Year + 1).ToString("00");
                    }
                    else
                    {
                        label = start.Year.ToString();
                    }
                    columnRange = HistoryRange.Month;
                    break;
                case HistoryRange.Month:
                    year = year - (100 * century);
                    label = string.Format("{0:00}/{1:00}", start.Month, year);
                    columnRange = HistoryRange.Day;
                    break;
                case HistoryRange.Day:
                    label = string.Format("{0:00}", start.Day);
                    columnRange = HistoryRange.Day;
                    break;
            }
            ColumnLabel clabel = new Charts.ColumnLabel(label);

            HistoryChartColumn column = new HistoryChartColumn() { Amount = total, Range = columnRange, Label = clabel, Values = bucket, Brush = brush };
            clabel.Data = column;
            this.collection.Add(column);
        }

        private void OnExport(object sender, RoutedEventArgs e)
        {
            var data = this.Chart.Data;
            if (data != null && data.Series != null && data.Series.Count > 0)
            {
                data.Export();
            }
        }

        private void Rotate(object sender, RoutedEventArgs e)
        {
            this.Chart.Orientation = this.Chart.Orientation == Orientation.Vertical ? Orientation.Horizontal : Orientation.Vertical;
        }
    }
}
