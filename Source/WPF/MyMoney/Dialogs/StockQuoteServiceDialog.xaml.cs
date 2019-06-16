using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Walkabout.StockQuotes;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for StockQuoteServiceDialog.xaml
    /// </summary>
    public partial class StockQuoteServiceDialog : Window
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
            var current = _stockQuotes.Settings;
            int found = -1;
            for (int i = 0; i < list.Count(); i++)
            {
                var item = list[i];
                if (item.Name == current.Name)
                {
                    // put this one first!
                    found = i;
                }
            }
            if (found >= 0)
            {
                // put the current settings first, and replace defaults with our actual saved settings.
                var item = list[found];
                item.ApiKey = current.ApiKey;
                item.ApiRequestsPerDayLimit = current.ApiRequestsPerDayLimit;
                item.ApiRequestsPerMinuteLimit = current.ApiRequestsPerMinuteLimit;
                item.ApiRequestsPerMonthLimit = current.ApiRequestsPerMonthLimit;
                list.RemoveAt(found);
                list.Insert(0, item);
            }
            foreach (var item in list)
            {
                this.ComboServiceName.Items.Add(item.Name);
            }
            this._list = list;
            this.ComboServiceName.SelectedIndex = 0;
            this.ComboServiceName.SelectionChanged += ComboServiceName_SelectionChanged;
            this.DataContext = list[0];
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
    }
}
