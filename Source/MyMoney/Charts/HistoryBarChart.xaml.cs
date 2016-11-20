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

namespace Walkabout.Charts
{
    public class HistoryChartColumn
    {
        public Brush Brush { get; set; }
        public decimal Amount { get; set; }
        public string Label { get; set; }
        public decimal Average { get; set; }
        public bool Partial { get; set; } // whether this column has partial data (doesn't seem to be a full set of transactions in this date range).

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public List<Transaction> Transactions { get; set; }
    };

    /// <summary>
    /// Interaction logic for BudgetChart.xaml
    /// </summary>
    public partial class HistoryBarChart : UserControl
    {
        ObservableCollection<HistoryChartColumn> collection = new ObservableCollection<HistoryChartColumn>();

        enum Range { Year, Month }

        bool invert;
        HistoryChartColumn selection;

        public HistoryBarChart()
        {
            InitializeComponent();

            RangeCombo.Items.Add(Range.Year);
            RangeCombo.Items.Add(Range.Month);
            RangeCombo.SelectedIndex = 0;
            RangeCombo.SelectionChanged += new SelectionChangedEventHandler(RangeCombo_SelectionChanged);
            AreaChart.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(AreaChart_PreviewMouseLeftButtonDown);

            this.IsVisibleChanged += new DependencyPropertyChangedEventHandler(HistoryBarChart_IsVisibleChanged);
        }

        void HistoryBarChart_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateChart();
            }));
        }

        public MyMoney MyMoney { get; set; }

        /// <summary>
        /// Category to filter on
        /// </summary>
        public Category Category { get; set; }

        /// <summary>
        /// Payee to filter on.
        /// </summary>
        public Payee Payee { get; set; }

        /// <summary>
        /// The selected column in the chart
        /// </summary>
        public HistoryChartColumn Selection
        {
            get { return selection; }
            set
            {
                if (selection != value)
                {
                    selection = value;
                    OnSelectionChanged();
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

        void AreaChart_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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
                        Selection = data;
                        return;
                    }
                }
                d = VisualTreeHelper.GetParent(d);
            }
        }


        void RangeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateChart();
        }

        public void UpdateChart()
        {
            try
            {
                SeriesAmount.ItemsSource = null;
                collection.Clear();

                if (MyMoney == null || (Category == null && Payee == null) || !this.IsVisible)
                {
                    return;
                }

                IList<Transaction> rows = null;
                Brush brush = null;

                if (Category != null)
                {
                    rows = MyMoney.Transactions.GetTransactionsByCategory(Category, null);

                    if (!string.IsNullOrEmpty(Category.InheritedColor))
                    {
                        Color c = ColorAndBrushGenerator.GenerateNamedColor(Category.InheritedColor);
                        brush = new SolidColorBrush(c);
                    }
                }
                else if (Payee != null)
                {
                    rows = MyMoney.Transactions.GetTransactionsByPayee(Payee, null);
                    Color c = ColorAndBrushGenerator.GenerateNamedColor(Payee.Name);
                    brush = new SolidColorBrush(c);
                }
                if (rows == null)
                {
                    return;
                }

                if (brush == null)
                {
                    brush = Brushes.Maroon;
                }

                Range range = Range.Year;
                int maxColumns = 20;
                if (this.RangeCombo.SelectedItem != null)
                {
                    string value = this.RangeCombo.SelectedItem.ToString();
                    Enum.TryParse<Range>(value, out range);
                }
                if (range == Range.Month)
                {
                    maxColumns = 24;
                }

                ComputeChartParameters(rows);

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
                    if (start == DateTime.MinValue || start.Year != td.Year || (range == Range.Month && start.Month != td.Month))
                    {
                        if (start != DateTime.MinValue)
                        {
                            AddColumn(start, range, total, transactions, brush);
                        }
                        start = new DateTime(td.Year, td.Month, 1);
                        total = 0; 
                        transactions = new List<Transaction>();
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
                AreaChart.InvalidateArrange();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
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
            double sum = (from c in collection select c.Transactions.Count).Sum();
            double avg = sum / count;

            HistoryChartColumn first = collection[0];
            HistoryChartColumn last = collection[count - 1];

            double x = 0;
            List<Point> points = new List<Point>();
            foreach (HistoryChartColumn c in collection)
            {                
                if ((c == last || c == last) && c.Transactions.Count < (avg / 2))
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

        private void ComputeChartParameters(IList<Transaction> rows)
        {
            int count = 0;
            int negatives = 0;
            foreach (Transaction t in rows)
            {
                if (t.Transfer != null || t.Amount == 0)
                {
                    continue;
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
        }

        private void AddColumn(DateTime start, Range range, decimal total, List<Transaction> transactions, Brush brush)
        {
            int century = start.Year / 100;
            int year = start.Year;

            string label = null;
            if (range == Range.Year)
            {
                label = start.Year.ToString();
            }
            else
            {
                year = year - (100 * century);
                label = string.Format("{0:00}/{1:00}", start.Month, year);
            }

            collection.Add(new HistoryChartColumn() { Amount = total, Label = label, Transactions = transactions, Brush = brush });
        }


    }
}
