using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Dialogs;
using Walkabout.Utilities;
using Walkabout.Migrate;
using Walkabout.Taxes;
using System.IO;
using System.Windows.Media.Imaging;
using Walkabout.Help;
#if PerformanceBlocks
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
#endif

namespace Walkabout.Views.Controls
{
    /// <summary>
    /// Interaction logic for AccountsControl.xaml
    /// </summary>
    public partial class AccountsControl : UserControl, IClipboardClient, IContainerStatus
    {
        #region Commands
        public static RoutedUICommand CommandFileImport;
        public static RoutedUICommand CommandSynchronize;
        public static RoutedUICommand CommandBalance;
        public static RoutedUICommand CommandNewAccount;
        public static RoutedUICommand CommandDownloadAccounts;
        public static RoutedUICommand CommandAddNewLoanAccount;
        public static RoutedUICommand CommandDeleteAccount;
        public static RoutedUICommand CommandViewTransfers;
        public static RoutedUICommand CommandExportAccount;
        public static RoutedUICommand CommandToggleClosedAccounts;

        static AccountsControl()
        {
            CommandFileImport = new RoutedUICommand("Properties", "Properties", typeof(AccountsControl));
            CommandSynchronize = new RoutedUICommand("Synchronize", "Synchronize", typeof(AccountsControl));
            CommandBalance = new RoutedUICommand("Balance", "Balance", typeof(AccountsControl));
            CommandNewAccount = new RoutedUICommand("NewAccount", "NewAccount", typeof(AccountsControl));
            CommandDownloadAccounts = new RoutedUICommand("DownloadAccounts", "DownloadAccounts", typeof(AccountsControl));
            CommandAddNewLoanAccount = new RoutedUICommand("NewLoanAccount", "NewLoanAccount", typeof(AccountsControl));
            CommandDeleteAccount = new RoutedUICommand("DeleteAccount", "DeleteAccount", typeof(AccountsControl));
            CommandViewTransfers = new RoutedUICommand("ViewTransfers", "ViewTransfers", typeof(AccountsControl));
            CommandExportAccount = new RoutedUICommand("ExportAccount", "ExportAccount", typeof(AccountsControl));
            CommandToggleClosedAccounts = new RoutedUICommand("ToggleClosedAccounts", "ToggleClosedAccounts", typeof(AccountsControl));
        }

        #endregion 

        #region PROPERTIES

        public IServiceProvider Site { get; set; }

        private MyMoney myMoney;

        public MyMoney MyMoney
        {
            get { return myMoney; }
            set
            {
                if (this.myMoney != null)
                {
                    myMoney.Accounts.Changed -= new EventHandler<ChangeEventArgs>(OnAccountsChanged);
                    myMoney.Rebalanced -= new EventHandler<ChangeEventArgs>(OnBalanceChanged);
                    myMoney.Changed -= new EventHandler<ChangeEventArgs>(OnMoneyChanged);                    
                }
                myMoney = value;

                if (value != null)
                {
                    myMoney.Accounts.Changed += new EventHandler<ChangeEventArgs>(OnAccountsChanged);
                    myMoney.Rebalanced += new EventHandler<ChangeEventArgs>(OnBalanceChanged);
                    myMoney.Changed += new EventHandler<ChangeEventArgs>(OnMoneyChanged);
                    OnAccountsChanged(this, new ChangeEventArgs(myMoney.Accounts, null, ChangeType.Reloaded));
                }
                else
                {
                    Select(null);
                }
            }
        }

        private void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
            while (args != null)
            {
                if (args.Item is Account)
                {
                    Dispatcher.BeginInvoke(new Action(Rebind));
                    return;
                }
                args = args.Next;
            }
        }

        public Account SelectedAccount
        {
            get { return Selected as Account; }
            set { Select(value); }
        }

        public Object Selected
        {
            get { return this.listBox1.SelectedItem; }
            set { Select(value); }
        }

