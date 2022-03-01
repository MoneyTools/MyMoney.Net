using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Dialogs;
using Walkabout.Utilities;
using Walkabout.Migrate;
using System.IO;
using Walkabout.Commands;
using Walkabout.Configuration;
using System.ComponentModel;
using System.Collections.ObjectModel;
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
        public static RoutedUICommand CommandExportList;
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
            CommandExportList = new RoutedUICommand("ExportList", "ExportList", typeof(AccountsControl));
            CommandToggleClosedAccounts = new RoutedUICommand("ToggleClosedAccounts", "ToggleClosedAccounts", typeof(AccountsControl));
        }

        #endregion 

        #region PROPERTIES

        private DelayedActions delayedActions = new DelayedActions();

        public IServiceProvider Site { get; set; }

        private MyMoney myMoney;

        private ObservableCollection<AccountViewModel> items = new ObservableCollection<AccountViewModel>();


        public MyMoney MyMoney
        {
            get { return myMoney; }
            set
            {
                if (this.myMoney != null)
                {
                    myMoney.Accounts.Changed -= new EventHandler<ChangeEventArgs>(OnAccountsChanged);
                    myMoney.Changed -= new EventHandler<ChangeEventArgs>(OnMoneyChanged);                    
                }
                myMoney = value;

                if (value != null)
                {
                    myMoney.Accounts.Changed += new EventHandler<ChangeEventArgs>(OnAccountsChanged);
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
                    delayedActions.StartDelayedAction("rebind", Rebind, TimeSpan.FromMilliseconds(30));
                    return;
                }
                args = args.Next;
            }
        }

        public Account SelectedAccount
        {
            get { return (this.listBox1.SelectedItem is AccountItemViewModel m) ? m.Account : null; }
            set {
                var item = (from i in this.items where i is AccountItemViewModel m && m.Account == value select i).FirstOrDefault();
                if (item != null)
                {
                    this.Selected = item;
                }
                else if (!DisplayClosedAccounts)
                {
                    DisplayClosedAccounts = true;
                    this.SelectedAccount = value;
                }
            }
        }

        public AccountViewModel Selected
        {
            get { return this.listBox1.SelectedItem as AccountViewModel; }
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

                this.listBox1.ItemsSource = this.items;
                UpdateContextMenuView();
#if PerformanceBlocks
            }
