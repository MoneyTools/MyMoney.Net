using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Walkabout.Data;
using System;
using System.Linq;
using System.Collections.Generic;


namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for SelectAccountDialog.xaml
    /// </summary>
    public partial class SelectAccountDialog : Window
    {

        public SelectAccountDialog()
        {
            InitializeComponent();

            ButtonOk.IsEnabled = false;

            // TO DO - THEME ALL DIALOG BOX
            //ResourceDictionary theme = new ResourceDictionary();
            //theme.Source = new Uri("Themes/Theme-Flat.xaml", UriKind.Relative);
            //Resources.MergedDictionaries.Add(theme);
        }

        private void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Cancel = true;
            this.Close();
        }

        public bool Cancel { get; set; }

        public bool AddAccount { get; set; }

        private void ButtonAdd_Click(object sender, RoutedEventArgs e)
        {
            this.AddAccount = true;
            this.Close();
        }


        public void SetUnknownAccountPrompt(string text)
        {
            TextBlockPrompt.Text = text;
        }

        public void SetAccounts(IList<Account> list)
        {
            // put closed accounts at the end.
            foreach (var a in list.ToArray())
            {
                if (a.IsClosed)
                {
                    list.Remove(a);
                    list.Add(a);
                }
            }
            this.ListBoxAccounts.ItemsSource = list;
        }

        public Account SelectedAccount
        {
            get
            {
                return this.ListBoxAccounts.SelectedItem as Account;
            }
        }

        private void ListBoxAccounts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ButtonOk.IsEnabled = true;
        }


    }

    static class AccountHelper
    {
        static public Account PickAccount(MyMoney money, string id)
        {
            SelectAccountDialog frm = new SelectAccountDialog();
            frm.Title = "Select Account for: " + id;
            frm.SetAccounts(money.Accounts.GetAccounts());
            frm.Owner = App.Current.MainWindow;
            Account a = null;
            if (frm.ShowDialog() == true)
            {
                a = frm.SelectedAccount;
                Debug.Assert(a != null, "FormAccountSelect should have selected an account");
            }
            if (frm.AddAccount)
            {
                AccountDialog newAccountDialog = new AccountDialog(money, a, App.Current.MainWindow as IServiceProvider);
                newAccountDialog.Owner = App.Current.MainWindow;
                if (newAccountDialog.ShowDialog() == true)
                {
                    a = newAccountDialog.TheAccount;
                    money.Accounts.Add(a);
                }                
            }
            else if (frm.Cancel)
            {
                return null;
            }
            return a;
        }
    }
}
