using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Commands;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Dialogs;
using Walkabout.Help;
using Walkabout.Migrate;
using Walkabout.Utilities;

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
        public static RoutedUICommand CommandImportCsv;
        public static RoutedUICommand CommandEditCsvMapping;
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
            CommandImportCsv = new RoutedUICommand("ImportCsv", "ImportCsv", typeof(AccountsControl));
            CommandEditCsvMapping = new RoutedUICommand("EditCsv", "EditCsv", typeof(AccountsControl));
            CommandToggleClosedAccounts = new RoutedUICommand("ToggleClosedAccounts", "ToggleClosedAccounts", typeof(AccountsControl));
        }

        #endregion

        #region PROPERTIES

        private readonly DelayedActions delayedActions = new DelayedActions();

        private MyMoney myMoney;

        private readonly ObservableCollection<AccountViewModel> items = new ObservableCollection<AccountViewModel>();

        private DatabaseSettings databaseSettings;

        public IServiceProvider Site { get; set; }

        public DatabaseSettings DatabaseSettings
        {
            get => this.databaseSettings;
            set
            {
                if (this.databaseSettings != null)
                {
                    this.databaseSettings.PropertyChanged -= this.OnDatabaseSettingsChanged;
                }
                this.databaseSettings = value;
                this.databaseSettings.PropertyChanged += this.OnDatabaseSettingsChanged;
                this.DelayedRebind();
            }
        }

        private void OnDatabaseSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ShowCurrency")
            {
                this.UpdateShowCurrency();
            }
        }

        private void UpdateShowCurrency()
        {
            bool newValue = this.databaseSettings.ShowCurrency;
            // propagate to the view model.
            foreach (var item in this.items)
            {
                item.ShowCurrency = newValue;
            }
        }

        public MyMoney MyMoney
        {
            get { return this.myMoney; }
            set
            {
                if (this.myMoney != null)
                {
                    this.myMoney.Accounts.Changed -= new EventHandler<ChangeEventArgs>(this.OnAccountsChanged);
                    this.myMoney.Changed -= new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);
                    this.myMoney.Rebalanced -= new EventHandler<ChangeEventArgs>(this.OnBalanceChanged);
                }
                this.myMoney = value;

                if (value != null)
                {
                    this.myMoney.Accounts.Changed += new EventHandler<ChangeEventArgs>(this.OnAccountsChanged);
                    this.myMoney.Changed += new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);
                    this.myMoney.Rebalanced += new EventHandler<ChangeEventArgs>(this.OnBalanceChanged);
                    this.OnAccountsChanged(this, new ChangeEventArgs(this.myMoney.Accounts, null, ChangeType.Reloaded));
                }
                else
                {
                    this.Select(null);
                }
            }
        }

        private void OnBalanceChanged(object sender, ChangeEventArgs args)
        {
            this.DelayedRebind();
        }

        private void DelayedRebind()
        {
            this.delayedActions.StartDelayedAction("rebind", this.Rebind, TimeSpan.FromMilliseconds(30));
        }


        private void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
            while (args != null)
            {
                if (args.Item is Account || args.Item is Currencies)
                {
                    this.DelayedRebind();
                    return;
                }
                args = args.Next;
            }
        }

        private AccountViewModel GetViewModel(Account account)
        {
            return (from i in this.items where i is AccountItemViewModel m && m.Account == account select i).FirstOrDefault();
        }

        public Account SelectedAccount
        {
            get { return (this.listBox1.SelectedItem is AccountItemViewModel m) ? m.Account : null; }
            set
            {
                var item = this.GetViewModel(value);
                if (item == null)
                {
                    // we need the view model now, can't wait for delayed rebind.
                    this.delayedActions.CancelDelayedAction("rebind");
                    this.Rebind();
                    item = this.GetViewModel(value);
                }
                if (item != null)
                {
                    this.Selected = item;
                }
                else if (!this.DisplayClosedAccounts && !value.IsDeleted)
                {
                    this.DisplayClosedAccounts = true; // this depends on rebind happening synchronously!
                    this.Selected = this.GetViewModel(value);
                }
            }
        }

        public AccountViewModel Selected
        {
            get { return this.listBox1.SelectedItem as AccountViewModel; }
            set { this.Select(value); }
        }

        private bool displayClosedAccounts = false;

        private TextBlock statusArea;

        #endregion

        #region EVENTS
        private bool hideEvents;
        public event EventHandler SelectionChanged;
        public event EventHandler<ChangeEventArgs> BalanceAccount;
        public event EventHandler<ChangeEventArgs> SyncAccount;
        public event EventHandler<ChangeEventArgs> ShowTransfers;
        #endregion

        #region IClipboardClient SUPPORT
        public bool CanCut
        {
            get { return this.SelectedAccount != null; }
        }
        public bool CanCopy
        {
            get { return this.SelectedAccount != null; }
        }
        public bool CanPaste
        {
            get { return Clipboard.ContainsText(); }
        }
        public bool CanDelete
        {
            get { return this.SelectedAccount != null; }
        }
        public void Cut()
        {
            Account a = this.SelectedAccount;
            if (a != null)
            {
                a = this.DeleteAccount(a);
                CopyToClipboard(a);
            }
        }

        private static void CopyToClipboard(Account a)
        {
            if (a != null)
            {
                string xml = a.Serialize();
                Clipboard.SetDataObject(xml, true);
            }
        }

        public void Copy()
        {
            Account a = this.SelectedAccount;
            CopyToClipboard(a);
        }

        public void Delete()
        {
            Account a = this.SelectedAccount;

            if (a != null)
            {
                this.DeleteAccount(a);
            }
        }

        public void Paste()
        {
            IDataObject data = Clipboard.GetDataObject();
            if (data.GetDataPresent(typeof(string)))
            {
                string xml = (string)data.GetData(typeof(string));
                XmlImporter importer = new XmlImporter(this.myMoney, this.Site);
                importer.ImportAccount(xml);
                if (importer.LastAccount == null)
                {
                    MessageBoxEx.Show("Clipboard doesn't seem to contain valid account information", "Paste Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        #region IContainerStatus SUPPORT

        public void SetTextBlock(TextBlock statusControl)
        {
            this.statusArea = statusControl;
            if (this.statusArea != null)
            {
                this.statusArea.FontSize = 14;
            }
        }

        #endregion

        public AccountsControl()
        {

#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.AccountsControlInitialize))
            {
#endif
            this.InitializeComponent();

            this.listBox1.PreviewMouseDown += new MouseButtonEventHandler(this.listBox1_PreviewMouseDown);
            this.listBox1.SelectionChanged += new SelectionChangedEventHandler(this.OnListBoxSelectionChanged);
            this.listBox1.MouseDoubleClick += new MouseButtonEventHandler(this.OnListBoxMouseDoubleClick);

            foreach (object o in this.AccountsControlContextMenu.Items)
            {
                MenuItem m = o as MenuItem;
                if (m != null)
                {
                    m.CommandTarget = this;
                }
            }

            this.listBox1.ItemsSource = this.items;
            this.UpdateContextMenuView();
#if PerformanceBlocks
            }
#endif
        }

        private void listBox1_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(this);
            if (!this.HitScrollBar(pos) && e.ChangedButton == MouseButton.Left)
            {
                uint delay = NativeMethods.GetDoubleClickTime();
                this.delayedActions.StartDelayedAction("SingleClick", this.OnShowAllTransactions, TimeSpan.FromMilliseconds(delay + 100));
            }
        }

        private void OnShowAllTransactions()
        {
            // ok, we have a single click, time to tell the transaction view to show all transactions
            // (undo any filtering user has selected, or "custom" view created from a report).
            this.RaiseSelectionEvent(this.selected, true);
        }

        private void OnListBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            this.delayedActions.CancelDelayedAction("SingleClick");
            object item = null;
            try
            {
                this.GetElementFromPoint(this.listBox1, e.GetPosition(this.listBox1));
            } 
            catch (Exception ex)
            {
                Debug.WriteLine("Ignoring hit test exception : " + ex.Message);
            }

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
                    this.EditDetails(a);
                }
            }
        }

        private object GetElementFromPoint(ItemsControl hostingControl, Point point)
        {
            DependencyObject element = hostingControl.InputHitTest(point) as DependencyObject;
            while (element != null)
            {

                if (element == hostingControl)
                {
                    return null;
                }

                object item = hostingControl.ItemContainerGenerator.ItemFromContainer(element);

                bool itemFound = !item.Equals(DependencyProperty.UnsetValue);

                if (itemFound)
                {
                    return item;
                }
                var parent = VisualTreeHelper.GetParent(element) as DependencyObject;
                if (parent == element) {
                    break;
                }
                element = parent;
            }

            return null;
        }

        public void OnAccountsChanged(object sender, ChangeEventArgs e)
        {
            if ((e.ChangeType == ChangeType.TransientChanged || e.ChangeType == ChangeType.Changed) && e.Item is Account && e.Name == "Balance")
            {
                // then we only need to rebalance the headers.  The AccountItemViewModel balances are auto-updated by that view model.
                this.UpdateSectionHeaderBalances();
            }
            else
            {
                this.DelayedRebind();
            }
        }

        private void UpdateSectionHeaderBalances()
        {
            foreach (var item in this.items)
            {
                if (item is AccountSectionHeader header)
                {
                    header.UpdateBalance();
                }
            }
        }

        private void Rebind()
        {
            this.UpdateContextMenuView();

            this.hideEvents = true;
            if (this.myMoney != null)
            {
                //---------------------------------------------------------
                // First make a copy of the collection in order to
                // help LINQ do it's magic over the collection
                List<Account> inputList = new List<Account>(this.myMoney.Accounts.GetAccounts(!this.displayClosedAccounts));

                var selected = this.SelectedAccount;

                this.items.Clear();

                decimal netWorth = 0;

                var accountOfTypeBanking = from a in inputList where a.Type == AccountType.Checking || a.Type == AccountType.Savings || a.Type == AccountType.Cash select a;
                AccountSectionHeader sh = BundleAccount("Banking", this.items, accountOfTypeBanking);
                sh.DefaultCurrency = this.myMoney.Currencies.DefaultCurrency;
                netWorth += sh.BalanceInNormalizedCurrencyValue;

                var accountOfTypeCredit = from a in inputList where a.Type == AccountType.Credit || a.Type == AccountType.CreditLine select a;
                sh = BundleAccount("Credit", this.items, accountOfTypeCredit);
                sh.DefaultCurrency = this.myMoney.Currencies.DefaultCurrency;
                netWorth += sh.BalanceInNormalizedCurrencyValue;

                var accountOfTypeBrokerage = from a in inputList where a.Type == AccountType.Brokerage || a.Type == AccountType.MoneyMarket select a;
                sh = BundleAccount("Brokerage", this.items, accountOfTypeBrokerage);
                sh.DefaultCurrency = this.myMoney.Currencies.DefaultCurrency;
                sh.Clicked += (s, e) => { AppCommands.CommandReportInvestment.Execute(null, this); };
                netWorth += sh.BalanceInNormalizedCurrencyValue;

                var accountOfTypeRetirement = from a in inputList where a.Type == AccountType.Retirement select a;
                sh = BundleAccount("Retirement", this.items, accountOfTypeRetirement);
                sh.DefaultCurrency = this.myMoney.Currencies.DefaultCurrency;
                sh.Clicked += (s, e) => { AppCommands.CommandReportInvestment.Execute(null, this); };
                netWorth += sh.BalanceInNormalizedCurrencyValue;

                var accountOfTypeAsset = from a in inputList where a.Type == AccountType.Asset select a;
                sh = BundleAccount("Assets", this.items, accountOfTypeAsset);
                sh.DefaultCurrency = this.myMoney.Currencies.DefaultCurrency;
                netWorth += sh.BalanceInNormalizedCurrencyValue;

                var accountOfTypeLoan = from a in inputList where a.Type == AccountType.Loan select a;
                sh = BundleAccount("Loans", this.items, accountOfTypeLoan);
                sh.DefaultCurrency = this.myMoney.Currencies.DefaultCurrency;
                netWorth += sh.BalanceInNormalizedCurrencyValue;

                if (this.statusArea != null)
                {
                    this.statusArea.Text = StringHelpers.GetFormattedAmount(netWorth) + (this.databaseSettings.ShowCurrency ? " " + this.myMoney.Currencies.DefaultCurrency?.Symbol : "");
                }

                if (selected != null)
                {
                    this.SelectedAccount = selected;
                }

                this.UpdateShowCurrency();
            }
            else
            {
                this.items.Clear();
            }
            this.hideEvents = false;
        }


        public bool DisplayClosedAccounts
        {
            get { return this.displayClosedAccounts; }
            set
            {
                this.displayClosedAccounts = value;
                this.Rebind();
            }
        }

        private static AccountSectionHeader BundleAccount(string caption, ObservableCollection<AccountViewModel> output, IEnumerable<Account> accountOfTypeBanking)
        {
            AccountSectionHeader sectionHeader = new AccountSectionHeader();

            if (accountOfTypeBanking.Any())
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
        private AccountViewModel selected;
        private AccountViewModel selectingItem;

        private void Select(AccountViewModel item)
        {
            this.selectingItem = item;
            this.listBox1.SelectedItem = item;
            this.listBox1.ScrollIntoView(item);
            this.SetSelected(item);
            this.selectingItem = null;
        }

        private void SetSelected(AccountViewModel item)
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
                    this.SetHelpKeywordForSelectedItem(item);
                }
            }
        }

        private void SetHelpKeywordForSelectedItem(AccountViewModel item)
        {
            if (item is AccountItemViewModel accountItem)
            {
                switch (accountItem.Account.Type)
                {
                    case AccountType.Savings:
                    case AccountType.Checking:
                    case AccountType.MoneyMarket:
                    case AccountType.Cash:
                        HelpService.SetHelpKeyword(this, "Accounts/BankAccounts/");
                        break;
                    case AccountType.Credit:
                    case AccountType.CreditLine:
                        HelpService.SetHelpKeyword(this, "Accounts/CreditCardAccounts/");
                        break;
                    case AccountType.Brokerage:
                    case AccountType.Retirement:
                        HelpService.SetHelpKeyword(this, "Accounts/InvestmentAccounts/");
                        break;
                    case AccountType.Asset:
                        HelpService.SetHelpKeyword(this, "Accounts/Assets/");
                        break;
                    case AccountType.Loan:
                        HelpService.SetHelpKeyword(this, "Accounts/Loan/");
                        break;
                    default:
                        break;
                }
            }
        }

        private void OnListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.delayedActions.CancelDelayedAction("SingleClick");
            if (e.AddedItems.Count == 0)
            {
                // no special cleanup when account deleted, we should have already selected
                // a new account by now.
                this.RaiseSelectionEvent(this.selectingItem, false);
                return;
            }
            AccountViewModel added = (AccountViewModel)e.AddedItems[0];
            if (added == this.selectingItem)
            {
                // then this even is in response to a programatic change, not a user click.
                return;
            }
            this.RaiseSelectionEvent(added, false);
        }

        private void RaiseSelectionEvent(AccountViewModel selected, bool force)
        {
            AccountSectionHeader ash = selected as AccountSectionHeader;

            if (ash != null)
            {
                ash.OnClick();
            }

            if ((force || this.selected != selected) && SelectionChanged != null)
            {
                this.SetSelected(selected);

                // checked it really is a different account (could be different object
                // but the same account because of a rebind).
                if (!this.hideEvents)
                {
                    SelectionChanged(this, EventArgs.Empty);
                }
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

            this.RaiseSelectionEvent(this.Selected, force: true);

            return account;
        }

        private void EditDetails(Account a)
        {
            if (a.Type == AccountType.Loan)
            {
                Walkabout.Dialogs.LoanDialog dialog = new Dialogs.LoanDialog(this.myMoney, a);
                dialog.Owner = App.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    this.myMoney.Rebalance(a);
                }
            }
            else
            {
                Walkabout.Dialogs.AccountDialog dialog = new Dialogs.AccountDialog(this.myMoney, a, this.Site);
                dialog.Owner = App.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    this.myMoney.Rebalance(a);
                }
            }
        }

        private void Export(Account a, string filename)
        {
            if (filename.ToLowerInvariant().EndsWith(".txf"))
            {
                TaxReportDialog options = new TaxReportDialog();
                options.Month = this.databaseSettings.FiscalYearStart;
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
                List<object> data = new List<object>();
                data.Add(a);
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
                this.EditDetails(a);
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
            e.CanExecute = a != null && a.OnlineAccount != null;
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
            LoanDialog dialog = new LoanDialog(this.MyMoney, a);
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
                    this.Export(a, fd.FileName);
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
            if (this.SelectedAccount != null && this.SelectedAccount.IsClosed && this.DisplayClosedAccounts)
            {
                // then de-select it since user doesn't want to see closed accounts any more.
                this.Selected = null;
            }
            this.DisplayClosedAccounts = !this.DisplayClosedAccounts;
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
                    this.ExportList(fd.FileName);
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
                sw.WriteLine("Type,Account,Closed,Currency,Balance,LastBalanceDate");
                foreach (var account in this.MyMoney.Accounts.GetAccounts())
                {
                    var type = account.Type;
                    if (type != AccountType.CategoryFund)
                    {
                        var name = CsvStore.CsvSafeString(account.Name);
                        var balance = CsvStore.CsvSafeString(account.Balance.ToString("C3"));
                        var currency = account.GetCurrency().Symbol;
                        var closed = account.IsClosed;
                        var lastBalance = account.LastBalance;
                        sw.WriteLine($"\"{type}\",\"{name}\",\"{closed}\",\"{currency}\",\"{balance}\",\"{lastBalance}\"");
                    }
                }
            }

            NativeMethods.ShellExecute(IntPtr.Zero, "Open", fileName, "", "", NativeMethods.SW_SHOWNORMAL);
        }
        #endregion

        private void OnImportAccountCsv(object sender, ExecutedRoutedEventArgs e)
        {
            Account a = this.SelectedAccount;
            if (a != null)
            {
                OpenFileDialog fd = new OpenFileDialog();
                fd.Title = "Import .csv file";
                fd.Filter = "*.csv (csv files)|*.csv";
                fd.CheckFileExists = true;
                fd.RestoreDirectory = true;
                if (fd.ShowDialog() == true)
                {
                    var file = fd.FileName;
                    this.ImportCsv(a, file);
                }
            }
        }

        private void ImportCsv(Account account, string fileName)
        {
            try
            {
                // load existing csv map if we have one.
                var map = this.LoadMap(account);
                var ti = new CsvTransactionImporter(this.myMoney, account, map);
                CsvImporter importer = new CsvImporter(this.myMoney, ti);
                importer.Import(fileName);
                ti.Commit();
                map.Save();
                this.myMoney.Rebalance(account);
            }
            catch (UserCanceledException)
            {
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show(ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        private bool HasCsvMap()
        {
            Account a = this.SelectedAccount;
            if (a != null)
            {
                var map = this.LoadMap(a);
                return map.Fields != null && map.Fields.Count > 0;
            }
            return false;
        }

        private CsvMap LoadMap(Account a)
        {
            if (this.databaseSettings != null)
            {
                var dir = Path.Combine(Path.GetDirectoryName(this.databaseSettings.SettingsFileName), "CsvMaps");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var filename = Path.Combine(dir, a.Id + ".xml");
                return CsvMap.Load(filename);
            }
            return new CsvMap();
        }

        private void OnEditCsvMapping(object sender, ExecutedRoutedEventArgs e)
        {
            var account = this.SelectedAccount;
            if (account != null)
            {
                try
                {
                    var map = this.LoadMap(account);
                    var ti = new CsvTransactionImporter(this.myMoney, account, map);
                    ti.EditCsvMap(null);
                } 
                catch (UserCanceledException)
                {
                    // do nothing.
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.Message, "Error Editing CSV Map", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
            }
        }

        private void HasCsvMapping(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.SelectedAccount != null && this.HasCsvMap();
        }
    }

    public class AccountViewModel : INotifyPropertyChanged
    {
        private bool showCurrency;
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
                    this.OnSelectedChanged();
                }
            }
        }

        protected virtual void OnSelectedChanged() { }

        public bool ShowCurrency
        {
            get
            {
                return this.showCurrency;
            }
            set
            {
                this.showCurrency = value;
                this.OnPropertyChanged("ShowCurrency");
            }
        }

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
        private readonly Account account;

        public AccountItemViewModel(Account a)
        {
            this.account = a;
            this.account.PropertyChanged += this.OnPropertyChanged;
        }

        ~AccountItemViewModel()
        {
            this.account.PropertyChanged -= this.OnPropertyChanged;
        }

        public override bool Equals(object obj)
        {
            if (obj is AccountItemViewModel m)
            {
                return m.account == this.account;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        protected override void OnSelectedChanged()
        {
            this.OnPropertyChanged("NameForeground");
            this.OnPropertyChanged("BalanceForeground");
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            this.OnPropertyChanged(e.PropertyName);
            switch (e.PropertyName)
            {
                case "Unaccepted":
                    this.OnPropertyChanged("NameForeground");
                    this.OnPropertyChanged("FontWeight");
                    break;
                case "Name":
                    this.OnPropertyChanged("TooltipRow1");
                    break;
                case "BalanceNormalized":
                case "Balance":
                    this.OnPropertyChanged("BalanceForeground");
                    this.OnPropertyChanged("Balance");
                    break;
                case "LastBalance":
                    this.OnPropertyChanged("TooltipRow2");
                    this.OnPropertyChanged("IconTooltip");
                    this.OnPropertyChanged("WarningIconVisibility");
                    this.OnPropertyChanged("WarningIconTooltip");
                    break;
            }
        }

        public Account Account { get => this.account; }

        public string Name
        {
            get => this.account.Name;
        }

        public decimal Balance
        {
            get => this.account.BalanceNormalized;
        }

        public string BalanceAsString
        {
            get
            {
                return StringHelpers.GetFormattedAmount(this.Balance, this.Account.NormalizedCultureInfo);
            }
        }

        public string Currency
        {
            get
            {
                return this.account.Currency;
            }
        }

        public string CurrencyNormalized
        {
            get
            {
                return this.account.NormalizedCurrency;
            }
        }

        public string CountryFlag
        {
            get
            {

                Currency c = this.account.GetCurrency();
                if (c == null)
                {
                    var money = (MyMoney)this.account.Parent.Parent;
                    c = money.Currencies.DefaultCurrency;
                }

                if (c != null)
                {
                    var found = (from ci in Walkabout.WpfConverters.CultureHelpers.CurrencyCultures
                                 where ci.CultureCode == c.CultureCode
                                 select ci).FirstOrDefault();

                    if (found != null)
                    {
                        return "/Icons/Flags/" + found.TwoLetterISORegionName.ToLower() + ".png";
                    }
                }

                return null;
            }
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
                return this.account.Name + ": " + this.account.AccountId;
            }
        }

        public string TooltipRow2
        {
            get
            {
                DateTime dt = this.account.LastBalance;
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
                    months += years * 12;
                    return string.Format("Reminder: you have not balanced this account in {0} months\n" +
                           "You can change this reminder using 'Reconcile Warning' in the account properties", months);
                }
            }
        }
    }

    public class AccountSectionHeader : AccountViewModel
    {
        private decimal balanceNormalized;
        private CultureInfo cultureInfo;
        private Currency defaultCurrency;


        public string Name
        {
            get => this.Title;
        }

        public string Title { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is AccountSectionHeader m)
            {
                return m.Title == this.Title;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public decimal BalanceInNormalizedCurrencyValue
        {
            get => this.balanceNormalized;
            set
            {
                if (this.balanceNormalized != value)
                {
                    this.balanceNormalized = value;
                    this.OnPropertyChanged("BalanceInNormalizedCurrencyValue");
                    this.OnPropertyChanged("BalanceForeground");
                }
            }
        }

        public string BalanceAsString
        {
            get
            {
                var ci = Currency.GetCultureForCurrency(this.DefaultCurrency.Symbol);
                return StringHelpers.GetFormattedAmount(this.BalanceInNormalizedCurrencyValue, ci) + (this.ShowCurrency ? " " + this.DefaultCurrency?.Symbol : "");
            }
        }

        public CultureInfo CultureInfo { get => this.cultureInfo; set => this.cultureInfo = value; }

        public Currency DefaultCurrency
        {
            get
            {
                if (this.defaultCurrency == null)
                {
                    this.defaultCurrency = Currencies.GetDefaultCurrency();
                }
                return this.defaultCurrency;
            }

            set
            {
                this.defaultCurrency = value;
            }
        }

        protected override void OnSelectedChanged()
        {
            this.OnPropertyChanged("BalanceForeground");
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
            if (this.Accounts != null)
            {
                foreach (Account a in this.Accounts)
                {
                    balance += a.BalanceNormalized;
                }
            }
            this.BalanceInNormalizedCurrencyValue = balance;
        }
    }

}
