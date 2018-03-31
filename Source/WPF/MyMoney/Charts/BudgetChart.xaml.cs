using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Commands;
using Walkabout.Data;
using Walkabout.Reports;
using Walkabout.Utilities;

namespace Walkabout.Charts
{
    /// <summary>
    /// Interaction logic for BudgetChart.xaml
    /// </summary>
    public partial class BudgetChart : UserControl
    {
        MyMoney money;
        Category filter;
        BudgetData selection;

        public BudgetChart()
        {
            InitializeComponent();
            IsVisibleChanged += new DependencyPropertyChangedEventHandler(OnIsVisibleChanged);

            LineChart.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(LineChart_MouseLeftButtonDown);
            LineChart.DataContext = new ObservableCollection<BudgetData>();
        }

        public MyMoney MyMoney
        {
            get { return money; }
            set { money = value; UpdateChart(); }
        }

        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateChart();
            }));
        }

        public BudgetData Selection
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

        void LineChart_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject d = e.OriginalSource as DependencyObject;
            if (e == null)
            {
                return;
            }
            while (d != null) {
                FrameworkElement f = d as FrameworkElement ;
                if (f != null && f.DataContext != null)
                {
                    BudgetData data = f.DataContext as BudgetData;
                    if (data != null)
                    {
                        Selection = data;
                        return;
                    }
                }
                d = VisualTreeHelper.GetParent(d);
            }
        }

        public Category CategoryFilter 
        { 
            get { return this.filter; }
            set { this.filter = value; LineChart.LegendTitle = (filter == null) ? "Total Budget" : filter.Name; }        
        }

        public void UpdateChart()
        {
            if (this.MyMoney == null || !this.IsVisible)
            {
                return;
            }

            BudgetReport report = new BudgetReport(null, this.money);
            ObservableCollection<BudgetData> list = new ObservableCollection<BudgetData>();

            if (CategoryFilter == null)
            {
                Dictionary<string, BudgetData> totals = new Dictionary<string, BudgetData>();

                decimal sum = 0; // for new average

                // then we need to do all categories and compute the totals.
                foreach (Category rc in money.Categories.GetRootCategories())
                {
                    if (rc.Type != CategoryType.Expense)
                    {
                        continue;
                    }

                    report.CategoryFilter = rc;
                    foreach (BudgetData b in report.Compute())
                    {
                        BudgetData total = null;
                        if (!totals.TryGetValue(b.Name, out total))
                        {
                            totals[b.Name] = b;
                        }
                        else
                        {
                            total.Merge(b);
                            sum += (decimal)b.Average;
                        }
                    }
                }

                if (totals.Count > 0)
                {
                    decimal average = sum / totals.Count;
                    foreach (BudgetData data in totals.Values)
                    {
                        data.Average = (double)average;
                    }
                }

                ComputeLinearRegression(totals.Values);
                list = new ObservableCollection<BudgetData>(from b in totals.Values orderby b.BudgetDate ascending select b);
            }
            else
            {
                report.CategoryFilter = this.CategoryFilter;
                IEnumerable<BudgetData> result = report.Compute();
                ComputeLinearRegression(result);
                // Now update the observable collection.
                list = new ObservableCollection<BudgetData>(from b in result orderby b.BudgetDate ascending select b);
            }

            // compute cumulative totals.
            double budgetTotal = 0;
            double actualTotal = 0;
            foreach (BudgetData d in list)
            {
                budgetTotal += d.Budget;
                actualTotal += d.Actual;
                d.BudgetCumulative = budgetTotal;
                d.ActualCumulative = actualTotal;
            }

            try
            {
                BudgetSeries.ItemsSource = list;
                ActualSeries.ItemsSource = list;
                AverageSeries.ItemsSource = list;
                BudgetCumulativeSeries.ItemsSource = list;
                ActualCumulativeSeries.ItemsSource = list;
                LineChart.InvalidateArrange();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
            }
        }


        private void ComputeLinearRegression(IEnumerable<BudgetData> budget)
        {
            // Compute linear regression
            double x = 0;
            int count = budget.Count();
            if (count == 0) {
                return;
            }

            List<Point> points = new List<Point>();

            double sum = (from c in budget select c.Budgeted.Count).Sum();
            double avg = sum / count;
            BudgetData first = budget.First();
            BudgetData last = budget.Last();

            foreach (BudgetData c in budget)
            {
                if ((c == last || c == last) && c.Budgeted.Count < (avg / 2))
                {
                    // skip the first column if we have enough data since it might be an incomplete year/month.
                    continue;
                }

                // ignore projected columns in our calculation.
                if (!c.IsProjected)
                {
                    points.Add(new Point(x++, (double)c.Actual));
                }
            }

            double a, b;    //  y = a + b.x
            MathHelpers.LinearRegression(points, out a, out b);

            // create "Average" points that represent this line.
            x = 0;
            foreach (BudgetData c in budget)
            {
                double y = a + (b * x);
                c.Average = y;
                x++;
            }
        }

        private void OnShowReport(object sender, RoutedEventArgs e)
        {
            RoutedUICommand cmd = AppCommands.CommandReportBudget;
            cmd.Execute(null, this);   
        }

        private void OnShowAreaChart(object sender, RoutedEventArgs e)
        {
            AreaChart.Visibility = System.Windows.Visibility.Visible;
            LineChart.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void OnShowLineChart(object sender, RoutedEventArgs e)
        {
            AreaChart.Visibility = System.Windows.Visibility.Collapsed;
            LineChart.Visibility = System.Windows.Visibility.Visible;
        }


    }
}
