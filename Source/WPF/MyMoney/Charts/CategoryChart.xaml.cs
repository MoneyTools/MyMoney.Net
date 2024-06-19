using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Walkabout.Data;

#if PerformanceBlocks
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
#endif

namespace Walkabout.Charts
{
    /// <summary>
    /// Interaction logic for ExpensesCategoryView.xaml
    /// </summary>
    public partial class CategoryChart : UserControl
    {
        private IList<Transaction> transactions;
        private bool dataDirty;
        private bool chartDirty;
        private readonly Category unassigned;
        private readonly Category transferredIn;
        private readonly Category transferredOut;
        private Category filter;
        private CategoryData selection;
        private Dictionary<Category, CategoryData> map = new Dictionary<Category, CategoryData>();

        public CategoryChart()
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.CategoryChartInitialize))
            {
#endif
            this.InitializeComponent();
            IsVisibleChanged += new DependencyPropertyChangedEventHandler(this.OnIsVisibleChanged);

            this.unassigned = new Category() { Name = "Unassigned", Type = Data.CategoryType.None };
            this.transferredIn = new Category() { Name = "Transferred In", Type = Data.CategoryType.Transfer };
            this.transferredOut = new Category() { Name = "Transferred Out", Type = Data.CategoryType.Transfer };

            this.PieChart.PieSliceClicked += this.OnPieSliceClicked;
            this.PieChart.PieSliceHover += this.OnPieSliceHovered;
            this.PieChart.ToolTipGenerator = this.OnGenerateTip;

            this.Legend.Toggled += this.OnLegendToggled; 
            this.Legend.Selected += this.OnLegendSelected;
#if PerformanceBlocks
            }
