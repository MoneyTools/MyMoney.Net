using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Data;
using Walkabout.Utilities;
using Walkabout.Views;

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
                Transaction last = Transactions.LastOrDefault();
                return (last != null) ? last.Date : DateTime.Now;
            }
        }
        public HistoryRange Range { get; set; }

        /// <summary>
        /// The set of transactions in this column
        /// </summary>
        public IEnumerable<Transaction> Transactions { get; set; }
    };

    /// <summary>
    /// Interaction logic for BudgetChart.xaml
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
            BarChart.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(BarChart_PreviewMouseLeftButtonDown);

            this.IsVisibleChanged += new DependencyPropertyChangedEventHandler(HistoryBarChart_IsVisibleChanged);
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


        void BarChart_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject d = e.OriginalSource as DependencyObject;
            if (e == null)
            {
                return;
            }
            while (d != null)
            {
                FrameworkElement f = d as FrameworkElement;
                if (f != null && f.DataContext != null)
                {
                    HistoryChartColumn data = f.DataContext as HistoryChartColumn;
                    if (data != null)
                    {
                        this.selection = data;
                        OnSelectionChanged();
                        return;
                    }
                    ColumnLabel label = f.DataContext as ColumnLabel;
                    if (label != null)
                    {
                        this.selection = label.Data;
                        OnSelectionChanged();
                        return;
                    }
                }
                d = VisualTreeHelper.GetParent(d);
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
                SeriesAmount.ItemsSource = null;
                collection.Clear();

                if (this.Selection == null)
                {
                    return;
                }

                IEnumerable<Transaction> rows = this.Selection.Transactions;

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
                List<Transaction> transactions = new List<Transaction>();

                foreach (Transaction t in rows)
                {
                    if (t.Transfer != null)
                    {
                        // skip transfers.
                        continue;
                    }
                    decimal amount = t.Amount;
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
                            AddColumn(start, range, total, transactions, brush);
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
                        transactions = new List<Transaction>();
                    }
                    if (t.Date < start)
                    {
                        continue;
                    }
                    total += amount;
                    transactions.Add(t);
                }
                if (total != 0)
                {
                    AddColumn(start, range, total, transactions, brush);
                }
                while (collection.Count > maxColumns)
                {
                    collection.RemoveAt(0);
                }

                ComputeLinearRegression();

                ObservableCollection<HistoryChartColumn> copy = new ObservableCollection<HistoryChartColumn>(this.collection);

                SeriesAmount.ItemsSource = copy;
                AverageSeries.ItemsSource = copy;
                BarChart.InvalidateArrange();
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
            double sum = (from c in collection select c.Transactions.Count()).Sum();
            double avg = sum / count;

            HistoryChartColumn first = collection[0];
            HistoryChartColumn last = collection[count - 1];

            double x = 0;
            List<Point> points = new List<Point>();
            foreach (HistoryChartColumn c in collection)
            {
                if ((c == last || c == last) && c.Transactions.Count() < (avg / 2))
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

        private TimeSpan ComputeChartParameters(IEnumerable<Transaction> rows)
        {
            int count = 0;
            int negatives = 0;
            DateTime startDate = DateTime.MinValue;
            DateTime endDate = DateTime.MinValue;
            foreach (Transaction t in rows)
            {
                if (t.Transfer != null || t.Amount == 0)
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
                if (t.Amount < 0)
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

        private void AddColumn(DateTime start, HistoryRange range, decimal total, List<Transaction> transactions, Brush brush)
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
            HistoryChartColumn column = new HistoryChartColumn() { Amount = total, Label = clabel, Transactions = transactions, Brush = brush };
            clabel.Data = column;
            collection.Add(column);
        }

    }
}
