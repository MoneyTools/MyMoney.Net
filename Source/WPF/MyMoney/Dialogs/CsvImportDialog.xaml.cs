using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Walkabout.Controls;
using Walkabout.Migrate;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for CsvImportDialog.xaml
    /// </summary>
    public partial class CsvImportDialog : BaseDialog
    {
        private readonly string[] fields;
        private CsvMap map = new CsvMap();        

        public CsvImportDialog(string[] expectedColumns)
        {
            this.InitializeComponent();
            this.fields = expectedColumns;
            this.DataContext = this.map;
        }

        internal void SetHeaders(IEnumerable<string> headers)
        {
            this.map.Fields = new List<CsvFieldMap>(from h in headers select new CsvFieldMap() { Header = h });
            this.FieldListView.ItemsSource = this.map.Fields;
        }

        public bool Negate
        {
            get { return this.map.Negate; }
            set { this.map.Negate = value; }
        }

        public string[] TransactionFields
        {
            get
            {
                return this.fields;
            }
        }

        public CsvMap Mapping => this.map;

        public void SetMap(CsvMap map)
        {
            this.map = map;
            this.FieldListView.ItemsSource = this.map.Fields;
            this.DataContext = this.map;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (!this.Validate())
            {
                return;
            }
            this.DialogResult = true;
            this.Close();
        }

        private bool Validate()
        {
            // check all fields are mapped.
            int count = 0;
            HashSet<string> unique = new HashSet<string>();
            foreach (var f in this.map.Fields)
            {
                if (!string.IsNullOrEmpty(f.Field))
                {
                    if (unique.Contains(f.Field))
                    {
                        MessageBoxEx.Show("You have mapped the field '" + f.Field + "' twice?", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                    unique.Add(f.Field);
                    count++;
                }
            }

            if (count < this.fields.Length)
            {
                var rc = MessageBoxEx.Show("You have not mapped all the fields, are you sure you want to continue?", "Confirm Incomplete Mapping",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (rc == MessageBoxResult.No)
                {
                    return false;
                }
            }
            return true;
        }

        private void ComboBox_FilterChanged(object sender, RoutedEventArgs e)
        {
            FilteringComboBox combo = sender as FilteringComboBox;
            combo.Items.Filter = new Predicate<object>((o) =>
            {
                return o.ToString().IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0;
            });
        }
    }
}
