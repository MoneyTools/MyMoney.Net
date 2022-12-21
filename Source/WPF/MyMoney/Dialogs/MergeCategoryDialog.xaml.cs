using System;
using System.Windows;
using System.Windows.Controls;
using Walkabout.Controls;
using Walkabout.Data;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for MoveMergeCategoryDialog.xaml
    /// </summary>
    public partial class MergeCategoryDialog : BaseDialog
    {
        private MyMoney _money;
        private readonly string _statusFormat;
        private Category _source;

        public MergeCategoryDialog()
        {
            this.InitializeComponent();
            this.ButtonOK.IsEnabled = false;
            this._statusFormat = this.Status.Text;
        }

        public Category SourceCategory
        {
            get => this._source;
            set
            {
                this._source = value;
                if (value != null)
                {
                    this.Status.Text = string.Format(this._statusFormat, value.GetFullName());
                }
            }
        }

        public MyMoney Money
        {
            get => this._money;
            set
            {
                this._money = value;
                this.Categories.ItemsSource = this._money.Categories.SortedCategories;
            }
        }

        public Category SelectedCategory => this.Categories.SelectedItem as Category;

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void OnCategorySelected(object sender, SelectionChangedEventArgs e)
        {
            this.ButtonOK.IsEnabled = this.Categories.SelectedItem != null;
        }

        private void ComboBoxForCategory_FilterChanged(object sender, RoutedEventArgs e)
        {
            FilteringComboBox combo = sender as FilteringComboBox;
            combo.Items.Filter = new Predicate<object>((o) => { return ((Category)o).GetFullName().IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0; });
        }
    }
}