#endif  
        }

        void listBox1_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(this);
            if (!this.HitScrollBar(pos))
            {
                uint delay = NativeMethods.GetDoubleClickTime();
                delayedActions.StartDelayedAction("SingleClick", OnShowAllTransactions, TimeSpan.FromMilliseconds(delay + 100));
            }
        }

        private void OnShowAllTransactions()
        {
            // ok, we have a single click, time to tell the transaction view to show all transactions
            // (undo any filtering user has selected, or "custom" view created from a report).
            RaiseSelectionEvent(this.selected, true);
        }

        void OnListBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            delayedActions.CancelDelayedAction("SingleClick");
            object item = GetElementFromPoint(listBox1, e.GetPosition(listBox1));

            if (item is AccountViewModel m)
            {
                if (item is AccountSectionHeader)
                {
                    // Don't allow Property editing of AccountSectionHeader types
                }
                else
                {
                    this.Selected = m;
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

        public void OnAccountsChanged(object sender, ChangeEventArgs e)
        {
            if ((e.ChangeType == ChangeType.TransientChanged || e.ChangeType == ChangeType.Changed) && e.Item is Account && e.Name == "Balance")
            {
                // then we only need to rebalance the headers.  The AccountItemViewModel balances are auto-updated by that view model.
                UpdateSectionHeaderBalances();
            }
            else
            {
                Rebind();
            }
        }

        void UpdateSectionHeaderBalances()
        {
            foreach (var item in this.items)
            {
                if (item is AccountSectionHeader header)
                {
                    header.UpdateBalance();
                }
            }
        }

        void Rebind()
        {
            UpdateContextMenuView();

            if (myMoney != null)
            {
                //---------------------------------------------------------
                // First make a copy of the collection in order to 
                // help LINQ do it's magic over the collection
                List<Account> inputList = new List<Account>(myMoney.Accounts.GetAccounts(!this.displayClosedAccounts));

                this.items.Clear();

                decimal netWorth = 0;

                var accountOfTypeBanking = from a in inputList where a.Type == AccountType.Checking || a.Type == AccountType.Savings || a.Type == AccountType.Cash select a;
                AccountSectionHeader sh = BundleAccount("Banking", this.items, accountOfTypeBanking);
                netWorth += sh.BalanceInNormalizedCurrencyValue;

                var accountOfTypeCredit = from a in inputList where a.Type == AccountType.Credit || a.Type == AccountType.CreditLine select a;
                sh = BundleAccount("Credit", this.items, accountOfTypeCredit);
                netWorth += sh.BalanceInNormalizedCurrencyValue;

                var accountOfTypeBrokerage = from a in inputList where a.Type == AccountType.Brokerage || a.Type == AccountType.MoneyMarket select a;
                sh = BundleAccount("Brokerage", this.items, accountOfTypeBrokerage);
                sh.Clicked += (s, e) => { AppCommands.CommandReportInvestment.Execute(null, this); };
                netWorth += sh.BalanceInNormalizedCurrencyValue;

                var accountOfTypeRetirement = from a in inputList where a.Type == AccountType.Retirement select a;
                sh = BundleAccount("Retirement", this.items, accountOfTypeRetirement);
                sh.Clicked += (s, e) => { AppCommands.CommandReportInvestment.Execute(null, this); };
                netWorth += sh.BalanceInNormalizedCurrencyValue;

                var accountOfTypeAsset = from a in inputList where a.Type == AccountType.Asset select a;
                sh = BundleAccount("Assets", this.items, accountOfTypeAsset);
                netWorth += sh.BalanceInNormalizedCurrencyValue;

                var accountOfTypeLoan = from a in inputList where a.Type == AccountType.Loan select a;
                sh = BundleAccount("Loans", this.items, accountOfTypeLoan);
                netWorth += sh.BalanceInNormalizedCurrencyValue;

                if (statusArea != null)
                {
                    statusArea.Text = netWorth.ToString("C");
                }

            }
            else
            {
                this.items.Clear();
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

        private static AccountSectionHeader BundleAccount(string caption, ObservableCollection<AccountViewModel> output, IEnumerable<Account> accountOfTypeBanking)
        {
            AccountSectionHeader sectionHeader = new AccountSectionHeader();

            if (accountOfTypeBanking.Count() > 0)
            {

                sectionHeader.Title = caption;

                List<Account> bundle = new List<Account>();
                sectionHeader.Accounts = bundle;

                output.Add(sectionHeader);

                foreach (Account a in accountOfTypeBanking)
                {
                    output.Add(new AccountItemViewModel(a));
                    bundle.Add(a);
                }

                sectionHeader.UpdateBalance();
            }
            return sectionHeader;
        }

        // our idea of what is selected.  We only raise SelectionChanged event if this doesn't match.
        AccountViewModel selected;

        void Select(AccountViewModel item)
        {
            this.listBox1.SelectedItem = item;
            this.listBox1.ScrollIntoView(item);
            SetSelected(item);
        }

        void SetSelected(AccountViewModel item)
        {
            if (item != this.selected)
            {
                if (this.selected != null)
                {
                    this.selected.IsSelected = false;
                }
                this.selected = item;
                if (this.selected != null)
                {
                    this.selected.IsSelected = true;
                }
            }
        }

        void OnListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            delayedActions.CancelDelayedAction("SingleClick");
            object selected = (e.AddedItems.Count > 0) ? e.AddedItems[0] : null;
            RaiseSelectionEvent(selected as AccountViewModel, false);
        }

        void RaiseSelectionEvent(AccountViewModel selected, bool force) 
        { 
            AccountSectionHeader ash = selected as AccountSectionHeader;

            if (ash != null)
            {
                ash.OnClick();
            }

            if ((force || this.selected != selected) && SelectionChanged != null)
            {
                SetSelected(selected);
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
            AccountViewModel prev = null;
            AccountViewModel next = null;
            bool found = false;
            for (int i = 0; i < this.items.Count; i++)
            {
                AccountViewModel item = this.items[i];
                Account a = (item is AccountItemViewModel m) ? m.Account : null;
                if (a != null)
                {
                    if (a == account)
                    {
                        found = true;
                    }
                    else if (!found)
                    {
                        prev = item;
                    }
                    else if (next == null)
                    {
                        next = item;
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
                options.Month = Settings.TheSettings.FiscalYearStart;
                options.Owner = App.Current.MainWindow;
                if (options.ShowDialog() == true)
                {
                    TxfExporter e = new TxfExporter(this.myMoney);
                    using (StreamWriter sw = new StreamWriter(filename))
                    {
                        DateTime startDate = new DateTime(options.Year, options.Month + 1, 1);
                        if (options.Month > 0)
                        {
                            // then the FY year ends on the specified year.
                            startDate = startDate.AddYears(-1);
                        }
                        DateTime endDate = startDate.AddYears(1);
                        e.ExportCapitalGains(a, sw, startDate, endDate, options.ConsolidateSecuritiesOnDateSold);
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
            Account a = this.SelectedAccount;
            if (a != null)
            {
                EditDetails(a);
            }
        }

        private void CanShowAccountDetails(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.Selected is AccountItemViewModel;
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
            e.CanExecute = this.Selected is AccountItemViewModel;
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
            e.CanExecute = this.Selected is AccountItemViewModel;
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
            e.CanExecute = this.Selected is AccountItemViewModel;
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
            e.CanExecute = this.Selected is AccountItemViewModel;
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

        private void UpdateContextMenuView()
        {
            this.MenuDisplayClosedAccounts.Header = this.DisplayClosedAccounts ? "Hide Closed Accounts" : "Display Closed Accounts";
        }

        private void OnExportAccountList(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileDialog fd = new SaveFileDialog();
            fd.CheckPathExists = true;
            fd.AddExtension = true;
            fd.Filter = StringHelpers.CreateFileFilter("*.csv|(*.csv)");
            fd.FileName = "accounts.csv";
            if (fd.ShowDialog(App.Current.MainWindow) == true)
            {
                try
                {
                    ExportList(fd.FileName);
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.Message, "Error Exporting", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportList(string fileName)
        {
            using (StreamWriter sw = new StreamWriter(fileName))
            {
                sw.WriteLine("Type,Account,Balance");
                foreach (var item in this.items)
                {
                    if (item is AccountItemViewModel vm)
                    {
                        sw.WriteLine(vm.Account.Type + "," + vm.Account.Name + "," + vm.Balance);
                    }
                }
            }

            NativeMethods.ShellExecute(IntPtr.Zero, "Open", fileName, "", "", NativeMethods.SW_SHOWNORMAL);
        }
        #endregion

    }

    public class AccountViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool selected;

        public bool IsSelected
        {
            get => this.selected;
            set
            {
                if (this.selected != value)
                {
                    this.selected = value;
                    OnSelectedChanged();
                }
            }
        }

        protected virtual void OnSelectedChanged() { }


        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }


    public class AccountItemViewModel : AccountViewModel
    {
        private Account account;

        public AccountItemViewModel(Account a)
        {
            this.account = a;
            this.account.PropertyChanged += OnPropertyChanged;
        }

        ~AccountItemViewModel()
        {
            this.account.PropertyChanged -= OnPropertyChanged;
        }

        protected override void OnSelectedChanged() 
        {
            OnPropertyChanged("NameForeground");
            OnPropertyChanged("BalanceForeground");
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
            switch (e.PropertyName) {
                case "Unaccepted":
                    OnPropertyChanged("NameForeground");
                    OnPropertyChanged("FontWeight");
                    break;
                case "Name":
                    OnPropertyChanged("TooltipRow1");
                    break;
                case "BalanceNormalized":
                case "Balance":
                    OnPropertyChanged("BalanceForeground");
                    OnPropertyChanged("Balance");
                    break;
                case "LastBalance":
                    OnPropertyChanged("TooltipRow2");
                    OnPropertyChanged("WarningIcon");
                    OnPropertyChanged("IconTooltip");
                    OnPropertyChanged("WarningIconVisibility");
                    OnPropertyChanged("WarningIconTooltip");
                    break;
            }
        }

        public Account Account { get => this.account; }

        public string Name
        {
            get => account.Name;
        }

        public decimal Balance
        {
            get => this.account.BalanceNormalized;
        }


        public FontWeight FontWeight
        {
            get
            {
                if (this.account.Unaccepted > 0)
                {
                    return FontWeights.Bold;
                }
                return FontWeights.Normal;
            }
        }

        public Brush NameForeground
        {
            get
            {
                Account a = this.account;
                string brush = "SystemControlDisabledBaseMediumLowBrush";
                if (a.IsClosed)
                {
                    brush = "SystemControlDisabledBaseMediumLowBrush";
                }
                else if (this.IsSelected)
                {
                    brush = "ListItemSelectedForegroundBrush";
                }
                else if (a.Unaccepted > 0)
                {
                    brush = "ListItemForegroundUnacceptedBrush";
                }
                else
                {
                    brush = "ListItemForegroundBrush";
                }

                return AppTheme.Instance.GetThemedBrush(brush);
            }
        }

        public Brush BalanceForeground
        {
            get
            {
                string brush = "SystemControlDisabledBaseMediumLowBrush";
                if (this.IsSelected)
                {
                    brush = "ListItemSelectedForegroundBrush";
                }
                else if (this.account.BalanceNormalized < 0)
                {
                    brush = "NegativeCurrencyForegroundBrush";
                }
                return AppTheme.Instance.GetThemedBrush(brush);
            }
        }

        public string TooltipRow1
        {
            get
            {
                return account.Name + ": " + account.AccountId;
            }
        }

        public string TooltipRow2
        {
            get
            {
                DateTime dt = (DateTime)this.account.LastBalance;
                if (dt != DateTime.MinValue)
                {
                    return string.Format("Last Balanced {0}", dt.ToShortDateString());
                }
                return "";
            }
        }

        public string TooltipRow3
        {
            get
            {
                Account a = this.account;
                if (a.Parent is Accounts s && s.Parent is MyMoney m)
                {
                    Transaction t = m.Transactions.GetLatestTransactionFrom(a);
                    if (t != null)
                    {
                        return string.Format("Last Transaction\n  Date: {0}\n  Payee: {1}\n  Amount: {2:C2}", t.Date.ToShortDateString(), t.PayeeName, t.Amount);
                    }
                }
                return "";
            }
        }

        public object WarningIcon
        {
            get
            {
                if (WarningIconVisibility == Visibility.Visible)
                {
                    return new Uri("pack://application:,,,/MyMoney;component/Dialogs/Icons/Warning.png");
                }
                return DependencyProperty.UnsetValue;
            }
        }

        public Visibility WarningIconVisibility
        {
            get
            {
                Account a = this.account;
                if (a != null && !a.IsClosed)
                {
                    if (a.ReconcileWarning > 0 && a.LastBalance < DateTime.Now.AddMonths(-a.ReconcileWarning - 1))
                    {
                        return Visibility.Visible;
                    }
                }
                return Visibility.Collapsed;
            }
        }

        public string WarningIconTooltip
        {
            get
            {
                var a = this.account;
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


    public class AccountSectionHeader : AccountViewModel
    {
        private decimal balanceNormalized;

        public string Title { get; set; }

        public decimal BalanceInNormalizedCurrencyValue {
            get => balanceNormalized;
            set {
                if (balanceNormalized != value)
                {
                    balanceNormalized = value;
                    OnPropertyChanged("BalanceInNormalizedCurrencyValue");
                    OnPropertyChanged("BalanceForeground");
                }
            }
        }

        protected override void OnSelectedChanged()
        {
            OnPropertyChanged("BalanceForeground");
        }

        public Brush BalanceForeground
        {
            get
            {
                string brush = "SystemControlDisabledBaseMediumLowBrush";
                if (this.IsSelected)
                {
                    brush = "ListItemSelectedForegroundBrush";
                }
                else if (this.balanceNormalized < 0)
                {
                    brush = "NegativeCurrencyForegroundBrush";
                }
                return AppTheme.Instance.GetThemedBrush(brush);
            }
        }

        public List<Account> Accounts { get; set; }

        public event EventHandler Clicked;

        public void OnClick()
        {
            if (Clicked != null)
            {
                Clicked(this, EventArgs.Empty);
            }
        }

        internal void UpdateBalance()
        {
            decimal balance = 0;
            if (Accounts != null)
            {
                foreach (Account a in Accounts)
                {
                    balance += a.BalanceNormalized;
                }
            }
            this.BalanceInNormalizedCurrencyValue = balance;
        }
    }
    
}
