using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Walkabout.StockQuotes;
using Walkabout.Utilities;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        private bool _initialized = false;

        public StockQuoteServiceDialog()
        {
            this.InitializeComponent();
            this._initialized = true;
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
        private void OnServiceSelected(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized)
            {
                return;
            }
            int i = this.ComboServiceName.SelectedIndex;
            if (i >= 0 && i < this._list.Count)
            {
                var service = this._list[i];
                this.DataContext = service;
                this.ButtonDisable.IsEnabled = !string.IsNullOrEmpty(service.ApiKey);
                this.ButtonOk.IsEnabled = true;
                this._selection = service;
            }
            else
            {
                this.DataContext = null;
                this.ButtonDisable.IsEnabled = false;
                this.ButtonOk.IsEnabled = false;
                this._selection = null;
            }
        }

        private void UpdateDialog()
        {
            foreach (StockServiceSettings item in this.ComboServiceName.Items) 
            {
                item.PropertyChanged -= this.OnSettingsChanged;
            }
            this.ComboServiceName.Items.Clear();
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
                item.PropertyChanged += this.OnSettingsChanged;
            }

            this._list = current;
            this.ComboServiceName.SelectedIndex = 0;
        }

        private async void OnSettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            StockServiceSettings settings = (StockServiceSettings)sender;
            if (e.PropertyName == "ApiKey" && !string.IsNullOrEmpty(settings.ApiKey))
            {
                this.ShowErrorMessage("Checking API key...");
                string error = await this._stockQuotes.TestApiKeyAsync(settings);
                // check this is still the current item.
                if (this._selection != null && this._selection.Name == settings.Name)
                {
                    this.ShowErrorMessage(error);
                }
            }
            else
            {
                this.ShowErrorMessage("");
            }
        }

        private void ShowErrorMessage(string msg)
        {
            this.ErrorMessage.Text = msg;
        }

        public List<StockServiceSettings> Settings
        {
            get { return this._list; }
        }

        private void Apply()
        {
            // TODO: apply has nothing to do since we don't yet have a proper cancel that restores the edited settings on cancel.
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
            if (this._selection != null && Uri.TryCreate(this._selection.Address, UriKind.Absolute, out Uri uri))
            {
                InternetExplorer.OpenUrl(IntPtr.Zero, uri);
            }
        }

        private void OnDisable(object sender, RoutedEventArgs e)
        {
            if (this._selection != null)
            {
                this._selection.ApiKey = null;
            }
        }

    }
}
