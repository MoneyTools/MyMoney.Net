using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Walkabout.Data;
using Walkabout.Controls;
using System.Collections;
using Walkabout.Utilities;
using System.Windows.Data;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for AccountDialog.xaml
    /// </summary>
    public partial class LoanDialog : Window
    {
       
        public MyMoney Money { get; set; }

        Account editingAccount = new Account();

        public Account EditingAccount
        {
            get
            {
                return editingAccount;
            }
        }
        
        Account theAccount = new Account();
        List<OnlineAccount> newOnlineAccounts = new List<OnlineAccount>();
        ObservableCollection<object> onlineAccounts = new ObservableCollection<object>();

        public Account TheAccount
        {
            get { return theAccount; }
            set
            {
                theAccount = value;
                if (value != null)
                {
                    editingAccount = value.ShallowCopy();
                    this.DataContext = EditingAccount;
                }
            }
        }

        const string NewLabel = "New...";

        public LoanDialog(MyMoney money, Account a)
        {
            this.Money = money;

            InitializeComponent();

            this.Owner = Application.Current.MainWindow;
            this.TheAccount = a;


            onlineAccounts.Add(string.Empty); // so you can clear it out.

            foreach (OnlineAccount oa in Money.OnlineAccounts.Items)
            {
                if (string.IsNullOrEmpty(oa.Name))
                {
                    // what's this doing here?
                }
                else
                {
                    InsertAccount(oa);
                }
            }
        }


        private void ComboBoxForCategory_FilterChanged(object sender, RoutedEventArgs e)
        {
            FilteringComboBox combo = sender as FilteringComboBox;
            combo.Items.Filter = new Predicate<object>((o) => { return ((Category)o).GetFullName().IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0; });
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
            foreach (OnlineAccount oa in Money.OnlineAccounts.Items)
            {
                if (editingAccount.OnlineAccount.Name == oa.Name)
                {
                    return oa;
                }
            }
            return null;
        }

        private void ButtonOk(object sender, RoutedEventArgs e)
        {
            theAccount.Name = editingAccount.Name;
            theAccount.AccountId = editingAccount.AccountId;
            theAccount.Description = editingAccount.Description;
            theAccount.Type = AccountType.Loan;
            theAccount.CategoryForPrincipal = editingAccount.CategoryForPrincipal;
            theAccount.CategoryForInterest = editingAccount.CategoryForInterest;

            theAccount.OpeningBalance = editingAccount.OpeningBalance;
            if (editingAccount.OnlineAccount != null)
            {
                theAccount.OnlineAccount = GetMatchingOnlineAccount(editingAccount.OnlineAccount.Name);
            }
            theAccount.Currency = editingAccount.Currency;
            theAccount.WebSite = editingAccount.WebSite;
            theAccount.IsClosed = editingAccount.IsClosed;
            theAccount.IsBudgeted = editingAccount.IsBudgeted;

            this.DialogResult = true;
        }

        private void ButtonGoToWebSite(object sender, RoutedEventArgs e)
        {

            string url = editingAccount.WebSite.ToLower();
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
                MessageBoxEx.Show(string.Format("Invalid web site:{0}\"{1}\"", Environment.NewLine, editingAccount.WebSite));
            }
        }



        private void OnCancel(object sender, RoutedEventArgs e)
        {
            foreach (OnlineAccount oa in this.newOnlineAccounts)
            {
                if (oa != null)
                {
                    Money.OnlineAccounts.RemoveOnlineAccount(oa);
                }
            }
            this.DialogResult = false;
            this.Close();
        }


      



    }
}
