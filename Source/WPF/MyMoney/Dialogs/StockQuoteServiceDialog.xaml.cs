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
        private StockQuoteManager _stockQuotes;
        private List<StockServiceSettings> _list;
        private StockServiceSettings _selection;

        public StockQuoteServiceDialog()
        {
            this.InitializeComponent();
        }

        public StockQuoteManager StockQuoteManager
        {
            get { return this._stockQuotes; }
            set
            {
                this._stockQuotes = value;
                this.UpdateDialog();
            }
        }

        private void UpdateDialog()
        {
            this.ComboServiceName.SelectionChanged -= this.ComboServiceName_SelectionChanged;
            var list = this._stockQuotes.GetDefaultSettingsList();
            var current = new List<StockServiceSettings>();
            // find the settings that match the current list of stock quote services.
            for (int i = 0; i < list.Count(); i++)
            {
                bool found = false;
                var item = list[i];
                foreach (var c in this._stockQuotes.Settings)
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
            this.ComboServiceName.SelectionChanged += this.ComboServiceName_SelectionChanged;
            this.DataContext = current[0];
        }

        public List<StockServiceSettings> Settings
        {
            get { return this._list; }
        }

        private void ComboServiceName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int i = this.ComboServiceName.SelectedIndex;
            this.DataContext = this._list[i];
        }

        private void Apply()
        {
            int i = this.ComboServiceName.SelectedIndex;
            this._selection = this._list[i];
        }

        public StockServiceSettings SelectedSettings
        {
            get { return this._selection; }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            this.Apply();
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
