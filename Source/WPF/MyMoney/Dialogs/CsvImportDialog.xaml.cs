using System;
using System.Collections.Generic;
using System.Diagnostics.PerformanceData;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml;
using Walkabout.Migrate;
using Walkabout.Controls;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for CsvImportDialog.xaml
    /// </summary>
    public partial class CsvImportDialog : BaseDialog
    {
        string[] fields;
        List<CsvFieldMap> map;

        public CsvImportDialog(string[] expectedColumns)
        {
            InitializeComponent();
            this.fields = expectedColumns;
        }

        internal void SetHeaders(IEnumerable<string> headers)
        {
            map = new List<CsvFieldMap>(from h in headers select new CsvFieldMap() { Header = h });
            FieldListView.ItemsSource = map;
        }

        public string[] TransactionFields
        {
            get
            {
                return fields;
            }
        }

        public List<CsvFieldMap> Mapping => map;

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (!Validate())
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
            foreach (var f in map)
            {
                if (!string.IsNullOrEmpty(f.Field))
                {
                    if (unique.Contains(f.Field))
                    {
                        MessageBox.Show("You have mapped the field '" + f.Field + "' twice?", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                    unique.Add(f.Field);
                    count++;
                }
            }

            if (count < this.fields.Length)
            {
                var rc = MessageBox.Show("You have not mapped all the fields, are you sure you want to continue?", "Confirm Incomplete Mapping",
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
