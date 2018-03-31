using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Walkabout.Data;
using Walkabout.Utilities;
using Walkabout.Controls;
using Walkabout.Taxes;
using System.Windows.Data;
using System.Text;
using System.Windows.Threading;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for CategoryDialog.xaml
    /// </summary>
    public partial class CategoryDialog : Window
    {
        #region PROPERTIES



        private MyMoney money;

        public MyMoney MyMoney
        {
            get { return this.money; }
            set
            {
                this.money = value;
            }
        }


        Categories categories;
        Category category;
        Account transfer;
        NumberFormatInfo nfi = new NumberFormatInfo();
        TaxCategoryCollection taxCategories = new TaxCategoryCollection();

        public Category Category
        {
            get { return this.category; }
            set
            {
                SetCategory(value);
            }
        }

        public Account Transfer
        {
            get { return this.transfer; }
            set
            {
                SetTransfer(value);
            }
        }

        public string Message
        {

            get { return this.labelMessage.Text; }

            set
            {
                this.labelMessage.Text = value;
            }

        }
        #endregion

        /// <summary>
        /// Easy wrapper for launching the Category Dialog
        /// </summary>
        /// <param name="myMoney"></param>
        /// <param name="categoryName"></param>
        /// <returns></returns>
        public static CategoryDialog ShowDialogCategory(MyMoney myMoney, string categoryName)
        {
            CategoryDialog dialog = new CategoryDialog(myMoney, categoryName);
            dialog.Owner = Application.Current.MainWindow;
            return dialog;
        }


        /// <summary>
        /// Constructor
        /// </summary>
        public CategoryDialog(MyMoney money, string categoryName)
        {
            this.money = money;

            this.nfi = new NumberFormatInfo();
            this.nfi.NumberDecimalDigits = 2;
            this.nfi.CurrencyNegativePattern = 0;

            InitializeComponent();

            this.comboBoxType.Items.Add(CategoryType.None);
            this.comboBoxType.Items.Add(CategoryType.Income);
            this.comboBoxType.Items.Add(CategoryType.Expense);
            this.comboBoxType.Items.Add(CategoryType.Savings);
            this.comboBoxType.Items.Add(CategoryType.Investments);

            this.categories = money.Categories;

            taxCategories.Insert(0, new TaxCategory()); // empty item allows user to clear the tax category.
            ListCollectionView view = new ListCollectionView(taxCategories);            
            this.comboTaxCategory.ItemsSource = view;

            RefreshCategories();

            Category categoryFound = categories.FindCategory(categoryName);
            if (categoryFound == null)
            {
                this.comboBoxCategory.Text = categoryName;
                this.comboBoxType.SelectedItem = CategoryType.Expense; // default.
            }
            else
            {
                SetCategory(categoryFound);
            }

            okButton.Click += new RoutedEventHandler(OnOkButton_Click);
        }

        TextBox CategoryNameTextBox
        {
            get
            {
                TextBox edit = this.comboBoxCategory.FindFirstDescendantOfType<TextBox>();
                return edit;
            }
        }

        internal void Select(string p)
        {
            TextBox edit = CategoryNameTextBox;
            if (edit != null)
            {
                string s = edit.Text;
                int i = s.IndexOf(p);
                if (i > 0)
                {
                    if (edit.SelectionStart != i || edit.SelectionLength != p.Length)
                    {
                        edit.Focus();
                        edit.SelectionStart = i;
                        edit.SelectionLength = p.Length;
                    }
                }
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // try again.
                    Select(p);
                }), DispatcherPriority.Background);
            }
        }

        private void ComboBoxForTaxCategory_FilterChanged(object sender, RoutedEventArgs e)
        {
            FilteringComboBox combo = sender as FilteringComboBox;
            combo.Items.Filter = new Predicate<object>((o) => {
                TaxCategory tc = (TaxCategory)o;
                return (tc.FormName != null && tc.FormName.IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (tc.Name != null && tc.Name.IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0);
            });
        }

        string GetListLabel(Category c)
        {
            if (c.ParentCategory == null) return c.Label;
            StringBuilder sb = new StringBuilder();
            sb.Append(':');
            sb.Append(c.Label);
            Category parent = c.ParentCategory;
            while (parent != null)
            {
                sb.Insert(0, "    ");
                parent = parent.ParentCategory;
            }
            return sb.ToString();
        }

        void RefreshCategories()
        {
            ItemCollection items = this.comboBoxCategory.Items;
            items.Clear();

            foreach (Category c in this.categories.GetCategories())
            {
                items.Add(new IntelliComboBoxItem(GetListLabel(c), c.Name, c));
            }

            foreach (Account a in this.money.Accounts.GetAccounts())
            {
                if (!a.IsClosed && !a.IsDeleted)
                {
                    string s = "Transfer to/from: " + a.Name;
                    items.Add(new IntelliComboBoxItem(s, s, a));
                }
            }
        }

        ColorPickerPanel ColorPicker
        {
            get 
            {
                return (ColorPickerPanel)this.ColorDropDown.Popup.Child;
            }
        }

        void SetCategory(Category c)
        {
            this.category = c;
            this.transfer = null;
            this.comboBoxType.IsEnabled = this.textBoxDescription.IsEnabled = true;
            if (category == null)
            {
                this.comboBoxCategory.Text = string.Empty;
                this.textBoxDescription.Text = string.Empty;
                this.comboBoxType.Text = string.Empty;
                this.textBoxBudget.Text = string.Empty;
                this.comboTaxCategory.SelectedItem = null;
                ColorPicker.Color = Colors.Transparent;
            }
            else
            {
                this.textBoxDescription.Text = category.Description;
                this.comboBoxType.SelectedItem = category.Type;
                this.comboBoxCategory.Text = c.Name;

                if (c.BudgetRange != CalendarRange.Monthly)
                {
                    // convert to monthly since that's all we support right now.
                    switch (c.BudgetRange)
                    {
                        case CalendarRange.Daily:
                            c.Budget = (c.Budget * 365) / 12;
                            break;
                        case CalendarRange.Weekly:
                            c.Budget = (c.Budget * 52) / 12;
                            break;
                        case CalendarRange.BiWeekly:
                            c.Budget = (c.Budget * 26) / 12;
                            break;
                        case CalendarRange.BiMonthly:
                            c.Budget = (c.Budget / 2);
                            break;
                        case CalendarRange.TriMonthly:
                            c.Budget = (c.Budget / 3);
                            break;
                        case CalendarRange.Quarterly:
                            c.Budget = (c.Budget * 4) / 12;
                            break;
                        case CalendarRange.SemiAnnually:
                            c.Budget = (c.Budget * 2) / 12;
                            break;
                        case CalendarRange.Annually:
                            c.Budget = (c.Budget / 12);
                            break;
                    }
                    c.BudgetRange = CalendarRange.Monthly;
                }

                this.textBoxBudget.Text = c.Budget.ToString("n", this.nfi);
                this.comboTaxCategory.SelectedItem = taxCategories.Find(c.TaxRefNum);
                ColorPicker.Color = Colors.Transparent;

                try
                {
                    if (!string.IsNullOrEmpty(category.InheritedColor))
                    {
                        ColorPicker.Color = ColorAndBrushGenerator.GenerateNamedColor(category.InheritedColor);
                    }
                }
                catch
                {
                }
            }

            ShowHistoricalRange(c);
            ShowActual();
        }


        void SetTransfer(Account a)
        {
            this.transfer = a;
            this.category = null;
            this.comboBoxType.Text = string.Empty;
            this.textBoxDescription.Text = string.Empty;
            this.comboBoxType.IsEnabled = this.textBoxDescription.IsEnabled = false;
            if (transfer != null)
            {
                this.comboBoxCategory.Text = "Transfer to/from:" + a.Name;
            }
            else
            {
                this.comboBoxCategory.Text = string.Empty;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            this.money = null;
            base.OnClosed(e);
        }


        void OnOkButton_Click(object sender, RoutedEventArgs e)
        {
            Add();
            this.DialogResult = true;
            this.Close();
        }

        /// <summary>
        /// Propagate parent types to children categories
        /// </summary>
        private void PropagateCategoryTypeToChildren(Category c)
        {
            if (c.Subcategories != null)
            {
                foreach (Category child in c.Subcategories)
                {
                    child.Type = c.Type;
                    PropagateCategoryTypeToChildren(child);
                }
            }
        }

        void Add()
        {
            if (this.transfer != null)
            {
                return;
            }

            money.Categories.BeginUpdate(true);
            try
            {
                IntelliComboBoxItem item = (IntelliComboBoxItem)this.comboBoxCategory.SelectedItem;
                string cat = this.comboBoxCategory.Text;
                if (item != null)
                {
                    if (cat == item.ToString())
                    {
                        cat = item.EditValue.ToString();
                    }
                }

                CategoryType type = (CategoryType)StringHelpers.ParseEnum(typeof(CategoryType), this.comboBoxType.Text, (int)CategoryType.None);
                string text = cat;
                if (text != null && text.Length > 0)
                {
                    this.category = this.categories.GetOrCreateCategory(text, type);
                }

                TaxCategory tc = this.comboTaxCategory.SelectedItem as TaxCategory;
                if (tc != null)
                {
                    this.category.TaxRefNum = tc.RefNum;
                }
                else
                {
                    this.category.TaxRefNum = 0;
                }

                this.category.Description = this.textBoxDescription.Text;
                if (this.category.Type != type)
                {
                    this.category.Type = type;
                    PropagateCategoryTypeToChildren(this.category);
                }

                this.category.Budget = StringHelpers.ParseDecimal(this.textBoxBudget.Text, 0);

                var picker = this.ColorPicker;
                Color color = picker.Color;
                this.category.Color = color.ToString();
                ColorAndBrushGenerator.SetNamedColor(this.category.GetFullName(), color);
            }
            finally
            {
                // if parent categories were added then set their type & color also.            
                money.Categories.EndUpdate();
            }
        }

        private class HistoryRange
        {
            string label;
            Predicate<Transaction> filter;

            public HistoryRange(string label, Predicate<Transaction> filter)
            {
                this.label = label;
                this.filter = filter;
            }

            public override string ToString()
            {
                return this.label;
            }

            public Predicate<Transaction> Filter { get { return this.filter; } }
        }

        bool ignorePastRangeChange;

        private void PastRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ignorePastRangeChange)
            {
                return;
            }
            ShowActual();
        }

        private void ShowHistoricalRange(Category c)
        {
            ignorePastRangeChange = true;
            HistoryRange previous = PastRange.SelectedItem as HistoryRange;

            DateTime first = DateTime.MaxValue;
            DateTime last = DateTime.MinValue;

            IList<Transaction> data = this.money.Transactions.GetTransactionsByCategory(this.category, null);
            int count = 0;
            foreach (Transaction t in data)
            {
                count++;
                DateTime d = t.Date;
                if (d < first)
                {
                    first = d;
                }
                if (d > last)
                {
                    last = d;
                }
            }

            PastRange.Items.Clear();
            PastRange.Items.Add(new HistoryRange("All history", null));
            PastRange.SelectedIndex = 0;

            if (count > 0)
            {
                TimeSpan span = last - first;
                foreach (int year in new int[] { 5, 3, 2, 1 })
                {
                    if (span.TotalDays > (365 * year))
                    {
                        DateTime start = last.AddYears(-year);
                        string label = year + " year";
                        if (year > 1) label += "s";
                        PastRange.Items.Add(new HistoryRange(label, new Predicate<Transaction>((t) => { return t.Date >= start; })));
                    }
                }
                foreach (int m in new int[] { 6, 4, 3, 2, 1 })
                {
                    double months = (span.TotalDays * 12) / 365;
                    if (months > m)
                    {
                        DateTime start = last.AddMonths(-m);
                        string label = m + " month";
                        if (m > 1) label += "s";
                        PastRange.Items.Add(new HistoryRange(label, new Predicate<Transaction>((t) => { return t.Date >= start; })));
                    }
                }
            }

            if (previous != null)
            {
                foreach (HistoryRange r in PastRange.Items)
                {
                    if (previous.ToString() == r.ToString())
                    {
                        PastRange.SelectedItem = r; // keep this one selected.
                        break;
                    }
                }
            }
            
            ignorePastRangeChange = false;
        }

        private void ShowActual()
        {
            
            textBoxBudgetActual.Text = string.Empty;
            if (this.category != null)
            {
                HistoryRange history = PastRange.SelectedItem as HistoryRange;
                Predicate<Transaction> filter = null;
                if (history != null)
                {
                    filter = history.Filter;
                }

                CalendarRange range = CalendarRange.Monthly;
                IList<Transaction> data = this.money.Transactions.GetTransactionsByCategory(this.category, filter);
                DateTime start = DateTime.Now;
                DateTime end = DateTime.Now;
                bool first = true;
                decimal balance = 0;
                foreach (Transaction t in data)
                {
                    balance += t.GetCategorizedAmount(this.category);
                    if (first)
                    {
                        start = t.Date;
                        first = false;
                    }
                    end = t.Date;
                }

                TimeSpan span = end - start;

                decimal d = (decimal)span.Days;

                decimal actual = 0;
                if (d != 0)
                {
                    actual = Category.DailyToRange(balance / d, range, 1);
                    actual = Math.Abs(actual);
                }
                textBoxBudgetActual.Text = actual.ToString("n", this.nfi);
            }
        }

        private void comboBoxType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {            
        }

        private void comboBoxCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IntelliComboBoxItem item = (IntelliComboBoxItem)this.comboBoxCategory.SelectedItem;
            if (item != null)
            {
                object tag = item.Tag;
                if (tag is Category)
                {
                    this.Category = (Category)tag;
                }
                else if (item.Tag is Account)
                {
                    this.Transfer = (Account)tag;
                }
            }
        }

        private void ColorPickerPanel_ColorChanged(object sender, EventArgs e)
        {
            ColorPickerPanel picker = (ColorPickerPanel)sender;
            Rectangle r = (Rectangle)ColorDropDown.Content;
            Color color = picker.Color;
            SolidColorBrush brush = new SolidColorBrush(color);
            r.Fill = brush;
        }

    }
}
