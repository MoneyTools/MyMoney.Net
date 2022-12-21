using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for AccountDialog.xaml
    /// </summary>
    public partial class LoanDialog : BaseDialog
    {

        public MyMoney Money { get; set; }

        private Account editingAccount = new Account();

        public Account EditingAccount
        {
            get
            {
                return this.editingAccount;
            }
        }

        private Account theAccount = new Account();
        private readonly List<OnlineAccount> newOnlineAccounts = new List<OnlineAccount>();
        private readonly ObservableCollection<object> onlineAccounts = new ObservableCollection<object>();

        public Account TheAccount
        {
            get { return this.theAccount; }
            set
            {
                this.theAccount = value;
                if (value != null)
                {
                    this.editingAccount = value.ShallowCopy();
                    this.DataContext = this.EditingAccount;
                }
            }
        }

        private const string NewLabel = "New...";

        public LoanDialog(MyMoney money, Account a)
        {
            this.Money = money;

            this.InitializeComponent();

            this.Owner = Application.Current.MainWindow;
            this.TheAccount = a;


            this.onlineAccounts.Add(string.Empty); // so you can clear it out.

            foreach (OnlineAccount oa in this.Money.OnlineAccounts.Items)
            {
                if (string.IsNullOrEmpty(oa.Name))
                {
                    // what's this doing here?
                }
                else
                {
                    this.InsertAccount(oa);
                }
            }

            List<string> currencies = new List<string>(Enum.GetNames(typeof(RestfulWebServices.CurrencyCode)));
            currencies.Sort();
            this.ComboBoxCurrency.ItemsSource = currencies;
        }


        private void ComboBoxForCategory_FilterChanged(object sender, RoutedEventArgs e)
        {
            FilteringComboBox combo = sender as FilteringComboBox;
            combo.Items.Filter = new Predicate<object>((o) => { return ((Category)o).GetFullName().IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0; });
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
            foreach (OnlineAccount oa in this.Money.OnlineAccounts.Items)
            {
                if (this.editingAccount.OnlineAccount.Name == oa.Name)
                {
                    return oa;
                }
            }
            return null;
        }

        private void ButtonOk(object sender, RoutedEventArgs e)
        {
            this.theAccount.Name = this.editingAccount.Name;
            this.theAccount.AccountId = this.editingAccount.AccountId;
            this.theAccount.Description = this.editingAccount.Description;
            this.theAccount.Type = AccountType.Loan;
            this.theAccount.CategoryForPrincipal = this.editingAccount.CategoryForPrincipal;
            this.theAccount.CategoryForInterest = this.editingAccount.CategoryForInterest;

            this.theAccount.OpeningBalance = this.editingAccount.OpeningBalance;
            if (this.editingAccount.OnlineAccount != null)
            {
                this.theAccount.OnlineAccount = this.GetMatchingOnlineAccount(this.editingAccount.OnlineAccount.Name);
            }
            this.theAccount.Currency = this.editingAccount.Currency;
            this.theAccount.WebSite = this.editingAccount.WebSite;
            this.theAccount.IsClosed = this.editingAccount.IsClosed;

            this.DialogResult = true;
        }

        private void ButtonGoToWebSite(object sender, RoutedEventArgs e)
        {

            string url = this.editingAccount.WebSite.ToLower();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBoxEx.Show("You must supply the web site address");
                return;
            }


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
                MessageBoxEx.Show(string.Format("Invalid web site:{0}\"{1}\"", Environment.NewLine, this.editingAccount.WebSite));
            }
        }



        private void OnCancel(object sender, RoutedEventArgs e)
        {
            foreach (OnlineAccount oa in this.newOnlineAccounts)
            {
                if (oa != null)
                {
                    this.Money.OnlineAccounts.RemoveOnlineAccount(oa);
                }
            }
            this.DialogResult = false;
            this.Close();
        }
    }
}
