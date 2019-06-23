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
            if (current == null)
            {
                current = new List<StockServiceSettings>();
            }
            for (int i = 0; i < list.Count(); i++)
            {
                bool found = false;
                var item = list[i];
                foreach (var c in current)
                {
                    if (c.Name == item.Name)
                    {
                        found = true;
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
    }
}
