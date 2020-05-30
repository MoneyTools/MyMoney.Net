using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.DataVisualization.Charting;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
using Walkabout.Interfaces.Reports;
using Walkabout.Interfaces.Views;
using System.Deployment.Application;
using System.Threading.Tasks;
using System.Security.Policy;

#if PerformanceBlocks
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
using System.Globalization;
#endif

namespace Walkabout
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IServiceProvider, IStatusService, IViewNavigator
    {
        #region PROPERTIES PRIVATE

        internal static Uri DownloadSite = new Uri("http://www.lovettsoftware.com/Downloads/MyMoney/");
        internal static string InstallUrl = "https://github.com/clovett/myMoney.Net";

        private Settings settings;
        private UndoManager navigator;
        private UndoManager manager;
        private AttachmentManager attachmentManager;
        internal MyMoney myMoney = new MyMoney();
        private ChangeTracker tracker;
        private DispatcherTimer timer;
        private bool chartsDirty;
        private Dictionary<string, MenuItem> menuToThemeMapping = new Dictionary<string, MenuItem>();

        //---------------------------------------------------------------------
        // The Toolbox controls
        private AccountsControl accountsControl;
        private CategoriesControl categoriesControl;
        private PayeesControl payeesControl;
        private SecuritiesControl securitiesControl;
        private RentsControl rentsControl;


        private string caption;
        private BalanceControl balanceControl;

        private ExchangeRates exchangeRates;
        private StockQuoteManager quotes;
        private int mainThreadId;
        private int loadTime = Environment.TickCount;
        private RecentFilesMenu recentFilesMenu;
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

                this.attachmentManager = new AttachmentManager(this.myMoney);
                this.attachmentManager.AttachmentDirectory = settings.AttachmentDirectory;

                var stockService = settings.StockServiceSettings;
                if (stockService == null)
                {
                    settings.StockServiceSettings = new List<StockServiceSettings>();
                    settings.StockServiceSettings.Add(IEXTrading.GetDefaultSettings());
                }

                Walkabout.Utilities.UiDispatcher.CurrentDispatcher = this.Dispatcher;
                this.mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

                ParseCommandLine();

                this.navigator = new UndoManager(1000); // view state stack
                this.manager = new UndoManager(1000);


                System.Windows.Forms.Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.CatchException);
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(OnUnhandledException);
                App.Current.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler(OnDispatcherUnhandledException);
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

                // Make sure the menu check boxes are initialized correctly.
                SetTheme(settings.Theme);

                InitializeComponent();

                //-----------------------------------------------------------------
                // ACCOUNTS CONTROL
                this.accountsControl = new AccountsControl();
                this.accountsControl.Site = (IServiceProvider)this;
                this.accountsControl.TabIndex = 1;
                this.accountsControl.Name = "AccountsControl";
                this.accountsControl.MyMoney = this.myMoney;
                this.accountsControl.SyncAccount += new EventHandler<ChangeEventArgs>(OnAccountsPanelSyncAccount);
                this.accountsControl.BalanceAccount += new EventHandler<ChangeEventArgs>(OnAccountsPanelBalanceAccount);
                this.accountsControl.ShowTransfers += new EventHandler<ChangeEventArgs>(OnAccountsPanelShowTransfers);
                this.accountsControl.DisplayClosedAccounts = this.settings.DisplayClosedAccounts;

                //-----------------------------------------------------------------
                // CATEGORIES CONTROL
                this.categoriesControl = new CategoriesControl();
                this.categoriesControl.TabIndex = 2;
                this.categoriesControl.Name = "CategoriesControl";
                this.categoriesControl.MyMoney = this.myMoney;
                this.categoriesControl.Site = (IServiceProvider)this;

                //-----------------------------------------------------------------
                // PAYEES CONTROL
                this.payeesControl = new PayeesControl();
                this.payeesControl.TabIndex = 3;
                this.payeesControl.Name = "PayeesControl";
                this.payeesControl.MyMoney = this.myMoney;
                this.payeesControl.Site = (IServiceProvider)this;

                //-----------------------------------------------------------------
                // STOCKS CONTROL
                this.securitiesControl = new SecuritiesControl();
                this.securitiesControl.TabIndex = 4;
                this.securitiesControl.Name = "SecuritiesControl";
                this.securitiesControl.MyMoney = this.myMoney;

                if (settings.RentalManagement)
                {
                    //-----------------------------------------------------------------
                    // RENTAL CONTROL
                    this.rentsControl = new RentsControl();
                    this.rentsControl.TabIndex = 5;
                    this.rentsControl.Name = "RentsControl";
                    this.rentsControl.MyMoney = this.myMoney;
                }

                //-----------------------------------------------------------------
                // Set the default view to be the Transaction view
                SetCurrentView<TransactionsView>();
                this.navigator.Pop();


                //-----------------------------------------------------------------
                // These events must be set after view.ServiceProvider is set
                //
                this.accountsControl.SelectionChanged += new EventHandler(OnSelectionChangeFor_Account);
                this.categoriesControl.SelectionChanged += new EventHandler(OnSelectionChangeFor_Categories);
                this.categoriesControl.GroupSelectionChanged += new EventHandler(OnSelectionChangeFor_CategoryGroup);
                this.categoriesControl.SelectedTransactionChanged += new EventHandler(CategoriesControl_SelectedTransactionChanged);
                this.payeesControl.SelectionChanged += new EventHandler(OnSelectionChangeFor_Payees);
                this.securitiesControl.SelectionChanged += new EventHandler(OnSelectionChangeFor_Securities);

                if (rentsControl != null)
                {
                    //-----------------------------------------------------------------
                    // Setup the Rental module
                    //
                    this.rentsControl.SelectionChanged += new EventHandler(OnSelectionChangeFor_Rents);
                }

                this.exchangeRates = new ExchangeRates();

                //-----------------------------------------------------------------
                // Setup the "file import" module
                //
                FileSystemWatcher fsw = new FileSystemWatcher();
                fsw.Path = ProcessHelper.GetAndUnsureLocalUserAppDataPath;
                fsw.NotifyFilter = NotifyFilters.LastWrite;
                fsw.Changed += new FileSystemEventHandler(OnImportFolderContentHasChanged);
                fsw.EnableRaisingEvents = true;

                //-----------------------------------------------------------------
                // Setup the ToolBox (aka Accordion)
                //
                this.toolBox.Add("ACCOUNTS", "AccountsSelector", this.accountsControl);
                this.toolBox.Add("CATEGORIES", "CategoriesSelector", this.categoriesControl, true);
                this.toolBox.Add("PAYEES", "PayeesSelector", this.payeesControl, true);
                this.toolBox.Add("SECURITIES", "SecuritiesSelector", this.securitiesControl, true);

                if (rentsControl != null)
                {
                    this.toolBox.Add("RENTS", "RentsSelector", this.rentsControl);
                }

                this.toolBox.Expanded += new RoutedEventHandler(OnToolBoxItemsExpanded);
                this.toolBox.FilterUpdated += new Accordion.FilterEventHandler(OnToolBoxFilterUpdated);

                Keyboard.AddGotKeyboardFocusHandler(this, new KeyboardFocusChangedEventHandler(OnKeyboardFocusChanged));

                //---------------------------------------------------------------
                // Setup the Graph area
                //
                PieChartExpenses.SelectionChanged += new EventHandler(PieChartSelectionChanged);
                PieChartIncomes.SelectionChanged += new EventHandler(PieChartSelectionChanged);
                
                //-----------------------------------------------------------------
                // Setup the Loan area
                //
                //this.LoanChart.MyMoney = this.myMoney;

                // Setup the HistoryChart
                HistoryChart.SelectionChanged += new EventHandler(HistoryChart_SelectionChanged);

                //-----------------------------------------------------------------
                // Transaction Graph
                //
                this.TransactionGraph.MouseDown += new MouseButtonEventHandler(OnGraphMouseDown);

                //-----------------------------------------------------------------
                // Main context setup
                //
                this.DataContext = this.myMoney;
                this.DataContextChanged += new DependencyPropertyChangedEventHandler(OnDataContextChanged);

                TransactionView.QuickFilterChanged += new EventHandler(TransactionView_QuickFilterChanged);

                ButtonShowUpdateInfo.Visibility = System.Windows.Visibility.Collapsed;

                this.Loaded += new RoutedEventHandler(OnMainWindowLoaded);

                //----------------------------------------------------------------------
                // Download tab
                OfxDownloadControl.SelectionChanged += OfxDownloadControl_SelectionChanged;
                TabDownload.Visibility = System.Windows.Visibility.Hidden;

                //----------------------------------------------------------------------
                // Output Window
                TabOutput.Visibility = System.Windows.Visibility.Collapsed;
                AddHandler(OutputPane.ShowOutputEvent, new RoutedEventHandler(OnShowOutputWindow));
                AddHandler(OutputPane.HideOutputEvent, new RoutedEventHandler(OnHideOutputWindow));

                this.recentFilesMenu = new RecentFilesMenu(MenuRecentFiles);
                this.recentFilesMenu.SetFiles(settings.RecentFiles);
                this.recentFilesMenu.RecentFileSelected += OnRecentFileSelected;

                this.TransactionGraph.ServiceProvider = this;

#if PerformanceBlocks
            }
