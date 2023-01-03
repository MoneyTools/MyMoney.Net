using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using Microsoft.Win32;
using Walkabout.Assitance;
using Walkabout.Attachments;
using Walkabout.Charts;
using Walkabout.Configuration;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Dialogs;
using Walkabout.Migrate;
using Walkabout.StockQuotes;
using Walkabout.Reports;
using Walkabout.Taxes;
using Walkabout.Utilities;
using Walkabout.Views;
using Walkabout.Views.Controls;
using Walkabout.Setup;
using System.Xml.Linq;
using Walkabout.Help;
using Walkabout.Ofx;
using Walkabout.Interfaces.Views;
using System.Deployment.Application;
using System.Threading.Tasks;


#if PerformanceBlocks
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
#endif

namespace Walkabout
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IServiceProvider, IStatusService, IViewNavigator
    {
        #region PROPERTIES PRIVATE

        internal static Uri DownloadSite = new Uri("https://lovettsoftwarestorage.blob.core.windows.net/downloads/MyMoney/");
        internal static string InstallUrl = "https://github.com/clovett/myMoney.Net";

        private readonly DelayedActions delayedActions = new DelayedActions();
        private readonly Settings settings;
        private readonly UndoManager navigator;
        private readonly UndoManager manager;
        private AttachmentManager attachmentManager;
        private StatementManager statementManager;
        private bool canSave;
        private IDatabase database;
        private DatabaseSettings databaseSettings = new DatabaseSettings();

        internal MyMoney myMoney = new MyMoney();
        private ChangeTracker tracker;

        //---------------------------------------------------------------------
        // The Toolbox controls
        private readonly AccountsControl accountsControl;
        private readonly CategoriesControl categoriesControl;
        private readonly PayeesControl payeesControl;
        private readonly SecuritiesControl securitiesControl;
        private RentsControl rentsControl;


        private string caption;
        private BalanceControl balanceControl;

        private readonly ExchangeRates exchangeRates;
        private StockQuoteManager quotes;
        private StockQuoteCache cache;
        private readonly int mainThreadId;
        private uint loadTime = NativeMethods.TickCount;
        private readonly RecentFilesMenu recentFilesMenu;
        private AnimatedMessage animatedStatus;
        #endregion

        #region CONSTRUCTORS

        public MainWindow()
        {
            UiDispatcher.CurrentDispatcher = this.Dispatcher;
        }

        public MainWindow(Settings settings)
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.MainWindowInitialize))
            {
#endif
                UiDispatcher.CurrentDispatcher = this.Dispatcher;
                this.settings = settings;
                this.settings.PropertyChanged += this.OnSettingsChanged;

                this.attachmentManager = new AttachmentManager(this.myMoney);
                this.attachmentManager.AttachmentDirectory = settings.AttachmentDirectory;

                this.statementManager = new StatementManager(this.myMoney);
                this.statementManager.StatementsDirectory = settings.StatementsDirectory;

                var stockService = settings.StockServiceSettings;
                if (stockService == null)
                {
                    settings.StockServiceSettings = new List<StockServiceSettings>();
                    settings.StockServiceSettings.Add(IEXCloud.GetDefaultSettings());
                }

                Walkabout.Utilities.UiDispatcher.CurrentDispatcher = this.Dispatcher;
                this.mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                this.OnThemeChanged(settings.Theme);
                this.ParseCommandLine();

                this.navigator = new UndoManager(1000); // view state stack
                this.manager = new UndoManager(1000);


                System.Windows.Forms.Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.CatchException);
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(this.OnUnhandledException);
                App.Current.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler(this.OnDispatcherUnhandledException);
                TaskScheduler.UnobservedTaskException += this.TaskScheduler_UnobservedTaskException;

                this.InitializeComponent();

                //-----------------------------------------------------------------
                // ACCOUNTS CONTROL
                this.accountsControl = new AccountsControl();
                this.accountsControl.Site = this;
                this.accountsControl.TabIndex = 1;
                this.accountsControl.Name = "AccountsControl";
                this.accountsControl.MyMoney = this.myMoney;
                this.accountsControl.DatabaseSettings = this.databaseSettings;
                this.accountsControl.SyncAccount += new EventHandler<ChangeEventArgs>(this.OnAccountsPanelSyncAccount);
                this.accountsControl.BalanceAccount += new EventHandler<ChangeEventArgs>(this.OnAccountsPanelBalanceAccount);
                this.accountsControl.ShowTransfers += new EventHandler<ChangeEventArgs>(this.OnAccountsPanelShowTransfers);
                this.accountsControl.DisplayClosedAccounts = this.settings.DisplayClosedAccounts;

                //-----------------------------------------------------------------
                // CATEGORIES CONTROL
                this.categoriesControl = new CategoriesControl();
                this.categoriesControl.TabIndex = 2;
                this.categoriesControl.Name = "CategoriesControl";
                this.categoriesControl.MyMoney = this.myMoney;
                this.categoriesControl.Site = this;

                //-----------------------------------------------------------------
                // PAYEES CONTROL
                this.payeesControl = new PayeesControl();
                this.payeesControl.TabIndex = 3;
                this.payeesControl.Name = "PayeesControl";
                this.payeesControl.MyMoney = this.myMoney;
                this.payeesControl.Site = this;

                //-----------------------------------------------------------------
                // STOCKS CONTROL
                this.securitiesControl = new SecuritiesControl();
                this.securitiesControl.TabIndex = 4;
                this.securitiesControl.Name = "SecuritiesControl";
                this.securitiesControl.MyMoney = this.myMoney;

                this.UpdateRentalManagement();

                //-----------------------------------------------------------------
                // Set the default view to be the Transaction view
                this.SetCurrentView<TransactionsView>();
                this.navigator.Pop();

                //-----------------------------------------------------------------
                // These events must be set after view.ServiceProvider is set
                //
                this.accountsControl.SelectionChanged += new EventHandler(this.OnSelectionChangeFor_Account);
                this.categoriesControl.SelectionChanged += new EventHandler(this.OnSelectionChangeFor_Categories);
                this.categoriesControl.GroupSelectionChanged += new EventHandler(this.OnSelectionChangeFor_CategoryGroup);
                this.categoriesControl.SelectedTransactionChanged += new EventHandler(this.CategoriesControl_SelectedTransactionChanged);
                this.payeesControl.SelectionChanged += new EventHandler(this.OnSelectionChangeFor_Payees);
                this.securitiesControl.SelectionChanged += new EventHandler(this.OnSelectionChangeFor_Securities);

                this.exchangeRates = new ExchangeRates();

                //-----------------------------------------------------------------
                // Setup the "file import" module
                //
                FileSystemWatcher fsw = new FileSystemWatcher();
                fsw.Path = ProcessHelper.ImportFileListFolder;
                fsw.NotifyFilter = NotifyFilters.LastWrite;
                fsw.Changed += new FileSystemEventHandler(this.OnImportFolderContentHasChanged);
                fsw.EnableRaisingEvents = true;

                //-----------------------------------------------------------------
                // Setup the ToolBox (aka Accordion)
                //
                this.toolBox.Add("ACCOUNTS", "AccountsSelector", this.accountsControl);
                this.toolBox.Add("CATEGORIES", "CategoriesSelector", this.categoriesControl, true);
                this.toolBox.Add("PAYEES", "PayeesSelector", this.payeesControl, true);
                this.toolBox.Add("SECURITIES", "SecuritiesSelector", this.securitiesControl, true);

                this.OnUpdateRentalTab();

                this.toolBox.Expanded += new RoutedEventHandler(this.OnToolBoxItemsExpanded);
                this.toolBox.FilterUpdated += new Accordion.FilterEventHandler(this.OnToolBoxFilterUpdated);

                Keyboard.AddGotKeyboardFocusHandler(this, new KeyboardFocusChangedEventHandler(this.OnKeyboardFocusChanged));

                //---------------------------------------------------------------
                // Setup the Graph area
                //
                this.PieChartExpenses.SelectionChanged += new EventHandler(this.PieChartSelectionChanged);
                this.PieChartIncomes.SelectionChanged += new EventHandler(this.PieChartSelectionChanged);

                //-----------------------------------------------------------------
                // Setup the Loan area
                //
                //this.LoanChart.MyMoney = this.myMoney;

                // Setup the HistoryChart
                this.HistoryChart.SelectionChanged += new EventHandler(this.HistoryChart_SelectionChanged);

                //-----------------------------------------------------------------
                // Transaction Graph
                //
                this.TransactionGraph.MouseDown += new MouseButtonEventHandler(this.OnGraphMouseDown);

                //-----------------------------------------------------------------
                // Main context setup
                //
                this.DataContext = this.myMoney;
                DataContextChanged += new DependencyPropertyChangedEventHandler(this.OnDataContextChanged);

                this.TransactionView.QuickFilterChanged += new EventHandler(this.TransactionView_QuickFilterChanged);

                this.ButtonShowUpdateInfo.Visibility = System.Windows.Visibility.Collapsed;

                Loaded += new RoutedEventHandler(this.OnMainWindowLoaded);

                //----------------------------------------------------------------------
                // Download tab
                this.OfxDownloadControl.SelectionChanged += this.OfxDownloadControl_SelectionChanged;
                this.TabDownload.Visibility = System.Windows.Visibility.Hidden;

                //----------------------------------------------------------------------
                // Output Window
                this.TabOutput.Visibility = System.Windows.Visibility.Collapsed;
                this.AddHandler(OutputPane.ShowOutputEvent, new RoutedEventHandler(this.OnShowOutputWindow));
                this.AddHandler(OutputPane.HideOutputEvent, new RoutedEventHandler(this.OnHideOutputWindow));

                this.recentFilesMenu = new RecentFilesMenu(this.MenuRecentFiles);
                this.recentFilesMenu.SetFiles(settings.RecentFiles);
                this.recentFilesMenu.RecentFileSelected += this.OnRecentFileSelected;

                this.TransactionGraph.ServiceProvider = this;
                this.AppSettingsPanel.Closed += this.OnAppSettingsPanelClosed;

#if PerformanceBlocks
            }
#endif
        }

        private void OnSettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Settings settings = (Settings)sender;
            switch (e.PropertyName)
            {
                case "Theme":
                    this.OnThemeChanged(this.settings.Theme);
                    break;
            }
        }

        private void DatabaseSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            DatabaseSettings settings = (DatabaseSettings)sender;
            switch (e.PropertyName)
            {
                case "RentalManagement":
                    this.UpdateRentalManagement();
                    this.OnUpdateRentalTab(); // in case tab needs to be added back.
                    break;
                case "FiscalYearStart":
                    this.HistoryChart.FiscalYearStart = settings.FiscalYearStart;
                    break;
            }

            // save right away, but decoulpled from UI thread.
            this.delayedActions.StartDelayedAction("SaveDatabaseSettings", () =>
            {
                try
                {
                    this.databaseSettings.Save();
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show("Error saving updated database settings: " + ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }, TimeSpan.FromSeconds(1));
        }


        private void UpdateRentalManagement()
        {
            if (this.databaseSettings.RentalManagement)
            {
                //-----------------------------------------------------------------
                // RENTAL CONTROL
                if (this.rentsControl == null)
                {
                    this.rentsControl = new RentsControl();
                    this.rentsControl.TabIndex = 5;
                    this.rentsControl.Name = "RentsControl";
                    this.rentsControl.SelectionChanged += new EventHandler(this.OnSelectionChangeFor_Rents);
                }
                this.rentsControl.MyMoney = this.myMoney;
            }
            else
            {
                this.rentsControl = null;
            }
        }

        private void OnUpdateRentalTab()
        {
            if (this.rentsControl != null)
            {
                if (!this.toolBox.ContainsTab("RENTS"))
                {
                    this.toolBox.Add("RENTS", "RentsSelector", this.rentsControl);
                }
            }
            else
            {
                if (this.toolBox.ContainsTab("RENTS"))
                {
                    this.toolBox.RemoveTab("RENTS");
                }
            }
        }

        private void OnStockQuoteHistoryAvailable(object sender, StockQuoteHistory history)
        {
            Security s = this.myMoney.Securities.FindSymbol(history.Symbol, false);
            if (s != null && history.History.Count > 0)
            {
                StockQuote quote = history.History[history.History.Count - 1];
                if (quote.Date > s.PriceDate)
                {
                    this.myMoney.BeginUpdate(this);
                    try
                    {
                        s.LastPrice = s.Price;
                        s.Price = quote.Close;
                        s.PriceDate = quote.Date;
                        this.delayedActions.StartDelayedAction("updateBalance", this.UpdateBalance, TimeSpan.FromSeconds(1));
                    }
                    finally
                    {
                        this.myMoney.EndUpdate();
                    }
                }
            }
            if (this.TransactionView.ActiveSecurity != null && this.TransactionView.ActiveSecurity.Symbol == history.Symbol)
            {
                this.StockGraph.Generator = new SecurityGraphGenerator(history, this.TransactionView.ActiveSecurity);
            }
        }

        private void UpdateBalance()
        {
            this.myMoney.BeginUpdate(this);
            try
            {
                CostBasisCalculator calculator = new CostBasisCalculator(this.myMoney, DateTime.Now);
                foreach (Account a in this.myMoney.Accounts.GetAccounts())
                {
                    if (a.Type != AccountType.Loan)
                    {
                        this.myMoney.Rebalance(calculator, a);
                    }
                }
            }
            finally
            {
                this.myMoney.EndUpdate();
            }
        }

        private void OnRecentFileSelected(object sender, RecentFileEventArgs e)
        {
            if (!this.SaveIfDirty())
            {
                return;
            }

            Settings.TheSettings.Database = e.FileName;
            this.BeginLoadDatabase();
        }

        private void OfxDownloadControl_SelectionChanged(object sender, OfxDocumentControlSelectionChangedEventArgs e)
        {
            IViewNavigator navigator = this;
            var data = e.Data;
            if (data != null && data.Added != null && data.Added.Count > 0)
            {
                navigator.ViewTransactions(data.Added);
            }
        }

        private void OnShowOutputWindow(object sender, RoutedEventArgs e)
        {
            this.ShowOutputWindow();
        }

        private void ShowOutputWindow()
        {
            this.TabOutput.Visibility = System.Windows.Visibility.Visible;
            this.TabForGraphs.SelectedItem = this.TabOutput;
        }

        private void OnHideOutputWindow(object sender, RoutedEventArgs e)
        {
            this.HideOutputWindow();
        }

        private void HideOutputWindow()
        {
            this.TabOutput.Visibility = System.Windows.Visibility.Collapsed;
            this.TabForGraphs.SelectedItem = this.TabTrends;
        }

        private void ClearOutput()
        {
            this.OutputView.Clear();
            this.HideOutputWindow();
        }

        private void OnCloseOutputWindow(object sender, RoutedEventArgs e)
        {
            this.OnHideOutputWindow(sender, e);
        }

        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.Loaded))
            {
#endif
                if (!string.IsNullOrEmpty(this.newDatabaseName))
                {
                    // This only happens during testing when we launch the app with the "-nd" command line option.
                    this.CreateNewDatabase(this.newDatabaseName);
                }
                else if (!this.emptyWindow)
                {
                    // windows 11 has a weird behavior where main window does not appear before the 
                    // Open Database dialog unless we do this delay here.
                    this.delayedActions.StartDelayedAction("loaddata", this.BeginLoadDatabase, TimeSpan.FromMilliseconds(1));
                }
#if PerformanceBlocks
            }
