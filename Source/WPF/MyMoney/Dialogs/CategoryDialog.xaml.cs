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

            InitializeComponent();

            labelMessage.Visibility = Visibility.Collapsed;
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
                this.comboTaxCategory.SelectedItem = null;
                ColorPicker.Color = Colors.Transparent;
            }
            else
            {
                this.textBoxDescription.Text = category.Description;
                this.comboBoxType.SelectedItem = category.Type;
                this.comboBoxCategory.Text = c.Name;

                this.comboTaxCategory.SelectedItem = taxCategories.Find(c.TaxRefNum);
                ColorPicker.Color = Colors.Transparent;

                try
                {
                    string ic = category.InheritedColor;
                    if (!string.IsNullOrEmpty(ic))
                    {
                        ColorPicker.Color = ColorAndBrushGenerator.GenerateNamedColor(ic);
                    }
                }
                catch
                {
                }
            }
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