#endif
        }

        private void OnStockQuoteHistoryAvailable(object sender, StockQuoteHistory history)
        {
            if (TransactionView.ActiveSecurity != null && TransactionView.ActiveSecurity.Symbol == history.Symbol)
            {
                StockGraph.Generator = new SecurityGraphGenerator(history, TransactionView.ActiveSecurity);
            }
        }

        private void OnRecentFileSelected(object sender, RecentFileEventArgs e)
        {
            if (!SaveIfDirty())
                return;

            Settings.TheSettings.Database = e.FileName;
            BeginLoadDatabase();
        }

        void OfxDownloadControl_SelectionChanged(object sender, OfxDocumentControlSelectionChangedEventArgs e)
        {
            IViewNavigator navigator = (IViewNavigator)this;
            var data = e.Data;
            if (data != null && data.Added != null && data.Added.Count > 0)
            {
                navigator.ViewTransactions(data.Added);
            }
        }

        private void StartTimer()
        {
            if (timer == null)
            {
                timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, new EventHandler(OnTick), this.Dispatcher);
                timer.Start();
            }
        }

        private void StopTimer()
        {
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }
        }

        void OnShowOutputWindow(object sender, RoutedEventArgs e)
        {
            ShowOutputWindow();
        }

        void ShowOutputWindow()
        {
            TabOutput.Visibility = System.Windows.Visibility.Visible;
            TabForGraphs.SelectedItem = TabOutput;
        }

        void OnHideOutputWindow(object sender, RoutedEventArgs e)
        {
            HideOutputWindow();
        }

        void HideOutputWindow()
        {
            TabOutput.Visibility = System.Windows.Visibility.Collapsed;
            TabForGraphs.SelectedItem = TabTrends;
        }

        void ClearOutput()
        {
            OutputView.Clear();
            HideOutputWindow();
        }

        private void OnCloseOutputWindow(object sender, RoutedEventArgs e)
        {
            this.OnHideOutputWindow(sender, e);
        }

        void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.Loaded))
            {
#endif
                if (!string.IsNullOrEmpty(newDatabaseName))
                {
                    // This only happens during testing when we launch the app with the "-nd" command line option.
                    CreateNewDatabase(newDatabaseName);
                }
                else if (!emptyWindow)
                {
                    BeginLoadDatabase();
                }
#if PerformanceBlocks
            }
