using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Walkabout.StockQuotes;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for StockQuoteServiceDialog.xaml
    /// </summary>
    public partial class StockQuoteServiceDialog : BaseDialog
    {
        StockQuoteManager _stockQuotes;
        List<StockServiceSettings> _list;
        StockServiceSettings _selection;

        public StockQuoteServiceDialog()
        {
            InitializeComponent();
        }

        public StockQuoteManager StockQuoteManager
        {
            get { return _stockQuotes;  }
            set
            {
                _stockQuotes = value;
                UpdateDialog();
            }
        }

        private void UpdateDialog()
        {
            this.ComboServiceName.SelectionChanged -= ComboServiceName_SelectionChanged;
            var list = _stockQuotes.GetDefaultSettingsList();
            var current = new List<StockServiceSettings>();
            // find the settings that match the current list of stock quote services.
            for (int i = 0; i < list.Count(); i++)
            {
                bool found = false;
                var item = list[i];
                foreach (var c in _stockQuotes.Settings)
                {
                    if (c.Name == item.Name || c.OldName == item.Name || c.Name == item.OldName)
                    {
                        c.Name = item.Name; // in case it was renamed.
                        current.Add(c);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    current.Add(item);
                }
            }

            foreach (var item in current)
            {
                this.ComboServiceName.Items.Add(item.Name);
            }
            this._list = current;
            this.ComboServiceName.SelectedIndex = 0;
            this.ComboServiceName.SelectionChanged += ComboServiceName_SelectionChanged;
            this.DataContext = current[0];
        }

        public List<StockServiceSettings> Settings
        {
            get { return _list; }
        }

        private void ComboServiceName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int i = this.ComboServiceName.SelectedIndex;
            this.DataContext = _list[i];
        }

        private void Apply()
        {
            int i = this.ComboServiceName.SelectedIndex;
            _selection = _list[i];
        }

        public StockServiceSettings SelectedSettings
        {
            get { return _selection; }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            Apply();
            this.DialogResult = true;
            this.Close();
        }
        private void OnCancel(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void OnBrowse(object sender, RoutedEventArgs e)
        {
            if (this.ComboServiceName.SelectedItem is string s && Uri.TryCreate(s, UriKind.Absolute, out Uri uri))
            {
                InternetExplorer.OpenUrl(IntPtr.Zero, uri);
            }
        }
    }
}
