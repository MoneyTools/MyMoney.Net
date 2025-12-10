using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Walkabout.Configuration;
using Walkabout.Data;
using Walkabout.Utilities;

namespace Walkabout.Controls
{
    /// <summary>
    /// Interaction logic for SettingsDialog.xaml
    /// </summary>
    public partial class AppSettings : UserControl
    {
        private Settings settings;
        private DatabaseSettings databaseSettings;
        private IDatabase database;
        private Currencies currencies;

        private readonly IDictionary<string, string> themes = new SortedDictionary<string, string>() {
            { "Light", "Light" },
            { "Dark", "Dark" }
        };

        /// <summary>
        /// Constructor
        /// </summary>
        public AppSettings()
        {
            this.InitializeComponent();

            this.settings = Settings.TheSettings;
            IsVisibleChanged += this.OnIsVisibleChanged;

            // Fiscal Years
            int year = DateTime.Now.Year;
            for (int i = 0; i < 12; i++)
            {
                var month = new DateTime(year, i + 1, 1);
                var label = month.ToString("MMMM");
                this.comboBoxFiscalYear.Items.Add(label);
            }

            // Themes
            foreach (var theme in this.themes.Keys)
            {
                this.comboBoxTheme.Items.Add(theme);
            }
            this.comboBoxTheme.SelectedItem = this.settings.Theme;

            // Match transactions
            this.textBoxTransferSearchDays.Text = this.settings.TransferSearchDays.ToString();
        }

        internal void SetSite(IServiceProvider site)
        {
            this.settings = (Settings)site.GetService(typeof(Settings));
            this.databaseSettings = (DatabaseSettings)site.GetService(typeof(DatabaseSettings));
            this.database = (IDatabase)site.GetService(typeof(IDatabase));

            // Display Currency As
            this.currencies = (Currencies)site.GetService(typeof(Currencies));
        }

        public event EventHandler Closed;

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool b)
            {
                if (b)
                {
                    this.checkBoxRentalSupport.IsChecked = this.databaseSettings.RentalManagement;
                    this.checkBoxShowCurrency.IsChecked = this.databaseSettings.ShowCurrency;
                    this.checkBoxPlaySounds.IsChecked = this.settings.PlaySounds;
                    this.checkBoxAcceptReconciled.IsChecked = this.settings.AcceptReconciled;
                    this.comboBoxFiscalYear.SelectedIndex = this.databaseSettings.FiscalYearStart;
                    this.textBoxFastForex.Text = this.settings.FastForexKey;

                    this.comboBoxCurrency.Items.Clear();
                    this.currencies?.GetCurrencies().ToList().ForEach(currency =>
                    {
                        this.comboBoxCurrency.Items.Add(currency.Symbol);
                    });

                    // Ensure that USD is always an option
                    if (!this.comboBoxCurrency.Items.Contains("USD"))
                    {
                        this.comboBoxCurrency.Items.Insert(0, "USD");
                    }

                    this.comboBoxCurrency.SelectedIndex = this.comboBoxCurrency.Items.IndexOf(this.databaseSettings.DisplayCurrency);

                    Visibility passwordVisibility = Visibility.Visible;
                    if (this.database != null && this.database.DbFlavor == DbFlavor.Sqlite)
                    {
                        passwordVisibility = Visibility.Collapsed;
                    }


                    this.passwordPrompt.Visibility = passwordVisibility;
                    this.editPasswordBox.Visibility = passwordVisibility;

                    foreach (string theme in this.comboBoxTheme.Items)
                    {
                        if (this.themes[theme] == this.settings.Theme)
                        {
                            this.comboBoxTheme.SelectedIndex = this.comboBoxTheme.Items.IndexOf(theme);
                        }
                    }
                }
                else
                {
                    if (Closed != null)
                    {
                        Closed(this, EventArgs.Empty);
                    }
                }
            }
        }

        public event EventHandler PasswordChanged;

        public string Password
        {
            get { return this.editPasswordBox.Password; }
            set { this.editPasswordBox.Password = value; }
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (PasswordChanged != null)
            {
                PasswordChanged(this, e);
            }
        }

        private void OnFiscalYearChanged(object sender, SelectionChangedEventArgs e)
        {
            this.databaseSettings.FiscalYearStart = this.comboBoxFiscalYear.SelectedIndex;
        }

        private void OnCurrencyChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.comboBoxCurrency.SelectedValue != null)
            {
                this.databaseSettings.DisplayCurrency = this.comboBoxCurrency.SelectedValue.ToString();
                this.UpdateCultureCode();
            }
        }

        private void UpdateCultureCode()
        {
            if (this.currencies != null)
            {
                this.cultureCodeInfo.Content = "";

                this.currencies.GetCurrencies().ToList().ForEach(currency =>
                {
                    if (currency.Symbol == this.databaseSettings.DisplayCurrency)
                    {
                        CultureInfo culture = StringHelpers.GetDefaultCultureInfo(currency.CultureCode);
                        var amountInTargetCulture = string.Format(culture, "{0:C}", currency.Ratio);
                        this.cultureCodeInfo.Content = currency.Name + " $1 USD=" + amountInTargetCulture + " in " + currency.CultureCode + " " + culture.DisplayName;
                    }
                });
            }
        }

        private void OnShowCurrencyChanged(object sender, RoutedEventArgs e)
        {
            this.databaseSettings.ShowCurrency = this.checkBoxShowCurrency.IsChecked == true;
        }

        private void OnRentalSupportChanged(object sender, RoutedEventArgs e)
        {
            this.databaseSettings.RentalManagement = this.checkBoxRentalSupport.IsChecked == true;
        }

        private void OnPlaySoundsChanged(object sender, RoutedEventArgs e)
        {
            this.settings.PlaySounds = this.checkBoxPlaySounds.IsChecked == true;
        }

        private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                string name = (string)e.AddedItems[0];
                if (this.themes.TryGetValue(name, out string theme))
                {
                    this.settings.Theme = theme;
                }
            }
        }

        private void OnTransferDaysChanged(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(this.textBoxTransferSearchDays.Text, out int x) && x > 0)
            {
                this.settings.TransferSearchDays = x;
            }
        }

        private void OnAcceptReconciledChanged(object sender, RoutedEventArgs e)
        {
            this.settings.AcceptReconciled = this.checkBoxAcceptReconciled.IsChecked == true;
        }

        private void OnOfxOverrideChanged(object sender, RoutedEventArgs e)
        {
            this.settings.ImportOFXAsUTF8 = this.checkBoxOfxUtf8Override.IsChecked == true;
        }

        private void OnFastForexKeyChanged(object sender, TextChangedEventArgs e)
        {
            var key = this.textBoxFastForex.Text.Trim();
            if (key != this.settings.FastForexKey)
            {
                this.settings.FastForexKey = key;
            }
        }
    }
}