#endif
        }

        private void TransactionView_QuickFilterChanged(object sender, EventArgs e)
        {
            this.SetChartsDirty();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Log it to error window instead of crashing the app
            if (e.IsTerminating)
            {
                string msg = null;
                if (e.ExceptionObject != null)
                {
                    msg = "The reason is:\n" + e.ExceptionObject.ToString();
                }

                this.SaveIfDirty("The program is terminating, do you want to save your changes?", msg);
            }
            else if (e.ExceptionObject != null)
            {
                this.HandleUnhandledException(e.ExceptionObject);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception != null)
            {
                this.HandleUnhandledException(e.Exception);
            }
            e.SetObserved();
        }

        // stop re-entrancy
        private bool handlingException;

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (this.handlingException)
            {
                e.Handled = false;
            }
            else
            {
                this.handlingException = true;
                UiDispatcher.Invoke(new Action(() =>
                {
                    try
                    {
                        e.Handled = this.HandleUnhandledException(e.Exception);
                    }
                    catch (Exception)
                    {
                    }
                    this.handlingException = false;
                }));
            }
        }

        private bool HandleUnhandledException(object exceptionObject)
        {
            Exception ex = exceptionObject as Exception;
            string message = null;
            string details = null;
            if (ex == null && exceptionObject != null)
            {
                message = exceptionObject.GetType().FullName;
                details = exceptionObject.ToString();
            }
            else
            {
                message = ex.Message;
                details = ex.ToString();
            }

            try
            {
                MessageBoxEx.Show(message, "Unhandled Exception - Please email details to Chris", details, MessageBoxButton.OK, MessageBoxImage.Error);
                return true;
            }
            catch (Exception)
            {
                // hmmm, if we can't show the dialog then perhaps this is some sort of stack overflow.
                // save the details to a file, terminate the process and 
                this.SaveCrashLog(message, details);
            }
            return false;
        }

        private void SaveCrashLog(string message, string details)
        {
            string path = this.database.DatabasePath;
            string crashFile = Path.Combine(Path.GetDirectoryName(path), "crash.xml");
            XDocument doc = new XDocument(new XElement("Crash",
                new XElement("Message", message),
                new XElement("Details", details)));
            doc.Save(crashFile);
        }

        private void CheckCrashLog()
        {
            string path = this.database.DatabasePath;
            string crashFile = Path.Combine(Path.GetDirectoryName(path), "crash.xml");
            if (File.Exists(crashFile))
            {
                XDocument doc = XDocument.Load(crashFile);
                File.Delete(crashFile);

                string message = (string)doc.Root.Element("Message");
                string details = (string)doc.Root.Element("Details");

                MessageBoxEx.Show(message, "Money app crashed previously - Please email details to Chris", details, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopTracking()
        {
            using (this.tracker)
            {
                if (this.tracker != null)
                {
                    this.tracker.DirtyChanged -= this.OnDirtyChanged;
                }
                this.tracker = null;
            }
            this.attachmentManager.Stop();
            this.statementManager.Stop();
            if (this.myMoney != null)
            {
                this.myMoney.Changed -= new EventHandler<ChangeEventArgs>(this.OnChangedUI);
            }
        }

        private void StartTracking()
        {
            this.tracker = new ChangeTracker(this.myMoney, this);
            this.tracker.DirtyChanged += new EventHandler(this.OnDirtyChanged);
            this.attachmentManager.AttachmentDirectory = this.settings.AttachmentDirectory;
            this.attachmentManager.Start();
            this.statementManager.StatementsDirectory = this.settings.StatementsDirectory;
            this.statementManager.Start();
            this.myMoney.Changed -= new EventHandler<ChangeEventArgs>(this.OnChangedUI);
            this.myMoney.Changed += new EventHandler<ChangeEventArgs>(this.OnChangedUI);
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            MyMoney money = (MyMoney)e.NewValue;
            if (money != this.myMoney)
            {
                this.myMoney = money;
                this.StopTracking();

                if (this.quotes != null)
                {
                    this.CleanupStockQuoteManager();
                }
                this.UpdateRentalManagement();
                this.UpdateCaption(null);

                this.myMoney.BeginUpdate(this);
                try
                {
                    if (this.database != null)
                    {
                        string path = this.database.DatabasePath;
                        this.SetupStockQuoteManager(money);
                        OfxRequest.OfxLogPath = Path.Combine(Path.GetDirectoryName(path), "Logs");
                    }

                    this.accountsControl.MyMoney = this.myMoney;
                    this.categoriesControl.MyMoney = this.myMoney;
                    this.payeesControl.MyMoney = this.myMoney;
                    this.securitiesControl.MyMoney = this.myMoney;

                    this.attachmentManager.Stop();
                    this.attachmentManager = new AttachmentManager(this.myMoney);
                    this.attachmentManager.AttachmentDirectory = this.settings.AttachmentDirectory;

                    this.statementManager.Stop();
                    this.statementManager = new StatementManager(this.myMoney);
                    this.statementManager.StatementsDirectory = this.settings.StatementsDirectory;

                }
                finally
                {
                    this.myMoney.EndUpdate();
                }

                this.Cursor = Cursors.Arrow;

                this.SetCurrentView<TransactionsView>();
                this.TransactionView.Money = this.myMoney;
                IView view = this.TransactionView;

                // try again to restore the selected account/payee, whatever, since we now have loaded data to play with
                ViewState state = this.settings.GetViewState(view.GetType());
                if (state != null)
                {
                    view.ViewState = state;
                }

                if (this.TransactionView.ActiveAccount != null)
                {
                    this.accountsControl.SelectedAccount = this.TransactionView.ActiveAccount;
                }

                if (this.myMoney.Accounts.Count == 0)
                {
                    // make sure we clear out current view (this is necessary if you go from a loaded database
                    // to a new empty one with no accounts yet).
                    this.ViewTransactions(new Transaction[0]);
                }

                GraphState graphState = this.settings.GraphState;
                if (graphState != null)
                {
                    this.SetGraphState(graphState);
                }

                this.exchangeRates.MyMoney = money;

                this.ClearOutput();
                this.ClearOfxDownloads();

                if (this.settings.LastStockRequest != DateTime.Today && this.quotes != null)
                {
                    this.quotes.UpdateQuotes();
                    this.exchangeRates.UpdateRates();
                }

                this.ShowNetWorth();

                this.UpdateCategoryColors();

                this.SetChartsDirty();

                this.HideBalancePanel(false, false);

                this.Dispatcher.BeginInvoke(new Action(this.AfterLoadChecks), DispatcherPriority.Background);
            }
        }

        private void SetupStockQuoteManager(MyMoney money)
        {
            this.quotes = new StockQuoteManager(this, this.settings.StockServiceSettings, this.GetStockQuotePath());
            this.quotes.DownloadComplete += new EventHandler<EventArgs>(this.OnStockDownloadComplete);
            this.quotes.HistoryAvailable += this.OnStockQuoteHistoryAvailable;
            this.cache = new StockQuoteCache(money, this.quotes.DownloadLog);
        }

        private string GetStockQuotePath()
        {
            string path = this.database.DatabasePath;
            return Path.Combine(Path.GetDirectoryName(path), "StockQuotes");
        }

        private void ClearOfxDownloads()
        {
            this.OfxDownloadControl.Cancel();
            this.HideDownloadTab();
        }

        private void AfterLoadChecks()
        {
            this.myMoney.BeginUpdate(this);
            try
            {
                // remove dangling transactions.
                foreach (Transaction t in this.myMoney.Transactions)
                {
                    if (t.Account == null)
                    {
                        this.myMoney.Transactions.Remove(t);
                    }
                }

                CostBasisCalculator calculator = new CostBasisCalculator(this.myMoney, DateTime.Now);

                // This can be done on the background thread before we wire up 
                // all the event handlers (for improved performance).
                foreach (Account a in this.myMoney.Accounts.GetAccounts())
                {
                    if (a.Type != AccountType.Loan)
                    {
                        this.myMoney.Rebalance(calculator, a);
                    }
                }

                this.myMoney.CheckSecurities();

                this.myMoney.CheckCategoryFunds();

                this.myMoney.TransactionExtras.MigrateTaxYears(this.myMoney, this.databaseSettings.FiscalYearStart);
            }
            finally
            {
                this.myMoney.EndUpdate();
            }

            this.SetDirty(false);

            // one last thing (this can take over the "current view" so it comes last).
            this.TransactionView.CheckTransfers();

            this.CheckLastVersion();

            this.StartTracking();

            this.CheckCrashLog();
        }

        private void OnStockDownloadComplete(object sender, EventArgs e)
        {
            this.settings.LastStockRequest = DateTime.Today;
        }

        private void CategoriesControl_SelectedTransactionChanged(object sender, EventArgs e)
        {
            bool isTransactionViewAlready = this.CurrentView is TransactionViewState;
            // This happens when there is an error budgeting a transaction, so we want to display the transaction so it can be fixed.
            this.TransactionView.ViewTransactions(this.categoriesControl.SelectedTransactions);
            if (!isTransactionViewAlready)
            {
                this.navigator.Pop();
            }
        }

        private void OnToolBoxFilterUpdated(object sender, string filter)
        {
            if (sender is CategoriesControl)
            {
                this.categoriesControl.Filter(filter);
            }

            if (sender is PayeesControl)
            {
                this.payeesControl.Filter(filter);
            }

            if (sender is SecuritiesControl)
            {
                this.securitiesControl.Filter(filter);
            }

        }

        #endregion

        #region Command Line

        private bool emptyWindow;
        private string newDatabaseName;
        private bool noSettings;

        private void ParseCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1, n = args.Length; i < n; i++)
            {
                string arg = args[i];
                if (arg[0] == '-' || arg[0] == '/')
                {
                    switch (arg.Substring(1).TrimStart('-').ToLowerInvariant())
                    {
                        case "?":
                        case "h":
                        case "help":
                            this.ShowUsage();
                            Application.Current.Shutdown();
                            break;
                        case "n":
                            this.emptyWindow = true;
                            break;
                        case "nd":
                            if (i + 1 < n)
                            {
                                this.newDatabaseName = args[++i];
                            }
                            break;
                        case "nosettings":
                            this.noSettings = true;
                            break;
                    }
                }

                // are we asked to open a database file?
                {
                    string possibleFilename = arg.Trim('"');
                    if (possibleFilename.EndsWith(".mymoney.db", StringComparison.OrdinalIgnoreCase) ||
                        possibleFilename.EndsWith(".mmdb", StringComparison.OrdinalIgnoreCase) ||
                        possibleFilename.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                        possibleFilename.EndsWith(".bxml", StringComparison.OrdinalIgnoreCase))
                    {
                        Settings.TheSettings.Database = possibleFilename;
                    }
                }
            }
        }

        private void ShowUsage()
        {
            MessageBoxEx.Show(@"Usage MyMoney.exe [options]

/n        Open empty window with no pre loaded database
/nd <name> Open new database with given name.  If name ends with '.myMoney.sdf' a CE database is created
*.ofx     Import the given ofx files
*.qif     Import the given qif files
/h        Show this help page", "Command Line Help", MessageBoxButton.OK, MessageBoxImage.None);
        }

        #endregion

        #region Toolbox Selection change events

        /// <summary>
        /// When one of the item in the accordion gets expanded this event will get fired
        /// so that we can switch the Transaction view to match the toolbox content that was selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnToolBoxItemsExpanded(object sender, RoutedEventArgs e)
        {
            if (sender is AccountsControl)
            {
                this.OnSelectionChangeFor_Account(sender, e);
            }

            if (sender is PayeesControl)
            {
                this.OnSelectionChangeFor_Payees(sender, e);
            }

            if (sender is SecuritiesControl)
            {
                this.OnSelectionChangeFor_Securities(sender, e);
            }

            if (sender is CategoriesControl)
            {
                this.OnSelectionChangeFor_Categories(sender, e);
            }

            if (sender is RentsControl)
            {
                this.OnSelectionChangeFor_Rents(sender, e);
            }
        }

#if DEBUG
        private int lastHandlerCount;
#endif

        private void OnSelectionChangeFor_Account(object sender, EventArgs e)
        {
#if DEBUG
            var count = this.myMoney.TotalChangeListenerCount;
            if (count != this.lastHandlerCount)
            {
                Debug.WriteLine($"Number of listeners changed from {this.lastHandlerCount} to {count}");
                this.lastHandlerCount = count;
            }
#endif

            //-----------------------------------------------------------------
            // Look for Accounts
            //
            Account a = this.accountsControl.SelectedAccount;

            if (a != null)
            {
                if (a.Type == AccountType.Loan)
                {
                    this.SaveViewStateOfCurrentView();
                    bool isLoanViewAlready = this.CurrentView is LoansView;
                    LoansView view = this.SetCurrentView<LoansView>();
                    view.AccountSelected = a;
                    if (!isLoanViewAlready)
                    {
                        this.navigator.Pop();
                    }
                    this.TrackSelectionChanges();
                }
                else
                {
                    bool isTransactionViewAlready = this.CurrentView is TransactionsView;
                    TransactionsView view = this.SetCurrentView<TransactionsView>();
                    view.ViewTransactionsForSingleAccount(a, TransactionSelection.Current, 0);

                    if (!isTransactionViewAlready)
                    {
                        this.navigator.Pop();
                    }
                    this.TrackSelectionChanges();
                }

                return;
            }

        }

        private void OnSelectionChangeFor_Categories(object sender, EventArgs e)
        {
            Category c = this.categoriesControl.Selected;
            if (c != null)
            {
                this.ViewTransactionsByCategory(c);
            }
        }

        private void OnSelectionChangeFor_CategoryGroup(object sender, EventArgs e)
        {
            CategoryGroup g = this.categoriesControl.SelectedGroup;
            if (g != null)
            {
                this.ViewTransactionsByCategoryGroup(g);
            }
        }

        private void ViewTransactionsByCategory(Category c)
        {
            bool isTransactionViewAlready = this.CurrentView is TransactionsView;
            TransactionsView view = this.SetCurrentView<TransactionsView>();
            long selectedId = -1;
            view.ViewTransactionsForCategory(c, selectedId);
            if (!isTransactionViewAlready)
            {
                this.navigator.Pop();
            }
            this.TrackSelectionChanges();
        }

        private void ViewTransactionsByCategoryGroup(CategoryGroup g)
        {
            bool isTransactionViewAlready = this.CurrentView is TransactionsView;
            TransactionsView view = this.SetCurrentView<TransactionsView>();
            List<Transaction> total = new List<Data.Transaction>();
            foreach (Category c in g.Subcategories)
            {
                IList<Transaction> transactions = this.myMoney.Transactions.GetTransactionsByCategory(c,
                    new Predicate<Transaction>((t) => { return true; }));
                total.AddRange(transactions);
            }

            total.Sort(Transactions.SortByDate);
            view.ViewTransactionsForCategory(new Data.Category() { Name = g.Name }, total);
            if (!isTransactionViewAlready)
            {
                this.navigator.Pop();
            }
            this.TrackSelectionChanges();
        }


        private void OnSelectionChangeFor_Payees(object sender, EventArgs e)
        {
            Payee p = this.payeesControl.Selected;
            if (p != null)
            {
                bool isTransactionViewAlready = this.CurrentView is TransactionsView;
                TransactionsView view = this.SetCurrentView<TransactionsView>();
                view.ViewTransactionsForPayee(this.payeesControl.Selected, view.SelectedRowId);
                if (!isTransactionViewAlready)
                {
                    this.navigator.Pop();
                }
                this.TrackSelectionChanges();
            }
        }

        private void OnSelectionChangeFor_Securities(object sender, EventArgs e)
        {
            Security s = this.securitiesControl.Selected;
            if (s != null)
            {
                this.ViewTransactionsBySecurity(s);
            }
        }

        public void ViewTransactionsBySecurity(Security security)
        {
            if (security != null)
            {
                bool isTransactionViewAlready = this.CurrentView is TransactionsView;
                TransactionsView view = this.SetCurrentView<TransactionsView>();
                view.ViewTransactionsForSecurity(security, view.SelectedRowId);
                if (!isTransactionViewAlready)
                {
                    this.navigator.Pop();
                }
                this.TrackSelectionChanges();
            }
        }

        private void OnSelectionChangeFor_Rents(object sender, EventArgs e)
        {
            if (this.rentsControl == null)
            {
                return;
            }
            object currentlySelected = this.rentsControl.Selected;

            if (currentlySelected is RentBuilding)
            {
                this.SaveViewStateOfCurrentView();
                RentSummaryView summary = this.SetCurrentView<RentSummaryView>();
                summary.SetViewToRentBuilding(currentlySelected as RentBuilding);
            }
            else if (currentlySelected is RentalBuildingSingleYear)
            {
                this.SaveViewStateOfCurrentView();
                RentSummaryView summary = this.SetCurrentView<RentSummaryView>();
                summary.SetViewToRentalBuildingSingleYear(currentlySelected as RentalBuildingSingleYear);
            }
            else if (currentlySelected is RentalBuildingSingleYearSingleDepartment)
            {
                TransactionsView view = this.SetCurrentView<TransactionsView>();
                view.ViewTransactionRentalBuildingSingleYearDepartment(currentlySelected as RentalBuildingSingleYearSingleDepartment);
            }

            this.SetChartsDirty();

            this.TrackSelectionChanges();
        }

        private void SetChartsDirty()
        {
            this.delayedActions.StartDelayedAction("updateCharts", this.TryUpdateCharts, TimeSpan.FromMilliseconds(10));
        }

        private void TryUpdateCharts()
        {
            if (!this.myMoney.IsUpdating)
            {
                this.UpdateCharts();
            }
            else
            {
                this.SetChartsDirty();
            }
        }

        #endregion

        #region View Management


        public IView CurrentView
        {
            get { return this.EditingZone.Content as IView; }
            set { this.EditingZone.Content = value; }
        }

        private readonly Dictionary<Type, IView> cacheViews = new Dictionary<Type, IView>();

        /// <summary>
        /// Get or create a single instance of any IView implementation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private T GetOrCreateView<T>() where T : IView
        {
            if (this.cacheViews.ContainsKey(typeof(T)) == false)
            {
                //
                // This is the first time that we see this type of view implementation
                // lets create the only instance needed
                object newInstance = Activator.CreateInstance(typeof(T));

                // If this view derives from IView the we need to hookup some event listeners
                IView iView = newInstance as IView;
                if (iView != null)
                {

                    iView.ServiceProvider = this;
                    iView.BeforeViewStateChanged += new EventHandler(this.OnBeforeViewStateChanged);
                    iView.AfterViewStateChanged += new EventHandler<AfterViewStateChangedEventArgs>(this.OnAfterViewStateChanged);
                    iView.Money = this.myMoney;

                    ViewState state = this.settings.GetViewState(typeof(T));
                    if (state != null)
                    {
                        iView.ViewState = state;
                    }
                    else
                    {
                        XmlElement stateNode = this.settings.GetViewStateNode(typeof(T));
                        if (stateNode != null)
                        {
                            ViewState newState = iView.DeserializeViewState(new XmlNodeReader(stateNode));
                            iView.ViewState = newState;
                            this.settings.SetViewState(typeof(T), newState);
                        }
                    }
                    // Cache the new singleton instance
                    this.cacheViews.Add(typeof(T), iView);
                }
                else
                {
                    MessageBoxEx.Show("Internal error");
                }
            }

            IView o = this.cacheViews[typeof(T)];

            return (T)o;
        }


        /// <summary>
        /// Gets or Creates a single view and makes it the current active view
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="saveState">Whether to fire OnBeforeViewStateChanged event</param>
        /// <returns></returns>
        private T SetCurrentView<T>() where T : IView
        {
            IView newView = this.GetOrCreateView<T>();
            this.CurrentView = newView;
            this.CurrentView.ActivateView();
            return (T)this.CurrentView;
        }


        /// <summary>
        /// Helper method for partying on the popular transaction view
        /// </summary>
        private TransactionsView TransactionView
        {
            get
            {
                var result = this.GetOrCreateView<TransactionsView>();
                result.ViewModelChanged -= this.OnTransactionViewModelChanged;
                result.ViewModelChanged += this.OnTransactionViewModelChanged;
                return result;
            }
        }

        private void OnTransactionViewModelChanged(object sender, EventArgs e)
        {
            this.SetChartsDirty();
        }

        #endregion

        #region Mouse & Keyboard Handling

        private IClipboardClient GetClipboardClient(IInputElement f)
        {
            if (f == null)
            {
                return null;
            }

            if (f is TextBox box)
            {
                // text edit mode, so disable row level copy/paste command.
                return new TextBoxClipboardClient(box);
            }
            if (f is ComboBox combo)
            {
                // text edit mode, so disable row level copy/paste command.
                return new ComboBoxClipboardClient(combo);
            }
            else if (f is RichTextBox rbox)
            {
                return new RichTextBoxClipboardClient(rbox);
            }
            else if (f is IClipboardClient client)
            {
                return client;
            }
            else if (f == this.TransactionGraph)
            {
                // todo: do we want to be able to copy stuff from this graph?
                // currentFocus = this.TransactionGraph;
                return null;
            }
            else
            {
                DependencyObject d = f as DependencyObject;
                while (d != null)
                {
                    if (d is IClipboardClient c)
                    {
                        return c;
                    }
                    else if (d is FlowDocumentView view)
                    {
                        return new FlowDocumentViewClipboardClient(view);
                    }
                    else if (d is Inline inline)
                    {
                        d = inline.Parent;
                    }
                    else
                    {
                        d = VisualTreeHelper.GetParent(d);
                    }
                }
            }
            return null;
        }

        private void OnKeyboardFocusChanged(object sender, KeyboardFocusChangedEventArgs e)
        {
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            bool alt = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
            bool altLeft = alt && e.SystemKey == Key.Left;
            bool altRight = alt && e.SystemKey == Key.Right;


            if (e.Key == Key.BrowserBack || altLeft)
            {
                if (this.navigator.CanUndo)
                {
                    this.Back();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.BrowserForward || altRight)
            {
                if (this.navigator.CanRedo)
                {
                    this.Forward();
                    e.Handled = true;
                }
            }

            base.OnPreviewKeyDown(e);
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            if (e.XButton1 == MouseButtonState.Pressed)
            {
                if (this.navigator.CanUndo)
                {
                    this.Back();
                }
            }
            else if (e.XButton2 == MouseButtonState.Pressed)
            {
                if (this.navigator.CanUndo)
                {
                    this.Forward();
                }
            }
            else
            {
                var pos = e.GetPosition(this.AppSettingsPanel);
                if (pos.X < 0 || pos.Y < 0)
                {
                    this.AppSettingsPanel.Visibility = Visibility.Collapsed;
                }
            }
            base.OnPreviewMouseDown(e);
        }



        #endregion

        #region Navigation History

        private void Back()
        {
            //Debug.WriteLine("");
            //Debug.WriteLine("------------------------------------------------------------");
            //Debug.WriteLine("****** Main window Back button was pressed, current view is " + ((IView)this.CurrentView).Caption);

            if (this.navigator.Current == null)
            {


                //Debug.WriteLine("****** Main window Back Navigator.Current is NULL");

                this.SaveViewStateOfCurrentView();   // save current state so we can come back here if user press FORWARD button
                this.navigator.Undo();          // undo the state we just pushed on the stack
            }

            var cmd = this.navigator.Undo();
            if (cmd != null)
            {
                cmd.Undo();
            }
        }

        private void Forward()
        {
            this.navigator.Redo();
        }


        private void OnCommandBackCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.navigator.CanUndo;
            e.Handled = true;
        }

        private void OnCommandBackExecute(object sender, ExecutedRoutedEventArgs e)
        {
            this.Back();
        }

        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.navigator.CanRedo;
            e.Handled = true;
        }

        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            this.Forward();
        }

        #region IViewNavigation


        /// <summary>
        /// Implement the "Navigate To" for switching to a Transaction view 
        /// </summary>
        /// <param name="transaction"></param>
        public void NavigateToTransaction(Transaction transaction)
        {
            UiDispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        TransactionsView transactionView = this.TransactionView;
                        if (transactionView != null)
                        {
                            transactionView.ViewTransactionsForSingleAccount(transaction.Account, TransactionSelection.Specific, transaction.Id);
                            this.SetCurrentView<TransactionsView>();

                        }
                    }));
        }

        public void ViewTransactions(IEnumerable<Transaction> list)
        {
            UiDispatcher.BeginInvoke(
                new Action(() =>
                {
                    TransactionsView view = this.SetCurrentView<TransactionsView>();
                    view.ViewTransactions(list);

                    // now we only want one view.
                    this.navigator.Pop();

                    //PendingChangeDropDown.IsChecked = false;
                }));
        }

        public void NavigateToSecurity(Security security)
        {
            this.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    SecuritiesView view = this.ViewSecurities();
                    if (view != null)
                    {
                        view.GotoSecurity(security);
                    }
                }));
        }

        #endregion

        #endregion

        #region Initialization

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            this.LoadConfig();

            this.TransactionView.QueryPanel.IsVisibleChanged += new DependencyPropertyChangedEventHandler(this.OnQueryPanelIsVisibleChanged);


            TempFilesManager.Initialize();


        }

        public bool HasDatabase { get { return this.database != null || this.isLoading; } }

        private void OnThemeChanged(string themeToApply)
        {
            if (themeToApply == "Dark")
            {
                ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Dark;
                AppTheme.Instance.SetTheme("Themes/Dark.xaml");
            }
            else
            {
                ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Light;
                AppTheme.Instance.SetTheme("Themes/Light.xaml");
            }
        }

        private void LoadConfig()
        {
            if (!File.Exists(this.settings.ConfigFile) || this.noSettings)
            {
                Rect bounds = SystemParameters.WorkArea;
                if (bounds.Width != 0 && bounds.Height != 0)
                {
                    this.Top = bounds.Top;
                    this.Left = bounds.Left;
                    this.Width = bounds.Width;
                    this.Height = bounds.Height;
                }
                this.TransactionView.ViewAllSplits = false;
                this.TransactionView.OneLineView = false;
            }
            else
            {
                if (this.settings.WindowSize.Width != 0 && this.settings.WindowSize.Height != 0)
                {
                    Point location = this.settings.WindowLocation;
                    this.Left = location.X;
                    this.Top = location.Y;

                    this.Width = this.settings.WindowSize.Width;
                    this.Height = this.settings.WindowSize.Height;
                }
                if (this.settings.ToolBoxWidth > 20)
                {
                    this.toolBox.Width = this.settings.ToolBoxWidth;
                    this.GridColumns.ColumnDefinitions[0].Width = new GridLength(this.settings.ToolBoxWidth);
                }
                if (this.settings.GraphHeight > 20)
                {
                    this.TransactionGraph.Height = this.settings.GraphHeight;
                }

                this.caption = this.settings.Database;
            }
        }

        private void SaveConfig()
        {
            Settings s = this.settings;
            if (this.TransactionView.QueryPanel != null)
            {
                this.settings.Query = this.TransactionView.QueryPanel.GetQuery();
            }
            s.WindowLocation = new Point((int)this.Left, (int)this.Top);
            s.WindowSize = new Size((int)this.Width, (int)this.Height);
            s.ToolBoxWidth = Convert.ToInt32(this.toolBox.Width);
            s.GraphHeight = (int)this.TransactionGraph.Height;
            s.DisplayClosedAccounts = this.accountsControl.DisplayClosedAccounts;
            s.RecentFiles = this.recentFilesMenu.ToArray();

            if (this.database != null)
            {
                s.Database = this.database.DatabasePath;
                s.UserId = this.database.UserId;
                s.Server = this.database.Server;
                s.BackupPath = this.database.BackupPath;
            }

            // save view state
            foreach (KeyValuePair<Type, IView> pair in this.cacheViews)
            {
                IView view = pair.Value;
                s.SetViewState(view.GetType(), view.ViewState);
            }

            s.GraphState = this.GetGraphState();
            ProcessHelper.CreateSettingsDirectory();
            if (!string.IsNullOrEmpty(s.ConfigFile) && s.Persist)
            {
                try
                {
                    s.Save();
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show("Error saving updated settings: " + ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        public GraphState GetGraphState()
        {
            var tgraph = this.TransactionGraph;
            return tgraph.GetGraphState();
        }

        public void SetGraphState(GraphState state)
        {
            var tgraph = this.TransactionGraph;
            tgraph.SetGraphState(state);
        }

        private void SaveViewStateOfCurrentView()
        {
            IView view = this.CurrentView;
            if (view != null)
            {
                var state = view.ViewState;
                if (state != null)
                {
                    this.navigator.Push(new ViewCommand(this, view, state));
                }
            }
        }

        private class ViewCommand : Command
        {
            private readonly MainWindow window;
            private readonly ViewState state;
            private readonly IView view;

            public ViewState State { get { return this.state; } }
            public IView View { get { return this.view; } }

            public ViewCommand(MainWindow window, IView view, ViewState state)
            {
                this.window = window;
                this.view = view;
                this.state = state;
            }

            public override void Done()
            {
            }

            public override void Undo()
            {
                this.window.CurrentView = this.view;
                this.view.ViewState = this.state;
            }

            public override void Redo()
            {
                this.window.CurrentView = this.view;
                this.view.ViewState = this.state;
            }
        }


        #endregion

        #region Importing

        // the problem is file change events can generate many of these in a burst, so we need a timer to delay actual loading.
        private void OnImportFolderContentHasChanged(object sender, FileSystemEventArgs e)
        {
            this.delayedActions.StartDelayedAction("ImportFiles", this.LoadImportFiles, TimeSpan.FromMilliseconds(250));
        }

        // Could be on a background thread.
        private void LoadImportFiles()
        {
            try
            {
                var filename = ImportFileListPath;
                if (File.Exists(filename))
                {
                    XDocument doc = XDocument.Load(filename);
                    File.Delete(filename);
                    List<string> qifFiles = new List<string>();
                    List<string> ofxFiles = new List<string>();
                    foreach (var e in doc.Root.Elements())
                    {
                        var path = (string)e.Attribute("Path");
                        if (File.Exists(path))
                        {
                            if (ProcessHelper.IsFileQIF(path))
                            {
                                if (!qifFiles.Contains(path))
                                {
                                    qifFiles.Add(path);
                                }
                            }
                            else if (ProcessHelper.IsFileOFX(path))
                            {
                                if (!ofxFiles.Contains(path))
                                {
                                    ofxFiles.Add(path);
                                }
                            }
                        }
                    }
                    if (ofxFiles.Count > 0)
                    {
                        this.delayedActions.StartDelayedAction("ImportOfx", () => { this.ImportOfx(ofxFiles.ToArray()); }, TimeSpan.FromMilliseconds(1));
                    }

                    if (qifFiles.Count > 0)
                    {
                        this.delayedActions.StartDelayedAction("ImportQif", () => { this.ImportQif(qifFiles.ToArray()); }, TimeSpan.FromMilliseconds(1));
                    }
                }
            }
            catch (Exception ex)
            {

                MessageBoxEx.Show(ex.Message, "Error while attempting to import", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int ImportQif(string[] files)
        {
            int total = 0;
            Cursor saved = this.Cursor;
            this.Cursor = Cursors.Wait;
            try
            {
                Account acct = null;
                int len = files.Length;
                this.ShowProgress(0, len, 0, null);
                for (int i = 0; i < len; i++)
                {
                    string file = files[i];
                    var prompt = string.Format("Please select the account that you want to import {0} into or click the Add New Account button at the bottom of this window:",
                        Path.GetFileName(file));
                    Account selected = AccountHelper.PickAccount(this.myMoney, null, prompt);
                    if (selected == null)
                    {
                        // user is cancelling!
                        break;
                    }
                    acct = selected;
                    this.ShowProgress(0, len, i, string.Format("Importing '{0}'", file));

                    try
                    {
                        int count;
                        QifImporter importer = new QifImporter(this.myMoney);
                        acct = importer.Import(selected, file, out count);
                        total += count;
                    }
                    catch (Exception ex)
                    {
                        MessageBoxEx.Show(ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }
                }

                this.ShowProgress(0, len, -1, string.Format("Loaded {0} transactions", total));

                var view = this.SetCurrentView<TransactionsView>();
                if (view.CheckTransfers() && acct != null)
                {
                    view.ViewTransactionsForSingleAccount(acct, TransactionSelection.Current, 0);
                }
            }
            finally
            {
                this.Cursor = saved;
            }
            return total;
        }

        #endregion

        #region Caption

        private void UpdateCaption(string caption)
        {
            this.caption = caption;
            string database = this.database != null ? Path.GetFileName(this.database.DatabasePath) : string.Empty;
            if (caption != null)
            {
                if (database.Length > 0)
                {
                    database += " - ";
                }

                this.Title = database + caption;

                if (this.dirty)
                {
                    this.Title += "*";
                }
            }
            else
            {
                this.Title = database;
            }
        }

        #endregion

        #region Data

        private bool isLoading;

        private void BeginLoadDatabase()
        {
            this.Cursor = Cursors.Wait;
            try
            {
                this.ShowMessage("Loading data base...");

                try
                {
                    string path = this.settings.Database;
                    if (!string.IsNullOrEmpty(path) && !File.Exists(path))
                    {
                        MessageBoxEx.Show("Previous database no longer exists in " + path, "Database Moved", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    string name = ("" + path).Trim().ToLowerInvariant();
                    string password = null;
                    bool error = false;
                    try
                    {
                        password = DatabaseSecurity.LoadDatabasePassword(name);
                    }
                    catch
                    {
                        error = true;
                    }
                    if (error)
                    {
                        // hmmm, no password saved, so we need to prompt for one.
                        PasswordWindow pw = new PasswordWindow();
                        pw.UserName = Environment.GetEnvironmentVariable("USERNAME");
                        pw.Owner = Application.Current.MainWindow;
                        pw.Optional = true;
                        if (pw.ShowDialog() == true)
                        {
                            password = pw.PasswordConfirmation;
                        }
                        else
                        {
                            // don't open it then...
                            this.settings.Database = null;
                        }
                    }

                    if (!string.IsNullOrEmpty(this.settings.Database))
                    {
                        this.isLoading = true;
                        Task.Run(() => this.LoadDatabase(password));
                    }
                    else
                    {
                        this.NewDatabase();
                        this.StartTracking();
                        this.LoadImportFiles();
                    }
                }
                catch (Exception e)
                {
                    MessageBoxEx.Show(e.Message, "Error Loading Database", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        // Background thread.
        private void LoadDatabase(object state)
        {
            string server = this.settings.Server;
            string database = this.settings.Database;
            string backuppath = this.settings.BackupPath;
            string userid = this.settings.UserId;
            string password = (string)state;

            if (string.IsNullOrWhiteSpace(database) == false)
            {
                UiDispatcher.BeginInvoke(new System.Action(() => { this.Cursor = Cursors.Wait; }));

                this.LoadDatabase(server, database, userid, password, backuppath);

                UiDispatcher.BeginInvoke(new System.Action(() => { this.Cursor = Cursors.Arrow; }));

            }

            this.LoadImportFiles();
        }

        private void ShowNetWorth()
        {
            decimal total = 0;
            foreach (Account a in this.myMoney.Accounts.GetAccounts(true))
            {
                total += a.BalanceNormalized;
            }
            this.ShowMessage("Net worth: " + total.ToString("C"));
        }

        private void LoadDatabase(string server, string databaseName, string userId, string password, string backupPath)
        {
            MyMoney newMoney = null;
            IDatabase database = null;
            Stopwatch watch = new Stopwatch();

#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.Model, MeasurementId.Load))
            {
#endif
                try
                {
                    this.ShowMessage("Loading...");
                    string ext = Path.GetExtension(databaseName).ToLowerInvariant();
                    if (ext == ".xml")
                    {
                        database = new XmlStore(databaseName, password)
                        {
                            UserId = userId,
                            Password = password,
                            BackupPath = backupPath,
                        };
                    }
                    else if (ext == ".bxml")
                    {
                        database = new BinaryXmlStore(databaseName, password)
                        {
                            UserId = userId,
                            Password = password,
                            BackupPath = backupPath,
                        };
                    }
                    else if (databaseName.EndsWith(".sdf", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!SqlCeDatabase.IsSqlCEInstalled)
                        {
                            throw new Exception("SQL Express does not appear to be installed any more so we can't open your existing database. " +
                                "If you want to open it, then please visit http://www.microsoft.com/download/en/details.aspx?id=184 to install " +
                                "SQL Server Compact Edition 4.0 and try again");
                        }
                        else
                        {
                            database = new SqlCeDatabase()
                            {
                                DatabasePath = Path.GetFullPath(databaseName),
                                UserId = userId,
                                Password = password,
                                BackupPath = backupPath
                            };
                            database.Create();
                        }
                    }
                    else if (databaseName.EndsWith(".db", StringComparison.OrdinalIgnoreCase) || databaseName.EndsWith(".mmdb", StringComparison.OrdinalIgnoreCase))
                    {
                        database = new SqliteDatabase()
                        {
                            DatabasePath = Path.GetFullPath(databaseName),
                            UserId = userId,
                            Password = password,
                            BackupPath = backupPath
                        };
                        database.Create();
                    }
                    else
                    {
                        database = new SqlServerDatabase()
                        {
                            Server = server,
                            DatabasePath = databaseName,
                            UserId = userId,
                            Password = password,
                            BackupPath = backupPath,
                            SecurityService = new SecurityService()
                        };
                        database.Create();
                    }


                    if (database.UpgradeRequired)
                    {
                        if (MessageBoxEx.Show(
                            @"Your database needs to be upgraded to the latest format;
                        \n
                        click YES to upgrade,
                        \n
                        click NO to leave your database untouched and abort loading",
                            "Confirm upgrade",
                            "Upgrade Required",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            database.Upgrade();
                        }
                        else
                        {
                            return;
                        }
                    }

                    DatabaseSecurity.SaveDatabasePassword(database.DatabasePath, password);

                    this.loadTime = NativeMethods.TickCount;
                    watch.Start();

                    newMoney = database.Load(this);

                    watch.Stop();
                }
                catch (Exception e)
                {
                    this.ShowMessage("");
                    MessageBoxEx.Show(e.Message, "Error Loading Database", MessageBoxButton.OK, MessageBoxImage.Error);
                }
#if PerformanceBlocks
            }
#endif

            if (newMoney != null)
            {
                UiDispatcher.BeginInvoke(new Action(() =>
                {
                    this.database = database;
                    this.MenuFileAddUser.Visibility = database.SupportsUserLogin ? Visibility.Visible : Visibility.Collapsed;
                    this.CreateAttachmentDirectory();
                    this.CreateStatementsDirectory();
                    this.DataContext = newMoney; // this sets this.myMoney.
                    this.canSave = true;
                    this.isLoading = false;
                    this.UpdateDatabaseSettings(DatabaseSettings.LoadFrom(database));
                    string label = Path.GetFileName(database.DatabasePath);
                    var msg = "Loaded from " + label + " in " + (int)watch.Elapsed.TotalMilliseconds + " milliseconds";
                    if (string.IsNullOrEmpty(password))
                    {
                        this.delayedActions.StartDelayedAction("NoPassword", () =>
                        {
                            string end = msg + " (database has no password!!)";
                            this.AnimateStatus(msg, end);
                        }, TimeSpan.FromSeconds(10));
                    }

                    this.InternalShowMessage(msg);
                    this.skipMessagesUntil = DateTime.Now.AddSeconds(5);

                    this.recentFilesMenu.AddRecentFile(database.DatabasePath);
                }));
            }
        }

        private void UpdateDatabaseSettings(DatabaseSettings settings)
        {
            this.databaseSettings.PropertyChanged -= this.DatabaseSettings_PropertyChanged;
            this.databaseSettings = settings;
            this.accountsControl.DatabaseSettings = this.databaseSettings;
            this.databaseSettings.PropertyChanged += this.DatabaseSettings_PropertyChanged;
            if (this.databaseSettings.MigrateSettings(this.settings))
            {
                try
                {
                    this.databaseSettings.Save();
                    this.settings.Save();
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show("Error saving updated settings: " + ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            this.databaseSettings.RaiseAllEvents();
        }

        private void AnimateStatus(string start, string end)
        {
            if (this.animatedStatus == null)
            {
                this.animatedStatus = new AnimatedMessage((string value) => { this.StatusMessage.Content = value; });
            }
            this.animatedStatus.Start(start, end, TimeSpan.FromMilliseconds(50));
        }

        public IDatabase Database
        {
            get { return this.database; }
        }

        public static string ImportFileListPath
        {
            get
            {
                return Path.Combine(ProcessHelper.ImportFileListFolder, "imports.xml");
            }
        }

        private CreateDatabaseDialog InitializeCreateDatabaseDialog()
        {
            CreateDatabaseDialog frm = new CreateDatabaseDialog();
            return frm;
        }

        private void CreateAttachmentDirectory()
        {
            if (this.database != null)
            {
                string path = this.database.DatabasePath;
                this.settings.AttachmentDirectory = this.attachmentManager.SetupAttachmentDirectory(path);
            }
        }

        private void CreateStatementsDirectory()
        {
            if (this.database != null)
            {
                string path = this.database.DatabasePath;
                this.settings.StatementsDirectory = this.statementManager.SetupStatementsDirectory(path);
            }
        }

        private bool NewDatabase()
        {
            if (!this.SaveIfDirty())
            {
                return false;
            }

            if (this.database == null ||
                MessageBoxEx.Show("Are you sure you want to create a new money database?", "New Database",
                    MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
            {
                CreateDatabaseDialog frm = this.InitializeCreateDatabaseDialog();
                frm.Owner = this;
                frm.Mode = ConnectMode.Create;
                if (frm.ShowDialog() == false)
                {
                    return false;
                }

                this.canSave = false;
                try
                {
                    this.LoadDatabase(null, frm.Database, null, frm.Password, frm.BackupPath);
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.Message, "Create Error", MessageBoxButton.OKCancel, MessageBoxImage.Error);
                    return false;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// This method is only used during testing when we need to create a new test database on the fly.
        /// </summary>
        private void CreateNewDatabase(string databaseName)
        {
            try
            {
                if (databaseName.EndsWith(Walkabout.Data.SqlCeDatabase.OfficialSqlCeFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    databaseName = Path.GetFullPath(databaseName);
                    this.database = new Walkabout.Data.SqlCeDatabase()
                    {
                        DatabasePath = databaseName
                    };
                    this.database.Create();
                }
                else if (databaseName.EndsWith(Walkabout.Data.SqliteDatabase.OfficialSqliteFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    databaseName = Path.GetFullPath(databaseName);
                    this.database = new Walkabout.Data.SqliteDatabase()
                    {
                        DatabasePath = databaseName
                    };
                    this.database.Create();
                }
                else
                {
                    this.database = new SqlServerDatabase()
                    {
                        Server = ".\\SQLEXPRESS",
                        DatabasePath = databaseName,
                        SecurityService = new SecurityService()
                    };
                    this.database.Create();
                }

                MyMoney newMoney = this.database.Load(this);
                this.DataContext = newMoney;
                this.canSave = true;
                this.isLoading = false;
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show(ex.ToString(), "Error creating new database", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenDatabase()
        {
            if (!this.SaveIfDirty())
            {
                return;
            }

            CreateDatabaseDialog frm = this.InitializeCreateDatabaseDialog();
            frm.Owner = this;
            frm.Mode = ConnectMode.Connect;
            if (frm.ShowDialog() == true)
            {
                try
                {
                    this.LoadDatabase(null, frm.Database, null, frm.Password, frm.BackupPath);
                    this.CreateAttachmentDirectory();
                    this.CreateStatementsDirectory();
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.ToString(), "Error opening database", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        internal bool SaveIfDirty()
        {
            return this.SaveIfDirty("Do you want to save your changes?", null);
        }

        internal bool SaveIfDirty(string message, string details)
        {
            if (this.dirty)
            {
                MessageBoxResult rc = MessageBoxEx.Show(message, "Save Changes", details, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (rc == MessageBoxResult.No)
                {
                    return true;
                }
                else if (rc == MessageBoxResult.Yes)
                {
                    if (!this.Save())
                    {
                        return false;
                    }
                }
                else
                {
                    return false; // cancel.
                }
            }
            return true;
        }

        private bool Save()
        {
            this.canSave = true;
            try
            {
                this.Cursor = Cursors.Wait;
                Debug.Assert(this.database != null);

                Stopwatch watch = new Stopwatch();
                watch.Start();

                this.myMoney.Save(this.database);

                watch.Stop();
                string label = Path.GetFileName(this.database.DatabasePath);
                this.ShowMessage("Saved to " + label + " in " + (int)watch.Elapsed.TotalMilliseconds + " milliseconds");

                this.SetDirty(false);

                if (this.settings.PlaySounds)
                {
                    Sounds.PlaySound("Walkabout.Icons.Ding.wav");
                }
                return true;
            }
            catch (Exception e)
            {
                MessageBoxEx.Show(e.Message, "Error saving data", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private bool PromptForPassword(out string password)
        {
            password = null;
            PasswordWindow pswd = new PasswordWindow();
            pswd.Owner = Application.Current.MainWindow;
            pswd.Optional = true;
            if (pswd.ShowDialog() == true)
            {
                password = pswd.PasswordConfirmation;
            }
            else
            {
                if (MessageBox.Show("Are you sure you want to save the data without password protection?", "No Password", MessageBoxButton.YesNoCancel, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return false;
                }
            }
            return true;
        }

        private void SaveAsSqlCe(string filename)
        {
            string password = null;
            if (!this.PromptForPassword(out password))
            {
                return;
            }

            SqlCeDatabase database = new SqlCeDatabase()
            {
                DatabasePath = filename,
                Password = password
            };
            this.SaveNewDatabase(database, password);
        }

        private void SaveNewDatabase(SqlServerDatabase database, string password)
        {
            try
            {
                if (this.database != null)
                {
                    // just in case user is saving over the same database file.
                    this.database.Disconnect();
                }

                // force all the data to be written.
                this.myMoney.MarkAllNew();

                // ensure schema exists
                database.LazyCreateTables();

                // save the settings
                DatabaseSecurity.SaveDatabasePassword(database.DatabasePath, password);
                this.settings.BackupPath = null;

                // switch over
                this.database = database;
                this.Save();

                this.CreateAttachmentDirectory();
                this.CreateStatementsDirectory();
                this.SetDirty(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Saving", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsSqlite(string filename)
        {
            string password = null;
            if (!this.PromptForPassword(out password))
            {
                return;
            }

            SqliteDatabase database = new SqliteDatabase()
            {
                DatabasePath = filename,
                Password = password
            };

            this.SaveNewDatabase(database, password);

        }

        private void SaveAsXml(string filename)
        {
            try
            {
                XmlStore xs = new XmlStore(filename, null);
                this.settings.BackupPath = null;

                this.database = xs;
                this.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Saving", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsBinaryXml(string filename)
        {
            string password = null;
            if (!this.PromptForPassword(out password))
            {
                return;
            }

            try
            {
                BinaryXmlStore xs = new BinaryXmlStore(filename, password);
                DatabaseSecurity.SaveDatabasePassword(xs.DatabasePath, password);
                this.settings.BackupPath = null;

                this.database = xs;

                this.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Saving", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportCsv(string filename)
        {
            CsvStore csv = new CsvStore(filename, this.TransactionView.Rows);
            csv.Save(this.myMoney);
        }


        private TabItem ShowDownloadTab()
        {
            TabControl tc = this.TabForGraphs;
            TabItem item = this.TabDownload;
            item.Visibility = System.Windows.Visibility.Visible;
            tc.SelectedItem = item;
            return item;
        }

        private void OnDownloadTabClose(object sender, RoutedEventArgs e)
        {
            OfxDownloadControl dc = this.TabDownload.Content as OfxDownloadControl;
            dc.Cancel();
            this.HideDownloadTab();
        }

        private void HideDownloadTab()
        {
            this.TabDownload.Visibility = System.Windows.Visibility.Hidden;
            this.TabForGraphs.SelectedItem = this.TabTrends;
        }

        private int ImportOfx(string[] files)
        {
            TabItem item = this.ShowDownloadTab();
            OfxDownloadControl dc = item.Content as OfxDownloadControl;
            dc.BeginImport(this.myMoney, files);
            return 0;
        }

        private int ImportXml(string file)
        {
            var importer = new XmlImporter(this.myMoney);
            int total = importer.Import(file);
            Account acct = importer.LastAccount;

            var view = this.SetCurrentView<TransactionsView>();
            if (view.CheckTransfers() && acct != null)
            {
                view.ViewTransactionsForSingleAccount(acct, TransactionSelection.Current, 0);
            }
            return total;
        }


        private int ImportMoneyFile(string[] fileNames)
        {
            if (MessageBoxEx.Show("Merging money databases only works if both started with the same state and you just want to merge the deltas.  Do you want to continue?",
                "Merge Warning",
                MessageBoxButton.OKCancel, MessageBoxImage.Hand) == MessageBoxResult.OK)
            {
                MoneyFileImportDialog d = new MoneyFileImportDialog();
                d.Owner = this;
                d.Navigator = this;
                d.Import(this.myMoney, this.attachmentManager, this.statementManager, fileNames);
                d.Show();
            }
            return 0;
        }

        #endregion

        #region TRACK CHANGES

        private void OnChangedUI(object sender, ChangeEventArgs args)
        {
            bool chartsDirty = false;
            for (ChangeEventArgs e = args; e != null; e = e.Next)
            {
                if (e.Item is Transaction)
                {
                    if ((e.ChangeType != ChangeType.None && e.ChangeType != ChangeType.Changed) ||
                        (e.Name != "Status" && e.Name != "StatusString" && e.Name != "IsReconciling" && e.Name != "ReconciledDate" &&
                         e.Name != "Flags" && e.Name != "Unaccepted")) // technically going to Void and back could require chart update, but unaccepted or not makes no difference.
                    {
                        chartsDirty = true;
                    }
                }
                else if (e.Item is Payee)
                {
                    if ((e.ChangeType != ChangeType.None && e.ChangeType != ChangeType.Changed) ||
                        (e.Name != "UnacceptedTransactions" && e.Name != "Flags"))
                    {
                        chartsDirty = true;
                    }
                }
                else if (e.Item is Account)
                {
                    if ((e.ChangeType != ChangeType.None && e.ChangeType != ChangeType.Changed) ||
                        (e.Name != "Unaccepted"))
                    {
                        chartsDirty = true;
                    }
                }
                else
                {
                    // might need to update payee & category charts
                    chartsDirty = true;
                }
            }
            if (chartsDirty)
            {
                this.SetChartsDirty();
            }
        }

        private void OnDirtyChanged(object sender, EventArgs e)
        {
            if (this.isLoading) // ignore these.
            {
                return;
            }

            if (this.tracker.IsDirty)
            {
                UiDispatcher.BeginInvoke(new Action(() =>
                {
                    this.SetDirty(true);
                }));
            }
        }

        private bool dirty;

        private void SetDirty(bool dirty)
        {
            if (!dirty && this.tracker != null)
            {
                this.tracker.Clear();
            }
            this.dirty = dirty;
            this.UpdateCaption(this.caption);
            if (dirty)
            {
                this.SetChartsDirty();
            }
        }


        public class EventTracking
        {
            public string EventAction { get; set; }
            public string EventData { get; set; }
        }


        private void OnOpeningPendingChangeFlyout(object sender, object e)
        {
            this.pendingStack.Children.Clear();

            System.Windows.Input.CommandManager.InvalidateRequerySuggested();

            if (this.tracker != null)
            {
                this.pendingStack.Children.Add(this.tracker.GetSummary());
            }
        }

        private void PendingChangeClicked(object sender, object args)
        {
            this.Save();
        }

        #endregion

        #region Balancing

        private void OnAccountsPanelBalanceAccount(object sender, ChangeEventArgs e)
        {
            this.BalanceAccount((Account)e.Item);
        }


        public void BalanceAccount(Account a)
        {
            this.HideQueryPanel();
            this.HideBalancePanel(false, false);

            this.SetCurrentView<TransactionsView>();
            this.TransactionView.OnStartReconcile(a);

            this.balanceControl = new BalanceControl();
            this.balanceControl.Reconcile(this.myMoney, a, this.statementManager);
            this.balanceControl.StatementDateChanged += new EventHandler(this.OnBalanceStatementDateChanged);
            this.toolBox.Add("BALANCE", "BalanceSelector", this.balanceControl);
            this.toolBox.Selected = this.balanceControl;

            this.balanceControl.Balanced += new EventHandler<BalanceEventArgs>(this.OnButtonBalanceDone);
            this.balanceControl.Focus();
            this.OnBalanceStatementDateChanged(this, EventArgs.Empty);

        }

        private void OnBalanceStatementDateChanged(object sender, EventArgs e)
        {
            //
            // The user has changed some date in the Balance Control 
            // We will now update the transaction list view to reflect the new selected date range
            //
            this.TransactionView.SetReconcileDateRange(
                this.balanceControl.SelectedPreviousStatement,
                this.balanceControl.StatementDate,
                this.balanceControl.IsLatestStatement
                );
        }

        private void OnButtonBalanceDone(object sender, BalanceEventArgs e)
        {
            this.HideBalancePanel(e.Balanced, e.HasStatement);
        }

        private void HideBalancePanel(bool balanced, bool hasStatement)
        {
            if (this.balanceControl != null)
            {
                this.balanceControl.Balanced -= new EventHandler<BalanceEventArgs>(this.OnButtonBalanceDone);
                this.balanceControl.StatementDateChanged -= new EventHandler(this.OnBalanceStatementDateChanged);
                this.toolBox.Remove(this.balanceControl);
                this.balanceControl = null;
                this.toolBox.Selected = this.accountsControl;

                this.TransactionView.OnEndReconcile(!balanced, hasStatement);
            }
        }
        #endregion

        #region MANAGE VIEW

        private void OnBeforeViewStateChanged(object sender, EventArgs e)
        {
            this.SaveViewStateOfCurrentView();
        }

        private AfterViewStateChangedEventArgs viewStateChanging;

        private void OnAfterViewStateChanged(object sender, AfterViewStateChangedEventArgs e)
        {
            if (this.CurrentView == null)
            {
                return;
            }

            this.viewStateChanging = e;

            ITransactionView view = this.CurrentView as ITransactionView;
            if (view != null)
            {
                // Search back in this.navigator for previously saved view state information so we can jump back to the same row we were on before.
                this.RestorePreviouslySavedSelection(view, e.SelectedRowId);

                this.TransactionView.QuickFilterUX.FilterText = this.TransactionView.QuickFilter;

                if (this.TransactionView.IsReconciling && this.balanceControl != null)
                {
                    this.toolBox.Selected = this.balanceControl;
                }
                else if (view.ActiveAccount != null)
                {
                    this.accountsControl.SelectedAccount = view.ActiveAccount;
                    this.toolBox.Selected = this.accountsControl;
                }
                else if (view.ActivePayee != null)
                {
                    this.payeesControl.Selected = view.ActivePayee;
                    this.toolBox.Selected = this.payeesControl;
                }
                else if (view.ActiveCategory != null)
                {
                    this.categoriesControl.Selected = view.ActiveCategory;
                    this.toolBox.Selected = this.categoriesControl;
                }
                else if (view.ActiveRental != null)
                {
                    if (this.rentsControl != null)
                    {
                        this.rentsControl.Selected = view.ActiveRental;
                        this.toolBox.Selected = this.rentsControl;
                    }
                }
                else if (view.ActiveSecurity != null)
                {
                    this.securitiesControl.Selected = view.ActiveSecurity;
                    this.toolBox.Selected = this.securitiesControl;
                    this.StockGraph.Generator = null; // wait for stock history to load.
                }

            }
            else
            {
                LoansView otherPossibleView = this.CurrentView as LoansView;
                if (otherPossibleView != null)
                {
                    this.accountsControl.SelectedAccount = otherPossibleView.AccountSelected;
                    this.toolBox.Selected = this.accountsControl;
                }
            }

            this.viewStateChanging = null;
            this.SetChartsDirty();

            //
            // All views must prepare a nice caption that we will show in the Main window title bar
            //
            this.UpdateCaption(this.CurrentView.Caption);

        }

        private void RestorePreviouslySavedSelection(IView view, long selectedRowId)
        {
            for (int i = this.navigator.Count - 1; i >= 0; i--)
            {
                ViewCommand vc = this.navigator[i] as ViewCommand;
                if (vc != null)
                {
                    TransactionViewState ts = vc.State as TransactionViewState;
                    if (ts != null)
                    {
                        TransactionViewState vs = view.ViewState as TransactionViewState;
                        if (vs != null && vs.Account == ts.Account && vs.Category == ts.Category && vs.Payee == ts.Payee && vs.Rental == ts.Rental)
                        {
                            // If the view was already told to navigate to a specific transaction, then we need to honor that and not override it.
                            if (this.TransactionView.SelectedRowId != selectedRowId)
                            {
                                this.TransactionView.SelectedRowId = ts.SelectedRow;
                            }
                            break;
                        }
                    }
                }
            }
        }

        public void ShowTransfers(Account a)
        {
            this.SetCurrentView<TransactionsView>();
            this.TransactionView.ViewTransfers(a);
        }

        private void TrackSelectionChanges()
        {
            if (this.viewStateChanging != null && this.viewStateChanging.SelectedRowId != -1)
            {
                // then we're navigating to a specific row, so don't restore previous row.
                return;
            }

            // Find current view in the history and restore it's current selection
            for (int i = this.navigator.Count - 1; i >= 0; i--)
            {
                ViewCommand vs = this.navigator[i] as ViewCommand;
                if (vs != null)
                {
                    if (vs.View == this.TransactionView)
                    {
                        TransactionViewState state = vs.State as TransactionViewState;
                        if (state != null)
                        {
                            TransactionsView view = this.TransactionView;
                            string account = view.ActiveAccount != null ? view.ActiveAccount.Name : null;
                            string payee = view.ActivePayee != null ? view.ActivePayee.Name : null;
                            string category = view.ActiveCategory != null ? view.ActiveCategory.Name : null;
                            string security = view.ActiveSecurity != null ? view.ActiveSecurity.Name : null;
                            string rental = view.ActiveRental != null ? view.ActiveRental.Name : null;

                            if (state.Account == account &&
                                state.Payee == payee &&
                                state.Category == category &&
                                state.Security == security &&
                                state.Rental == rental)
                            {
                                // Restore previously selected row.
                                view.SelectedRowId = state.SelectedRow;
                                return;
                            }
                        }
                    }

                    if (vs.View is LoansView)
                    {
                        // Loan account was selected
                        return;
                    }
                }
            }
        }

        #endregion

        #region Manage Graph

        private bool inGraphMouseDown;

        private void OnGraphMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                this.inGraphMouseDown = true;
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Transaction t = this.TransactionGraph.SelectedItem as Transaction;
                    if (t != null)
                    {
                        IView view = this.TransactionView;
                        view.SelectedRow = t;
                    }
                }
            }
            finally
            {
                this.inGraphMouseDown = false;
            }
        }

        private void HistoryChart_SelectionChanged(object sender, EventArgs e)
        {
            HistoryChartColumn selection = this.HistoryChart.Selection;
            if (selection != null)
            {
                List<Transaction> list = new List<Transaction>(from v in selection.Values select (Transaction)v.UserData);
                this.TransactionView.QuickFilter = ""; // need to clear this as they might conflict.
                var view = this.SetCurrentView<TransactionsView>();
                if (this.TransactionView.ActiveCategory != null)
                {
                    view.ViewTransactionsForCategory(this.TransactionView.ActiveCategory, list);
                }
                else if (this.TransactionView.ActivePayee != null)
                {
                    view.ViewTransactionsForPayee(this.TransactionView.ActivePayee, list);
                }
            }
        }

        /// <summary>
        /// Return the category if it is a parent, otherwise, return it's parent. 
        /// This ensures the HistoryChart always has something interesting to show.
        /// </summary>
        private Category GetParentCategory(Category c)
        {
            if (c == null)
            {
                return null;
            }

            return c.HasSubcategories ? c : (c.ParentCategory != null ? c.ParentCategory : c);
        }

        private void UpdateCharts()
        {
            if (this.inGraphMouseDown)
            {
                return;
            }

#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.UpdateCharts))
            {
#endif
                this.myMoney.BeginUpdate(this);
                try
                {

                    if (this.CurrentView == this.TransactionView)
                    {
                        //-------------------------------------------------------------
                        // Only update the overtime graph if it is currently being displayed

                        Category filter = this.TransactionView.ActiveCategory;

                        bool pieChartSelected = this.TabExpenses.IsSelected || this.TabIncomes.IsSelected;

                        if (this.TransactionView.ActiveCategory != null ||
                            this.TransactionView.ActivePayee != null)
                        {
                            bool historyWasNotVisible = this.TabHistory.Visibility != System.Windows.Visibility.Visible;
                            this.TabHistory.Visibility = System.Windows.Visibility.Visible;
                            this.TabTrends.Visibility = System.Windows.Visibility.Visible;

                            this.UpdateHistoryChart();

                            if ((historyWasNotVisible || this.TabLoan.IsSelected || this.TabRental.IsSelected) && !pieChartSelected)
                            {
                                this.TabHistory.IsSelected = true;
                            }
                        }
                        else if (this.TransactionView.ActiveAccount != null)
                        {
                            this.TabTrends.Visibility = System.Windows.Visibility.Visible;
                            this.TabHistory.Visibility = System.Windows.Visibility.Collapsed;
                            if (this.TabLoan.IsSelected || this.TabRental.IsSelected || this.TabHistory.IsSelected)
                            {
                                this.HistoryChart.Selection = null;
                                this.TabTrends.IsSelected = true;
                            }
                        }

                        this.UpdateTransactionGraph(this.TransactionView.Rows, this.TransactionView.ActiveAccount, this.TransactionView.ActiveCategory);

                        // expense categories.
                        this.TabExpenses.Visibility = System.Windows.Visibility.Visible;
                        Category parent = this.GetParentCategory(filter);
                        IList<Transaction> rows = this.TransactionView.Rows as IList<Transaction>;

                        if (parent != filter)
                        {
                            rows = this.myMoney.Transactions.GetTransactionsByCategory(parent, this.TransactionView.GetTransactionIncludePredicate());
                        }
                        if (parent == null)
                        {
                            // sometimes we have custom rows from a report, like cash flow report that is
                            // a categorized group, but we didn't get a "filter" for it, but we can find this out here
                            // so we get a nice pie chart breakdown of the reported rows.
                            parent = this.FindCommonParent(rows);
                        }

                        this.PieChartExpenses.CategoryFilter = parent;
                        this.PieChartExpenses.Unknown = this.myMoney.Categories.Unknown;
                        this.PieChartExpenses.Transactions = rows;

                        // income categories
                        this.TabIncomes.Visibility = System.Windows.Visibility.Visible;

                        this.PieChartIncomes.CategoryFilter = parent;
                        this.PieChartIncomes.Unknown = this.myMoney.Categories.Unknown;
                        this.PieChartIncomes.Transactions = rows;

                        // view the stock history
                        if (this.TransactionView.ActiveSecurity != null)
                        {
                            this.TabStock.Visibility = System.Windows.Visibility.Visible;
                        }
                        else
                        {
                            if (this.TabStock.IsSelected)
                            {
                                this.TabStock.IsSelected = false;
                                this.TabTrends.IsSelected = true;
                            }
                            this.TabStock.Visibility = System.Windows.Visibility.Collapsed;
                        }

                        // Hide these Tabs
                        this.TabLoan.Visibility = System.Windows.Visibility.Collapsed;
                        this.TabRental.Visibility = System.Windows.Visibility.Collapsed;

                    }
                    else if (this.CurrentView is LoansView)
                    {
                        this.TabLoan.Visibility = System.Windows.Visibility.Visible;
                        this.TabLoan.IsSelected = true;

                        // Hide these TABS
                        this.TabTrends.Visibility = System.Windows.Visibility.Collapsed;
                        this.TabIncomes.Visibility = System.Windows.Visibility.Collapsed;
                        this.TabExpenses.Visibility = System.Windows.Visibility.Collapsed;
                        this.TabStock.Visibility = System.Windows.Visibility.Collapsed;
                        this.TabHistory.Visibility = System.Windows.Visibility.Collapsed;

                        LoansView loandView = this.CurrentView as LoansView;
                        this.LoanChart.LoanPayments = loandView.LoanPayments;
                    }
                    else if (this.CurrentView is RentSummaryView)
                    {
                        // Show these TABS
                        this.TabRental.Visibility = System.Windows.Visibility.Visible;
                        this.TabRental.IsSelected = true;


                        // Hide these TABS
                        this.TabTrends.Visibility = System.Windows.Visibility.Collapsed;
                        this.TabIncomes.Visibility = System.Windows.Visibility.Collapsed;
                        this.TabExpenses.Visibility = System.Windows.Visibility.Collapsed;
                        this.TabStock.Visibility = System.Windows.Visibility.Collapsed;
                        this.TabLoan.Visibility = System.Windows.Visibility.Collapsed;
                        this.TabHistory.Visibility = System.Windows.Visibility.Collapsed;

                        // Set the data for the graph
                        this.RentalChart.ProfitsAndLostEntries.Clear();

                        if (this.RentalChart.IsVisible)
                        {
                            if (this.rentsControl.Selected is RentBuilding)
                            {
                                RentBuilding selected = this.rentsControl.Selected as RentBuilding;

                                foreach (RentalBuildingSingleYear rbsy in selected.Years.Values)
                                {
                                    this.RentalChart.ProfitsAndLostEntries.Add(
                                        new RentalData()
                                        {
                                            Label = rbsy.Period,
                                            Income = Convert.ToDouble(Math.Abs(rbsy.TotalIncome)),
                                            ExpenseTaxes = Convert.ToDouble(Math.Abs(rbsy.TotalExpensesGroup.TotalTaxes)),
                                            ExpenseRepair = Convert.ToDouble(Math.Abs(rbsy.TotalExpensesGroup.TotalRepairs)),
                                            ExpenseMaintenance = Convert.ToDouble(Math.Abs(rbsy.TotalExpensesGroup.TotalMaintenance)),
                                            ExpenseManagement = Convert.ToDouble(Math.Abs(rbsy.TotalExpensesGroup.TotalManagement)),
                                            ExpenseInterest = Convert.ToDouble(Math.Abs(rbsy.TotalExpensesGroup.TotalInterest))
                                        });
                                }
                            }
                            else if (this.rentsControl.Selected is Walkabout.Data.RentalBuildingSingleYear)
                            {
                                RentalBuildingSingleYear selected = this.rentsControl.Selected as RentalBuildingSingleYear;
                            }

                            this.RentalChart.ProfitsAndLostEntries.Reverse();
                            this.RentalChart.RenderChart();
                        }

                    }
                }
                finally
                {
                    this.myMoney.EndUpdate();
                }

#if PerformanceBlocks
            }
#endif
        }

        private Category FindCommonParent(IList<Transaction> rows)
        {
            Category c = null;
            if (rows == null)
            {
                return null;
            }
            foreach (var t in rows)
            {
                Category tc = t.Category;
                if (tc == null)
                {
                    return null; // cannot have a common root in this case.
                }
                if (c == null)
                {
                    c = tc.Root;
                }
                else if (c != tc.Root)
                {
                    return null;
                }
            }
            // ah ha, then we do have a common root!
            return c;
        }

        private void UpdateHistoryChart()
        {
            if (this.TransactionView.ViewModel == null)
            {
                return;
            }
            this.HistoryChart.FiscalYearStart = this.databaseSettings.FiscalYearStart;

            // pick a color based on selected category or payee.
            Category cat = this.TransactionView.ActiveCategory;
            Payee payee = this.TransactionView.ActivePayee;
            Brush brush = null; // this will request the default color.
            if (cat != null)
            {
                if (!string.IsNullOrEmpty(cat.InheritedColor))
                {
                    Color c = ColorAndBrushGenerator.GenerateNamedColor(cat.InheritedColor);
                    brush = new SolidColorBrush(c);
                }
            }
            if (brush == null && payee != null)
            {
                Color c = ColorAndBrushGenerator.GenerateNamedColor(payee.Name);
                brush = new SolidColorBrush(c);
            }
            HistoryChartColumn selection = this.HistoryChart.Selection;
            if (selection == null)
            {
                selection = new Charts.HistoryChartColumn() { Range = HistoryRange.Year };
            }
            List<HistoryDataValue> rows = new List<Charts.HistoryDataValue>();
            foreach (var transaction in this.TransactionView.ViewModel)
            {
                if (transaction.Transfer != null)
                {
                    continue;
                }

                decimal amount;

                switch (this.TransactionView.ActiveViewName)
                {
                    case TransactionViewName.ByCategory:
                    case TransactionViewName.ByCategoryCustom:
                        amount = transaction.CurrencyNormalizedAmount(transaction.AmountMinusTax);
                        break;

                    case TransactionViewName.ByPayee:
                        amount = transaction.CurrencyNormalizedAmount(transaction.Amount);
                        break;

                    default:
                        amount = transaction.Amount;
                        break;
                }
                // Todo Trend graph is inconsistent with the below ...
                if (transaction.Investment != null)
                {
                    if (transaction.InvestmentType == InvestmentType.Add)
                    {
                        // add the value of this event to the amount of this transaction
                        amount += transaction.Investment.UnitPrice * transaction.Investment.Units;
                    }
                    else if (transaction.InvestmentType == InvestmentType.Remove)
                    {
                        // subtract the value of this event to the amount of this transaction
                        amount -= transaction.Investment.UnitPrice * transaction.Investment.Units;
                    }
                }
                rows.Add(new HistoryDataValue()
                {
                    Date = transaction.Date,
                    UserData = transaction,
                    Value = amount
                });
            }

            selection.Values = rows;
            selection.Brush = brush;
            this.HistoryChart.Selection = selection;
        }

        private bool generatingTrendGraph;

        private async void UpdateTransactionGraph(IEnumerable data, Account account, Category category)
        {
            if (account != null && (account.Type == AccountType.Retirement || account.Type == AccountType.Brokerage))
            {
                if (!this.generatingTrendGraph)
                {
                    try
                    {
                        // only allow one at a time, since cancellation/restart is not efficient.
                        this.generatingTrendGraph = true;
                        this.TransactionGraph.Generator = null;
                        var gen = new BrokerageAccountGraphGenerator(this.myMoney, this.cache, account);
                        var sp = (IServiceProvider)this;
                        // Prepare is slow, but it can be done entirely on a background thread.
                        await Task.Run(async () => await gen.Prepare((IStatusService)sp.GetService(typeof(IStatusService))));
                        if (this.TransactionView.ActiveAccount == account)
                        {
                            this.TransactionGraph.Generator = gen;
                        }
                    }
                    finally
                    {
                        this.generatingTrendGraph = false;
                    }
                }
            }
            else
            {
                this.TransactionGraph.Generator = new TransactionGraphGenerator(data, account, category, this.TransactionView.ActiveViewName);
            }
        }

        private void UpdateCategoryColors()
        {
            foreach (Category c in this.myMoney.Categories.GetRootCategories())
            {
                if (!string.IsNullOrEmpty(c.Color))
                {
                    try
                    {
                        Color color = (Color)ColorConverter.ConvertFromString(c.Color);
                        ColorAndBrushGenerator.SetNamedColor(c.GetFullName(), color);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void PieChartSelectionChanged(object sender, EventArgs e)
        {
            CategoryChart chart = (CategoryChart)sender;
            CategoryData data = chart.Selection;
            if (data != null)
            {
                TransactionsView view = this.SetCurrentView<TransactionsView>();
                view.ViewTransactionsForCategory(data.Category, data.Transactions);
            }
        }

        #endregion

        #region Manage Query


        private void ShowQueryPanel()
        {
            this.MenuQueryShowForm.IsChecked = true;
            this.SetCurrentView<TransactionsView>();
            this.CurrentView.IsQueryPanelDisplayed = true;
            this.TransactionView.QueryPanel.OnShow();
        }

        private void HideQueryPanel(bool force = false)
        {
            if (force || this.MenuQueryShowForm.IsChecked)
            {
                this.MenuQueryShowForm.IsChecked = false;
                this.CurrentView.IsQueryPanelDisplayed = false;

                // Come back to the View state that we had before entering Query View
                this.Back();
                this.Forward();
            }
        }

        private void OnQueryPanelIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.MenuQueryShowForm.IsChecked = (bool)e.NewValue;
        }

        private void OnCommandAdhocQuery(object sender, ExecutedRoutedEventArgs e)
        {
            SqlServerDatabase realDb = this.database as SqlServerDatabase;
            if (realDb != null)
            {
                FreeStyleQueryDialog dialog = new FreeStyleQueryDialog(this.myMoney, realDb);
                dialog.Show();
            }
        }


        private void OnCommandShowQuery(object sender, ExecutedRoutedEventArgs e)
        {
            this.MenuQueryShowForm.IsChecked = !this.MenuQueryShowForm.IsChecked;
            if (this.MenuQueryShowForm.IsChecked)
            {
                this.ShowQueryPanel();

            }
            else
            {
                this.HideQueryPanel(true);
            }
        }

        private void OnCommandQueryRun(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                this.ExecuteQuery();
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show(ex.ToString(), "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteQuery()
        {
            if (this.TransactionView.QueryPanel != null)
            {
                this.settings.Query = this.TransactionView.QueryPanel.GetQuery();
                bool isTransactionViewAlready = this.CurrentView is TransactionsView;
                TransactionsView view = this.SetCurrentView<TransactionsView>();
                view.ViewTransactionsForAdvancedQuery(this.settings.Query);
                if (!isTransactionViewAlready)
                {
                    // then we have one too many view state changes saved on the Back history stack.
                    this.navigator.Pop();
                }
            }
        }

        private void OnCommandQueryClear(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.TransactionView.QueryPanel != null)
            {
                this.TransactionView.QueryPanel.Clear();
            }
        }

        private void OnQueryPanelGridSplitterDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            double change = e.VerticalChange;
        }


        #endregion

        #region IServiceProvider Members


        object IServiceProvider.GetService(Type service)
        {
            if (service == typeof(MyMoney))
            {
                return this.myMoney;
            }
            else if (service == typeof(Settings))
            {
                return this.settings;
            }
            else if (service == typeof(DatabaseSettings))
            {
                return this.databaseSettings;
            }
            else if (service == typeof(AccountsControl))
            {
                return this.accountsControl;
            }
            else if (service == typeof(CategoriesControl))
            {
                return this.categoriesControl;
            }
            else if (service == typeof(PayeesControl))
            {
                return this.payeesControl;
            }
            else if (service == typeof(RentsControl))
            {
                return this.rentsControl;
            }
            else if (service == typeof(Accordion))
            {
                return this.toolBox;
            }
            else if (service == typeof(StockQuoteManager))
            {
                return this.quotes;
            }
            else if (service == typeof(StockQuoteCache))
            {
                return this.cache;
            }
            else if (service == typeof(ExchangeRates))
            {
                return this.exchangeRates;
            }
            else if (service == typeof(IStatusService))
            {
                return this;
            }
            else if (service == typeof(QueryViewControl))
            {
                return this.TransactionView.QueryPanel;
            }
            else if (service == typeof(MainWindow))
            {
                return this;
            }
            else if (service == typeof(IViewNavigator))
            {
                return this;
            }
            else if (service == typeof(IDatabase))
            {
                return this.database;
            }
            else if (service == typeof(AttachmentManager))
            {
                return this.attachmentManager;
            }
            else if (service == typeof(StatementManager))
            {
                return this.statementManager;
            }
            else if (service == typeof(OutputPane))
            {
                return this.OutputView;
            }
            else if (service == typeof(TransactionCollection))
            {
                return this.TransactionView.ViewModel;
            }
            return null;
        }

        #endregion

        #region Edit Menu        

        private void OnCommandCanUndo(object sender, CanExecuteRoutedEventArgs e)
        {
            // not implemented yet
            e.CanExecute = false;
            e.Handled = true;
        }

        private void OnCommandUndo(object sender, ExecutedRoutedEventArgs e)
        {
        }

        private void OnCommandCanRedo(object sender, CanExecuteRoutedEventArgs e)
        {
            // not implemented yet
            e.CanExecute = false;
            e.Handled = true;
        }
        private void OnCommandRedo(object sender, ExecutedRoutedEventArgs e)
        {
        }
        private void OnCommandCanCut(object sender, CanExecuteRoutedEventArgs e)
        {
            IClipboardClient c = this.GetClipboardClient(Keyboard.FocusedElement);
            if (c != null)
            {
                e.CanExecute = c.CanCut;
                e.Handled = true;
            }
        }
        private void OnCommandCut(object sender, ExecutedRoutedEventArgs e)
        {
            IClipboardClient c = this.GetClipboardClient(Keyboard.FocusedElement);
            if (c != null)
            {
                c.Cut();
            }
        }
        private void OnCommandCanCopy(object sender, CanExecuteRoutedEventArgs e)
        {
            IClipboardClient c = this.GetClipboardClient(Keyboard.FocusedElement);
            if (c != null)
            {
                e.CanExecute = c.CanCopy;
                e.Handled = true;
            }
        }

        private void OnCommandCopy(object sender, ExecutedRoutedEventArgs e)
        {
            IClipboardClient c = this.GetClipboardClient(Keyboard.FocusedElement);
            if (c != null)
            {
                try
                {
                    c.Copy();
                    e.Handled = true;
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.Message, "Copy Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void OnCommandCanPaste(object sender, CanExecuteRoutedEventArgs e)
        {
            IClipboardClient c = this.GetClipboardClient(Keyboard.FocusedElement);
            if (c != null)
            {
                e.CanExecute = c.CanPaste;
                e.Handled = true;
            }
        }
        private void OnCommandPaste(object sender, ExecutedRoutedEventArgs e)
        {
            IClipboardClient c = this.GetClipboardClient(Keyboard.FocusedElement);
            if (c != null)
            {
                try
                {
                    c.Paste();
                    e.Handled = true;
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.Message, "Paste Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void OnCommandCanDelete(object sender, CanExecuteRoutedEventArgs e)
        {
            IClipboardClient c = this.GetClipboardClient(Keyboard.FocusedElement);
            if (c != null)
            {
                e.CanExecute = c.CanDelete;
                e.Handled = true;
            }
        }
        private void OnCommandDelete(object sender, ExecutedRoutedEventArgs e)
        {
            IClipboardClient c = this.GetClipboardClient(Keyboard.FocusedElement);
            if (c != null)
            {
                try
                {
                    c.Delete();
                    e.Handled = true;
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.Message, "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        #region Reports Menu        

        private void OnFlowDocumentViewClosed(object sender, EventArgs e)
        {
            this.SetCurrentView<TransactionsView>();
        }

        private void OnCommandNetWorth(object sender, ExecutedRoutedEventArgs e)
        {
            this.SaveViewStateOfCurrentView();
            FlowDocumentView view = this.SetCurrentView<FlowDocumentView>();
            view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportNetworth");
            view.Closed -= new EventHandler(this.OnFlowDocumentViewClosed);
            view.Closed += new EventHandler(this.OnFlowDocumentViewClosed);
            HelpService.SetHelpKeyword(view, "Networth Report");
            NetWorthReport report = new NetWorthReport(view, this.myMoney, this.cache);
            report.SecurityDrillDown += this.OnReportDrillDown;
            report.CashBalanceDrillDown += this.OnReportCashDrillDown;
            _ = view.Generate(report);
        }

        private void OnCommandReportInvestment(object sender, ExecutedRoutedEventArgs e)
        {
            this.ViewInvestmentPortfolio();
        }

        private void ViewInvestmentPortfolio()
        {
            this.SaveViewStateOfCurrentView();
            FlowDocumentView view = this.SetCurrentView<FlowDocumentView>();
            view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportPortfolio");
            view.Closed -= new EventHandler(this.OnFlowDocumentViewClosed);
            view.Closed += new EventHandler(this.OnFlowDocumentViewClosed);
            HelpService.SetHelpKeyword(view, "Investment Portfolio");
            PortfolioReport report = new PortfolioReport(view, this.myMoney, null, this, DateTime.Now);
            report.DrillDown += this.OnReportDrillDown;
            _ = view.Generate(report);
        }

        private void OnReportDrillDown(object sender, SecurityGroup e)
        {
            // create new report just for this drill down in security group.
            this.SaveViewStateOfCurrentView();
            FlowDocumentView view = this.SetCurrentView<FlowDocumentView>();
            view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportPortfolio");
            view.Closed -= new EventHandler(this.OnFlowDocumentViewClosed);
            view.Closed += new EventHandler(this.OnFlowDocumentViewClosed);
            HelpService.SetHelpKeyword(view, "Investment Portfolio - " + e.Type);
            PortfolioReport report = new PortfolioReport(view, this.myMoney, this, e.Date, e);
            _ = view.Generate(report);
        }

        private void OnReportCashDrillDown(object sender, AccountGroup e)
        {
            // create new report just for this drill down to show specific account cash-only balances.
            this.SaveViewStateOfCurrentView();
            FlowDocumentView view = this.SetCurrentView<FlowDocumentView>();
            view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportPortfolio");
            view.Closed -= new EventHandler(this.OnFlowDocumentViewClosed);
            view.Closed += new EventHandler(this.OnFlowDocumentViewClosed);
            HelpService.SetHelpKeyword(view, e.Title);
            PortfolioReport report = new PortfolioReport(view, this.myMoney, this, e.Date, e);
            _ = view.Generate(report);
        }

        private void OnTaxReport(object sender, ExecutedRoutedEventArgs e)
        {
            this.SaveViewStateOfCurrentView();
            FlowDocumentView view = this.SetCurrentView<FlowDocumentView>();
            view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportTaxes");
            view.Closed -= new EventHandler(this.OnFlowDocumentViewClosed);
            view.Closed += new EventHandler(this.OnFlowDocumentViewClosed);
            HelpService.SetHelpKeyword(view, "Tax Report");
            TaxReport report = new TaxReport(view, this.myMoney, this.databaseSettings.FiscalYearStart);
            _ = view.Generate(report);
        }

        private void OnCommandW2Report(object sender, ExecutedRoutedEventArgs e)
        {
            this.SaveViewStateOfCurrentView();
            FlowDocumentView view = this.SetCurrentView<FlowDocumentView>();
            view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportW2");
            view.Closed -= new EventHandler(this.OnFlowDocumentViewClosed);
            view.Closed += new EventHandler(this.OnFlowDocumentViewClosed);
            HelpService.SetHelpKeyword(view, "W2 Report");
            W2Report report = new W2Report(view, this.myMoney, this, this.databaseSettings.FiscalYearStart);
            _ = view.Generate(report);
        }

        private void HasActiveAccount(object sender, CanExecuteRoutedEventArgs e)
        {
            TransactionsView view = this.CurrentView as TransactionsView;
            e.CanExecute = view != null && view.ActiveAccount != null;
        }

        private void OnCommandReportCashFlow(object sender, ExecutedRoutedEventArgs e)
        {
            this.SaveViewStateOfCurrentView();
            FlowDocumentView view = this.SetCurrentView<FlowDocumentView>();
            view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportCashFlow");
            view.Closed -= new EventHandler(this.OnFlowDocumentViewClosed);
            view.Closed += new EventHandler(this.OnFlowDocumentViewClosed);
            CashFlowReport report = new CashFlowReport(view, this.myMoney, this, this.databaseSettings.FiscalYearStart);
            report.Regenerate();
        }

        private void OnCommandReportUnaccepted(object sender, ExecutedRoutedEventArgs e)
        {
            this.SaveViewStateOfCurrentView();
            FlowDocumentView view = this.SetCurrentView<FlowDocumentView>();
            view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportUnaccepted");
            view.Closed -= new EventHandler(this.OnFlowDocumentViewClosed);
            view.Closed += new EventHandler(this.OnFlowDocumentViewClosed);
            var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            FlowDocumentReportWriter writer = new FlowDocumentReportWriter(view.DocumentViewer.Document, pixelsPerDip);
            UnacceptedReport report = new UnacceptedReport(this.myMoney);
            report.Generate(writer);
        }

        #endregion

        #region COMMANDS

        private void OnRemovedUnusedSecurities(object sender, RoutedEventArgs e)
        {
            this.myMoney.RemoveUnusedSecurities();
        }

        private void OnCommandFileNew(object sender, ExecutedRoutedEventArgs e)
        {
            this.NewDatabase();
        }

        private void OnCommandFileOpen(object sender, ExecutedRoutedEventArgs e)
        {
            this.OpenDatabase();
        }

        private void OnCommandFileSave(object sender, ExecutedRoutedEventArgs e)
        {
            this.TransactionView.Commit();
            this.Save();
        }

        private void OnCommandCanSave(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.canSave;
            e.Handled = true;
        }

        private void OnCommandFileSaveAs(object sender, ExecutedRoutedEventArgs e)
        {
            this.TransactionView.Commit();
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();

            List<string> fileTypes = new List<string>();
            fileTypes.Add(Properties.Resources.MoneySQLLiteFileFilter);
            if (SqlCeDatabase.IsSqlCEInstalled)
            {
                fileTypes.Add(Properties.Resources.MoneySQLCEFileFilter);
            }
            fileTypes.Add(Properties.Resources.XmlFileFilter);
            fileTypes.Add(Properties.Resources.BinaryXmlFileFilter);
            fileTypes.Add(Properties.Resources.CsvFileFilter);

            saveFileDialog1.Filter = string.Join("|", fileTypes.ToArray());
            saveFileDialog1.FilterIndex = 1;
            saveFileDialog1.RestoreDirectory = true;
            saveFileDialog1.OverwritePrompt = false;

            if (saveFileDialog1.ShowDialog(this) == true)
            {
                string fname = saveFileDialog1.FileName;
                if (File.Exists(fname))
                {
                    MessageBox.Show("Sorry that file already exists, please try a new name.", "Save As Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                string ext = Path.GetExtension(fname);
                switch (ext.ToLowerInvariant())
                {
                    case ".sdf":
                        this.SaveAsSqlCe(fname);
                        break;
                    case ".db":
                    case ".mmdb":
                        this.SaveAsSqlite(fname);
                        break;
                    case ".xml":
                        this.SaveAsXml(fname);
                        break;
                    case ".bxml":
                        this.SaveAsBinaryXml(fname);
                        break;
                    case ".csv":
                        this.ExportCsv(fname);
                        break;
                    default:
                        MessageBox.Show(string.Format("Don't know how to write file of type '{0}'", ext), "Save As Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }
            }
        }

        /// <summary>
        /// Provided filter string is not valid. Filter string should contain a description of the filter, followed by a vertical bar and the filter pattern. Must also separate multiple filter description and pattern pairs by a vertical bar. Must separate multiple extensions in a filter pattern with a semicolon. Example: "Image files (*.bmp, *.jpg)|*.bmp;*.jpg|All files (*.*)|*.*"
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCommandFileImport(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Filter = StringHelpers.CreateFileFilter(
                Properties.Resources.OfxFileFilter,
                Properties.Resources.QfxFileFilter,
                Properties.Resources.QifFileFilter,
                Properties.Resources.XmlFileFilter,
                Properties.Resources.CsvFileFilter,
                Properties.Resources.MoneyFileFilter,
                Properties.Resources.AllFileFilter);
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.RestoreDirectory = true;
            openFileDialog1.Multiselect = true;

            if (openFileDialog1.ShowDialog(this) == true)
            {
                int len = openFileDialog1.FileNames.Length;
                this.ShowProgress(0, len, 0, null);
                int count = 0;
                int totalTransactions = 0;

                Cursor saved = this.Cursor;
                this.Cursor = Cursors.Wait;
                try
                {
                    List<string> ofxFiles = new List<string>();
                    List<string> qifFiles = new List<string>();
                    List<string> csvFiles = new List<string>();
                    List<string> moneyFiles = new List<string>();

                    foreach (string file in openFileDialog1.FileNames)
                    {
                        string ext = System.IO.Path.GetExtension(file).ToLower();
                        this.ShowProgress(0, len, count, string.Format("Importing '{0}'", file));
                        try
                        {
                            switch (ext)
                            {
                                case ".qif":
                                    qifFiles.Add(file);
                                    break;
                                case ".ofx":
                                case ".qfx":
                                    ofxFiles.Add(file);
                                    break;
                                case ".xml":
                                    totalTransactions += this.ImportXml(file);
                                    break;
                                case ".csv":
                                    csvFiles.Add(file);

                                    break;
                                case ".db":
                                case ".mmdb":
                                    moneyFiles.Add(file);
                                    break;
                                default:
                                    MessageBox.Show("Unrecognized file extension " + ext + ", expecting .qif, .ofx, .csv or .xml");
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBoxEx.Show(ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        }

                        if (qifFiles.Count > 0)
                        {
                            totalTransactions += this.ImportQif(qifFiles.ToArray());
                        }
                        if (ofxFiles.Count > 0)
                        {
                            totalTransactions += this.ImportOfx(ofxFiles.ToArray());
                        }
                        if (moneyFiles.Count > 0)
                        {
                            totalTransactions += this.ImportMoneyFile(moneyFiles.ToArray());
                        }
                        if (csvFiles.Count > 0)
                        {
                            foreach (var name in csvFiles)
                            {
                                totalTransactions += this.ImportCsv(name);
                            }
                        }

                        count++;
                    }
                }
                finally
                {
                    this.Cursor = saved;
                }

                if (totalTransactions > 0)
                {
                    this.ShowProgress(0, len, -1, string.Format("Loaded {0} transactions", totalTransactions));
                }
                else
                {
                    this.ShowProgress(0, len, -1, null);
                }
            }
        }

        private int ImportCsv(string fileName)
        {
            int count = 0;
            try
            {
                {
                    Account acct = AccountHelper.PickAccount(this.myMoney, null, "Please select Account to import the CSV transactions to.");
                    if (acct != null)
                    {
                        // load existing csv map if we have one.
                        var map = this.LoadMap(acct);
                        var ti = new CsvTransactionImporter(this.myMoney, acct, map);
                        CsvImporter importer = new CsvImporter(this.myMoney, ti);
                        count = importer.Import(fileName);
                        ti.Commit();
                        map.Save();

                        this.myMoney.Rebalance(acct);

                        var view = this.SetCurrentView<TransactionsView>();
                        if (view.CheckTransfers() && acct != null)
                        {
                            view.ViewTransactionsForSingleAccount(acct, TransactionSelection.Current, 0);
                        }
                    }
                }
            }
            catch (UserCanceledException)
            {
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show(ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            return count;
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

        private void OnCommandCanOpenContainingFolder(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.database != null ? System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(this.database.DatabasePath)) : false;
        }

        private void OnCommandOpenContainingFolder(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.database != null)
            {
                string path = this.database.DatabasePath;
                string dir = System.IO.Path.GetDirectoryName(path);
                if (System.IO.Directory.Exists(dir))
                {
                    int hr = NativeMethods.ShellExecute(IntPtr.Zero, "explore", dir, "", "", NativeMethods.SW_SHOWNORMAL);
                    return;
                }
            }
        }

        private readonly string FilterDgml = "DGML files (*.dgml)|*.dgml";

        private void OnCommandFileExportAccountMap(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.Filter = this.FilterDgml;
            saveFileDialog1.FilterIndex = 1;
            saveFileDialog1.RestoreDirectory = true;

            if (saveFileDialog1.ShowDialog(this) == true)
            {
                string fname = saveFileDialog1.FileName;

                Exporters ex = new Exporters();
                ex.ExportDgmlAccountMap(this.myMoney, fname);

                NativeMethods.ShellExecute(IntPtr.Zero, "edit", fname, "", null, 1);
            }
        }

        private void OnCommandFileExtensionAssociation(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                string currentApplicationPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                Walkabout.Configuration.FileAssociation.Associate(".qif", currentApplicationPath);
                Walkabout.Configuration.FileAssociation.Associate(".qfx", currentApplicationPath);
                Walkabout.Configuration.FileAssociation.Associate(".ofx", currentApplicationPath);
                Walkabout.Configuration.FileAssociation.Associate(".mmdb", currentApplicationPath);

                MessageBoxEx.Show("File type .qif, .qfx, .ofx and .mmdb are now associated with this application");
            }
            catch (Exception exp)
            {
                MessageBoxEx.Show(exp.Message, "Error changing file associations", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCommandBackup(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileDialog fd = new SaveFileDialog();
            fd.Title = "Backup Location";
            string path = this.GetBackupPath();

            switch (this.database.DbFlavor)
            {
                case DbFlavor.SqlServer:
                    fd.Filter = Properties.Resources.SqlServerBackupFileFilter;
                    path = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".dat");
                    break;
                case DbFlavor.SqlCE:
                    fd.Filter = Properties.Resources.MoneySQLCEFileFilter;
                    path = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".sdf");
                    break;
                case DbFlavor.Sqlite:
                    fd.Filter = Properties.Resources.MoneySQLLiteFileFilter;
                    path = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".mmdb");
                    break;
                case DbFlavor.Xml:
                    fd.Filter = Properties.Resources.XmlFileFilter;
                    path = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".xml");
                    break;
                case DbFlavor.BinaryXml:
                    fd.Filter = Properties.Resources.BinaryXmlFileFilter;
                    path = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".bxml");
                    break;
                default:
                    break;
            }

            fd.FileName = path;

            if (fd.ShowDialog(this) == true)
            {
                // todo: threading, progress feedback, & confirmation of success.
                try
                {
                    this.Cursor = Cursors.Wait;
                    TempFilesManager.DeleteFile(fd.FileName); // don't let it accumulate.                    
                    this.database.Backup(fd.FileName);
                    this.ShowMessage("Backed up to " + fd.FileName);
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.Message, "Backup Error", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                finally
                {
                    this.Cursor = Cursors.Arrow;
                }
            }
        }

        private string GetBackupPath()
        {
            string path = (this.database != null) ? this.database.BackupPath : null;
            if (string.IsNullOrWhiteSpace(path))
            {
                string filename = "MyMoney";
                if (this.database.DatabasePath != null)
                {
                    filename = System.IO.Path.GetFileName(this.database.DatabasePath);
                }
                string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyMoney");
                string backupPath = System.IO.Path.Combine(folder, "Backups");
                if (!Directory.Exists(backupPath))
                {
                    Directory.CreateDirectory(backupPath);
                }
                path = Path.Combine(backupPath, filename);
            }
            return path;
        }

        private void OnCommandRestore(object sender, ExecutedRoutedEventArgs e)
        {
            CreateDatabaseDialog frm = this.InitializeCreateDatabaseDialog();
            frm.BackupPath = this.GetBackupPath();
            frm.Mode = ConnectMode.Restore;
            frm.Owner = this;
            if (frm.ShowDialog() == true)
            {
                try
                {
                    this.Cursor = Cursors.Wait;

                    if (File.Exists(frm.Database))
                    {
                        if (MessageBoxEx.Show(string.Format("Are you sure you want to replace the exising data in '{0}' with the backup data in '{1}'", frm.Database, frm.BackupPath), "Delete Existing File", MessageBoxButton.OKCancel, MessageBoxImage.Hand) != MessageBoxResult.OK)
                        {
                            return;
                        }
                    }

                    SqliteDatabase.Restore(frm.BackupPath, frm.Database, frm.Password);

                    // Now load it into memory and make sure tables are up to date in case anything changed since the backup was created.
                    this.LoadDatabase(null, frm.Database, null, frm.Password, frm.BackupPath);
                    this.CreateAttachmentDirectory();
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.Message, "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    this.Cursor = Cursors.Arrow;
                }
            }
        }

        private void OnCommandRevertChanges(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.dirty)
            {
                int changes = this.tracker.ChangeCount;
                if (MessageBoxEx.Show(string.Format("Are you sure you want to revert {0} changes", changes), "Revert Changes", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    this.DataContext = new MyMoney();
                    this.dirty = false;
                    this.BeginLoadDatabase();
                }
            }
        }

        private void OnCommandCanRevert(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.dirty;
            e.Handled = true;
        }

        private void OnCommandCanExecuteAddUser(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.database is SqlServerDatabase;
        }

        private void OnCommandFileAddUser(object sender, ExecutedRoutedEventArgs e)
        {
            SqlServerDatabase realDb = this.database as SqlServerDatabase;
            if (realDb != null)
            {
                AddLoginDialog dialog = new AddLoginDialog();
                dialog.Database = realDb;
                dialog.Owner = this;
                if (dialog.ShowDialog() == false)
                {
                    return;
                }
            }
        }

        private void OnCommandFileExit(object sender, ExecutedRoutedEventArgs e)
        {
            this.Close();
        }

        private void OnCommandViewSecurities(object sender, ExecutedRoutedEventArgs e)
        {
            this.ViewSecurities();
        }

        private SecuritiesView ViewSecurities()
        {
            this.SaveViewStateOfCurrentView();
            bool initialized = this.cacheViews.ContainsKey(typeof(SecuritiesView));
            SecuritiesView view = this.SetCurrentView<SecuritiesView>();
            if (!initialized)
            {
                view.SecurityNavigated -= this.OnSecurityNavigated;
                view.SecuritySelected -= this.OnSecuritySelected;
                view.SecurityNavigated += this.OnSecurityNavigated;
                view.SecuritySelected += this.OnSecuritySelected;
            }

            return view;
        }

        private async void OnSecuritySelected(object sender, SecuritySelectionEventArgs e)
        {
            if (e.Security != null)
            {
                this.TabTrends.Visibility = System.Windows.Visibility.Collapsed;
                this.TabHistory.Visibility = System.Windows.Visibility.Collapsed;
                this.TabExpenses.Visibility = System.Windows.Visibility.Collapsed;
                this.TabIncomes.Visibility = System.Windows.Visibility.Collapsed;
                this.TabStock.Visibility = System.Windows.Visibility.Visible;
                this.TabStock.IsSelected = true;

                var history = await this.quotes.GetCachedHistory(e.Security.Symbol);
                if (history != null)
                {
                    this.StockGraph.Generator = new SecurityGraphGenerator(history, e.Security);
                }
            }
        }

        private void OnSecurityNavigated(object sender, SecuritySelectionEventArgs e)
        {
            bool isTransactionViewAlready = this.CurrentView is TransactionViewState;
            TransactionsView view = this.SetCurrentView<TransactionsView>();
            view.ViewTransactionsForSecurity(e.Security, view.SelectedRowId);
            if (!isTransactionViewAlready)
            {
                this.navigator.Pop();
            }
        }

        private void OnCommandViewViewAliases(object sender, ExecutedRoutedEventArgs e)
        {
            this.SaveViewStateOfCurrentView();
            this.SetCurrentView<AliasesView>();
        }

        private void OnCommandViewCurrencies(object sender, ExecutedRoutedEventArgs e)
        {
            this.SaveViewStateOfCurrentView();
            this.SetCurrentView<CurrenciesView>();
        }


        private void OnCommandViewOptions(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.database != null)
            {
                this.AppSettingsPanel.Password = this.database.Password;
            }

            this.AppSettingsPanel.SetSite(this);
            WpfHelper.Flyout(this.AppSettingsPanel);
        }

        private void OnCommandToggleTheme(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.settings.Theme == "Light")
            {
                this.settings.Theme = "Dark";
            }
            else
            {
                this.settings.Theme = "Light";
            }
        }

        private void OnAppSettingsPanelClosed(object sender, EventArgs e)
        {
            var newPassword = this.AppSettingsPanel.Password;
            if (this.database != null && this.database.Password != newPassword)
            {
                try
                {
                    if (this.database != null)
                    {
                        this.database.Password = newPassword;
                    }
                    DatabaseSecurity.SaveDatabasePassword(this.database.DatabasePath, this.database.Password);
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.Message, "Error changing password on database", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnSynchronizeOnlineAccounts(object sender, ExecutedRoutedEventArgs e)
        {
            this.TransactionView.Commit();
            List<OnlineAccount> list = new List<OnlineAccount>();
            foreach (OnlineAccount oa in this.myMoney.OnlineAccounts.Items)
            {
                if (oa.Ofx != null && oa.Ofx.Length > 0 &&
                    oa.UserId != null && oa.UserId.Length > 0 &&
                    oa.Password != null && oa.Password.Length > 0)
                {
                    list.Add(oa);
                }
            }
            if (list.Count == 0)
            {
                TabItem item = this.ShowDownloadTab();
                OfxDownloadControl dc = item.Content as OfxDownloadControl;

                OfxDownloadData f = new OfxDownloadData(null, "Error", "");
                f.Message = @"You have not configured any online account to synchronize with. See Online -> Download Accounts...";
                f.IsError = true;
                f.IsDownloading = false;
                var data = new ThreadSafeObservableCollection<OfxDownloadData>();
                data.Add(f);
                dc.OfxEventTree.ItemsSource = data;
                return;
            }
            this.DoSync(list);
        }

        private void CanSynchronizeOnlineAccounts(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !this.isSynchronizing;
            e.Handled = true;
        }

        private void OnCommandUpdateSecurities(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.quotes != null)
            {
                this.quotes.UpdateQuotes();
                this.settings.LastStockRequest = DateTime.Today;
            }
        }

        private void CanUpdateSecurities(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.quotes != null && !this.quotes.Busy;
        }

        private void OnCommandShowLastUpdate(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.database != null)
            {
                FreeStyleQueryDialog dialog = new FreeStyleQueryDialog(this.myMoney, this.database);
                dialog.Query = this.database.GetLog();
                dialog.Show();
            }
        }


        private void OnCommandViewHelp(object sender, ExecutedRoutedEventArgs e)
        {
            InternetExplorer.OpenUrl(IntPtr.Zero, "https://github.com/clovett/MyMoney.Net/wiki");
        }

        private void OnCommandAddSampleData(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.myMoney.Transactions.Count > 0)
            {
                if (MessageBoxEx.Show("You already have some data, are you sure you want to add lots of additional sample data?", "Add Sample Data", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            SampleDatabase sample = new SampleDatabase(this.myMoney, this.quotes, this.GetStockQuotePath());
            sample.Create();

            this.toolBox.Selected = this.accountsControl;
            Account a = this.myMoney.Accounts.GetFirstAccount();
            this.accountsControl.OnAccountsChanged(this, new ChangeEventArgs(a, null, ChangeType.Inserted)); // trigger early rebind so we can select it
            this.accountsControl.SelectedAccount = a;
            this.OnSelectionChangeFor_Account(this, EventArgs.Empty);
            this.UpdateCharts();
        }

        private void MenuExportSampleData_Click(object sender, RoutedEventArgs e)
        {
            string temp = Path.Combine(Path.GetTempPath(), "SampleData.xml");
            SampleDatabase sample = new SampleDatabase(this.myMoney, this.quotes, this.GetStockQuotePath());
            sample.Export(temp);
            InternetExplorer.OpenUrl(IntPtr.Zero, temp);
        }

        private void OnCommandTroubleshootCheckTransfer(object sender, ExecutedRoutedEventArgs e)
        {
            this.TransactionView.CheckTransfers();
        }


        private void MenuFixSplits_Click(object sender, RoutedEventArgs e)
        {
            List<Transaction> list = new List<Transaction>();

            foreach (Transaction t in this.myMoney.Transactions.GetAllTransactions())
            {
                // Fix up for previous bug where we didn't delete splits properly.
                if (t.Category != this.myMoney.Categories.Split && t.IsSplit)
                {
                    if (t.Category != null)
                    {
                        string separator = (!string.IsNullOrEmpty(t.Memo)) ? ", " : string.Empty;
                        t.Memo += separator + " Had category " + t.Category.Name;
                    }
                    t.Category = this.myMoney.Categories.Split;
                    list.Add(t);
                }
            }

            if (list.Count > 0)
            {
                MessageBoxEx.Show("Fixed " + list.Count + " transactions that still have splits", "Check Splits", MessageBoxButton.OK, MessageBoxImage.Error);
                bool isTransactionViewAlready = this.CurrentView is TransactionViewState;
                TransactionsView view = this.SetCurrentView<TransactionsView>();
                view.ViewTransactions(list);
                if (!isTransactionViewAlready)
                {
                    this.navigator.Pop();
                }
            }
            else
            {
                MessageBoxEx.Show("Your splits are all good", "Check Splits", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }


        private void MenuRemoveDuplicateSecurities_Click(object sender, RoutedEventArgs e)
        {
            int removed = this.myMoney.RemoveDuplicateSecurities();
            MessageBoxEx.Show("Removed " + removed + " duplicate securities", "Removed Duplicate Securities", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuRemoveDuplicatePayees_Click(object sender, RoutedEventArgs e)
        {
            int removed = this.myMoney.RemoveDuplicatePayees();
            MessageBoxEx.Show("Removed " + removed + " duplicate payees", "Removed Duplicate Payees", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private void MenuRecomputeBudgetBalance_Click(object sender, RoutedEventArgs e)
        {
            this.myMoney.Categories.ComputeCategoryBalance();
        }


        #endregion

        #region ONLINE

        private void OnAccountsPanelShowTransfers(object sender, ChangeEventArgs e)
        {
            this.ShowTransfers((Account)e.Item);
        }

        private void OnAccountsPanelSyncAccount(object sender, ChangeEventArgs e)
        {
            this.SyncAccount((Account)e.Item);
        }

        public void SyncAccount(Account a)
        {
            if (a.OnlineAccount == null)
            {
                MessageBoxEx.Show("This account has no online account information associated with it.  You can associate online account information using the Account Properties dialog.", "Synchronization Error", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            List<OnlineAccount> accounts = new List<OnlineAccount>();
            accounts.Add(a.OnlineAccount);
            this.DoSync(accounts);
        }

        private bool isSynchronizing;

        private void DoSync(List<OnlineAccount> accounts)
        {
            this.isSynchronizing = true;
            try
            {
                this.accountsControl.MenuSync.IsEnabled = false;

                TabItem item = this.ShowDownloadTab();
                OfxDownloadControl dc = item.Content as OfxDownloadControl;
                dc.BeginDownload(this.myMoney, accounts);
            }
            finally
            {
                this.isSynchronizing = false;
            }
            this.accountsControl.MenuSync.IsEnabled = true;
        }

        private void OnCommandDownloadAccounts(object sender, ExecutedRoutedEventArgs e)
        {
            Account temp = new Account();
            temp.Type = AccountType.Checking;
            OnlineAccountDialog od = new OnlineAccountDialog(this.myMoney, temp, this);
            od.Owner = this;
            od.ShowDialog();
        }

        #endregion

        #region IStatusService Members

        public void ShowMessage(string text)
        {
            if (NativeMethods.TickCount < this.loadTime + 3000)
            {
                return;
            }
            this.InternalShowMessage(text);
        }

        DateTime skipMessagesUntil = DateTime.MinValue;

        public void InternalShowMessage(string text)
        {
            if (DateTime.Now < skipMessagesUntil)
            {
                return;
            }
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                this.ShowMessageUIThread(text);
            }));
        }

        private void ShowMessageUIThread(string text)
        {
            this.StatusMessage.Content = text;
        }

        public void ShowProgress(int min, int max, int value)
        {
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                this.ShowProgress(min, max, value, null);
            }));
        }

        public void ShowProgress(string message, int min, int max, int value)
        {
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                this.ShowProgress(min, max, value, message);
            }));
        }

        private void ShowProgress(int min, int max, int value, string msg)
        {
            this.ProgressBar.Minimum = min;
            this.ProgressBar.Maximum = max;
            if (value > 0)
            {
                this.ProgressBar.Visibility = System.Windows.Visibility.Visible;
                this.ProgressBar.Value = value;
            }
            else
            {
                this.ProgressBar.Visibility = System.Windows.Visibility.Hidden;
            }
            if (msg != null)
            {
                this.ProgressPrompt.Text = msg;
            }
            else
            {
                this.ProgressPrompt.Text = string.Empty;
            }
        }

        public void ClearStatus()
        {
            this.StatusMessage.Content = string.Empty;
        }

        private ChangeListRequest changeList;

        /// <summary>
        ///  Check if version has changed, and if so show version update info.
        /// </summary>
        private void CheckLastVersion()
        {
            this.changeList = new ChangeListRequest(this.settings);
            this.changeList.Completed += new EventHandler<SetupRequestEventArgs>(this.OnChangeListRequestCompleted);
            this.changeList.BeginGetChangeList(DownloadSite);
        }

        private void OnChangeListRequestCompleted(object sender, SetupRequestEventArgs e)
        {
            XDocument changes = e.Changes;
            if (changes != null && e.NewVersionAvailable)
            {
                this.ButtonShowUpdateInfoCaption.Text = "View Updates";
                this.ButtonShowUpdateInfo.Visibility = System.Windows.Visibility.Visible;
            }
            if (changes != null)
            {
                Task.Run(() => this.SaveCachedChangeList(changes));
            }

            // and see if we just installed a new version.
            string exe = ProcessHelper.MainExecutable;
            DateTime lastWrite = File.GetLastWriteTime(exe);
            if (lastWrite > this.settings.LastExeTimestamp)
            {
                string previous = this.settings.ExeVersion;
                this.settings.ExeVersion = NativeMethods.GetFileVersion(exe);
                this.settings.LastExeTimestamp = lastWrite;
                this.ShowChangeInfo(previous, changes, false);
            }
        }

        private void OnButtonShowUpdateInfoClick(object sender, RoutedEventArgs e)
        {
            // we found a new version online, so show the details about what's in it.
            this.ShowChangeInfo(this.settings.ExeVersion, this.changeList.Changes, true);
        }

        private void ShowChangeInfo(string previousVersion, XDocument changeList, bool installButton)
        {
            if (changeList == null)
            {
                changeList = this.GetCachedChangeList();
            }
            if (changeList != null)
            {
                this.SaveViewStateOfCurrentView();
                FlowDocumentView view = this.SetCurrentView<FlowDocumentView>();
                view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportUpdates");
                view.Closed -= new EventHandler(this.OnFlowDocumentViewClosed);
                view.Closed += new EventHandler(this.OnFlowDocumentViewClosed);
                HelpService.SetHelpKeyword(view, "Updates");
                ChangeInfoFormatter report = new ChangeInfoFormatter(view, installButton, previousVersion, changeList);
                report.InstallButtonClick += this.OnInstallButtonClick;
                _ = view.Generate(report);
            }
        }

        private void OnInstallButtonClick(object sender, EventArgs e)
        {
            if (!this.SaveIfDirty("Save your changes before installing new version?", null))
            {
                return;
            }

            InternetExplorer.OpenUrl(IntPtr.Zero, new Uri(InstallUrl));
            this.Close();
        }

        private string ChangeListCachePath
        {
            get { return Path.Combine(System.IO.Path.GetTempPath(), "MyMoney", "changes.xml"); }
        }

        private void SaveCachedChangeList(XDocument doc)
        {
            var fileName = this.ChangeListCachePath;
            var dir = Path.GetDirectoryName(fileName);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            doc.Save(fileName);
        }

        private XDocument GetCachedChangeList()
        {
            try
            {
                var fileName = this.ChangeListCachePath;
                if (File.Exists(fileName))
                {
                    XDocument doc = XDocument.Load(fileName);
                    return doc;
                }
            }
            catch
            {
                //MessageBoxEx.Show("Internal error parsing Walkabout.Setup.changes.xml");
            }

            return null;
        }

        private void OnCommandViewChanges(object sender, ExecutedRoutedEventArgs e)
        {
            this.ShowChangeInfo(this.settings.ExeVersion, null, false);
        }


        private void OnFind(object sender, ExecutedRoutedEventArgs e)
        {
            this.CurrentView.FocusQuickFilter();
        }

        #endregion

        #region Window Events

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // BUG BUG The user control is not getting resize automatically so we need to help it out
            this.toolBox.Width = e.NewSize.Width;
        }


        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            if (!this.SaveIfDirty())
            {
                e.Cancel = true;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            this.CleanupStockQuoteManager();

            if (this.exchangeRates != null)
            {
                this.exchangeRates.Dispose();
            }

            this.StopTracking();

            this.delayedActions.CancelAll();

            using (this.attachmentManager)
            {
            }
            try
            {
                if (this.HasDatabase)
                {
                    this.SaveConfig();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("SaveConfig failed: " + ex.Message);
            }

            TempFilesManager.Shutdown();
        }

        private void CleanupStockQuoteManager()
        {
            using (this.quotes)
            {
                if (this.quotes != null)
                {
                    this.quotes.DownloadComplete -= new EventHandler<EventArgs>(this.OnStockDownloadComplete);
                    this.quotes.HistoryAvailable -= this.OnStockQuoteHistoryAvailable;
                    this.quotes = null;
                }
            }
        }

        private void OnCommandHelpAbout(object sender, ExecutedRoutedEventArgs e)
        {
            string version;
            GC.Collect();
            if (ApplicationDeployment.IsNetworkDeployed)
            {
                version = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
            }
            else
            {
                version = this.GetType().Assembly.GetName().Version.ToString();
            }
            var msg = string.Format("MyMoney, Version {0}\r\n\r\nData provided by https://iexcloud.io/ and https://www.alphavantage.co/.", version);
            MessageBoxEx.Show(msg, "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnStockQuoteServiceOptions(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.quotes != null)
            {
                StockQuoteServiceDialog d = new StockQuoteServiceDialog();
                d.Owner = this;
                d.StockQuoteManager = this.quotes;
                if (d.ShowDialog() == true)
                {
                    // service may have changed, so update our persistent settings and 
                    // update the stock quote service to use it.
                    var settings = d.Settings;
                    this.settings.StockServiceSettings = settings;
                    this.quotes.Settings = settings;
                    this.quotes.UpdateQuotes();
                }
            }
        }


        #endregion

    }
}
