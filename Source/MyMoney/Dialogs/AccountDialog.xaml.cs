using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Walkabout.Controls;
using Walkabout.Data;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Network;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for AccountDialog.xaml
    /// </summary>
    public partial class AccountDialog : BaseDialog
    {
        IServiceProvider serviceProvider;
        MyMoney money;
        Account editingAccount = new Account();
        Account theAccount = new Account();
        List<OnlineAccount> newOnlineAccounts = new List<OnlineAccount>();
        ObservableCollection<object> onlineAccounts = new ObservableCollection<object>();
        const string NewLabel = "New...";

        public Account TheAccount
        {
            get { return theAccount; }
            set
            {
                theAccount = value;
                if (value != null)
                {
                    editingAccount = value.ShallowCopy();
                    this.DataContext = editingAccount;
                    UpdateUI();
                }
            }
        }

        public AccountDialog(MyMoney money, Account a, IServiceProvider sp)
        {
            InitializeComponent();

            this.serviceProvider = sp;

            onlineAccounts.Add(string.Empty); // so you can clear it out.
            this.money = money;
            this.Owner = Application.Current.MainWindow;
            this.TheAccount = a;
            onlineAccounts.Add(NewLabel); // so you can add new online accounts.

            List<string> currencies = new List<string>(Enum.GetNames(typeof(RestfulWebServices.CurrencyCode)));
            currencies.Sort();
            ComboBoxCurrency.ItemsSource = currencies;

            comboBoxOnlineAccount.ItemsSource = onlineAccounts;

            UpdateUI();

            money.Changed += new EventHandler<ChangeEventArgs>(OnMoneyChanged);


            CheckButtonStates();

            this.TextBoxName.Focus();

            this.Closed += AccountDialog_Closed;
        }

        void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
            OnOnlineAccountsChanged(sender, args);
            OnCurrencyDataChanged(sender, args);
        }

        void AccountDialog_Closed(object sender, EventArgs e)
        {
            money.Changed -= new EventHandler<ChangeEventArgs>(OnMoneyChanged);
        }

        void OnOnlineAccountsChanged(object sender, ChangeEventArgs args)
        {
            bool changed = false;
            while (args != null)
            {
                OnlineAccount oa = args.Item as OnlineAccount;
                if (oa != null)
                {
                    if (args.ChangeType == ChangeType.Inserted)
                    {
                        newOnlineAccounts.Add(args.Item as OnlineAccount);
                    }
                    changed = true;
                }
                args = args.Next;
            }
            if (changed)
            {
                UpdateOnlineAccounts();
            }
        }

        void OnCurrencyDataChanged(object sender, ChangeEventArgs args)
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
                UpdateRateText();
            }
        }

        void UpdateRateText()
        {
            TextRate.Text = "";

            string current = (string)ComboBoxCurrency.SelectedItem;
            if (string.IsNullOrEmpty(current))
            {
                return;
            }

            Currency c = this.money.Currencies.FindCurrency(current);            
            if (c != null && c.Symbol != "USD" && c.Ratio != 0)
            {
                TextRate.Text = string.Format("$US {0:N2}", c.Ratio);
            }
        }

        void UpdateUI()
        {
            UpdateOnlineAccounts();
            UpdateRateText();
        }

        bool updating;

        void UpdateOnlineAccounts()
        {
            updating = true;

            // Find any new accounts
            foreach (OnlineAccount oa in money.OnlineAccounts.Items)
            {
                if (string.IsNullOrEmpty(oa.Name))
                {
                    // cleanup - this should be in the database
                    oa.OnDelete();
                }
                else if (!onlineAccounts.Contains(oa))
                {
                    InsertAccount(oa);
                }
            }

            var existing = money.OnlineAccounts.GetOnlineAccounts();
            // find any removed accounts;
            foreach (object obj in new List<object>(onlineAccounts))
            {
                OnlineAccount oa = obj as OnlineAccount;
                if (oa != null && !existing.Contains(oa))
                {
                    onlineAccounts.Remove(oa);
                }
            }

            if (editingAccount.OnlineAccount != null)
            {
                this.comboBoxOnlineAccount.SelectedItem = editingAccount.OnlineAccount;
            }

            updating = false;
        }

        // Insert account in sorted order.
        void InsertAccount(OnlineAccount oa)
        {
            for (int i = 0, n = onlineAccounts.Count; i < n; i++)
            {
                OnlineAccount other = onlineAccounts[i] as OnlineAccount;
                if (other != null && string.Compare(other.Name, oa.Name, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    onlineAccounts.Insert(i, oa);
                    return;
                }
            }
            onlineAccounts.Add(oa);
        }

        OnlineAccount GetMatchingOnlineAccount(string name)
        {
            foreach (OnlineAccount oa in money.OnlineAccounts.Items)
            {
                if (editingAccount.OnlineAccount.Name == oa.Name)
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

            if (string.IsNullOrWhiteSpace(editingAccount.WebSite))
            {
                MessageBoxEx.Show("You must supply the web site address");
                return;
            }

            string url = editingAccount.WebSite.ToLower();


            if (!url.StartsWith("https://") && !url.StartsWith("http://"))
            {
                url = "http://" + url;
            }

            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                try
                {
                    Uri webSite = new Uri(url, UriKind.Absolute);

                    Process.Start(webSite.ToString());
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show("Count not start the web browser", null, ex.Message);
                }

            }
            else
            {
                MessageBoxEx.Show(string.Format("Invalid web site:{0}\"{1}\"", Environment.NewLine, editingAccount.WebSite));
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
                TheAccount = null;
                TheAccount = temp;
                UpdateUI();// make sure 'this.comboBoxOnlineAccount' has the new account
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
            if (!updating && comboBoxOnlineAccount.SelectedItem != null)
            {
                string name = comboBoxOnlineAccount.SelectedItem.ToString();
                if (name == NewLabel)
                {
                    OnButtonOnlineAccountDetails_Click(sender, e);
                }
                else if (string.IsNullOrEmpty(name))
                {
                    this.editingAccount.OnlineAccount = null; 
                }
                else
                {
                    this.editingAccount.OnlineAccount = comboBoxOnlineAccount.SelectedItem as OnlineAccount;
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
            ButtonOk.Focus();
            HandleOk(5);
        }

        private void HandleOk(int retries)
        {
            if (string.IsNullOrEmpty(editingAccount.Name))
            {
                // then the ok button should not have been enabled, or databinding has not caught up yet.
                // This is unbelievable, but TwoWay databinding is also "asynchronous", so if you click OK fast
                // enough the edited fields have not been copied back to the bound editingAccount object.  A 
                // background priority dispatch invoke allows that binding to complete before closing the dialog.
                if (retries > 0)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        HandleOk(retries - 1);
                    }), System.Windows.Threading.DispatcherPriority.Background);                   
                }
                return;                 
            }

            theAccount.Name = editingAccount.Name;
            theAccount.AccountId = editingAccount.AccountId;
            theAccount.OfxAccountId = editingAccount.OfxAccountId;
            theAccount.Description = editingAccount.Description;
            theAccount.Type = editingAccount.Type;

            theAccount.OpeningBalance = editingAccount.OpeningBalance;
            if (editingAccount.OnlineAccount != null)
            {
                theAccount.OnlineAccount = GetMatchingOnlineAccount(editingAccount.OnlineAccount.Name);
            }
            else
            {
                theAccount.OnlineAccount = null;
            }
            theAccount.Currency = editingAccount.Currency;
            theAccount.WebSite = editingAccount.WebSite;
            theAccount.Flags = editingAccount.Flags;
            theAccount.ReconcileWarning = editingAccount.ReconcileWarning;
            theAccount.LastSync = editingAccount.LastSync;

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
                    money.OnlineAccounts.RemoveOnlineAccount(oa);
                }
            }
            this.DialogResult = false;
            this.Close();
        }

        static char[] InvalidNameChars = new char[] { '{', '}', ':' };

        private void OnNameChanged(object sender, TextChangedEventArgs e)
        {
            CheckButtonStates();

            if (TextBoxName.Text.IndexOfAny(InvalidNameChars) >= 0)
            {
                TextBoxName.Background = Brushes.Red;
                TextBoxName.ToolTip = Walkabout.Properties.Resources.AccountNameValidChars;
                ButtonOk.IsEnabled = false;
            }
            else
            {
                TextBoxName.ClearValue(TextBox.BackgroundProperty);
            }
        }

        private void CheckButtonStates()
        {
            ButtonOk.IsEnabled = !string.IsNullOrEmpty(TextBoxName.Text);
        }

        private void OnCurrencyChanged(object sender, SelectionChangedEventArgs e)
        {
            TextRate.Text = "";

            string currency = (string)ComboBoxCurrency.SelectedItem;

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

            UpdateRateText();
        }

    }
}
