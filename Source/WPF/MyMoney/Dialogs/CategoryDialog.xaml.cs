using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Taxes;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for CategoryDialog.xaml
    /// </summary>
    public partial class CategoryDialog : BaseDialog
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

        private Categories categories;
        private Category category;
        private Account transfer;
        private NumberFormatInfo nfi = new NumberFormatInfo();
        private TaxCategoryCollection taxCategories = new TaxCategoryCollection();

        public Category Category
        {
            get { return this.category; }
            set
            {
                this.SetCategory(value);
            }
        }

        public Account Transfer
        {
            get { return this.transfer; }
            set
            {
                this.SetTransfer(value);
            }
        }

        public string Message
        {

            get { return this.labelMessage.Text; }

            set
            {
                this.labelMessage.Text = value;
                this.labelMessage.Visibility = !string.IsNullOrEmpty(value) ? Visibility.Visible : Visibility.Collapsed;
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

            this.InitializeComponent();

            this.labelMessage.Visibility = Visibility.Collapsed;
            this.comboBoxType.Items.Add(CategoryType.None);
            this.comboBoxType.Items.Add(CategoryType.Income);
            this.comboBoxType.Items.Add(CategoryType.Expense);
            this.comboBoxType.Items.Add(CategoryType.Savings);
            this.comboBoxType.Items.Add(CategoryType.Investments);

            this.categories = money.Categories;

            this.taxCategories.Insert(0, new TaxCategory()); // empty item allows user to clear the tax category.
            ListCollectionView view = new ListCollectionView(this.taxCategories);
            this.comboTaxCategory.ItemsSource = view;

            this.RefreshCategories();

            Category categoryFound = this.categories.FindCategory(categoryName);
            if (categoryFound == null)
            {
                this.comboBoxCategory.Text = categoryName;
                this.comboBoxType.SelectedItem = CategoryType.Expense; // default.
            }
            else
            {
                this.SetCategory(categoryFound);
            }

            this.okButton.Click += new RoutedEventHandler(this.OnOkButton_Click);
        }

        private TextBox CategoryNameTextBox
        {
            get
            {
                TextBox edit = this.comboBoxCategory.FindFirstDescendantOfType<TextBox>();
                return edit;
            }
        }

        internal void Select(string p)
        {
            TextBox edit = this.CategoryNameTextBox;
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
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // try again.
                    this.Select(p);
                }), DispatcherPriority.Background);
            }
        }

        private void ComboBoxForTaxCategory_FilterChanged(object sender, RoutedEventArgs e)
        {
            FilteringComboBox combo = sender as FilteringComboBox;
            combo.Items.Filter = new Predicate<object>((o) =>
            {
                TaxCategory tc = (TaxCategory)o;
                return (tc.FormName != null && tc.FormName.IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (tc.Name != null && tc.Name.IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0);
            });
        }

        private string GetListLabel(Category c)
        {
            if (c.ParentCategory == null)
            {
                return c.Label;
            }

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

        private void RefreshCategories()
        {
            ItemCollection items = this.comboBoxCategory.Items;
            items.Clear();

            foreach (Category c in this.categories.GetCategories())
            {
                items.Add(new IntelliComboBoxItem(this.GetListLabel(c), c.Name, c));
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

        private ColorPickerPanel ColorPicker
        {
            get
            {
                var flyout = this.ColorDropDown.Flyout; // for some reason the Flyout class with the Content property is not public!
                var pi = flyout.GetType().GetProperty("Content", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                return (ColorPickerPanel)pi.GetValue(flyout, null);
            }
        }

        private void SetCategory(Category c)
        {
            this.category = c;
            this.transfer = null;
            this.comboBoxType.IsEnabled = this.textBoxDescription.IsEnabled = true;
            if (this.category == null)
            {
                this.comboBoxCategory.Text = string.Empty;
                this.textBoxDescription.Text = string.Empty;
                this.comboBoxType.Text = string.Empty;
                this.comboTaxCategory.SelectedItem = null;
                this.ColorPicker.Color = Colors.Transparent;
            }
            else
            {
                this.textBoxDescription.Text = this.category.Description;
                this.comboBoxType.SelectedItem = this.category.Type;
                this.comboBoxCategory.Text = c.Name;

                this.comboTaxCategory.SelectedItem = this.taxCategories.Find(c.TaxRefNum);
                this.ColorPicker.Color = Colors.Transparent;

                try
                {
                    string ic = this.category.InheritedColor;
                    if (!string.IsNullOrEmpty(ic))
                    {
                        this.ColorPicker.Color = ColorAndBrushGenerator.GenerateNamedColor(ic);
                    }
                }
                catch
                {
                }
            }
        }

        private void SetTransfer(Account a)
        {
            this.transfer = a;
            this.category = null;
            this.comboBoxType.Text = string.Empty;
            this.textBoxDescription.Text = string.Empty;
            this.comboBoxType.IsEnabled = this.textBoxDescription.IsEnabled = false;
            if (this.transfer != null)
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

        private void OnOkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Add();
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
                    this.PropagateCategoryTypeToChildren(child);
                }
            }
        }

        private void Add()
        {
            if (this.transfer != null)
            {
                return;
            }

            this.money.Categories.BeginUpdate(true);
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
                    this.PropagateCategoryTypeToChildren(this.category);
                }

                var picker = this.ColorPicker;
                Color color = picker.Color;
                this.category.Color = color.ToString();
                ColorAndBrushGenerator.SetNamedColor(this.category.GetFullName(), color);
            }
            finally
            {
                // if parent categories were added then set their type & color also.            
                this.money.Categories.EndUpdate();
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
            Rectangle r = (Rectangle)this.ColorDropDown.Content;
            Color color = picker.Color;
            SolidColorBrush brush = new SolidColorBrush(color);
            r.Fill = brush;
        }

    }
}