        private bool displayClosedAccounts = false;

        private TextBlock statusArea;

        #endregion

        #region EVENTS

        public event EventHandler SelectionChanged;

        public event EventHandler<ChangeEventArgs> BalanceAccount;
        public event EventHandler<ChangeEventArgs> SyncAccount;
        public event EventHandler<ChangeEventArgs> ShowTransfers;
        #endregion
        
        #region IClipboardClient SUPPORT
        public bool CanCut
        {
            get { return SelectedAccount != null; }
        }
        public bool CanCopy
        {
            get { return SelectedAccount != null; }
        }
        public bool CanPaste
        {
            get { return Clipboard.ContainsText(); }
        }
        public bool CanDelete
        {
            get { return SelectedAccount != null; }
        }
        public void Cut()
        {
            Account a = SelectedAccount;
            if (a != null)
            {
                a = DeleteAccount(a);
                CopyToClipboard(a);
            }
        }

        static void CopyToClipboard(Account a)
        {
            if (a != null)
            {
                string xml = a.Serialize();
                Clipboard.SetDataObject(xml, true);
            }
        }

        public void Copy()
        {
            Account a = SelectedAccount;
            CopyToClipboard(a);
        }

        public void Delete()
        {
            Account a = SelectedAccount;

            if (a != null)
            {
                DeleteAccount(a);
            }
        }

        public void Paste()
        {
            IDataObject data = Clipboard.GetDataObject();
            if (data.GetDataPresent(typeof(string)))
            {
                string xml = (string)data.GetData(typeof(string));
                Importer importer = new Importer(this.myMoney);
                Account a = importer.ImportAccount(xml);
                if (a == null)
                {
                    MessageBoxEx.Show("Clipboard doesn't seem to contain valid account information", "Paste Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        #region IContainerStatus SUPPORT

        public void SetTextBlock(TextBlock statusControl)
        {
            statusArea = statusControl;
            if (statusArea != null)
            {
                statusArea.FontSize = 11;
            }
        }

        #endregion

        public AccountsControl()
        {
            
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.AccountsControlInitialize))
            {
#endif
            InitializeComponent();

            this.listBox1.PreviewMouseDown += new MouseButtonEventHandler(listBox1_PreviewMouseDown);
            this.listBox1.SelectionChanged += new SelectionChangedEventHandler(OnListBoxSelectionChanged);
            this.listBox1.MouseDoubleClick += new MouseButtonEventHandler(OnListBoxMouseDoubleClick);

            foreach (object o in AccountsControlContextMenu.Items)
            {
                MenuItem m = o as MenuItem;
                if (m != null) {
                    m.CommandTarget = this;
                }
            }
      
#if PerformanceBlocks
            }
#endif  
        }

        void listBox1_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            AccountSectionHeader item = GetElementFromPoint(listBox1, e.GetPosition(listBox1)) as AccountSectionHeader;

            if (item != null)
            {
                if (item.IsSelectable == false)
                {
                    e.Handled = true;
                }
            }
            
        }

        void OnListBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            object item = GetElementFromPoint(listBox1, e.GetPosition(listBox1));

            if (item != null)
            {
                if (item is AccountSectionHeader)
                {
                    // Don't allow Property editing of AccountSectionHeader types
                }
                else
                {
                    this.Selected = item;
                    Account a = this.SelectedAccount;
                    EditDetails(a);
                }
            }
        }

        private object GetElementFromPoint(ItemsControl hostingControl, Point point)
        {
            UIElement element = (UIElement)hostingControl.InputHitTest(point);
            while (element != null)
            {

                if (element == hostingControl)
                {
                    return null;
                }

                object item = hostingControl.ItemContainerGenerator.ItemFromContainer(element);

                bool itemFound = !(item.Equals(DependencyProperty.UnsetValue));

                if (itemFound)
                {
                    return item;
                }
                element = (UIElement)VisualTreeHelper.GetParent(element);
            }