#endif
        }

        void TransactionView_QuickFilterChanged(object sender, EventArgs e)
        {
            SetChartsDirty();
        }

        void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Log it to error window instead of crashing the app
            if (e.IsTerminating)
            {
                string msg = null;
                if (e.ExceptionObject != null)
                {
                    msg = "The reason is:\n" + e.ExceptionObject.ToString();
                }

                SaveIfDirty("The program is terminating, do you want to save your changes?", msg);
            }
            else if (e.ExceptionObject != null)
            {
                HandleUnhandledException(e.ExceptionObject);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception != null)
            {
                HandleUnhandledException(e.Exception);
            }
            e.SetObserved();
        }

        // stop re-entrancy
        bool handlingException;

        void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (handlingException)
            {
                e.Handled = false;
            }
            else
            {
                handlingException = true;
                UiDispatcher.Invoke(new Action(() =>
                {
                    try
                    {
                        e.Handled = HandleUnhandledException(e.Exception);
                    }
                    catch (Exception)
                    {
                    }
                    handlingException = false;
                }));
            }
        }

        bool HandleUnhandledException(object exceptionObject)
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
                SaveCrashLog(message, details);
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

        void StopTracking()
        {
            if (tracker != null)
            {
                tracker.DirtyChanged -= OnDirtyChanged;
            }
            tracker = null;
            this.attachmentManager.Stop();
            if (this.myMoney != null)
            {
                this.myMoney.Changed -= new EventHandler<ChangeEventArgs>(OnChangedUI);
            }
        }

        void StartTracking()
        {
            tracker = new ChangeTracker(this.myMoney, this);
            tracker.DirtyChanged += new EventHandler(OnDirtyChanged);
            this.attachmentManager.AttachmentDirectory = settings.AttachmentDirectory;
            this.attachmentManager.Start();
            this.myMoney.Changed -= new EventHandler<ChangeEventArgs>(OnChangedUI);
            this.myMoney.Changed += new EventHandler<ChangeEventArgs>(OnChangedUI);
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            MyMoney money = (MyMoney)e.NewValue;
            if (money != this.myMoney)
            {
                this.myMoney = money;
                StopTracking();

                if (this.quotes != null)
                {
                    using (this.quotes)
                    {
                        this.quotes.DownloadComplete -= new EventHandler<EventArgs>(OnStockDownloadComplete);
                        this.quotes.HistoryAvailable -= OnStockQuoteHistoryAvailable;
                    }
                }
                if (settings.RentalManagement)
                {
                    this.rentsControl.MyMoney = this.myMoney;
                }
                UpdateCaption(null);

                myMoney.BeginUpdate(this);
                try
                {
                    if (this.database != null)
                    {
                        string path = this.database.DatabasePath;

                        this.quotes = new StockQuoteManager((IServiceProvider)this, settings.StockServiceSettings, Path.Combine(Path.GetDirectoryName(path), "StockQuotes"));
                        this.quotes.DownloadComplete += new EventHandler<EventArgs>(OnStockDownloadComplete);
                        this.quotes.HistoryAvailable += OnStockQuoteHistoryAvailable;
                        OfxRequest.OfxLogPath = Path.Combine(Path.GetDirectoryName(path), "Logs");
                    }

                    this.accountsControl.MyMoney = this.myMoney;
                    this.categoriesControl.MyMoney = this.myMoney;
                    this.payeesControl.MyMoney = this.myMoney;
                    this.securitiesControl.MyMoney = this.myMoney;

                    this.attachmentManager.Stop();
                    this.attachmentManager = new AttachmentManager(this.myMoney);
                    this.attachmentManager.AttachmentDirectory = settings.AttachmentDirectory;
                }
                finally
                {
                    myMoney.EndUpdate();
                }

                this.Cursor = Cursors.Arrow;

                SetCurrentView<TransactionsView>();
                TransactionView.Money = myMoney;
                IView view = (IView)TransactionView;

                // try again to restore the selected account/payee, whatever, since we now have loaded data to play with
                ViewState state = this.settings.GetViewState(view.GetType());
                if (state != null)
                {
                    view.ViewState = state;
                }

                if (TransactionView.ActiveAccount != null)
                {
                    this.accountsControl.SelectedAccount = TransactionView.ActiveAccount;
                }

                if (myMoney.Accounts.Count == 0)
                {
                    // make sure we clear out current view (this is necessary if you go from a loaded database
                    // to a new empty one with no accounts yet).
                    ViewTransactions(new Transaction[0]);
                }

                GraphState graphState = this.settings.GraphState;
                if (graphState != null)
                {
                    SetGraphState(graphState);
                }

                this.exchangeRates.MyMoney = money;

                ClearOutput();
                ClearOfxDownloads();

                if (this.settings.LastStockRequest != DateTime.Today && this.quotes != null)
                {
                    this.quotes.UpdateQuotes();
                    this.exchangeRates.UpdateRates();
                }

                ShowNetWorth();

                UpdateCategoryColors();

                SetChartsDirty();

                HideBalancePanel(false);

                this.Dispatcher.BeginInvoke(new Action(AfterLoadChecks), DispatcherPriority.Background);
            }
        }

        private void ClearOfxDownloads()
        {
            OfxDownloadControl.Cancel();
            HideDownloadTab();
        }

        void AfterLoadChecks()
        {
            myMoney.BeginUpdate(this);
            try
            {
                // remove dangling transactions.
                foreach (Transaction t in myMoney.Transactions)
                {
                    if (t.Account == null)
                    {
                        myMoney.Transactions.Remove(t);
                    }
                }

                CostBasisCalculator calculator = new CostBasisCalculator(myMoney, DateTime.Now);

                // This can be done on the background thread before we wire up 
                // all the event handlers (for improved performance).
                foreach (Account a in myMoney.Accounts.GetAccounts())
                {
                    if (a.Type != AccountType.Loan)
                    {
                        myMoney.Rebalance(calculator, a);
                    }
                }

                myMoney.CheckSecurities();

                myMoney.CheckCategoryFunds();
            }
            finally
            {
                myMoney.EndUpdate();
            }

            SetDirty(false);

            // one last thing (this can take over the "current view" so it comes last).
            TransactionView.CheckTransfers();

            CheckLastVersion();

            StartTracking();

            CheckCrashLog();
        }

        void OnStockDownloadComplete(object sender, EventArgs e)
        {
            this.settings.LastStockRequest = DateTime.Today;
        }

        void CategoriesControl_SelectedTransactionChanged(object sender, EventArgs e)
        {
            bool isTransactionViewAlready = this.CurrentView is TransactionViewState;
            // This happens when there is an error budgeting a transaction, so we want to display the transaction so it can be fixed.
            TransactionView.ViewTransactions(this.categoriesControl.SelectedTransactions);
            if (!isTransactionViewAlready)
            {
                this.navigator.Pop();
            }
        }

        int tickCount;

        void OnTick(object sender, EventArgs e)
        {
            if (this.chartsDirty && !this.myMoney.IsUpdating)
            {
                this.chartsDirty = false;
                UpdateCharts();
            }
            tickCount++;

            // try cleanup temp files once a minute.
            if (tickCount % (60 / timer.Interval.TotalSeconds) == 0)
            {
                TempFilesManager.Cleanup();
            }

            if (!this.chartsDirty && !TempFilesManager.HasTempFiles)
            {
                StopTimer();
            }
        }
        
        void OnToolBoxFilterUpdated(object sender, string filter)
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

        bool emptyWindow;
        string newDatabaseName;
        bool noSettings;

        void ParseCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1, n = args.Length; i < n; i++)
            {
                string arg = args[i];
                if (arg[0] == '-' || arg[0] == '/')
                {
                    switch (arg.Substring(1).ToLowerInvariant())
                    {
                        case "?":
                        case "h":
                        case "help":
                            ShowUsage();
                            break;
                        case "n":
                            emptyWindow = true;
                            break;
                        case "nd":
                            if (i + 1 < n)
                            {
                                this.newDatabaseName = args[++i];
                            }
                            break;
                        case "nosettings":
                            noSettings = true;
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

        void ShowUsage()
        {
            MessageBoxEx.Show(@"Usage MyMoney.exe [options]

/n        Open empty window with no pre loaded database
/nd <name> Open new database with given name.  If name ends with '.myMoney.sdf' a CE database is created
*.ofx     Import the given ofx files
*.qif     Import the given qif files
/h        Show this help page", "Command Line Help", MessageBoxButton.OK, MessageBoxImage.Information);
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
                OnSelectionChangeFor_Account(sender, e);
            }

            if (sender is PayeesControl)
            {
                OnSelectionChangeFor_Payees(sender, e);
            }

            if (sender is SecuritiesControl)
            {
                OnSelectionChangeFor_Securities(sender, e);
            }

            if (sender is CategoriesControl)
            {
                OnSelectionChangeFor_Categories(sender, e);
            }

            if (sender is RentsControl)
            {
                OnSelectionChangeFor_Rents(sender, e);
            }
        }


        private void OnSelectionChangeFor_Account(object sender, EventArgs e)
        {
            //-----------------------------------------------------------------
            // Look for Accounts
            //
            Account a = this.accountsControl.Selected as Account;

            if (a != null)
            {
                if (a.Type == AccountType.Loan)
                {
                    bool isLoanViewAlready = CurrentView is LoansView;
                    LoansView view = SetCurrentView<LoansView>();
                    view.AccountSelected = a;
                    if (!isLoanViewAlready)
                    {
                        this.navigator.Pop();
                    }
                    TrackSelectionChanges();
                }
                else
                {
                    bool isTransactionViewAlready = CurrentView is TransactionsView;
                    TransactionsView view = SetCurrentView<TransactionsView>();
                    if (view.ActiveAccount != a)
                    {
                        view.ViewTransactionsForSingleAccount(a, TransactionSelection.Current, 0);
                    }
                    if (!isTransactionViewAlready)
                    {
                        this.navigator.Pop();
                    }
                    TrackSelectionChanges();
                }

                return;
            }

        }

        private void OnSelectionChangeFor_Categories(object sender, EventArgs e)
        {
            Category c = this.categoriesControl.Selected;
            if (c != null)
            {
                ViewTransactionsByCategory(c);
            }
        }

        private void OnSelectionChangeFor_CategoryGroup(object sender, EventArgs e)
        {
            CategoryGroup g = this.categoriesControl.SelectedGroup;
            if (g != null)
            {
                ViewTransactionsByCategoryGroup(g);
            }
        }

        private long GetFirstTransactionInCategory(TransactionsView view, Category c, DateTime date)
        {
            foreach (Transaction t in myMoney.Transactions.GetTransactionsByCategory(c, view.GetTransactionIncludePredicate()))
            {
                if (t.Date >= date && t.IsBudgeted)
                {
                    return t.Id;
                }
            }
            return -1;
        }

        private void ViewTransactionsByCategory(Category c)
        {
            bool isTransactionViewAlready = CurrentView is TransactionsView;
            TransactionsView view = SetCurrentView<TransactionsView>();
            long selectedId = -1;
            if (view.BalancingBudget)
            {
                selectedId = GetFirstTransactionInCategory(view, c, view.BudgetDate);
            }
            view.ViewTransactionsForCategory(c, selectedId);
            if (!isTransactionViewAlready)
            {
                this.navigator.Pop();
            }
            TrackSelectionChanges();
        }

        private void ViewTransactionsByCategoryGroup(CategoryGroup g)
        {
            bool isTransactionViewAlready = CurrentView is TransactionsView;
            TransactionsView view = SetCurrentView<TransactionsView>();
            List<Transaction> total = new List<Data.Transaction>();
            foreach (Category c in g.Subcategories)
            {
                IList<Transaction> transactions = myMoney.Transactions.GetTransactionsByCategory(c,
                    new Predicate<Transaction>((t) => { return true; }));
                total.AddRange(transactions);
            }

            total.Sort(Transactions.SortByDate);
            view.ViewTransactionsForCategory(new Data.Category() { Name = g.Name }, total);
            if (!isTransactionViewAlready)
            {
                this.navigator.Pop();
            }
            TrackSelectionChanges();
        }


        private void OnSelectionChangeFor_Payees(object sender, EventArgs e)
        {
            Payee p = this.payeesControl.Selected;
            if (p != null)
            {
                bool isTransactionViewAlready = CurrentView is TransactionsView;
                TransactionsView view = SetCurrentView<TransactionsView>();
                view.ViewTransactionsForPayee(this.payeesControl.Selected, view.SelectedRowId);
                if (!isTransactionViewAlready)
                {
                    this.navigator.Pop();
                }
                TrackSelectionChanges();
            }
        }

        private void OnSelectionChangeFor_Securities(object sender, EventArgs e)
        {
            Security s = this.securitiesControl.Selected;
            if (s != null)
            {
                ViewTransactionsBySecurity(s);
            }
        }

        public void ViewTransactionsBySecurity(Security security)
        {
            if (security != null)
            {
                bool isTransactionViewAlready = CurrentView is TransactionsView;
                TransactionsView view = SetCurrentView<TransactionsView>();
                view.ViewTransactionsForSecurity(security, view.SelectedRowId);
                if (!isTransactionViewAlready)
                {
                    this.navigator.Pop();
                }
                TrackSelectionChanges();
            }
        }

        private void OnSelectionChangeFor_Rents(object sender, EventArgs e)
        {
            object currentlySelected = this.rentsControl.Selected;

            if (currentlySelected is RentBuilding)
            {
                RentSummaryView summary = SetCurrentView<RentSummaryView>();
                summary.SetViewToRentBuilding(currentlySelected as RentBuilding);
            }
            else if (currentlySelected is RentalBuildingSingleYear)
            {
                RentSummaryView summary = SetCurrentView<RentSummaryView>();
                summary.SetViewToRentalBuildingSingleYear(currentlySelected as RentalBuildingSingleYear);
            }
            else if (currentlySelected is RentalBuildingSingleYearSingleDepartment)
            {
                TransactionsView view = SetCurrentView<TransactionsView>();
                view.ViewTransactionRentalBuildingSingleYearDepartment(currentlySelected as RentalBuildingSingleYearSingleDepartment);
            }

            SetChartsDirty();

            TrackSelectionChanges();
        }

        private void SetChartsDirty()
        {
            this.chartsDirty = true;
            StartTimer();
        }


        #endregion

        #region View Management


        public IView CurrentView
        {
            get { return EditingZone.Content as IView; }
            set { this.EditingZone.Content = value; }
        }

        Dictionary<Type, IView> cacheViews = new Dictionary<Type, IView>();

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

                    iView.ServiceProvider = (IServiceProvider)this;
                    iView.BeforeViewStateChanged += new EventHandler(OnBeforeViewStateChanged);
                    iView.AfterViewStateChanged += new EventHandler<AfterViewStateChangedEventArgs>(OnAfterViewStateChanged);
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
                    cacheViews.Add(typeof(T), iView);
                }
                else
                {
                    MessageBoxEx.Show("Internal error");
                }
            }

            IView o = cacheViews[typeof(T)];

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
            IView newView = GetOrCreateView<T>();

            if (newView != CurrentView)
            {
                //
                // Since the type of view is about to change
                // we need to notify the current type of view 
                // because the other type that is about to get activated
                // will not trigger a "Save State" for the previous view
                //
                OnBeforeViewStateChanged(null, null);
            }

            CurrentView = newView;
            CurrentView.ActivateView();
            return (T)CurrentView;
        }


        /// <summary>
        /// Helper method for partying on the popular transaction view
        /// </summary>
        private TransactionsView TransactionView
        {
            get
            {
                var result = GetOrCreateView<TransactionsView>();
                result.ViewModelChanged -= OnTransactionViewModelChanged;
                result.ViewModelChanged += OnTransactionViewModelChanged;
                return result;
            }
        }

        private void OnTransactionViewModelChanged(object sender, EventArgs e)
        {
            SetChartsDirty();
        }

        #endregion

        #region Mouse & Keyboard Handling

        object lastFocus = null;

        void OnKeyboardFocusChanged(object sender, KeyboardFocusChangedEventArgs e)
        {
            IInputElement f = e.NewFocus;
            if (f is TextBox || Keyboard.FocusedElement is TextBox)
            {
                // text edit mode, so disable row level copy/paste command.
                lastFocus = f;
            }
            else if (f == this.TransactionView.QueryPanel)
            {
                lastFocus = this.TransactionView.QueryPanel;
            }
            else if (f == this.TransactionGraph)
            {
                lastFocus = this.TransactionGraph;
            }
            else if (f is Inline)
            {
                // skip inlines, they cause VisualTreeHelper.GetParent to blow up!!!???
            }
            else
            {
                DependencyObject d = e.NewFocus as DependencyObject;
                while (d != null)
                {
                    if (d == TransactionView)
                    {
                        lastFocus = TransactionView;
                    }
                    else if (d is FlowDocumentView)
                    {
                        lastFocus = d;
                    }
                    d = VisualTreeHelper.GetParent(d);
                }
            }
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

                SaveViewStateOfCurrentView();   // save current state so we can come back here if user press FORWARD button
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
            Back();
        }

        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.navigator.CanRedo;
            e.Handled = true;
        }

        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Forward();
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
                            SetCurrentView<TransactionsView>();

                        }
                    }));
        }

        public void ViewTransactions(IEnumerable<Transaction> list)
        {
            UiDispatcher.BeginInvoke(
                new Action(() =>
                {
                    TransactionsView view = SetCurrentView<TransactionsView>();
                    view.ViewTransactions(list);

                    // now we only want one view.
                    this.navigator.Pop();

                    PendingChangeDropDown.IsChecked = false;
                }));
        }

        public void NavigateToSecurity(Security security)
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    SecuritiesView view = ViewSecurities();
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

            LoadConfig();

            // Menu association to themes 
            this.menuToThemeMapping.Add("Themes/Theme-VS2010.xaml", MenuViewThemeVS2010);
            this.menuToThemeMapping.Add("Themes/Theme-Flat.xaml", MenuViewThemeFlat);

            this.TransactionView.QueryPanel.IsVisibleChanged += new DependencyPropertyChangedEventHandler(OnQueryPanelIsVisibleChanged);

            TempFilesManager.Initialize();
        }

        public bool HasDatabase { get { return this.database != null || isLoading; } }

        void SetTheme(string themeToApply)
        {
            if (!string.IsNullOrEmpty(themeToApply))
            {

                if (settings.Theme != themeToApply)
                {
                    ProcessHelper.SetTheme(2, themeToApply);
                    settings.Theme = themeToApply;
                }

                // Turn on the correct check mark of the menu View/Theme 
                foreach (KeyValuePair<string, MenuItem> mt in menuToThemeMapping)
                {
                    if (mt.Key == themeToApply)
                    {
                        mt.Value.IsChecked = true;
                    }
                    else
                    {
                        mt.Value.IsChecked = false;
                    }
                }
            }
        }

        void LoadConfig()
        {
            if (!File.Exists(settings.ConfigFile) || noSettings)
            {
                Rect bounds = System.Windows.SystemParameters.WorkArea;
                if (bounds.Width != 0 && bounds.Height != 0)
                {
                    this.Top = bounds.Top;
                    this.Left = bounds.Left;
                    this.Width = bounds.Width;
                    this.Height = bounds.Height;
                }
                this.TransactionView.ViewAllSplits = false;
                this.TransactionView.OneLineView = false;
                this.settings.Theme = "Themes\\Theme-VS2010.xaml"; // Default to this theme on the first ever run
            }
            else
            {
                if (settings.WindowSize.Width != 0 && settings.WindowSize.Height != 0)
                {
                    Point location = settings.WindowLocation;
                    this.Left = location.X;
                    this.Top = location.Y;

                    this.Width = settings.WindowSize.Width;
                    this.Height = settings.WindowSize.Height;
                }
                if (settings.ToolBoxWidth > 20)
                {
                    toolBox.Width = settings.ToolBoxWidth;
                    GridColumns.ColumnDefinitions[0].Width = new GridLength(settings.ToolBoxWidth);
                }
                if (settings.GraphHeight > 20)
                {
                    TransactionGraph.Height = settings.GraphHeight;
                }

                this.caption = settings.Database;
            }
        }

        void SaveConfig()
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

            s.GraphState = GetGraphState();
            ProcessHelper.CreateSettingsDirectory();
            if (s.ConfigFile != null && s.Persist)
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                settings.Encoding = Encoding.UTF8;
                using (XmlWriter w = XmlWriter.Create(s.ConfigFile, settings))
                {
                    s.WriteXml(w);
                    w.Close();
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

        void SaveViewStateOfCurrentView()
        {
            IView view = CurrentView;
            if (view != null)
            {
                var state = view.ViewState;
                if (state != null)
                {
                    this.navigator.Push(new ViewCommand(this, view, state));
                }
            }
        }


        class ViewCommand : Command
        {
            MainWindow window;
            ViewState state;
            IView view;

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
                window.CurrentView = view;
                view.ViewState = state;
            }

            public override void Redo()
            {
                window.CurrentView = view;
                view.ViewState = state;
            }
        }


        #endregion

        #region Importing

        private DispatcherTimer delay;

        // the problem is file change events can generate many of these in a burst, so we need a timer to delay actual loading.
        private void OnImportFolderContentHasChanged(object sender, FileSystemEventArgs e)
        {
            if (delay == null)
            {
                delay = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, LoadImportFiles, this.Dispatcher);
            }
            delay.Stop();
            delay.Start();
        }

        public static string SpecialImportFileNameQif = "~IMPORT~.QIF";
        public static string SpecialImportFileNameOfx = "~IMPORT~.OFX";

        private void LoadImportFiles(object sender, EventArgs e)
        {
            delay.Stop();
            delay = null;
            try
            {

                TryToImportQIF();
                TryToImportOFX();
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show("Error while attempting to import", null, ex.Message);
            }

            // Refresh the Transaction view in case the imported file has modified the data currently being displayed
            // INotifyPropertyChanged and ObservableCollection should take care of this.
            var view = SetCurrentView<TransactionsView>();
            view.ViewTransactionsForSingleAccount(view.ActiveAccount, TransactionSelection.Current, 0);
        }

        private void TryToImportOFX()
        {
            string pathToImportFileOfx = System.IO.Path.Combine(ProcessHelper.GetAndUnsureLocalUserAppDataPath, SpecialImportFileNameOfx);
            if (File.Exists(pathToImportFileOfx))
            {
                string[] filesToImports = { pathToImportFileOfx };
                ImportOfx(filesToImports);

            }
        }

        private void TryToImportQIF()
        {
            string pathToImportFileQif = System.IO.Path.Combine(ProcessHelper.GetAndUnsureLocalUserAppDataPath, SpecialImportFileNameQif);
            if (File.Exists(pathToImportFileQif))
            {
                int count;
                QifImporter importer = new QifImporter(this.myMoney);
                importer.Import(this.accountsControl.SelectedAccount, pathToImportFileQif, out count);
                if (count > 0)
                {
                    this.ShowMessage(string.Format("Imported {0} records", count));
                }
                TempFilesManager.DeleteFile(pathToImportFileQif);
            }
        }



        private int ImportQif(string[] files)
        {
            int total = 0;
            Cursor saved = this.Cursor;
            this.Cursor = Cursors.Wait;
            try
            {
                Account selected = this.accountsControl.SelectedAccount;
                Account acct = null;
                int len = files.Length;
                ShowProgress(0, len, 0, null);
                for (int i = 0; i < len; i++)
                {
                    string file = files[i];
                    ShowProgress(0, len, i, string.Format("Importing '{0}'", file));

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
                ShowProgress(0, len, -1, string.Format("Loaded {0} transactions", total));

                var view = SetCurrentView<TransactionsView>();
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

        void UpdateCaption(string caption)
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

        void BeginLoadDatabase()
        {
            this.Cursor = Cursors.Wait;
            try
            {
                this.ShowMessage("Loading data base...");

                try
                    {
                        string path = this.settings.Database;
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
                            isLoading = true;
                            ThreadPool.QueueUserWorkItem(new WaitCallback(LoadDatabase), password);
                        }
                        else
                        {
                            this.NewDatabase();
                            StartTracking();
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

                LoadDatabase(server, database, userid, password, backuppath);

                UiDispatcher.BeginInvoke(new System.Action(() => { this.Cursor = Cursors.Arrow; }));

            }
        }

        private void ShowNetWorth()
        {
            decimal total = 0;
            foreach (Account a in this.myMoney.Accounts.GetAccounts(true))
            {
                total += a.BalanceNormalized;
            }
            ShowMessage("Net worth: " + total.ToString("C"));
        }

        private bool canSave = false;
        private IDatabase database;

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

                    this.loadTime = Environment.TickCount;
                    watch.Start();

                    newMoney = database.Load(this);

                    watch.Stop();
                }
                catch (Exception e)
                {
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
                    CreateAttachmentDirectory();
                    this.DataContext = newMoney;
                    canSave = true;
                    isLoading = false;
                    string label = Path.GetFileName(database.DatabasePath);
                    var msg = "Loaded from " + label + " in " + (int)watch.Elapsed.TotalMilliseconds + " milliseconds";
                    if (string.IsNullOrEmpty(password))
                    {
                        string end = msg + " (database has no password!!)";
                        AnimateStatus(msg, end);
                    }
                    else
                    {
                        InternalShowMessage(msg);
                    }

                    this.recentFilesMenu.AddRecentFile(database.DatabasePath);
                }));
            }
        }

        private void AnimateStatus(string start, string end)
        {
            if (animatedStatus == null)
            {
                animatedStatus = new AnimatedMessage((string value) => { StatusMessage.Content = value; });
            }
            animatedStatus.Start(start, end, TimeSpan.FromMilliseconds(50));
        }

        public IDatabase Database
        {
            get { return this.database; }
        }

        private CreateDatabaseDialog InitializeCreateDatabaseDialog()
        {
            CreateDatabaseDialog frm = new CreateDatabaseDialog();
            return frm;
        }

        private void CreateAttachmentDirectory()
        {
            if (database != null)
            {
                string path = database.DatabasePath;
                string localName = Path.GetFileNameWithoutExtension(path) + ".Attachments";
                string dir = Path.GetDirectoryName(path);
                string attachmentpath = Path.Combine(dir, localName);
                if (!Directory.Exists(attachmentpath))
                {
                    try
                    {
                        Directory.CreateDirectory(attachmentpath);
                    }
                    catch (System.UnauthorizedAccessException)
                    {
                        // Access to the path 'c:\Program Files\Microsoft SQL Server\MSSQL10.SQLEXPRESS\MSSQL\DATA\Test.Attachments' is denied.
                        attachmentpath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyMoney"), localName);
                        Directory.CreateDirectory(attachmentpath);
                    }
                }
                settings.AttachmentDirectory = attachmentpath;
            }
        }

        private bool NewDatabase()
        {
            if (!SaveIfDirty())
                return false;

            if (this.database == null ||
                MessageBoxEx.Show("Are you sure you want to create a new money database?", "New Database",
                    MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
            {
                CreateDatabaseDialog frm = InitializeCreateDatabaseDialog();
                frm.Owner = this;
                frm.Mode = ConnectMode.Create;
                if (frm.ShowDialog() == false)
                {
                    return false;
                }

                this.canSave = false;
                try
                {
                    LoadDatabase(null, frm.Database, null, frm.Password, frm.BackupPath);
                    CreateAttachmentDirectory();
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

                MyMoney newMoney = database.Load(this);
                this.DataContext = newMoney;
                canSave = true;
                isLoading = false;
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show(ex.ToString(), "Error creating new database", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenDatabase()
        {
            if (!SaveIfDirty())
            {
                return;
            }

            CreateDatabaseDialog frm = InitializeCreateDatabaseDialog();
            frm.Owner = this;
            frm.Mode = ConnectMode.Connect;
            if (frm.ShowDialog() == true)
            {
                try
                {
                    this.LoadDatabase(null, frm.Database, null, frm.Password, frm.BackupPath);
                    CreateAttachmentDirectory();
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.ToString(), "Error opening database", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        internal bool SaveIfDirty()
        {
            return SaveIfDirty("Do you want to save your changes?", null);
        }

        internal bool SaveIfDirty(string message, string details)
        {
            if (dirty)
            {
                MessageBoxResult rc = MessageBoxEx.Show(message, "Save Changes", details, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (rc == MessageBoxResult.No)
                {
                    return true;
                }
                else if (rc == MessageBoxResult.Yes)
                {
                    if (!Save())
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
            canSave = true;
            try
            {
                this.Cursor = Cursors.Wait;
                Debug.Assert(this.database != null);

                Stopwatch watch = new Stopwatch();
                watch.Start();

                myMoney.Save(this.database);

                watch.Stop();
                string label = Path.GetFileName(this.database.DatabasePath);
                ShowMessage("Saved to " + label + " in " + (int)watch.Elapsed.TotalMilliseconds + " milliseconds");

                SetDirty(false);
                UpdatePendingChangePopupState();

                Sounds.PlaySound("Walkabout.Icons.Ding.wav");
                return true;
            }
            catch (Exception e)
            {
                MessageBoxEx.Show("Error saving data\n" + e.Message);
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
            if (!PromptForPassword(out password))
            {
                return;
            }

            SqlCeDatabase database = new SqlCeDatabase()
            {
                DatabasePath = filename,
                Password = password
            };
            SaveNewDatabase(database, password);
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
                myMoney.MarkAllNew();

                // ensure schema exists
                database.LazyCreateTables();

                // save the settings
                DatabaseSecurity.SaveDatabasePassword(database.DatabasePath, password);
                settings.BackupPath = null;

                // switch over
                this.database = database;
                Save();

                CreateAttachmentDirectory();
                SetDirty(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Saving", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsSqlite(string filename)
        {
            string password = null;
            if (!PromptForPassword(out password))
            {
                return;
            }

            SqliteDatabase database = new SqliteDatabase()
            {
                DatabasePath = filename,
                Password = password
            };

            SaveNewDatabase(database, password);

        }

        private void SaveAsXml(string filename)
        {
            try
            {
                XmlStore xs = new XmlStore(filename, null);
                settings.BackupPath = null;

                this.database = xs;
                Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Saving", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsBinaryXml(string filename)
        {
            string password = null;
            if (!PromptForPassword(out password))
            {
                return;
            }

            try
            {
                BinaryXmlStore xs = new BinaryXmlStore(filename, password);
                DatabaseSecurity.SaveDatabasePassword(xs.DatabasePath, password);
                settings.BackupPath = null;

                this.database = xs;

                Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Saving", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportCsv(string filename)
        {
            CsvStore csv = new CsvStore(filename, TransactionView.Rows);
            csv.Save(this.myMoney);
        }

        private TabItem ShowDownloadTab()
        {
            TabControl tc = TabForGraphs;
            TabItem item = TabDownload;
            item.Visibility = System.Windows.Visibility.Visible;
            tc.SelectedItem = item;
            return item;
        }

        private void OnDownloadTabClose(object sender, RoutedEventArgs e)
        {
            OfxDownloadControl dc = TabDownload.Content as OfxDownloadControl;
            dc.Cancel();
            HideDownloadTab();
        }

        private void HideDownloadTab()
        {
            TabDownload.Visibility = System.Windows.Visibility.Hidden;
            TabForGraphs.SelectedItem = TabTrends;
        }

        private int ImportOfx(string[] files)
        {
            TabItem item = ShowDownloadTab();
            OfxDownloadControl dc = item.Content as OfxDownloadControl;
            dc.BeginImport(this.myMoney, files);
            return 0;
        }

        private int ImportXml(string file)
        {
            int total = 0;

            Account acct = null;

            int count;
            Importer importer = new Importer(myMoney);
            acct = importer.Import(file, out count);
            total += count;

            var view = SetCurrentView<TransactionsView>();
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
                d.Import(this.myMoney, this.attachmentManager, fileNames);
                d.Show();
            }
            return 0;
        }


        #endregion

        #region TRACK CHANGES

        private void OnChangedUI(object sender, ChangeEventArgs e)
        {
            SetChartsDirty();
        }

        void OnDirtyChanged(object sender, EventArgs e)
        {
            if (isLoading) // ignore these.
                return;

            if (tracker.IsDirty)
            {
                UiDispatcher.BeginInvoke(new Action(() =>
                {
                    SetDirty(true);
                }));
            }
        }

        private bool dirty;

        private void SetDirty(bool dirty)
        {
            if (!dirty && tracker != null)
            {
                tracker.Clear();
            }
            this.dirty = dirty;
            UpdateCaption(this.caption);
            if (dirty)
            {
                SetChartsDirty();
            }
        }


        public class EventTracking
        {
            public string EventAction { get; set; }
            public string EventData { get; set; }
        }

        private void PendingChangePopupOpened(object sender, EventArgs e)
        {
            UpdatePendingChangePopupState();
        }

        private void UpdatePendingChangePopupState()
        {
            Border border = (Border)PendingChangeDropDown.Popup.Child;
            Grid grid = (Grid)border.Child;
            if (grid.Children.Count == 2)
            {
                grid.Children.RemoveAt(1);
            }
            Button button = (Button)grid.Children[0];
            button.CommandTarget = this;

            System.Windows.Input.CommandManager.InvalidateRequerySuggested();

            if (tracker != null)
            {
                grid.Children.Add(tracker.GetSummary());
            }
        }

        private void PendingChangeDropDown_MouseEnter(object sender, MouseEventArgs e)
        {
            PendingChangeDropDown.IsChecked = true;
        }

        private void PendingChangeDropDown_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!PendingChangeDropDown.Popup.IsMouseOver)
            {
                PendingChangeDropDown.IsChecked = false;
                PendingChangeDropDown.Popup.IsOpen = false;
            }
        }

        private void PendingChangeDropDown_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Save();
        }

        #endregion

        #region Balancing

        private void OnAccountsPanelBalanceAccount(object sender, ChangeEventArgs e)
        {
            this.BalanceAccount((Account)e.Item);
        }


        public void BalanceAccount(Account a)
        {
            HideQueryPanel();
            HideBalancePanel(true);

            SetCurrentView<TransactionsView>();
            TransactionView.OnStartReconcile(a);

            this.balanceControl = new BalanceControl();
            this.balanceControl.Reconcile(this.myMoney, a);
            this.balanceControl.StatementDateChanged += new EventHandler(OnBalanceStatementDateChanged);
            this.toolBox.Add("BALANCE", "BalanceSelector", this.balanceControl);
            this.toolBox.Selected = this.balanceControl;

            this.balanceControl.Balanced += new EventHandler<BalanceEventArgs>(OnButtonBalanceDone);
            this.balanceControl.Focus();
            OnBalanceStatementDateChanged(this, EventArgs.Empty);

        }

        private void OnBalanceStatementDateChanged(object sender, EventArgs e)
        {
            //
            // The user has changed some date in the Balance Control 
            // We will now update the transaction list view to reflect the new selected date range
            //
            TransactionView.SetReconcileDateRange(
                this.balanceControl.SelectedPreviousStatement,
                this.balanceControl.StatementDate,
                this.balanceControl.IsLatestStatement
                );
        }

        private void OnButtonBalanceDone(object sender, BalanceEventArgs e)
        {
            HideBalancePanel(e.Balanced);
        }

        void HideBalancePanel(bool balanced)
        {
            if (this.balanceControl != null)
            {
                this.balanceControl.Balanced -= new EventHandler<BalanceEventArgs>(OnButtonBalanceDone);
                this.toolBox.Remove(this.balanceControl);
                this.balanceControl = null;
                this.toolBox.Selected = this.accountsControl;

                TransactionView.OnEndReconcile(!balanced);
            }
        }
        #endregion

        #region MANAGE VIEW

        private void OnBeforeViewStateChanged(object sender, EventArgs e)
        {
            this.SaveViewStateOfCurrentView();
        }

        AfterViewStateChangedEventArgs viewStateChanging;

        private void OnAfterViewStateChanged(object sender, AfterViewStateChangedEventArgs e)
        {
            if (CurrentView == null)
            {
                return;
            }

            viewStateChanging = e;

            ITransactionView view = CurrentView as ITransactionView;
            if (view != null)
            {
                // Search back in this.navigator for previously saved view state information so we can jump back to the same row we were on before.
                RestorePreviouslySavedSelection(view, e.SelectedRowId);

                this.TransactionView.QuickFilterUX.FilterText = TransactionView.QuickFilter;

                if (TransactionView.IsReconciling && this.balanceControl != null)
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
                    categoriesControl.Selected = view.ActiveCategory;
                    this.toolBox.Selected = this.categoriesControl;
                }
                else if (view.ActiveRental != null)
                {
                    rentsControl.Selected = view.ActiveRental;
                    this.toolBox.Selected = this.rentsControl;
                }
                else if (view.ActiveSecurity != null)
                {
                    securitiesControl.Selected = view.ActiveSecurity;
                    this.toolBox.Selected = this.securitiesControl;
                    StockGraph.Generator = null; // wait for stock history to load.
                }
            
            }
            else
            {
                LoansView otherPossibleView = CurrentView as LoansView;
                if (otherPossibleView != null)
                {
                    this.accountsControl.SelectedAccount = otherPossibleView.AccountSelected;
                    this.toolBox.Selected = this.accountsControl;
                }
            }

            viewStateChanging = null;
            SetChartsDirty();

            //
            // All views must prepare a nice caption that we will show in the Main window title bar
            //
            UpdateCaption(CurrentView.Caption);

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
                            if (TransactionView.SelectedRowId != selectedRowId)
                            {
                                TransactionView.SelectedRowId = ts.SelectedRow;
                            }
                            break;
                        }
                    }
                }
            }
        }

        public void ShowTransfers(Account a)
        {
            SetCurrentView<TransactionsView>();
            TransactionView.ViewTransfers(a);
        }

        private void TrackSelectionChanges()
        {
            if (viewStateChanging != null && viewStateChanging.SelectedRowId != -1)
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
                    if (vs.View == TransactionView)
                    {
                        TransactionViewState state = vs.State as TransactionViewState;
                        if (state != null)
                        {
                            TransactionsView view = (TransactionsView)this.TransactionView;
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
                inGraphMouseDown = true;
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Transaction t = this.TransactionGraph.SelectedItem as Transaction;
                    if (t != null)
                    {
                        IView view = (IView)this.TransactionView;
                        view.SelectedRow = t;
                    }
                }
            }
            finally
            {
                inGraphMouseDown = false;
            }
        }

        private void HistoryChart_SelectionChanged(object sender, EventArgs e)
        {
            HistoryChartColumn c = HistoryChart.Selection;
            if (c != null)
            {
                List<Transaction> list = new List<Transaction>(from v in c.Values select (Transaction)v.UserData);
                this.TransactionView.QuickFilter = ""; // need to clear this as they might conflict.
                var view = SetCurrentView<TransactionsView>();
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
            if (c == null) return null;
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

                        if (this.TransactionView.ActiveCategory != null ||
                            this.TransactionView.ActivePayee != null)
                        {
                            bool historyWasNotVisible = TabHistory.Visibility != System.Windows.Visibility.Visible;
                            TabHistory.Visibility = System.Windows.Visibility.Visible;
                            TabTrends.Visibility = System.Windows.Visibility.Visible;

                            UpdateHistoryChart();

                            if (historyWasNotVisible || TabLoan.IsSelected || TabRental.IsSelected)
                            {
                                TabHistory.IsSelected = true;
                            }
                        }
                        else
                        {
                            TabTrends.Visibility = System.Windows.Visibility.Visible;
                            TabHistory.Visibility = System.Windows.Visibility.Collapsed;
                            if (TabLoan.IsSelected || TabRental.IsSelected || TabHistory.IsSelected)
                            {
                                HistoryChart.Selection = null;
                                TabTrends.IsSelected = true;
                            }
                        }

                        UpdateTransactionGraph(TransactionView.Rows, TransactionView.ActiveAccount, TransactionView.ActiveCategory);

                        // expense categories.
                        TabExpenses.Visibility = System.Windows.Visibility.Visible;
                        Category parent = GetParentCategory(filter);
                        IList<Transaction> rows = TransactionView.Rows as IList<Transaction>;

                        if (parent != filter)
                        {
                            rows = myMoney.Transactions.GetTransactionsByCategory(parent, TransactionView.GetTransactionIncludePredicate());
                        }

                        PieChartExpenses.CategoryFilter = parent;
                        PieChartExpenses.Unknown = myMoney.Categories.Unknown;
                        PieChartExpenses.Transactions = rows;
                        TabExpensesHeaderText.Foreground = (PieChartExpenses.NetAmount != 0) ? (Brush)FindResource("TextBrush") : (Brush)FindResource("DisabledForegroundBrush");

                        // income categories
                        TabIncomes.Visibility = System.Windows.Visibility.Visible;

                        PieChartIncomes.CategoryFilter = parent;
                        PieChartIncomes.Unknown = myMoney.Categories.Unknown;
                        PieChartIncomes.Transactions = rows;
                        TabIncomesHeaderText.Foreground = (PieChartIncomes.NetAmount != 0) ? (Brush)FindResource("TextBrush") : (Brush)FindResource("DisabledForegroundBrush");

                        // view the stock history
                        if (TransactionView.ActiveSecurity != null)
                        {
                            TabStock.Visibility = System.Windows.Visibility.Visible;
                        }
                        else
                        {
                            if (TabStock.IsSelected)
                            {
                                TabStock.IsSelected = false;
                                TabTrends.IsSelected = true;
                            }
                            TabStock.Visibility = System.Windows.Visibility.Collapsed;
                        }

                        // Hide these Tabs
                        TabLoan.Visibility = System.Windows.Visibility.Collapsed;
                        TabRental.Visibility = System.Windows.Visibility.Collapsed;

                    }
                    else if (CurrentView is LoansView)
                    {
                        TabLoan.Visibility = System.Windows.Visibility.Visible;
                        TabLoan.IsSelected = true;

                        // Hide these TABS
                        TabTrends.Visibility = System.Windows.Visibility.Collapsed;
                        TabIncomes.Visibility = System.Windows.Visibility.Collapsed;
                        TabExpenses.Visibility = System.Windows.Visibility.Collapsed;
                        TabStock.Visibility = System.Windows.Visibility.Collapsed;
                        TabHistory.Visibility = System.Windows.Visibility.Collapsed;

                        if (this.LoanChart.IsVisible)
                        {
                            LoansView loandView = CurrentView as LoansView;
                            this.LoanChart.LoanPayements = loandView.LoanPayements;
                            this.LoanChart.UpdateChart();
                        }
                    }
                    else if (CurrentView is RentSummaryView)
                    {
                        // Show these TABS
                        TabRental.Visibility = System.Windows.Visibility.Visible;
                        TabRental.IsSelected = true;


                        // Hide these TABS
                        TabTrends.Visibility = System.Windows.Visibility.Collapsed;
                        TabIncomes.Visibility = System.Windows.Visibility.Collapsed;
                        TabExpenses.Visibility = System.Windows.Visibility.Collapsed;
                        TabStock.Visibility = System.Windows.Visibility.Collapsed;
                        TabLoan.Visibility = System.Windows.Visibility.Collapsed;
                        TabHistory.Visibility = System.Windows.Visibility.Collapsed;

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

        private void UpdateHistoryChart()
        {
            if (this.TransactionView.ViewModel == null)
            {
                return;
            }
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
            HistoryChartColumn selection = HistoryChart.Selection;
            if (selection == null)
            {
                selection = new Charts.HistoryChartColumn();
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
            selection.Values = rows;// this.TransactionView.ViewModel;
            selection.Brush = brush;
            HistoryChart.Selection = selection;
        }

        void UpdateTransactionGraph(IEnumerable data, Account account, Category category)
        {
            this.TransactionGraph.Generator = new TransactionGraphGenerator(data, account, category, this.TransactionView.ActiveViewName);
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
            CategoryData data = (CategoryData)chart.Selection;
            if (data != null)
            {
                TransactionsView view = SetCurrentView<TransactionsView>();
                view.ViewTransactionsForCategory(data.Category, data.Transactions);
            }
        }

        private void BudgetChartSelectionChanged(object sender, EventArgs e)
        {
            BudgetChart chart = (BudgetChart)sender;
            BudgetData data = chart.Selection;
            if (data != null)
            {
                TransactionsView view = SetCurrentView<TransactionsView>();
                if (chart.CategoryFilter != null)
                {
                    view.ViewTransactionsForCategory(chart.CategoryFilter, data.Budgeted);
                }
                else
                {
                    view.ViewTransactions(data.Budgeted);
                }
            }
        }

        /// <summary>
        /// Work around for removing Debug output message of missing data bindings
        /// </summary>
        protected static readonly DependencyProperty ActualLegendItemStyleProperty = DependencyProperty.Register("ActualLegendItemStyle", typeof(Style), typeof(DataPointSeries), null);
        protected Style ActualLegendItemStyle
        {
            get
            {
                return (base.GetValue(ActualLegendItemStyleProperty) as Style);
            }
            set
            {
                base.SetValue(ActualLegendItemStyleProperty, value);
            }
        }


        #endregion

        #region Manage Query


        private void ShowQueryPanel()
        {
            MenuQueryShowForm.IsChecked = true;
            SetCurrentView<TransactionsView>();
            this.CurrentView.IsQueryPanelDisplayed = true;
            this.TransactionView.QueryPanel.OnShow();
        }

        private void HideQueryPanel(bool force = false)
        {
            if (force || MenuQueryShowForm.IsChecked)
            {
                MenuQueryShowForm.IsChecked = false;
                this.CurrentView.IsQueryPanelDisplayed = false;

                // Come back to the View state that we had before entering Query View
                Back();
                Forward();
            }
        }

        private void OnQueryPanelIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            MenuQueryShowForm.IsChecked = (bool)e.NewValue;
        }

        private void OnCommandAdhocQuery(object sender, ExecutedRoutedEventArgs e)
        {
            SqlServerDatabase realDb = this.database as SqlServerDatabase;
            if (realDb != null)
            {
                FreeStyleQueryDialog dialog = new FreeStyleQueryDialog(myMoney, realDb);
                dialog.Show();
            }
        }


        private void OnCommandShowQuery(object sender, ExecutedRoutedEventArgs e)
        {
            MenuQueryShowForm.IsChecked = !MenuQueryShowForm.IsChecked;
            if (MenuQueryShowForm.IsChecked)
            {
                ShowQueryPanel();

            }
            else
            {
                HideQueryPanel(true);
            }
        }

        private void OnCommandQueryRun(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                ExecuteQuery();
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
                bool isTransactionViewAlready = CurrentView is TransactionsView;
                TransactionsView view = SetCurrentView<TransactionsView>();
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
            else if (service == typeof(ExchangeRates))
            {
                return this.exchangeRates;
            }
            else if (service == typeof(IStatusService))
            {
                return (IStatusService)this;
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
            else if (service == typeof(OutputPane))
            {
                return this.OutputView;
            }
            else if (service == typeof(TransactionCollection))
            {
                return this.TransactionView.ViewModel;
            }
            else if (service == typeof(TrendGraph))
            {
                return this.TransactionGraph;
            }
            return null;
        }

        #endregion

        #region Edit Menu

        private IClipboardClient ActiveClipboardClient
        {
            get
            {
                if (lastFocus == TransactionView)
                {
                    return TransactionView;
                }
                else if (lastFocus == toolBox)
                {
                    if (toolBox.Selected == accountsControl)
                    {
                        return accountsControl;
                    }
                    else if (toolBox.Selected == categoriesControl)
                    {
                        return categoriesControl;
                    }
                    else if (toolBox.Selected == payeesControl)
                    {
                        return payeesControl;
                    }
                    else if (toolBox.Selected == rentsControl)
                    {
                        return rentsControl;
                    }
                }
                return null;
            }
        }

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
            IClipboardClient c = ActiveClipboardClient;
            if (c != null)
            {
                e.CanExecute = c.CanCut;
                e.Handled = true;
            }
        }
        private void OnCommandCut(object sender, ExecutedRoutedEventArgs e)
        {
            IClipboardClient c = ActiveClipboardClient;
            if (c != null)
            {
                c.Cut();
            }
        }
        private void OnCommandCanCopy(object sender, CanExecuteRoutedEventArgs e)
        {
            IClipboardClient c = ActiveClipboardClient;
            if (c != null)
            {
                e.CanExecute = c.CanCopy;
                e.Handled = true;
            }
        }
        private void OnCommandCopy(object sender, ExecutedRoutedEventArgs e)
        {
            IClipboardClient c = ActiveClipboardClient;
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
            IClipboardClient c = ActiveClipboardClient;
            if (c != null)
            {
                e.CanExecute = c.CanPaste;
                e.Handled = true;
            }
        }
        private void OnCommandPaste(object sender, ExecutedRoutedEventArgs e)
        {
            IClipboardClient c = ActiveClipboardClient;
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
            IClipboardClient c = ActiveClipboardClient;
            if (c != null)
            {
                e.CanExecute = c.CanDelete;
                e.Handled = true;
            }
        }
        private void OnCommandDelete(object sender, ExecutedRoutedEventArgs e)
        {
            IClipboardClient c = ActiveClipboardClient;
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

        void OnFlowDocumentViewClosed(object sender, EventArgs e)
        {
            SetCurrentView<TransactionsView>();
        }

        private void OnCommandNetWorth(object sender, ExecutedRoutedEventArgs e)
        {
            FlowDocumentView view = SetCurrentView<FlowDocumentView>();
            view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportNetworth");
            view.Closed += new EventHandler(OnFlowDocumentViewClosed);
            HelpService.SetHelpKeyword(view, "Networth Report");
            NetWorthReport report = new NetWorthReport(this.myMoney);
            view.Generate(report);
        }

        private void OnCommandReportInvestment(object sender, ExecutedRoutedEventArgs e)
        {
            ViewInvestmentPortfolio();
        }

        private void ViewInvestmentPortfolio()
        {
            FlowDocumentView view = SetCurrentView<FlowDocumentView>();
            view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportPortfolio");
            view.Closed += new EventHandler(OnFlowDocumentViewClosed);
            HelpService.SetHelpKeyword(view, "Investment Portfolio");
            PortfolioReport report = new PortfolioReport(view, this.myMoney, null, this, DateTime.Now);
            view.Generate(report);
        }

        private void OnTaxReport(object sender, ExecutedRoutedEventArgs e)
        {
            FlowDocumentView view = SetCurrentView<FlowDocumentView>();
            view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportTaxes");
            view.Closed += new EventHandler(OnFlowDocumentViewClosed);
            HelpService.SetHelpKeyword(view, "Tax Report");
            TaxReport report = new TaxReport(view, this.myMoney);
            view.Generate(report);
        }

        private void OnCommandW2Report(object sender, ExecutedRoutedEventArgs e)
        {
            FlowDocumentView view = SetCurrentView<FlowDocumentView>();
            view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportW2");
            view.Closed += new EventHandler(OnFlowDocumentViewClosed);
            HelpService.SetHelpKeyword(view, "W2 Report");
            W2Report report = new W2Report(view, this.myMoney, this);
            view.Generate(report);
        }

        private void HasActiveAccount(object sender, CanExecuteRoutedEventArgs e)
        {
            TransactionsView view = this.CurrentView as TransactionsView;
            e.CanExecute = view != null && view.ActiveAccount != null;
        }

        private void OnCommandReportCashFlow(object sender, ExecutedRoutedEventArgs e)
        {
            FlowDocumentView view = SetCurrentView<FlowDocumentView>();
            view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportCashFlow");
            view.Closed += new EventHandler(OnFlowDocumentViewClosed);
            CashFlowReport report = new CashFlowReport(view, this.myMoney, this);
            report.Regenerate();
        }

        private void OnCommandReportUnaccepted(object sender, ExecutedRoutedEventArgs e)
        {
            FlowDocumentView view = SetCurrentView<FlowDocumentView>();
            view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportUnaccepted");
            view.Closed += new EventHandler(OnFlowDocumentViewClosed);
            FlowDocumentReportWriter writer = new FlowDocumentReportWriter(view.DocumentViewer.Document);
            UnacceptedReport report = new UnacceptedReport(this.myMoney);
            report.Generate(writer);
        }

        #endregion

        #region COMMANDS

        private void OnRemovedUnusedSecurities(object sender, RoutedEventArgs e)
        {
            myMoney.RemoveUnusedSecurities();
        }

        private void OnCommandFileNew(object sender, ExecutedRoutedEventArgs e)
        {
            NewDatabase();
        }

        private void OnCommandFileOpen(object sender, ExecutedRoutedEventArgs e)
        {
            OpenDatabase();
        }

        private void OnCommandFileSave(object sender, ExecutedRoutedEventArgs e)
        {
            TransactionView.Commit();
            Save();
        }

        private void OnCommandCanSave(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.canSave;
            e.Handled = true;
        }

        private void OnCommandFileSaveAs(object sender, ExecutedRoutedEventArgs e)
        {
            TransactionView.Commit();
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
                        SaveAsSqlCe(fname);
                        break;
                    case ".db":
                        SaveAsSqlite(fname);
                        break;
                    case ".xml":
                        SaveAsXml(fname);
                        break;
                    case ".bxml":
                        SaveAsBinaryXml(fname);
                        break;
                    case ".csv":
                        ExportCsv(fname);
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
                Properties.Resources.MoneyFileFilter,
                Properties.Resources.AllFileFilter);
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.RestoreDirectory = true;
            openFileDialog1.Multiselect = true;

            if (openFileDialog1.ShowDialog(this) == true)
            {
                int len = openFileDialog1.FileNames.Length;
                ShowProgress(0, len, 0, null);
                int count = 0;
                int totalTransactions = 0;

                Cursor saved = this.Cursor;
                this.Cursor = Cursors.Wait;
                try
                {
                    List<string> ofxFiles = new List<string>();
                    List<string> qifFiles = new List<string>();
                    List<string> moneyFiles = new List<string>();

                    foreach (string file in openFileDialog1.FileNames)
                    {
                        string ext = System.IO.Path.GetExtension(file).ToLower();
                        ShowProgress(0, len, count, string.Format("Importing '{0}'", file));
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
                                    totalTransactions += ImportXml(file);
                                    break;
                                case ".db":
                                    moneyFiles.Add(file);
                                    break;
                                default:
                                    MessageBox.Show("Unrecognized file extension " + ext + ", expecting .qif, .ofx or .xml");
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBoxEx.Show(ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        }

                        if (qifFiles.Count > 0)
                        {
                            totalTransactions += ImportQif(qifFiles.ToArray());
                        }
                        if (ofxFiles.Count > 0)
                        {
                            totalTransactions += ImportOfx(ofxFiles.ToArray());
                        }
                        if (moneyFiles.Count > 0)
                        {
                            totalTransactions += ImportMoneyFile(moneyFiles.ToArray());
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
                    ShowProgress(0, len, -1, string.Format("Loaded {0} transactions", totalTransactions));
                }
                else
                {
                    ShowProgress(0, len, -1, null);
                }
            }
        }

        private void OnCommandCanOpenContainingFolder(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (this.database != null ? System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(this.database.DatabasePath)) : false);
        }

        private void OnCommandOpenContainingFolder(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.database != null)
            {
                string path = this.database.DatabasePath;
                string dir = System.IO.Path.GetDirectoryName(path);
                if (System.IO.Directory.Exists(dir))
                {
                    int SW_SHOWNORMAL = 1;
                    int hr = NativeMethods.ShellExecute(IntPtr.Zero, "explore", dir, "", "", SW_SHOWNORMAL);
                    return;
                }
            }
        }

        private string FilterDgml = "DGML files (*.dgml)|*.dgml";

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
                MessageBoxEx.Show(exp.Message);
            }
        }

        private void OnCommandBackup(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileDialog fd = new SaveFileDialog();
            fd.Title = "Backup Location";
            string path = GetBackupPath();

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
                    ShowMessage("Backed up to " + fd.FileName);
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
            CreateDatabaseDialog frm = InitializeCreateDatabaseDialog();
            frm.BackupPath = GetBackupPath();
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
                    LoadDatabase(null, frm.Database, null, frm.Password, frm.BackupPath);
                    CreateAttachmentDirectory();
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
                int changes = tracker.ChangeCount;
                if (MessageBoxEx.Show(string.Format("Are you sure you want to revert {0} changes", changes), "Revert Changes", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    this.DataContext = new MyMoney();
                    this.dirty = false;
                    BeginLoadDatabase();
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
            ViewSecurities();
        }

        private SecuritiesView ViewSecurities()
        {
            bool initialized = this.cacheViews.ContainsKey(typeof(SecuritiesView));
            SecuritiesView view = SetCurrentView<SecuritiesView>();
            if (!initialized)
            {
                view.SecuritySelected += new EventHandler<SecuritySelectionEventArgs>(OnSecuritySelected);
            }
            return view;
        }

        private void OnSecuritySelected(object sender, SecuritySelectionEventArgs e)
        {
            bool isTransactionViewAlready = this.CurrentView is TransactionViewState;
            TransactionsView view = SetCurrentView<TransactionsView>();
            view.ViewTransactionsForSecurity(e.Security, view.SelectedRowId);
            if (!isTransactionViewAlready)
            {
                this.navigator.Pop();
            }
        }

        private void OnCommandViewViewAliases(object sender, ExecutedRoutedEventArgs e)
        {
            SetCurrentView<AliasesView>();
        }

        private void OnCommandViewCurrencies(object sender, ExecutedRoutedEventArgs e)
        {
            SetCurrentView<CurrenciesView>();
        }


        private void OnCommandViewOptions(object sender, ExecutedRoutedEventArgs e)
        {
            SettingsDialog dialog = new SettingsDialog();
            dialog.Owner = this;

            if (this.database != null)
            {
                dialog.Password = this.database.Password;
            }

            if (dialog.ShowDialog() == true)
            {
                if (database != null)
                {
                    database.Password = dialog.Password;
                    DatabaseSecurity.SaveDatabasePassword(database.DatabasePath, database.Password);
                }

                // this setting might have changed.
                if (attachmentManager.AttachmentDirectory != settings.AttachmentDirectory)
                {
                    attachmentManager.AttachmentDirectory = settings.AttachmentDirectory;
                    attachmentManager.Stop();
                    attachmentManager.Start();
                }
            }
        }

        private void OnCommandViewThemeVS2010(object sender, ExecutedRoutedEventArgs e)
        {
            SetTheme("Themes/Theme-VS2010.xaml");
        }

        private void OnCommandViewThemeFlat(object sender, ExecutedRoutedEventArgs e)
        {
            SetTheme("Themes/Theme-Flat.xaml");
        }


        private void OnSynchronizeOnlineAccounts(object sender, ExecutedRoutedEventArgs e)
        {
            TransactionView.Commit();
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
                TabItem item = ShowDownloadTab();
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
            DoSync(list);
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
                FreeStyleQueryDialog dialog = new FreeStyleQueryDialog(myMoney, this.database);
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
            if (myMoney.Transactions.Count > 0)
            {
                if (MessageBoxEx.Show("You already have some data, are you sure you want to add lots of additional sample data?", "Add Sample Data", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                {
                    return;
                }
            }
            SampleDatabase sample = new SampleDatabase(this.myMoney);
            sample.Create();

            this.toolBox.Selected = this.accountsControl;
            Account a = this.myMoney.Accounts.GetFirstAccount();
            this.accountsControl.OnAccountsChanged(this, new ChangeEventArgs(a, null, ChangeType.Inserted)); // trigger early rebind so we can select it
            this.accountsControl.SelectedAccount = a;
            OnSelectionChangeFor_Account(this, EventArgs.Empty);
            UpdateCharts();
        }

        private void MenuExportSampleData_Click(object sender, RoutedEventArgs e)
        {
            string temp = Path.Combine(Path.GetTempPath(), "SampleData.xml");
            SampleDatabase sample = new SampleDatabase(this.myMoney);
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
                TransactionsView view = SetCurrentView<TransactionsView>();
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

        void OnAccountsPanelShowTransfers(object sender, ChangeEventArgs e)
        {
            this.ShowTransfers((Account)e.Item);
        }

        void OnAccountsPanelSyncAccount(object sender, ChangeEventArgs e)
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
            DoSync(accounts);
        }

        bool isSynchronizing;

        void DoSync(List<OnlineAccount> accounts)
        {
            isSynchronizing = true;
            try
            {
                this.accountsControl.MenuSync.IsEnabled = false;

                TabItem item = ShowDownloadTab();
                OfxDownloadControl dc = item.Content as OfxDownloadControl;
                dc.BeginDownload(this.myMoney, accounts);
            }
            finally
            {
                isSynchronizing = false;
            }
            this.accountsControl.MenuSync.IsEnabled = true;
        }

        private void OnCommandDownloadAccounts(object sender, ExecutedRoutedEventArgs e)
        {
            Account temp = new Account();
            temp.Type = AccountType.Checking;
            OnlineAccountDialog od = new OnlineAccountDialog(this.myMoney, temp, (IServiceProvider)this);
            od.Owner = this;
            od.ShowDialog();
        }

        #endregion

        #region IStatusService Members

        public void ShowMessage(string text)
        {
            if (Environment.TickCount < this.loadTime + 3000)
            {
                return;
            }
            InternalShowMessage(text);
        }

        public void InternalShowMessage(string text)
        {
            if (Dispatcher.Thread == System.Threading.Thread.CurrentThread)
            {
                ShowMessageUIThread(text);
            }
            else
            {
                UiDispatcher.BeginInvoke(new Action(() =>
                {
                    ShowMessageUIThread(text);
                }));
            }
        }

        private void ShowMessageUIThread(string text)
        {
            StatusMessage.Content = text;
        }

        public void ShowProgress(int min, int max, int value)
        {
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                ShowProgress(min, max, value, null);
            }));
        }

        public void ShowProgress(string message, int min, int max, int value)
        {
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                ShowProgress(min, max, value, message);
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
            StatusMessage.Content = string.Empty;
        }

        ChangeListRequest changeList;

        /// <summary>
        ///  Check if version has changed, and if so show version update info.
        /// </summary>
        private void CheckLastVersion()
        {
            changeList = new ChangeListRequest(this.settings);
            changeList.Completed += new EventHandler<SetupRequestEventArgs>(OnChangeListRequestCompleted);
            changeList.BeginGetChangeList(DownloadSite);

            // and see if we just installed a new version.
            string exe = ProcessHelper.MainExecutable;
            DateTime lastWrite = File.GetLastWriteTime(exe);
            if (lastWrite > settings.LastExeTimestamp)
            {
                string previous = settings.ExeVersion;
                settings.ExeVersion = NativeMethods.GetFileVersion(exe);
                settings.LastExeTimestamp = lastWrite;
                ShowChangeInfo(previous, null, false);
            }
        }

        private void OnChangeListRequestCompleted(object sender, SetupRequestEventArgs e)
        {
            XDocument changes = e.Changes;
            if (changes != null && e.NewVersionAvailable)
            {
                Brush brush = (Brush)this.FindResource("WalkaboutToolbarExpanderBrushSelected");
                if (brush != null)
                {
                    ButtonShowUpdateInfo.Background = brush;
                }
                ButtonShowUpdateInfoCaption.Text = "View Updates";
                ButtonShowUpdateInfo.Visibility = System.Windows.Visibility.Visible;
            }
        }

        private void OnButtonShowUpdateInfoClick(object sender, RoutedEventArgs e)
        {
            // we found a new version online, so show the details about what's in it.
            ShowChangeInfo(settings.ExeVersion, changeList.Changes, true);
        }

        private void ShowChangeInfo(string previousVersion, XDocument changeList, bool installButton)
        {
            if (changeList == null)
            {
                changeList = GetBuiltInList();
            }
            if (changeList != null)
            {
                FlowDocumentView view = SetCurrentView<FlowDocumentView>();
                view.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, "ReportUpdates");
                view.Closed += new EventHandler(OnFlowDocumentViewClosed);
                HelpService.SetHelpKeyword(view, "Updates");
                ChangeInfoFormatter report = new ChangeInfoFormatter(view, installButton, previousVersion, changeList);
                report.InstallButtonClick += OnInstallButtonClick;
                view.Generate(report);
            }
        }

        void OnInstallButtonClick(object sender, EventArgs e)
        {
            if (!SaveIfDirty("Save your changes before installing new version?", null))
            {
                return;
            }

            InternetExplorer.OpenUrl(IntPtr.Zero, new Uri(InstallUrl));
            Close();
        }

        private XDocument GetBuiltInList()
        {
            try
            {
                // get the built in changelist.
                string filename = new Uri(new Uri(ProcessHelper.MainExecutable), "Setup/changes.xml").LocalPath;
                XDocument doc = XDocument.Load(filename);
                return doc;
            }
            catch
            {
                //MessageBoxEx.Show("Internal error parsing Walkabout.Setup.changes.xml");
            }

            return null;
        }

        private void OnCommandViewChanges(object sender, ExecutedRoutedEventArgs e)
        {
            ShowChangeInfo(settings.ExeVersion, null, false);
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
            toolBox.Width = e.NewSize.Width;
        }


        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            if (!SaveIfDirty())
            {
                e.Cancel = true;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            using (this.quotes)
            {
                this.quotes = null;
            }

            this.exchangeRates.Dispose();

            StopTracking();

            StopTimer();

            using (attachmentManager)
            {
            }
            try
            {
                if (HasDatabase)
                {
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("SaveConfig failed: " + ex.Message);
            }
            TempFilesManager.Shutdown();
        }

        private void OnCommandHelpAbout(object sender, ExecutedRoutedEventArgs e)
        {
            string version;
            if (ApplicationDeployment.IsNetworkDeployed)
            {
                version = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
            }
            else
            {
                version = this.GetType().Assembly.GetName().Version.ToString();
            }
            var msg = string.Format("MyMoney, Version {0}\r\n\r\nData provided by iextrading.com and alphavantage.com.", version);
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