#endif
        }

        private void OnLegendToggled(object sender, ChartDataValue e)
        {
            // filtering out a category.
            this.FilterChartData();
        }

        private void OnLegendSelected(object sender, ChartDataValue e)
        {
            // Drill into this category
            if (e.UserData is CategoryData cd)
            {
                this.Selection = cd;
            }
        }

        private UIElement OnGenerateTip(ChartDataValue value)
        {
            var tip = new StackPanel() { Orientation = Orientation.Vertical };
            tip.Children.Add(new TextBlock() { Text = value.Label, FontWeight = FontWeights.Bold });
            tip.Children.Add(new TextBlock() { Text = value.Value.ToString("C0") });
            return tip;
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                if (this.dataDirty)
                {
                    this.UpdateChart();
                }
                else
                {
                    this.ShowChart();
                }
            }
        }

        public CategoryData Selection
        {
            get { return this.selection; }
            set
            {
                if (this.selection != value)
                {
                    this.selection = value;
                    this.OnSelectionChanged();
                }
            }
        }

        public event EventHandler SelectionChanged;

        private void OnSelectionChanged()
        {
            if (SelectionChanged != null)
            {
                SelectionChanged(this, EventArgs.Empty);
            }
        }

        private void OnPieSliceHovered(object sender, ChartDataValue e)
        {
        }

        private void OnPieSliceClicked(object sender, ChartDataValue e)
        {
            if (e.UserData is CategoryData data)
            {
                this.Selection = data;
            }
        }

        // the type of category this chart instance is summarizing.
        public CategoryType CategoryType { get; set; }

        public Category CategoryFilter
        {
            get { return this.filter; }
            set { this.filter = value; }
        }

        public IList<Transaction> Transactions
        {
            get { return this.transactions; }
            set { this.transactions = value; this.UpdateChart(); }
        }

        public Category Unknown { get; set; }

        private bool MatchesFilter(Category c)
        {
            return this.filter == null || this.filter.Contains(c) || (this.filter == this.Unknown && c == null);
        }

        public decimal NetAmount { get; private set; }

        public decimal ComputeNetAmount()
        {
            // ALERT: this method has to be kept in sync with UpdateChart
            // search and see if we have any matching transactions to tally.
            decimal total = 0;

            bool willTally = false;

            if (this.transactions == null)
            {
                return 0;
            }

            foreach (object row in this.transactions)
            {
                Transaction t = row as Transaction;
                if (t == null)
                {
                    // must be Investments.
                    break;
                }
                decimal amount = t.Amount;
                bool isExpenses = t.Amount < 0;

                if (t.IsSplit)
                {
                    foreach (Split s in t.Splits.GetSplits())
                    {
                        decimal subtotal = s.Amount;
                        if (s.Transfer != null)
                        {
                            willTally = this.WillTally(t, t.amount < 0 ? this.transferredOut : this.transferredIn, amount, isExpenses);
                        }
                        else if (this.MatchesFilter(s.Category))
                        {
                            willTally = this.WillTally(t, s.Category, subtotal, isExpenses);
                        }
                        amount -= subtotal;
                    }
                    if (amount != 0)
                    {
                        willTally = this.WillTally(t, this.unassigned, amount, isExpenses);
                    }
                }
                else if (t.Transfer != null)
                {
                    willTally = this.WillTally(t, t.amount < 0 ? this.transferredOut : this.transferredIn, amount, isExpenses);
                }
                else if (this.MatchesFilter(t.Category))
                {
                    willTally = this.WillTally(t, t.Category, amount, isExpenses);
                }

                if (willTally)
                {
                    total += t.Amount;
                }
            }

            this.NetAmount = total;

            return total;
        }

        private void UpdateChart()
        {
            this.map = new Dictionary<Category, CategoryData>();

            // ALERT: this method has to be kept in sync with ComputeNetAmount

            if (!this.IsVisible)
            {
                this.dataDirty = true;

                return;
            }

            this.TotalAmount.Text = string.Format("{0:C2}", this.ComputeNetAmount());
            decimal total = 0;

            if (this.transactions != null)
            {
                foreach (object row in this.transactions)
                {
                    Transaction t = row as Transaction;
                    if (t == null)
                    {
                        // must be Investments.
                        break;
                    }
                    decimal amount = t.Amount;
                    bool isExpenses = t.Amount < 0;
                    bool tallied = false;

                    if (t.IsSplit)
                    {
                        foreach (Split s in t.Splits.GetSplits())
                        {
                            decimal subtotal = s.Amount;
                            if (s.Transfer != null)
                            {
                                tallied = this.Tally(t, t.amount < 0 ? this.transferredOut : this.transferredIn, amount, isExpenses);
                            }
                            else if (this.MatchesFilter(s.Category))
                            {
                                tallied = this.Tally(t, s.Category, subtotal, isExpenses);
                            }
                            amount -= subtotal;
                        }
                        if (amount != 0)
                        {
                            tallied = this.Tally(t, this.unassigned, amount, isExpenses);
                        }
                    }
                    else if (t.Transfer != null)
                    {
                        tallied = this.Tally(t, t.amount < 0 ? this.transferredOut : this.transferredIn, amount, isExpenses);
                    }
                    else if (this.MatchesFilter(t.Category))
                    {
                        tallied = this.Tally(t, t.Category, amount, isExpenses);
                    }
                    if (tallied)
                    {
                        total += t.Amount;
                    }
                }

                this.chartDirty = true;
            }

            this.dataDirty = false;

            this.ShowChart();
        }

        private void ShowChart()
        {
            // Now update the observable collection.
            if (this.IsVisible && this.chartDirty)
            {
                this.chartDirty = false;

                ChartDataSeries series = new ChartDataSeries() { Name = this.CategoryType.ToString() };

                foreach (CategoryData item in from c in this.map.Values orderby Math.Abs(c.Total) descending select c)
                {
                    series.Values.Add(new ChartDataValue() { Label = item.Name, Value = item.Total, Color = item.Color, UserData = item });
                }

                this.TotalAmount.Text = string.Format("{0:C2}", this.NetAmount);
                this.PieChart.Series = series;
                this.Legend.DataSeries = series;
            }
        }

        private void FilterChartData()
        {
            var data = this.PieChart.Series;
            double total = 0;
            foreach (var dv in data.Values)
            {
                if (!dv.Hidden)
                {
                    total += dv.Value;
                }
            }

            this.PieChart.Update();
            this.NetAmount = (decimal)total;
            this.TotalAmount.Text = string.Format("{0:C2}", this.NetAmount);
        }

        private bool WillTally(Transaction t, Category c, decimal total, bool expense)
        {
            // ALERT: this method has to be kept in sync with Tally

            if (c == null)
            {
                return true;
            }
            
            switch (this.CategoryType)
            {
                case CategoryType.None:
                    if (c.Type != Data.CategoryType.None && c.Type != Data.CategoryType.Transfer)
                    {
                        return false;
                    }
                    break;
                case CategoryType.Income:
                    if (c.Type != Data.CategoryType.Income && c.Type != Data.CategoryType.Savings)
                    {
                        return false;
                    }
                    break;
                case CategoryType.Expense:
                    if (c.Type != Data.CategoryType.Expense && c.Type != Data.CategoryType.RecurringExpense)
                    {
                        return false;
                    }
                    break;
                default:
                    System.Diagnostics.Debug.Assert(false, "Unexpected category type here, need to change this code if you want a chart of these");
                    return false;
            }

            if (this.filter == null)
            {
                c = c.Root;
            }
            else
            {
                while (c != null && c != this.filter && c.ParentCategory != this.filter)
                {
                    // then we are looking at a "grand child category", so need to roll this up to the child category 
                    c = c.ParentCategory;
                }
                if (c == null)
                {
                    return false;
                }
            }

            return true;
        }

        private bool Tally(Transaction t, Category c, decimal total, bool expense)
        {
            // ALERT: this method has to be kept in sync with WillTally

            if (c == null)
            {
                c = this.unassigned;
            }
            else
            {

                switch (this.CategoryType)
                {
                    case CategoryType.None:
                        if (c.Type != Data.CategoryType.None && c.Type != Data.CategoryType.Transfer)
                        {
                            return false;
                        }
                        break;
                    case CategoryType.Income:
                        if (c.Type != Data.CategoryType.Income && c.Type != Data.CategoryType.Savings)
                        {
                            return false;
                        }
                        break;
                    case CategoryType.Expense:
                        if (c.Type != Data.CategoryType.Expense && c.Type != CategoryType.RecurringExpense)
                        {
                            return false;
                        }
                        break;
                    default:
                        System.Diagnostics.Debug.Assert(false, "Unexpected category type here, need to change this code if you want a chart of these");
                        return false;
                }
            }

            if (this.filter == null)
            {
                c = c.Root;
            }
            else
            {
                while (c != null && c != this.filter && c.ParentCategory != this.filter)
                {
                    // then we are looking at a "grand child category", so need to roll this up to the child category 
                    c = c.ParentCategory;
                }
                if (c == null)
                {
                    return false;
                }
            }

            CategoryData cd;
            if (!this.map.TryGetValue(c, out cd))
            {
                this.map[c] = cd = new CategoryData(c);

                if (string.IsNullOrWhiteSpace(c.InheritedColor))
                {
                    c.Root.Color = cd.Color.ToString();
                }
            }

            cd.Transactions.Add(t);

            cd.Total += (double)total;
            return true;
        }

        private void OnExport(object sender, RoutedEventArgs e)
        {
            if (this.PieChart.Series != null && this.PieChart.Series.Values.Count > 0)
            {
                ChartData data = new ChartData();
                data.AddSeries(new ChartDataSeries() { Values = this.PieChart.Series.Values, Name = "Categories" });
                data.Export();
            }
        }
    }
}
