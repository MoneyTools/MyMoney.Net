using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Walkabout.Controls;
using Walkabout.Data;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for MoveMergeCategoryDialog.xaml
    /// </summary>
    public partial class MergeCategoryDialog : BaseDialog
    {
        MyMoney _money;
        string _statusFormat;
        Category _source;

        public MergeCategoryDialog()
        {
            InitializeComponent();
            ButtonOK.IsEnabled = false;
            _statusFormat = Status.Text;
        }

        public Category SourceCategory
        {
            get => _source;
            set
            {
                _source = value;
                if (value != null)
                {
                    Status.Text = string.Format(_statusFormat, value.GetFullName());
                }
            }
        }

        public MyMoney Money
        {
            get => _money;
            set
            {
                _money = value;
                Categories.ItemsSource = _money.Categories.SortedCategories;
            }
        }

        public Category SelectedCategory => Categories.SelectedItem as Category;

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
            ButtonOK.IsEnabled = Categories.SelectedItem != null;
        }

        private void ComboBoxForCategory_FilterChanged(object sender, RoutedEventArgs e)
        {
            FilteringComboBox combo = sender as FilteringComboBox;
            combo.Items.Filter = new Predicate<object>((o) => { return ((Category)o).GetFullName().IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0; });
        }
    }
}