            return null;
        }



        public void OnBalanceChanged(object sender, ChangeEventArgs e)
        {
            //            this.Invalidate();
        }

        public void OnAccountsChanged(object sender, ChangeEventArgs e)
        {
            Rebind();
        }

        void Rebind()
        {
            UpdateContextMenuView();

            if (myMoney != null)
            {
                this.Accounts = new List<object>(myMoney.Accounts.GetAccounts(!this.displayClosedAccounts));
            }
        }


        public bool DisplayClosedAccounts
        {
            get { return this.displayClosedAccounts; }
            set
            {
                this.displayClosedAccounts = value;
                Rebind();
            }
        }

        public IList<object> Accounts
        {
            get { return this.listBox1.DataContext as List<object>; }
            set
            {
                if (value != null)
                {
                    //---------------------------------------------------------
                    // First make a copy of the collection in order to 
                    // help LINQ do it's magic over the collection
                    List<Account> inputList = new List<Account>();
                    foreach (object o in value)
                    {
                        Account a = o as Account;
                        if (a != null)
                        {
                            // Assert Section in the List
                            inputList.Add(a);
                        }
                    }

                    List<object> output = new List<object>();

                    decimal netWorth = 0;

                    var accountOfTypeBanking = from a in inputList where a.Type == AccountType.Checking || a.Type == AccountType.Savings || a.Type == AccountType.Cash select a;
                    AccountSectionHeader sh = BundleAccount("Banking", output, accountOfTypeBanking);
                    netWorth += sh.BalanceInNormalizedCurrencyValue;

                    var accountOfTypeCredit = from a in inputList where a.Type == AccountType.Credit || a.Type == AccountType.CreditLine select a;
                    sh = BundleAccount("Credit", output, accountOfTypeCredit);
                    netWorth += sh.BalanceInNormalizedCurrencyValue;

                    var accountOfTypeInvestment = from a in inputList where a.Type == AccountType.Investment || a.Type == AccountType.MoneyMarket select a;
                    sh = BundleAccount("Investment", output, accountOfTypeInvestment);
                    sh.IsSelectable = true; // Only the INVESTEMENT Header can be selected 
                    netWorth += sh.BalanceInNormalizedCurrencyValue;

                    var accountOfTypeAsset = from a in inputList where a.Type == AccountType.Asset select a;
                    sh = BundleAccount("Assets", output, accountOfTypeAsset);
                    netWorth += sh.BalanceInNormalizedCurrencyValue;

                    var accountOfTypeLoan = from a in inputList where a.Type == AccountType.Loan select a;
                    sh = BundleAccount("Loans", output, accountOfTypeLoan);
                    netWorth += sh.BalanceInNormalizedCurrencyValue;

                    if (statusArea != null)
                    {
                        statusArea.Text = netWorth.ToString("C");
                    }
                    this.listBox1.DataContext = output;
                }
            }
        }

        private static AccountSectionHeader BundleAccount(string caption, List<object> output, IEnumerable<Account> accountOfTypeBanking)
        {
            AccountSectionHeader sectionHeader = new AccountSectionHeader();
            sectionHeader.IsSelectable = false; // By default the Account header are not selectable in the list box 

            if (accountOfTypeBanking.Count() > 0)
            {

                sectionHeader.Title = caption;

                List<Account> bundle = new List<Account>();
                sectionHeader.Accounts = bundle;

                output.Add(sectionHeader);

                foreach (Account a in accountOfTypeBanking)
                {
                    sectionHeader.BalanceInNormalizedCurrencyValue+= a.BalanceNormalized;
                    output.Add(a);
                    bundle.Add(a);
                }
            }
            return sectionHeader;
        }

        // our idea of what is selected.  We only raise SelectionChanged event if this doesn't match.
        object selected;

