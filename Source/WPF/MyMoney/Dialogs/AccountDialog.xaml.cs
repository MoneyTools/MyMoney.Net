using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Data;
using Walkabout.StockQuotes;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for AccountDialog.xaml
    /// </summary>
    public partial class AccountDialog : BaseDialog
    {
        private readonly IServiceProvider serviceProvider;
        private readonly MyMoney money;
        private Account editingAccount = new Account();
        private Account theAccount = new Account();
        private readonly List<OnlineAccount> newOnlineAccounts = new List<OnlineAccount>();
        private readonly ObservableCollection<object> onlineAccounts = new ObservableCollection<object>();
        private const string NewLabel = "New...";

        public Account TheAccount
        {
            get { return this.theAccount; }
            set
            {
                this.theAccount = value;
                if (value != null)
                {
                    this.editingAccount = value.ShallowCopy();
                    this.DataContext = this.editingAccount;
                    this.UpdateUI();
                }
            }
        }

        public AccountDialog(MyMoney money, Account a, IServiceProvider sp)
        {
            this.InitializeComponent();

            this.serviceProvider = sp;

            this.onlineAccounts.Add(string.Empty); // so you can clear it out.
            this.money = money;
            this.Owner = Application.Current.MainWindow;
            this.TheAccount = a;
            this.onlineAccounts.Add(NewLabel); // so you can add new online accounts.

            List<string> currencies = new List<string>(Enum.GetNames(typeof(RestfulWebServices.CurrencyCode)));
            currencies.Sort();
            this.ComboBoxCurrency.ItemsSource = currencies;

            this.comboBoxOnlineAccount.ItemsSource = this.onlineAccounts;

            foreach (var alias in money.AccountAliases)
            {
                if (alias.AccountId == a.AccountId && !alias.IsDeleted)
                {
                    this.comboBoxAccountAliases.Items.Add(alias);
                }
            }
            if (this.comboBoxAccountAliases.Items.Count > 0)
            {
                this.comboBoxAccountAliases.SelectedIndex = 0;
            }

            foreach (var field in typeof(TaxStatus).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                var value = (TaxStatus)field.GetValue(null);
                this.ComboBoxTaxStatus.Items.Add(value);
            }

            this.UpdateUI();

            money.Changed += new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);

            Unloaded += (s, e) =>
            {
                money.Changed -= new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);
            };


            this.CheckButtonStates();

            this.TextBoxName.Focus();

            Closed += this.AccountDialog_Closed;
        }

        private void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
            this.OnOnlineAccountsChanged(sender, args);
            this.OnCurrencyDataChanged(sender, args);
        }

        private void AccountDialog_Closed(object sender, EventArgs e)
        {
            this.money.Changed -= new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);
        }

        private void OnOnlineAccountsChanged(object sender, ChangeEventArgs args)
        {
            bool changed = false;
            while (args != null)
            {
                OnlineAccount oa = args.Item as OnlineAccount;
                if (oa != null)
                {
                    if (args.ChangeType == ChangeType.Inserted)
                    {
                        this.newOnlineAccounts.Add(args.Item as OnlineAccount);
                    }
                    changed = true;
                }
                args = args.Next;
            }
            if (changed)
            {
                this.UpdateOnlineAccounts();
            }
        }

        private void OnCurrencyDataChanged(object sender, ChangeEventArgs args)
        {
            bool changed = false;
            while (args != null)
            {
                if (args.Item is Currency || args.Item is Currencies)
                {
                    changed = true;
                }
                args = args.Next;
            }
            if (changed)
            {
                this.UpdateRateText();
            }
        }

        private void UpdateRateText()
        {
            this.TextRate.Text = "";

            string current = (string)this.ComboBoxCurrency.SelectedItem;
            if (string.IsNullOrEmpty(current))
            {
                return;
            }

            Currency c = this.money.Currencies.FindCurrency(current);
            if (c != null && !c.IsUSD && c.Ratio != 0)
            {
                this.TextRate.Text = string.Format("$US {0:N2}", c.Ratio);
            }
        }

        private void UpdateUI()
        {
            this.UpdateOnlineAccounts();
            this.UpdateRateText();
        }

        private bool updating;

        private void UpdateOnlineAccounts()
        {
            this.updating = true;

            // Find any new accounts
            foreach (OnlineAccount oa in this.money.OnlineAccounts.Items)
            {
                if (string.IsNullOrEmpty(oa.Name))
                {
                    // cleanup - this should be in the database
                    oa.OnDelete();
                }
                else if (!this.onlineAccounts.Contains(oa))
                {
                    this.InsertAccount(oa);
                }
            }

            var existing = this.money.OnlineAccounts.GetOnlineAccounts();
            // find any removed accounts;
            foreach (object obj in new List<object>(this.onlineAccounts))
            {
                OnlineAccount oa = obj as OnlineAccount;
                if (oa != null && !existing.Contains(oa))
                {
                    this.onlineAccounts.Remove(oa);
                }
            }

            if (this.editingAccount.OnlineAccount != null)
            {
                this.comboBoxOnlineAccount.SelectedItem = this.editingAccount.OnlineAccount;
            }

            this.updating = false;
        }

        // Insert account in sorted order.
        private void InsertAccount(OnlineAccount oa)
        {
            for (int i = 0, n = this.onlineAccounts.Count; i < n; i++)
            {
                OnlineAccount other = this.onlineAccounts[i] as OnlineAccount;
                if (other != null && string.Compare(other.Name, oa.Name, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    this.onlineAccounts.Insert(i, oa);
                    return;
                }
            }
            this.onlineAccounts.Add(oa);
        }

        private OnlineAccount GetMatchingOnlineAccount(string name)
        {
            foreach (OnlineAccount oa in this.money.OnlineAccounts.Items)
            {
                if (this.editingAccount.OnlineAccount.Name == oa.Name)
                {
                    return oa;
                }
            }
            return null;
        }





        /// <summary>
        /// The user wants to go to the web site associated with the account
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnButtonGoToWebSite(object sender, RoutedEventArgs e)
        {

            if (string.IsNullOrWhiteSpace(this.editingAccount.WebSite))
            {
                MessageBoxEx.Show("You must supply the web site address");
                return;
            }

            string url = this.editingAccount.WebSite.ToLower();


            if (!url.StartsWith("https://") && !url.StartsWith("http://"))
            {
                url = "http://" + url;
            }

            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                try
                {
                    Uri webSite = new Uri(url, UriKind.Absolute);

                    InternetExplorer.OpenUrl(IntPtr.Zero, webSite);
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show("Count not start the web browser", null, ex.Message);
                }

            }
            else
            {
                MessageBoxEx.Show(string.Format("Invalid web site:{0}\"{1}\"", Environment.NewLine, this.editingAccount.WebSite));
            }
        }

        private void OnButtonOnlineAccountDetails_Click(object sender, RoutedEventArgs e)
        {
            var saved = this.theAccount.OnlineAccount;
            OnlineAccountDialog online = new OnlineAccountDialog(this.money, this.editingAccount, this.serviceProvider);
            online.Owner = this;
            if (online.ShowDialog() == true)
            {
                // force update of this.editingAccount since the real account may have been modified
                // by the OnlineAccountDialog
                var temp = this.theAccount;
                this.TheAccount = null;
                this.TheAccount = temp;
                this.UpdateUI();// make sure 'this.comboBoxOnlineAccount' has the new account
                this.comboBoxOnlineAccount.SelectedItem = this.editingAccount.OnlineAccount;
            }
            else
            {
                this.editingAccount.OnlineAccount = saved;
                this.comboBoxOnlineAccount.SelectedItem = saved;
            }
        }

        private void OnComboBoxOnlineAccount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!this.updating && this.comboBoxOnlineAccount.SelectedItem != null)
            {
                string name = this.comboBoxOnlineAccount.SelectedItem.ToString();
                if (name == NewLabel)
                {
                    this.OnButtonOnlineAccountDetails_Click(sender, e);
                }
                else if (string.IsNullOrEmpty(name))
                {
                    this.editingAccount.OnlineAccount = null;
                }
                else
                {
                    this.editingAccount.OnlineAccount = this.comboBoxOnlineAccount.SelectedItem as OnlineAccount;
                }
            }
        }


        /// <summary>
        /// User wants to accept the changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnButtonOk(object sender, RoutedEventArgs e)
        {
            // take the focus off whatever field may have it which will force commit of edit and binding of the
            // new value back to editingAccount object
            this.ButtonOk.Focus();
            this.HandleOk(5);
        }

        private void HandleOk(int retries)
        {
            if (string.IsNullOrEmpty(this.editingAccount.Name))
            {
                // then the ok button should not have been enabled, or databinding has not caught up yet.
                // This is unbelievable, but TwoWay databinding is also "asynchronous", so if you click OK fast
                // enough the edited fields have not been copied back to the bound editingAccount object.  A 
                // background priority dispatch invoke allows that binding to complete before closing the dialog.
                if (retries > 0)
                {
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        this.HandleOk(retries - 1);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                return;
            }
            if (this.theAccount == null)
            {
                this.theAccount = new Account();
            }
            this.theAccount.Name = this.editingAccount.Name;
            this.theAccount.AccountId = this.editingAccount.AccountId;
            this.theAccount.OfxAccountId = this.editingAccount.OfxAccountId;
            this.theAccount.Description = this.editingAccount.Description;
            this.theAccount.Type = this.editingAccount.Type;

            this.theAccount.OpeningBalance = this.editingAccount.OpeningBalance;
            if (this.editingAccount.OnlineAccount != null)
            {
                this.theAccount.OnlineAccount = this.GetMatchingOnlineAccount(this.editingAccount.OnlineAccount.Name);
            }
            else
            {
                this.theAccount.OnlineAccount = null;
            }
            var currency = this.editingAccount.Currency;
            this.theAccount.Currency = currency;
            this.theAccount.WebSite = this.editingAccount.WebSite;
            this.theAccount.Flags = this.editingAccount.Flags;
            this.theAccount.ReconcileWarning = this.editingAccount.ReconcileWarning;
            this.theAccount.LastSync = this.editingAccount.LastSync;

            if (!string.IsNullOrEmpty(currency))
            {
                var c = this.money.Currencies.FindCurrency(currency);
                if (c == null)
                {
                    c = new Data.Currency() { Symbol = currency };
                    c.Ratio = 1;
                    var found = (from ci in Walkabout.WpfConverters.CultureHelpers.CurrencyCultures
                                 where ci.CurrencySymbol == currency
                                 select ci).FirstOrDefault();
                    if (found != null)
                    {
                        c.Name = found.DisplayName;
                        if (currency == "USD")
                        {
                            c.CultureCode = "en-US";
                        }
                        else
                        {
                            c.CultureCode = found.CultureCode;
                        }
                    }
                    this.money.Currencies.AddCurrency(c);
                }
            }
            this.DialogResult = true;
        }


        /// <summary>
        /// User has wants to discard any changed made to this account properties
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCancel(object sender, RoutedEventArgs e)
        {
            foreach (Account a in this.money.Accounts)
            {
                if (a.OnlineAccount != null)
                {
                    // in case the "Add" button was used...
                    this.newOnlineAccounts.Remove(a.OnlineAccount);
                }
            }

            foreach (OnlineAccount oa in this.newOnlineAccounts)
            {
                if (oa != null)
                {
                    this.money.OnlineAccounts.RemoveOnlineAccount(oa);
                }
            }
            this.DialogResult = false;
            this.Close();
        }

        private static readonly char[] InvalidNameChars = new char[] { '{', '}', ':' };

        private void OnNameChanged(object sender, TextChangedEventArgs e)
        {
            this.CheckButtonStates();

            if (this.TextBoxName.Text.IndexOfAny(InvalidNameChars) >= 0)
            {
                this.TextBoxName.Background = Brushes.Red;
                this.TextBoxName.ToolTip = Walkabout.Properties.Resources.AccountNameValidChars;
                this.ButtonOk.IsEnabled = false;
            }
            else
            {
                this.TextBoxName.ClearValue(TextBox.BackgroundProperty);
                this.TextBoxName.ToolTip = "";
            }
        }

        private void CheckButtonStates()
        {
            this.ButtonOk.IsEnabled = !string.IsNullOrEmpty(this.TextBoxName.Text);
        }

        private void OnCurrencyChanged(object sender, SelectionChangedEventArgs e)
        {
            this.TextRate.Text = "";

            string currency = (string)this.ComboBoxCurrency.SelectedItem;

            if (currency == "USD")
            {
                return;
            }

            System.Windows.Threading.Dispatcher dispatcher = this.Dispatcher;

            ExchangeRates rates = this.serviceProvider.GetService(typeof(ExchangeRates)) as ExchangeRates;
            if (rates != null)
            {
                rates.Enqueue(currency);
            }

            this.UpdateRateText();
        }

        private void OnAccountAliaseDeleted(object sender, RoutedEventArgs args)
        {
            if (sender is FrameworkElement e && e.DataContext is AccountAlias a)
            {
                if (a.Parent is AccountAliases container)
                {
                    container.RemoveAlias(a);
                    this.comboBoxAccountAliases.Items.Remove(a);
                }
            }
        }

        private void OnAccountAliasesKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is ComboBox cb)
            {
                e.Handled = true; // don't let it close the dialog.
                string text = cb.Text.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    // create new alias?
                    var a = this.money.AccountAliases.FindAlias(text);
                    if (a == null)
                    {
                        a = new AccountAlias() { AliasType = AliasType.None, Pattern = text, AccountId = this.theAccount.AccountId };
                        this.money.AccountAliases.AddAlias(a);
                        this.comboBoxAccountAliases.Items.Add(a);
                    }
                    else if (a.AccountId != this.theAccount.AccountId)
                    {
                        // user is moving the alias?
                        a.AccountId = this.theAccount.AccountId;
                    }
                }
            }
        }
    }
}
