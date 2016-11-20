using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        IList<Transaction> transactions;
        bool dataDirty;
        bool chartDirty;
        Category unassigned;
        Category transferredIn;
        Category transferredOut;
        Category filter;
        CategoryData selection;
        Dictionary<Category, CategoryData> map = new Dictionary<Category, CategoryData>();

        public CategoryChart()
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.CategoryChartInitialize))
            {
#endif
                InitializeComponent();
                IsVisibleChanged += new DependencyPropertyChangedEventHandler(OnIsVisibleChanged);

                unassigned = new Category() { Name = "Unknown", Type = Data.CategoryType.None };
                transferredIn = new Category() { Name = "Transferred In", Type = Data.CategoryType.Transfer };
                transferredOut = new Category() { Name = "Transferred Out", Type = Data.CategoryType.Transfer };
                PieChart.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(PieChart_MouseLeftButtonDown);

#if PerformanceBlocks
            }
#endif
        }

        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                if (dataDirty)
                {
                    UpdateChart();
                }
                else
                {
                    ShowChart();
                }
            }
        }

        public CategoryData Selection
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

        void PieChart_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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
                    CategoryData data = f.DataContext as CategoryData;
                    if (data != null)
                    {
                        Selection = data;
                        return;
                    }
                }
                d = VisualTreeHelper.GetParent(d);
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
            set { this.transactions = value; UpdateChart(); }
        }

        public Category Unknown { get; set; }

        private bool MatchesFilter(Category c)
        {
            return this.filter == null || this.filter.Contains(c) || (this.filter == Unknown && c == null);
        }

        public decimal NetAmount { get; private set; }

        public decimal ComputeNetAmount()
        {
            // ALERT: this method has to be kept in sync with UpdateChart
            // search and see if we have any matching transactions to tally.
            decimal total = 0;

            bool willTally = false;

            if (transactions == null)
            {
                return 0;
            }

            foreach (object row in transactions)
            {
                Transaction t = row as Transaction;
                if (t == null)
                {
                    // must be Investments.
                    break;
                }
                decimal amount = Math.Abs(t.Amount);
                bool isExpenses = t.Amount < 0;

                if (t.IsSplit)
                {
                    foreach (Split s in t.Splits.GetSplits())
                    {
                        decimal subtotal = Math.Abs(s.Amount);
                        if (s.Transfer != null)
                        {
                            willTally = WillTally(t, t.amount < 0 ? transferredOut : transferredIn, amount, isExpenses);
                        }
                        else if (MatchesFilter(s.Category))
                        {
                            willTally = WillTally(t, s.Category, subtotal, isExpenses);
                        }
                        amount -= subtotal;
                    }
                    if (amount != 0)
                    {
                        willTally = WillTally(t, unassigned, amount, isExpenses);
                    }
                }
                else if (t.Transfer != null)
                {
                    willTally = WillTally(t, t.amount < 0 ? transferredOut : transferredIn, amount, isExpenses);
                }
                else if (MatchesFilter(t.Category))
                {
                    willTally = WillTally(t, t.Category, amount, isExpenses);
                }

                if (willTally)
                {
                    total += t.Amount;
                }
            }

            NetAmount = total;

            return total;
        }

        private void UpdateChart()
        {
            this.map = new Dictionary<Category, CategoryData>();

            // ALERT: this method has to be kept in sync with ComputeNetAmount

            if (!this.IsVisible)
            {
                dataDirty = true;

                TotalAmount.Text = string.Format("{0:C2}", Math.Abs(ComputeNetAmount()));
                return;
            }

            decimal total = 0;

            if (transactions != null)
            {
                foreach (object row in transactions)
                {
                    Transaction t = row as Transaction;
                    if (t == null)
                    {
                        // must be Investments.
                        break;
                    }
                    decimal amount = Math.Abs(t.Amount);
                    bool isExpenses = t.Amount < 0;
                    bool tallied = false;

                    if (t.IsSplit)
                    {
                        foreach (Split s in t.Splits.GetSplits())
                        {
                            decimal subtotal = Math.Abs(s.Amount);
                            if (s.Transfer != null)
                            {
                                tallied = Tally(t, t.amount < 0 ? transferredOut : transferredIn, amount, isExpenses);
                            }
                            else if (MatchesFilter(s.Category))
                            {
                                tallied = Tally(t, s.Category, subtotal, isExpenses);
                            }
                            amount -= subtotal;
                        }
                        if (amount != 0)
                        {
                            tallied = Tally(t, unassigned, amount, isExpenses);
                        }
                    }
                    else if (t.Transfer != null)
                    {
                        tallied = Tally(t, t.amount < 0 ? transferredOut : transferredIn, amount, isExpenses);
                    }
                    else if (MatchesFilter(t.Category))
                    {
                        tallied = Tally(t, t.Category, amount, isExpenses);
                    }
                    if (tallied)
                    {
                        total += t.Amount;
                    }
                }

                chartDirty = true;
            }

            dataDirty = false;

            TotalAmount.Text = string.Format("{0:C2}", Math.Abs(total));

            ShowChart();
        }

        private void ShowChart()
        {
            // Now update the observable collection.
            if (this.IsVisible && chartDirty)
            {
                chartDirty = false;
                ObservableCollection<CategoryData> data = new ObservableCollection<CategoryData>();
                foreach (CategoryData cd in from c in map.Values orderby c.Total descending select c)
                {
                    data.Add(cd);
                }

                SetObservaleCollection(data);
            }
        }        

        public void SetObservaleCollection(ObservableCollection<CategoryData> categoryDataCollection)
        {
            PieSeries.ItemsSource = categoryDataCollection;
        }

        bool WillTally(Transaction t, Category c, decimal total, bool expense)
        {
            // ALERT: this method has to be kept in sync with Tally

            if (c == null)
            {
                c = unassigned;
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
                    if (c.Type != Data.CategoryType.Expense)
                    {
                        return false;
                    }
                    break;
                default:
                    System.Diagnostics.Debug.Assert(false, "Unexpected category type here, need to change this code if you want a chart of these");
                    return false;
            }

            if (filter == null)
            {
                c = c.Root;
            }
            else
            {
                while (c != null && c != filter && c.ParentCategory != filter)
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

        bool Tally(Transaction t, Category c, decimal total, bool expense)
        {
            // ALERT: this method has to be kept in sync with WillTally

            if (c == null)
            {
                c = unassigned;
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
                    if (c.Type != Data.CategoryType.Expense)
                    {
                        return false;
                    }
                    break;
                default:
                    System.Diagnostics.Debug.Assert(false, "Unexpected category type here, need to change this code if you want a chart of these");
                    return false;
            }

            if (filter == null)
            {
                c = c.Root;
            }
            else
            {
                while (c != null && c != filter && c.ParentCategory != filter)
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
            if (!map.TryGetValue(c, out cd))
            {
                map[c] = cd = new CategoryData(c);

                if (string.IsNullOrWhiteSpace(c.InheritedColor))
                {
                    c.Root.Color = cd.Color.ToString();
                }
            }

            cd.Transactions.Add(t);

            cd.Total += (double)total;
            return true;
        }

    }
}
