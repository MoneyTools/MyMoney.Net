using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Walkabout.Configuration;
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
        private bool _initialized = false;
        private string _password = string.Empty;
        private bool _selectingService;
        private DelayedActions _actions = new DelayedActions();

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
            if (!this._initialized)
            {
                return;
            }
            int i = this.ComboServiceName.SelectedIndex;
            if (i >= 0 && i < this._list.Count)
            {
                var service = this._list[i];
                this.DataContext = service;
                this._selectingService = true;
                this.PasswordBoxApiKey.Password = service.ApiKey;
                this._selectingService = false;
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
                this.PasswordBoxApiKey.Password = null;
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
            this.CheckMultiple();
        }

        private void OnSettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            this.ShowErrorMessage("");
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
                this.PasswordBoxApiKey.Password = "";
            }
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!this._selectingService && !_busy)
            {
                _actions.StartDelayedAction("CheckApiKey", this.CheckApiKey, TimeSpan.FromSeconds(1));
            }
        }

        bool _busy = false;

        async void CheckApiKey()
        {
            var box = this.PasswordBoxApiKey;
            StockServiceSettings settings = (StockServiceSettings)box.DataContext;
            settings.ApiKey = box.Password;
            if (string.IsNullOrEmpty(settings.ApiKey))
            {
                this.ShowErrorMessage("Account disabled.");
            }
            else
            {
                this.ShowErrorMessage("Checking API key...");
                this._busy = true;
                string error = null;
                try
                {
                    error = await this._stockQuotes.TestApiKeyAsync(settings);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                this._busy = false;
                if (this._selection != null && this._selection.Name == settings.Name)
                {
                    // user is still on the same service
                    if (!string.IsNullOrEmpty(error))
                    {
                        if (this._selection != null && this._selection.Name == settings.Name)
                        {
                            this.ShowErrorMessage(error);
                            this._busy = false;
                            return;
                        }
                    }
                }

                this.ShowErrorMessage("Service is working");

                // Check if multiple
                this.CheckMultiple();
            }
        }

        void CheckMultiple()
        {
            // Check if multiple
            List<string> enabled = new List<string>(from s in this._list
                                                    where !string.IsNullOrEmpty(s.ApiKey)
                                                    select s.Name);
            if (enabled.Count > 1)
            {
                var msg = string.Join(",", enabled);
                this.ShowErrorMessage($"Multiple services are enabled: {msg}, this is not recommended");
            }
        }

        private void OnPasswordLostFocus(object sender, RoutedEventArgs e)
        {
            if (!_busy && _actions.HasDelayedAction("CheckApiKey"))
            {
                // do it right away!
                _actions.StartDelayedAction("CheckApiKey", this.CheckApiKey, TimeSpan.FromMilliseconds(0));
            }
        }
    }
}