        void Select(object item)
        {
            if (item != null && !(item is AccountSectionHeader) && !this.Accounts.Contains(item) && 
                !DisplayClosedAccounts && myMoney.Accounts.GetAccounts(true).Contains(item))
            {
                // then user is trying to jump to a transaction in a hidden closed account, so make the closed
                // accounts visible otherwise jump will fail.
                DisplayClosedAccounts = true;
            }

            this.listBox1.SelectedItem = item;
            this.listBox1.ScrollIntoView(item);
            this.selected = item;
        }
        

        void OnListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            object selected = (e.AddedItems.Count > 0) ? e.AddedItems[0] : null;

            AccountSectionHeader ash = selected as AccountSectionHeader;

            if (ash != null && ash.IsSelectable == false)
            {
                // Don't allow selection of an AcccountHeader when it does not want to
                return;
            }

            if (this.selected != selected && SelectionChanged != null)
            {
                this.selected = selected;
                SelectionChanged(this, EventArgs.Empty);
            }
        }

        public Account DeleteAccount(Account account)
        {
            string caption = "Delete Account: " + account.Name;

            if (MessageBoxEx.Show(string.Format("Are you sure you want to delete account '{0}'?\nThis is not undoable operation.", account.Name),
                caption, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return null;
            }
            
            // figure out which account to select next.
            List<object> data = (List<object>)this.listBox1.DataContext;
            Account prev = null;
            Account next = null;
            bool found = false;
            for (int i = 0; i < data.Count; i++)
            {
                Account a = data[i] as Account;
                if (a != null)
                {
                    if (a == account)
                    {
                        found = true;
                    }
                    else if (!found)
                    {
                        prev = a;
                    }
                    else if (next == null)
                    {
                        next = a;
                        break;
                    }
                }
                else if (found && prev != null)
                {
                    // if we reach a section header then prefer the 'previous' account.
                    break;
                }
            }

            // mark it as deleted.
            MyMoney myMoney = this.MyMoney;
            myMoney.Accounts.RemoveAccount(account);

            // Rebind should have already happened, so we can now select the neighboring account.
            this.Selected = (next != null) ? next : prev;

            return account;
        }

        void EditDetails(Account a)
        {
            if (a.Type == AccountType.Loan)
            {
                Walkabout.Dialogs.LoanDialog dialog = new Dialogs.LoanDialog(myMoney, a);
                dialog.Owner = App.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    myMoney.Rebalance(a);
                }
            }
            else
            {
                Walkabout.Dialogs.AccountDialog dialog = new Dialogs.AccountDialog(myMoney, a, this.Site);
                dialog.Owner = App.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    myMoney.Rebalance(a);
                }
            }
        }

        void Export(Account a, string filename)
        {
            if (filename.ToLowerInvariant().EndsWith(".txf"))
            {
                TaxReportDialog options = new TaxReportDialog();
                options.Owner = App.Current.MainWindow;
                if (options.ShowDialog() == true)
                {
                    TxfExporter e = new TxfExporter(this.myMoney);
                    using (StreamWriter sw = new StreamWriter(filename))
                    {
                        e.ExportCapitalGains(a, sw, options.Year, options.ConsolidateSecuritiesOnDateSold);
                    }
                }
            }
            else
            {
                Exporters e = new Exporters();
                List<object> data =new List<object>();
                foreach (object row in this.MyMoney.Transactions.GetTransactionsFrom(a)) 
                {
                    data.Add(row);
                }
                e.Export(filename, data);                
            }
        }

        #region Command Handlers

        private void OnAccountDetails(object sender, ExecutedRoutedEventArgs e)
        {
            Account a = this.Selected as Account;

            if (a != null)
            {
                EditDetails(a);
            }
        }

        private void CanShowAccountDetails(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.Selected is Account;
            e.Handled = true;
        }

        private void OnSynchronizeAccount(object sender, ExecutedRoutedEventArgs e)
        {
            Account a = this.SelectedAccount;
            if (a != null)
            {
                if (SyncAccount != null)
                {
                    SyncAccount(this, new ChangeEventArgs(a, null, ChangeType.None));
                }
            }
        }

        private void CanSynchronizeAccount(object sender, CanExecuteRoutedEventArgs e)
        {
            Account a = this.SelectedAccount;
            e.CanExecute = (a != null && a.OnlineAccount != null);
            e.Handled = true;
        }

        private void OnBalanceAccount(object sender, ExecutedRoutedEventArgs e)
        {
            Account a = this.SelectedAccount;
            if (a != null)
            {
                if (BalanceAccount != null)
                {
                    BalanceAccount(this, new ChangeEventArgs(a, null, ChangeType.None));
                }
            }
        }

        private void CanBalanceAccount(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.Selected is Account;
            e.Handled = true;
        }

        private void OnAddNewAccount(object sender, ExecutedRoutedEventArgs e)
        {
            Account a = new Account();
            Walkabout.Dialogs.AccountDialog dialog = new Dialogs.AccountDialog(this.MyMoney, a, this.Site);
            dialog.Owner = App.Current.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                this.MyMoney.Accounts.AddAccount(a);

                //
                // Now select the newly create account
                //
                this.listBox1.SelectedItem = a;
            }
        }

        private void CanAddNewAccount(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }


        private void OnDownloadAccounts(object sender, ExecutedRoutedEventArgs e)
        {
            Account temp = new Account();
            temp.Type = AccountType.Checking;
            OnlineAccountDialog od = new OnlineAccountDialog(this.myMoney, temp, this.Site);
            od.Owner = App.Current.MainWindow;
            od.ShowDialog();
        }

        private void CanDownloadAccounts(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void OnAddNewLoanAccount(object sender, ExecutedRoutedEventArgs e)
        {
            Account a = new Account();
            a.Type = AccountType.Loan;
            Walkabout.Dialogs.LoanDialog dialog = new Dialogs.LoanDialog(this.MyMoney, a);
            dialog.Owner = App.Current.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                this.MyMoney.Accounts.AddAccount(a);

                //
                // Now select the newly create account
                //
                this.listBox1.SelectedItem = a;
            }
        }

        private void CanAddNewLoanAccount(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void OnDeleteAccount(object sender, ExecutedRoutedEventArgs e)
        {
            Account a = this.SelectedAccount;
            if (a != null)
            {
                this.DeleteAccount(a);
            }
        }

        private void CanDeleteAccount(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.Selected is Account;
            e.Handled = true;
        }

        private void OnViewTransfers(object sender, ExecutedRoutedEventArgs e)
        {
            Account a = this.SelectedAccount;
            if (a != null)
            {
                if (ShowTransfers != null)
                {
                    ShowTransfers(this, new ChangeEventArgs(a, null, ChangeType.None));
                }
            }
        }

        private void CanViewTransfers(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.Selected is Account;
            e.Handled = true;
        }

        private void OnExportAccount(object sender, ExecutedRoutedEventArgs e)
        {
            Account a = this.SelectedAccount;
            if (a == null)
            {
                return;
            }

            SaveFileDialog fd = new SaveFileDialog();
            fd.CheckPathExists = true;
            fd.AddExtension = true;
            fd.Filter = StringHelpers.CreateFileFilter(Properties.Resources.XmlFileFilter,
                Properties.Resources.TurboTaxFileFilter,
                Properties.Resources.AllFileFilter);
            fd.FileName = a.Name + ".xml";
            if (fd.ShowDialog(App.Current.MainWindow) == true)
            {
                try
                {
                    Export(a, fd.FileName);
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.Message, "Error Exporting", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CanExportAccount(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.Selected is Account;
            e.Handled = true;
        }


        private void OnToggleShowClosedAccounts(object sender, ExecutedRoutedEventArgs e)
        {
            this.DisplayClosedAccounts = !DisplayClosedAccounts;
        }

        private void CanToggleShowClosedAccounts(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        #endregion 

        #region MENU ITEM HANDLERS

        private void UpdateContextMenuView()
        {
            this.MenuDisplayClosedAccounts.IsChecked = this.DisplayClosedAccounts;
        }

        #endregion


    }

    public class AccountNameColorConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Brush textBrush = null;
            Account a = value as Account;
            if (a != null)
            {
                if (a.IsClosed)
                {
                    textBrush = Application.Current.TryFindResource("WalkaboutAccountClosedTextBrush") as Brush;
                    
                }
            }
            if (textBrush == null)
            {
                textBrush = Application.Current.TryFindResource("WalkaboutAccountEnabledTextBrush") as Brush;
            }

            if (textBrush == null)
            {
                textBrush = Brushes.DarkBlue;
            }

            return textBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }


    public class AccountFontWeightConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Account a = value as Account;
            if (a != null)
            {
                if (a.Unaccepted > 0)
                {
                    return FontWeights.Bold;
                }
            }
            return FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class AccountLastBalancedTooltipConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is DateTime)
            {
                DateTime dt = (DateTime)value;
                if (dt != DateTime.MinValue)
                {
                    return string.Format("Last Balanced {0}", dt.ToShortDateString());
                }
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class AccountLastTransactionTooltipConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Account)
            {
                Account a = (Account)value;
                if (a.Parent is Accounts)
                {
                    Accounts s = (Accounts)a.Parent;
                    if (s.Parent is MyMoney)
                    {
                        MyMoney m = (MyMoney)s.Parent;
                        Transaction t = m.Transactions.GetLatestTransactionFrom(a);
                        if (t != null)
                        {
                            return string.Format("Last Transaction\n  Date: {0}\n  Payee: {1}\n  Amount: {2:C2}", t.Date.ToShortDateString(), t.PayeeName, t.Amount);
                        }
                    }
                }
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class AccountWarningConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Account a = value as Account;
            if (a != null)
            {
                if (a.ReconcileWarning > 0 && a.LastBalance < DateTime.Now.AddMonths(-a.ReconcileWarning - 1))
                {
                    return GetReconcileWarningObject(a);
                }
            }
            return GetNoWarningObject(a);
        }

        protected virtual object GetNoWarningObject(Account a)
        {
            return null;
        }

        protected virtual object GetReconcileWarningObject(Account a)
        {
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public class AccountWarningIconConverter : AccountWarningConverter
    {
        static ImageSource WarningIcon = null;

        protected override object GetReconcileWarningObject(Account a)
        {
            if (WarningIcon == null)
            {
                WarningIcon = BitmapFrame.Create(new Uri("pack://application:,,,/MyMoney;component/Dialogs/Icons/Warning.png"));
            }
            return WarningIcon;
        }

    }

    public class AccountWarningVisibilityConverter : AccountWarningConverter
    {
        protected override object GetReconcileWarningObject(Account a)
        {
            return Visibility.Visible;
        }

        protected override object GetNoWarningObject(Account a)
        {
            return Visibility.Collapsed;
        }
    }

    public class AccountWarningTooltipConverter : AccountWarningConverter
    {
        protected override object GetReconcileWarningObject(Account a)
        {
            if (a.LastBalance == DateTime.MinValue)
            {
                return string.Format("Reminder: you have not balanced this account yet.\n" +
                       "You can change this reminder using 'Reconcile Warning' in the account properties",
                       a.LastBalance.ToShortDateString());
            }
            else
            {
                int months = DateTime.Now.Month - a.LastBalance.Month;
                int years = DateTime.Now.Year - a.LastBalance.Year;
                months += (years * 12);
                return string.Format("Reminder: you have not balanced this account in {0} months\n" +
                       "You can change this reminder using 'Reconcile Warning' in the account properties", months);
            }
        }
    }
    
}
