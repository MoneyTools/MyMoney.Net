using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using Walkabout.Attachments;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Dialogs;
using Walkabout.Migrate;
using Walkabout.Reports;
using Walkabout.Utilities;
using Walkabout.Help;
using System.ComponentModel;
using System.Data.SqlTypes;
using Walkabout.WpfConverters;
using System.Windows.Media.Imaging;
using Walkabout.Configuration;
using Walkabout.Interfaces.Views;
using Walkabout.StockQuotes;
using System.Windows.Media.Animation;
using ModernWpf.Controls;

#if PerformanceBlocks
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;

#endif

namespace Walkabout.Views
{
    public enum TransactionFilter
    {
        All,
        Accepted,
        Unaccepted,
        Reconciled,
        Unreconciled,
        Categorized,
        Uncategorized,
        Custom
    }

    public enum TransactionViewName
    {
        None,
        Account,
        Custom,
        Transfers,
        DanglingTransfers,
        ByPayee,
        BySecurity,
        ByCategory,
        ByCategoryCustom,
        ByQuery,
        Rental,
        Portfolio
    }

    public enum TransactionSelection
    {
        Specific, // use the specific transaction Id specified
        First, // select first row
        Last, // select last row
        Current, // keep existing selection
    }

    /// <summary>
    /// Interaction logic for TransactionsView.xaml
    /// </summary>
    public partial class TransactionsView : UserControl, ITransactionView, IClipboardClient
    {
        #region PROPERTIES PRIVATE

        private const int PortfolioTab = 1;
        private const int CashTab = 0;
        private int selectedTab = 0;
        private bool reconciling;

        private string caption;
        private IServiceProvider site;

        private MyMoney myMoney;
        private TypeToFind ttf;
        private DelayedActions delayedUpdates = new DelayedActions();

        private IEnumerable<Transaction> fixedList;
        #endregion

        #region PROPERTIES PUBLIC

        MoneyDataGrid theGrid;

        public MoneyDataGrid TheActiveGrid
        {
            get { return theGrid; }
            set { theGrid = value; }
        }

        public ListCollectionView PayeesAndTransferNames
        {
            get { return new ListCollectionView(GetPayeesAndTransfersList()); }
        }

        private List<string> GetPayeesAndTransfersList()
        {
            List<string> names = new List<string>();
            foreach (Payee p in myMoney.Payees.GetPayees())
            {
                names.Add(p.Name);
            }

            foreach (Account a in myMoney.Accounts.GetAccounts())
            {
                names.Add(Transaction.GetTransferCaption(a, false));
                names.Add(Transaction.GetTransferCaption(a, true));
            }

            names.Sort();

            // empty name so that if there is no choice it doesn't just pick the first payee.
            names.Insert(0, "");

            return names;
        }

        public ListCollectionView Securities
        {
            get
            {
                var list = myMoney.Securities.GetSortedSecurities();
                list.Insert(0, Security.None); // so user can remove the security.
                return new ListCollectionView(list);
            }
        }

        public ObservableCollection<InvestmentType> Activities
        {
            get { return (ObservableCollection<InvestmentType>)GetValue(ActivitiesProperty); }
            set { SetValue(ActivitiesProperty, value); }
        }

        public static readonly DependencyProperty ActivitiesProperty =
            DependencyProperty.Register("Activities", typeof(ObservableCollection<InvestmentType>), typeof(TransactionsView), new UIPropertyMetadata(null));


        public void UpdateActivities()
        {
            ObservableCollection<InvestmentType> list = Activities;
            if (list == null)
            {
                list = new ObservableCollection<InvestmentType>();

                foreach (InvestmentType it in Enum.GetValues(typeof(InvestmentType)))
                {
                    list.Add(it);
                }

                this.Activities = list;
            }
        }

        public static readonly DependencyProperty OneLineViewProperty = DependencyProperty.Register("OneLineView", typeof(bool), typeof(TransactionsView), new FrameworkPropertyMetadata(new PropertyChangedCallback(OnOneLineViewChanged)));

        private static void OnOneLineViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TransactionsView tv = (TransactionsView)d;
            tv.OnOneLineViewChanged();
        }

        private void OnOneLineViewChanged()
        {
            ToggleShowLinesImage.Symbol = (OneLineView) ? Symbol.List : Symbol.ShowResults;
            FireBeforeViewStateChanged();
            if (OneLineViewChanged != null)
            {
                OneLineViewChanged(this, EventArgs.Empty);
            }
            TheActiveGrid.AsyncScrollSelectedRowIntoView();
        }

        public bool OneLineView
        {
            get { return (bool)GetValue(OneLineViewProperty); }
            set
            {
                if (value != (bool)GetValue(OneLineViewProperty))
                {
                    SetValue(OneLineViewProperty, value);
                }
            }
        }

        public event EventHandler OneLineViewChanged;

        private void OnToggleLines(object sender, RoutedEventArgs e)
        {
            OneLineView = !OneLineView;
        }

        private void OnToggleShowSplits(object sender, RoutedEventArgs e)
        {
            ViewAllSplits = !ViewAllSplits;
        }

        private void OnToggleShowSecurities_Checked(object sender, RoutedEventArgs e)
        {
            if (portfolioReport != null)
            {
                ToggleExpandAll.ToolTip = "Hide Details";
                portfolioReport.ExpandAll();
                ToggleExpandAllImage.SetResourceReference(Image.SourceProperty, "CollapseAllIcon");
            }
        }

        private void OnToggleShowSecurities_Unchecked(object sender, RoutedEventArgs e)
        {
            if (portfolioReport != null)
            {
                ToggleExpandAll.ToolTip = "Show Details";
                portfolioReport.CollapseAll();
                ToggleExpandAllImage.SetResourceReference(Image.SourceProperty, "ExpandAllIcon");
            }
        }

        public DateTime BudgetDate { get; set; }

        public static readonly DependencyProperty ViewAllSplitsProperty = DependencyProperty.Register("ViewAllSplits", typeof(bool), typeof(TransactionsView), new FrameworkPropertyMetadata(new PropertyChangedCallback(OnViewAllSplitsChanged)));

        static void OnViewAllSplitsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TransactionsView tv = (TransactionsView)d;
            tv.OnViewAllSplitsChanged();
        }

        private void OnViewAllSplitsChanged()
        {
            FireBeforeViewStateChanged();

            // Toggle the Detail Split View 
            if (ViewAllSplits == true)
            {
                TheActiveGrid.RowDetailsTemplate = TryFindResource("myDataGridDetailMiniView") as DataTemplate;
                TheActiveGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Visible;
                splitVisibleRowId = this.SelectedRowId;
            }
            else
            {
                TheActiveGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed;
                splitVisibleRowId = -1;
            }
        }

        public bool ViewAllSplits
        {
            get { return (bool)GetValue(ViewAllSplitsProperty); }
            set
            {
                SetValue(ViewAllSplitsProperty, value);

                //ToggleShowSplits.IsChecked = value;

                ToggleShowSplits.ToolTip = value ? "Hide All Splits" : "Show All Splits";

                ToggleShowSplitsImage.Symbol = value ? Symbol.ShowBcc : Symbol.HideBcc;
            }
        }

        public TransactionFilter TransactionFilter
        {
            get { return (TransactionFilter)GetValue(TransactionFilterProperty); }
            set { SetValue(TransactionFilterProperty, value); }
        }

        private bool programaticFilterChange;

        private void SetTransactionFilterNoUpdate(TransactionFilter filter)
        {
            var previous = this.programaticFilterChange;
            this.programaticFilterChange = true;
            this.TransactionFilter = filter;
            this.programaticFilterChange = previous;
        }


        /// <summary>
        /// Here's our chance to update any general UX information for the current viewing state of the transactions view being displayed
        /// For instance this is where we will choose to display the big watermark to indicate if any type of filtering is being applied.
        /// This lets the user know that some of his data is intentionally not being displayed
        /// </summary>
        private void UpdateUX()
        {
            bool someFilteringIsBeingApplied = false;
            if (TransactionFilter != TransactionFilter.All)
            {
                someFilteringIsBeingApplied = true;
            }
            else if (string.IsNullOrWhiteSpace(quickFilter) == false)
            {
                someFilteringIsBeingApplied = true;
            }
            else if (this.currentDisplayName == TransactionViewName.ByQuery ||
                     this.currentDisplayName == TransactionViewName.Custom ||
                     this.currentDisplayName == TransactionViewName.DanglingTransfers ||
                     this.currentDisplayName == TransactionViewName.Transfers ||
                     this.currentDisplayName == TransactionViewName.ByCategoryCustom)
            {
                someFilteringIsBeingApplied = true;
            }

            if (someFilteringIsBeingApplied)
            {
                this.FilterWatermark.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                this.FilterWatermark.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        // Using a DependencyProperty as the backing store for TransactionFilter.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TransactionFilterProperty =
            DependencyProperty.Register("TransactionFilter", typeof(TransactionFilter), typeof(TransactionsView), new UIPropertyMetadata(TransactionFilter.All, new PropertyChangedCallback(OnTransactionFilterChanged)));

        private static void OnTransactionFilterChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            TransactionsView view = (TransactionsView)sender;
            view.OnTransactionFilterChanged();
        }

        private void OnTransactionFilterChanged()
        {
            switch (this.TransactionFilter)
            {
                case TransactionFilter.All:
                    this.TransactionViewMode.SelectedValue = this.FilterByNothing;
                    break;
                case TransactionFilter.Accepted:
                    this.TransactionViewMode.SelectedValue = this.FilterByAccepted;
                    break;
                case TransactionFilter.Unaccepted:
                    this.TransactionViewMode.SelectedValue = this.FilterByUnaccepted;
                    break;
                case TransactionFilter.Categorized:
                    this.TransactionViewMode.SelectedValue = this.FilterByCategorized;
                    break;
                case TransactionFilter.Uncategorized:
                    this.TransactionViewMode.SelectedValue = this.FilterByUncategorized;
                    break;
                case TransactionFilter.Reconciled:
                    this.TransactionViewMode.SelectedValue = this.FilterByReconciled;
                    break;
                case TransactionFilter.Unreconciled:
                    this.TransactionViewMode.SelectedValue = this.FilterByUnreconciled;
                    break;
                case TransactionFilter.Custom:
                    this.TransactionViewMode.SelectedValue = this.FilterByCustom;
                    break;
            }

            if (!programaticFilterChange)
            {
                if (this.currentDisplayName == TransactionViewName.Custom && this.activeAccount != null)
                {
                    this.currentDisplayName = TransactionViewName.Account;
                }
                Refresh();
            }
        }

        #endregion

        #region CONSTRUCTORS

        /// <summary>
        /// Default constructor needed for WPF Designers
        /// </summary>
        public TransactionsView()
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.TransactionViewInitialize))
            {
#endif

                InitializeComponent();

                TheGrid_BankTransactionDetails.ParentMenu = this.ContextMenu;
                TheGrid_BankTransactionDetails.CustomBeginEdit += OnCustomBeginEdit;

                TheGrid_TransactionFromDetails.ParentMenu = this.ContextMenu;
                TheGrid_TransactionFromDetails.CustomBeginEdit += OnCustomBeginEdit;

                TheGrid_InvestmentActivity.ParentMenu = this.ContextMenu;
                TheGrid_TransactionFromDetails.CustomBeginEdit += OnCustomBeginEdit;

                TheGrid_BySecurity.ParentMenu = this.ContextMenu;
                TheGrid_BySecurity.CustomBeginEdit += OnCustomBeginEdit;

                TheActiveGrid = TheGrid_BankTransactionDetails;

                InvestmentAccountTabs.SelectionChanged += new SelectionChangedEventHandler(OnInvestmentAccountTabs_SelectionChanged);

                //
                // Setup the grids and hide them, they were visible in the XAML in order to assist in designing
                //
                SetupGrid(TheGrid_BankTransactionDetails, true);
                SetupGrid(TheGrid_TransactionFromDetails, true);
                SetupGrid(TheGrid_InvestmentActivity, true);
                SetupGrid(TheGrid_BySecurity, true);

                TheGrid_BankTransactionDetails.Visibility = System.Windows.Visibility.Collapsed;
                TheGrid_TransactionFromDetails.Visibility = System.Windows.Visibility.Collapsed;
                TheGrid_InvestmentActivity.Visibility = System.Windows.Visibility.Collapsed;
                TheGrid_BySecurity.Visibility = System.Windows.Visibility.Collapsed;
                InvestmentPortfolioView.Visibility = System.Windows.Visibility.Collapsed;
                ToggleExpandAll.Visibility = System.Windows.Visibility.Collapsed;

                ContextMenu menu = this.ContextMenu;
                menu.DataContext = this;

                // setup initial state to be multi-line view
                OneLineView = false;
                OnOneLineViewChanged(); // initialize icon.

                // setup initial state of split details view.
                OnViewAllSplitsChanged();

                // setup initial transaction filter settings
                OnTransactionFilterChanged();

                this.Unloaded += OnTransactionViewUnloaded;
#if PerformanceBlocks
            }
#endif
        }

        private void OnCustomBeginEdit(object sender, DataGridCustomEditEventArgs e)
        {
            DataGridRow row = e.Row;
            if (row.DataContext is Transaction t && t.Account == null)
            {
                // user is messing around with a new database, and they have not created
                // any bank account yet.
                var accountTemplate = new Account() { Type = AccountType.Checking };
                AccountDialog newAccountDialog = new AccountDialog(this.myMoney, accountTemplate, this.site);
                newAccountDialog.Owner = App.Current.MainWindow;
                if (newAccountDialog.ShowDialog() == true)
                {
                    t.Account = newAccountDialog.TheAccount;
                    this.myMoney.Accounts.Add(t.Account);
                    TransactionCollection tc = this.TheActiveGrid.ItemsSource as TransactionCollection;
                    tc.Account = t.Account;
                }
                else
                {
                    // well we tried, but it is ok to continue, it just means the transaction
                    // will not be saved.
                }
            }
            RoutedEventArgs args = e.EditingEventArgs;
            if (row.IsSelected && args is MouseButtonEventArgs mouseArgs)
            {
                DataGridColumn column = e.Column;
                string name = GetHitFieldName(column, row, mouseArgs);
                if (!string.IsNullOrEmpty(name))
                {
                    e.Handled = true;
                    // lazy disptach to allow the actual editors to be created.
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        OnStartEdit(column, row, name);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        private string GetHitFieldName(DataGridColumn column, DataGridRow row, MouseButtonEventArgs args)
        {
            FrameworkElement contentPresenter = column.GetCellContent(row);
            HitTestResult result = VisualTreeHelper.HitTest(contentPresenter, args.GetPosition(contentPresenter));
            if (result != null && result.VisualHit is TransactionTextField field)
            {
                return field.FieldName;
            }
            return null;
        }

        private void OnStartEdit(DataGridColumn column, DataGridRow row, string fieldName)
        {
            // Special handling for the fact that we have a column containing 3 separate editors (Payee, Category, Memo).
            // This method is called before these editors show up because otherwise the mouse event args
            // could be OFF if the editors are larger than the non-editable cells.  So we find out what was hit,
            // then let the editors be created, then we put the matching editor into edit mode so the user doesn't
            // have to click twice!
            FrameworkElement contentPresenter = column.GetCellContent(row);
            var grid = WpfHelper.FindAncestor<MoneyDataGrid>(contentPresenter);
            if (grid == null)
            {
                return;
            }
            List<Control> editors = new List<Control>();
            WpfHelper.FindEditableControls(contentPresenter, editors);
            if (editors.Count > 0)
            {
                // find the editor with the matching field name.
                string editorName = "EditorFor" + fieldName;
                foreach (var editor in editors)
                {
                    if (editor.Name == editorName)
                    {
                        grid.OnStartEdit(editor);
                    }
                }
            }
        }

        private void OnTransactionViewUnloaded(object sender, RoutedEventArgs e)
        {
            delayedUpdates.CancelAll();
        }

        private void SetupContextMenuBinding(ContextMenu menu, string itemname, DependencyProperty property, Binding binding)
        {
            MenuItem item = (MenuItem)menu.FindName(itemname);
            if (item != null)
            {
                item.SetBinding(property, binding);
            }
        }

        private void SetupGrid(MoneyDataGrid grid, bool supportDragDrop)
        {
            grid.BeginningEdit += OnBeginEdit;
            grid.RowEditEnding += OnDataGridCommit;
            grid.PreviewKeyDown += new KeyEventHandler(OnDataGridPreviewKeyDown);
            grid.KeyDown += new KeyEventHandler(OnDataGrid_KeyDown);
            grid.SelectionChanged += new SelectionChangedEventHandler(TheGrid_SelectionChanged);
            grid.CellEditEnding += new EventHandler<DataGridCellEditEndingEventArgs>(OnDataGridCellEditEnding);
            grid.SupportDragDrop = supportDragDrop;
            this.SearchArea.DataContext = this;
        }

        void TearDownGrid(DataGrid grid)
        {
            grid.BeginningEdit -= OnBeginEdit;
            grid.RowEditEnding -= OnDataGridCommit;
            grid.PreviewKeyDown -= new KeyEventHandler(OnDataGridPreviewKeyDown);
            grid.KeyDown -= new KeyEventHandler(OnDataGrid_KeyDown);
            grid.SelectionChanged -= new SelectionChangedEventHandler(TheGrid_SelectionChanged);
        }


        private string CurrentColumnHeader
        {
            get
            {
                if (TheActiveGrid.CurrentColumn != null)
                {
                    object header = TheActiveGrid.CurrentColumn.Header;
                    if (header != null)
                    {
                        return header.ToString();
                    }
                }
                return string.Empty;
            }
        }

        private DataGrid FindDataGridContainingFocus()
        {
            DependencyObject e = Keyboard.FocusedElement as DependencyObject;
            while (e != null && !(e is DataGrid))
            {
                e = VisualTreeHelper.GetParent(e);
            }
            return e as DataGrid;
        }

        private bool IsKeyboardFocusInsideSplitsDataGrid
        {
            get
            {
                DataGrid focus = FindDataGridContainingFocus();
                if (focus != null && focus.Name == "TheGridForAmountSplit")
                {
                    return true;
                }
                return false;
            }
        }

        private void OnDataGridPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (IsKeyboardFocusInsideSplitsDataGrid)
            {
                MoneyDataGrid splitGrid = FindDataGridContainingFocus() as MoneyDataGrid;
                if (splitGrid == e.Source)
                {
                    switch (e.Key)
                    {
                        case Key.Tab:
                            {
                                if (e.KeyboardDevice.IsKeyDown(Key.LeftShift) || e.KeyboardDevice.IsKeyDown(Key.RightShift))
                                {
                                    e.Handled = splitGrid.MoveFocusToPreviousEditableField();
                                }
                                else
                                {
                                    e.Handled = splitGrid.MoveFocusToNextEditableField();
                                }
                                break;
                            }

                        case Key.Enter:
                            {
                                DataGrid grid = splitGrid;
                                if (grid != null && grid.CurrentCell != null && !this.IsEditing)
                                {
                                    grid.BeginEdit();
                                    e.Handled = true;
                                }
                                break;
                            }

                        case Key.Insert:
                            {
                                InsertNewRow();
                                e.Handled = true;
                                break;
                            }
                    }
                }
            }
            else
            {
                // transaction has the focus

                switch (e.Key)
                {
                    case Key.F12:
                        TheActiveGrid.ClearAutoEdit();
                        break;

                    case Key.Tab:
                        {
                            if (e.KeyboardDevice.IsKeyDown(Key.LeftShift) || e.KeyboardDevice.IsKeyDown(Key.RightShift))
                            {
                                e.Handled = TheActiveGrid.MoveFocusToPreviousEditableField();
                            }
                            else
                            {
                                e.Handled = TheActiveGrid.MoveFocusToNextEditableField();
                            }
                            break;
                        }

                    case Key.Enter:
                        {
                            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                            {
                                // Control-Enter set the transaction as ACCEPTED
                                Transaction t = this.SelectedTransaction;
                                if (t != null)
                                {
                                    t.Parent.BeginUpdate(true);
                                    try
                                    {
                                        AutoPopulateCategory(t);

                                        // ACCEPTED
                                        if (t.Category != null || t.Transfer != null)
                                        {
                                            // Set to ACCEPTED state only if there's Category and it's not a Transfer
                                            t.Unaccepted = false;
                                        }
                                    }
                                    finally
                                    {
                                        t.Parent.EndUpdate();
                                    }
                                    // Move to the next row
                                    SelectedRowIndex = SelectedRowIndex + 1;

                                    TheActiveGrid.ClearAutoEdit();
                                    e.Handled = true;
                                }
                            }
                            else
                            {
                                DataGrid grid = this.TheActiveGrid;
                                if (grid != null && grid.CurrentCell != null && !this.IsEditing)
                                {
                                    grid.BeginEdit();
                                    e.Handled = true;
                                }
                            }
                            break;
                        }

                    case Key.Insert:
                        {
                            InsertNewRow();
                            e.Handled = true;
                            break;
                        }

                    case Key.P:
                        // Toggle between Payee and Payment.
                        if (e.KeyboardDevice.IsKeyDown(Key.LeftCtrl) || e.KeyboardDevice.IsKeyDown(Key.RightCtrl))
                        {
                            if (this.IsEditing)
                            {
                                if (CurrentColumnHeader.Contains("Payee"))
                                {
                                    EditModeSetToColumn("Payment");
                                }
                                else
                                {
                                    EditModeSetToColumn("Payee");
                                }
                                e.Handled = true;
                            }
                        }
                        break;

                    case Key.PageUp:
                        //-----------------------------------------------------
                        // Quick jump to Payee
                        //
                        if (e.KeyboardDevice.IsKeyDown(Key.LeftCtrl) || e.KeyboardDevice.IsKeyDown(Key.RightCtrl))
                        {
                            if (this.IsEditing)
                            {
                                EditModeSetToColumn("Payee");
                                e.Handled = true;
                            }
                        }
                        break;

                    case Key.PageDown:
                        //-----------------------------------------------------
                        // Quick jump to Payment
                        //
                        if (e.KeyboardDevice.IsKeyDown(Key.LeftCtrl) || e.KeyboardDevice.IsKeyDown(Key.RightCtrl))
                        {
                            if (this.IsEditing)
                            {
                                EditModeSetToColumn("Payment");
                                e.Handled = true;
                            }
                        }
                        break;


                    case Key.Space:
                        if (IsReconciling)
                        {
                            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                            {
                                // Control-Space set the transaction as ACCEPTED & RECONCILED
                                Transaction t = this.SelectedTransaction;
                                if (t != null)
                                {
                                    t.Parent.BeginUpdate(true);
                                    try
                                    {
                                        // ACCEPTED
                                        if (t.Category != null || t.Transfer != null)
                                        {
                                            // Set to ACCEPTED state only if there's Category and it's not a Transfer
                                            t.Unaccepted = false;
                                        }

                                        // RECONCILED
                                        ReconcileThisTransaction(t);
                                    }
                                    finally
                                    {
                                        t.Parent.EndUpdate();
                                    }
                                    // Move to the next row
                                    SelectedRowIndex = SelectedRowIndex + 1;

                                    TheActiveGrid.ClearAutoEdit();
                                    e.Handled = true;
                                }
                            }
                        }
                        break;
                }
            }
        }


        /// <summary>
        /// Add a new row just below the currently selected Row
        /// and switch to EDIT mode
        /// </summary>
        private void InsertNewRow()
        {
            if (IsEditing)
            {
                Commit();
                TheActiveGrid.CommitEdit(DataGridEditingUnit.Row, true);
            }

            Transaction t = this.SelectedTransaction;
            if (t != null && !t.IsReadOnly)
            {
                Split s = selectedSplit as Split;
                if (s != null)
                {
                    InsertNewSplit();
                }
                else
                {
                    InsertNewTransaction();
                }
            }
        }

        private void InsertNewSplit()
        {
            // User wants to insert a new split just after the currently selected one.
            Transaction t = this.SelectedTransaction;
            if (t != null && !t.IsReadOnly)
            {
                Splits splits = t.NonNullSplits;

                Split s = splits.NewSplit();
                s.Category = t.Category;
                Split currentSplit = selectedSplit as Split;
                if (currentSplit != null)
                {
                    int current = splits.IndexOf(currentSplit);
                    splits.Insert(current + 1, s);
                }
                else
                {
                    splits.AddSplit(s);
                }

                // todo: put this split in edit mode...
            }
        }

        private void InsertNewTransaction()
        {
            //
            // User wants to insert a new transaction, just after the currently selected transaction
            // We will use the date of the selected transaction + 1 second to ensure that it is placed just after
            //
            DateTime dateForNewTransaction;

            Transaction t = this.SelectedTransaction;
            if (t == null)
            {
                dateForNewTransaction = DateTime.Now;
            }
            else
            {
                dateForNewTransaction = t.Date;
            }


            // We want to insert the transaction after the one currently selected one
            // TODO most of the transaction date are set to 00:00:00 
            // in the future we need to "reSort" all the seconds of all the transaction for that date for this account
            // For now we add 1 second to ensure that it is placed after the Date selected
            Transaction insertThisTransaction = this.myMoney.Transactions.NewTransaction(ActiveAccount);
            insertThisTransaction.Date = dateForNewTransaction.AddSeconds(1);
            this.myMoney.Transactions.AddTransaction(insertThisTransaction);


            TheActiveGrid.SelectedItem = insertThisTransaction;

            //-----------------------------------------------------
            // Start Edit mode of the new added row
            //
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                this.UpdateLayout();
                this.TheActiveGrid.UpdateLayout();

                Transaction tt = TheActiveGrid.SelectedItem as Transaction;

                //Debug.WriteLine("Current selected item is " + tt.Amount);

                TheActiveGrid.SelectedItem = insertThisTransaction;

                if (TheActiveGrid.SelectedItem == insertThisTransaction)
                {

                    //-------------------------------------------------
                    // The new row should be selected now.
                    // We must get the "Payee" cell and put it in edit mode
                    //
                    EditModeSetToColumn("Payee");
                }
            }), DispatcherPriority.ContextIdle);
        }

        private void EditModeSetToColumn(string header)
        {
            int columnToEdit = this.TheActiveGrid.GetColumnIndexByTemplateHeader(header);
            if (columnToEdit < 0)
            {
                return;
            }
            DataGridCellInfo dgci = this.TheActiveGrid.SelectedCells[columnToEdit];
            if (dgci != null)
            {
                DataGridCell dgc = TheActiveGrid.GetCell(TheActiveGrid.GetRowIndex(dgci), TheActiveGrid.GetColIndex(dgci));
                if (dgc == null)
                {
                    Debug.WriteLine("ERROR - Insert Row and Edit 'dgc' is NULL");
                }
                else
                {
                    dgc.Focus();
                    Debug.WriteLineIf(dgc.IsFocused == false, "could not set focus on the new row cell");
                    TheActiveGrid.BeginEdit();
                }
            }
        }

        private void OnDataGrid_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space:
                    if (IsReconciling)
                    {
                        ToggleTransactionStateReconciled(this.SelectedTransaction);
                        SelectedRowIndex = SelectedRowIndex + 1; // Move to the next row

                        e.Handled = true;
                    }
                    break;

                case Key.F6:
                    if (SelectedTransaction.NonNullSplits.Unassigned != 0)
                    {
                        System.Windows.Controls.DataGrid dataGrid = sender as System.Windows.Controls.DataGrid;
                        if (dataGrid != null)
                        {

                            Split s = dataGrid.CurrentCell.Item as Split;
                            if (s != null)
                            {
                                FrameworkElement fe = dataGrid.CurrentCell.Column.GetCellContent(s);
                                List<Control> editors = new List<Control>();
                                WpfHelper.FindEditableControls(fe, editors);
                                if (editors.Count > 0)
                                {
                                    TextBox tb = editors[0] as TextBox;
                                    if (tb != null)
                                    {
                                        decimal newValue = s.Amount + SelectedTransaction.NonNullSplits.Unassigned;
                                        tb.Text = Math.Abs(newValue).ToString();
                                    }
                                }
                                dataGrid.InvalidateVisual();

                                e.Handled = true;
                            }
                        }
                    }
                    break;
            }
        }


        private Transaction committed;

        private void OnDataGridCommit(object sender, DataGridRowEditEndingEventArgs e)
        {
            this.IsEditing = false;
            DataGrid grid = (DataGrid)sender;
            this.committed = e.Row.Item as Transaction;
            if (this.committed != null)
            {
                if (this.committed.Splits != null)
                {
                    this.committed.Splits.AmountMinusSalesTax = null;
                }

                if (this.committed.Investment != null)
                {
                    Security s = this.committed.InvestmentSecurity;
                    if (s != null && s.Price == 0 && this.committed.InvestmentUnitPrice != 0)
                    {
                        // This might be a new security, so as a convenience we can initialize the price for the user here.
                        s.Price = this.committed.InvestmentUnitPrice;
                    }
                }

                // Note: this must be DispatcherPriority.Background otherwise Rebalance() happens too soon and
                // doesn't see the new value!  BUGBUG: We should optimize this and only trigger rebalance if 
                // a field that affects the balance was actually changed.
                this.Dispatcher.BeginInvoke(new Action(Rebalance), DispatcherPriority.Background);

                if (committed.AmountError)
                {
                    // make sure the binding is reset to the original value.
                    committed.OnChanged("Credit");
                    committed.OnChanged("Debit");
                }
            }
        }

        private bool rebalancing;

        private void Rebalance()
        {
            rebalancing = true;
            if (this.activeAccount != null)
            {
                this.myMoney.Rebalance(this.activeAccount);
            }
            else if (this.ActiveRental != null)
            {
                this.myMoney.Rebalance(this.ActiveRental);
            }

            if (isDisplayInvalid)
            {
                Refresh();
                isDisplayInvalid = false;
            }
            else
            {
                RefreshVisibleColumns(TheActiveGrid.Name == "TheGrid_BySecurity" ? "RunningBalance" : "Balance");
            }
            rebalancing = false;
        }

        private void RefreshVisibleColumns(string colummName)
        {
            var rows = this.TheActiveGrid.GetVisibleRows();
            if (rows != null)
            {
                TransactionCollection c = TheActiveGrid.ItemsSource as TransactionCollection;
                if (c != null)
                {
                    for (int row = rows.Item1; row < rows.Item2 && row < c.Count; row++)
                    {
                        Transaction t = c[row];
                        t.RaisePropertyChanged(colummName);
                    }
                }
            }
        }

        private void OnDataGridRowDragDrop(object sender, DragEventArgs e)
        {
            DataGridRow row = (DataGridRow)sender;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var transaction = row.Item as Transaction;
                AttachmentManager mgr = this.ServiceProvider.GetService(typeof(AttachmentManager)) as AttachmentManager;
                if (transaction != null && transaction.HasAttachment == false && mgr != null)
                {
                    StringBuilder sb = new StringBuilder();
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    foreach (var file in files)
                    {
                        var extension = Path.GetExtension(file);

                        try
                        {
                            string attachmentFullPath = mgr.GetUniqueFileName(transaction, extension);
                            File.Copy(files[0], attachmentFullPath, true);
                            transaction.HasAttachment = true;
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine(ex.Message);
                        }
                    }
                    if (sb.Length > 0)
                    {
                        MessageBoxEx.Show(sb.ToString(), "Add Attachments Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                Transaction t = e.Data.GetData(typeof(Transaction)) as Transaction;
                if (t != null)
                {
                    Transaction u = row.Item as Transaction;
                    if (t != u && u != null && t.Amount == u.Amount)
                    {
                        Merge(t, u, true);
                    }
                }
            }

            ClearDragDropStyles(row);
        }

        private void Merge(Transaction t, Transaction u, bool promptForConfirmation)
        {

            bool swap = false;
            if (t.Status != TransactionStatus.Reconciled && u.Status == TransactionStatus.Reconciled)
            {
                // we need to keep the reconciled one, it won't let us delete a reconciled transaction.
                swap = true;
            }
            else if (t.Status == TransactionStatus.Reconciled && u.Status != TransactionStatus.Reconciled)
            {
                // we need to keep the reconciled one, it won't let us delete a reconciled transaction.
                swap = false;
            }
            else if (t.Transfer == null && u.Transfer != null)
            {
                // we need to keep the transfer one.
                swap = true;
            }
            else if (t.Transfer != null && u.Transfer == null)
            {
                // we need to keep the transfer one.
                swap = false;
            }
            else if (t.Unaccepted && !u.Unaccepted && t.Transfer == null)
            {
                // switch them around then.
                swap = true;
            }
            if (swap)
            {
                Transaction z = t; t = u; u = z;
            }
            if (!promptForConfirmation || MessageBoxEx.Show("Are you sure you want to merge these transactions?", "Warning", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    if (!t.HasAttachment && u.HasAttachment)
                    {
                        // Move attachments from u to t.
                        AttachmentManager mgr = this.ServiceProvider.GetService(typeof(AttachmentManager)) as AttachmentManager;
                        mgr.MoveAttachments(u, t);
                    }
                    t.Merge(u);
                    DeleteTransaction(u, false);
                    TheActiveGrid.SelectedItem = t;
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        enum DropType
        {
            Transaction,
            File
        }

        private void SetDragDropStyles(DataGridRow row, DropType type)
        {
            Transaction target = row.Item as Transaction;
            if (target != null)
            {
                switch (type)
                {
                    case DropType.Transaction:
                        target.TransactionDropTarget = true;
                        break;
                    case DropType.File:
                        target.AttachmentDropTarget = true;
                        break;
                    default:
                        break;
                }
            }
        }

        private void ClearDragDropStyles(DataGridRow row)
        {
            Transaction target = row.Item as Transaction;
            if (target != null)
            {
                target.TransactionDropTarget = false;
                target.AttachmentDropTarget = false;
            }
        }

        private void OnDataGridRowDragOver(object sender, DragEventArgs e)
        {
            OnDataGridRowDragEnter(sender, e);
        }

        private void OnDataGridRowDragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;

            DataGridRow row = (DataGridRow)sender;
            if (!row.IsEditing)
            {
                if (e.Data.GetDataPresent(typeof(Transaction)))
                {
                    Transaction t = e.Data.GetData(typeof(Transaction)) as Transaction;
                    if (t != null)
                    {
                        Transaction target = row.Item as Transaction;
                        if (target != null && t != target && t.Amount == target.Amount)
                        {
                            SetDragDropStyles(row, DropType.Transaction);
                            e.Effects = DragDropEffects.Move;
                        }
                    }
                }
                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var transaction = row.Item as Transaction;
                    if (transaction != null && transaction.HasAttachment == false)
                    {
                        var files = (string[])e.Data.GetData(DataFormats.FileDrop);

                        if (files.Length == 1 && IsValidAttachmentExtension(files[0]))
                        {
                            SetDragDropStyles(row, DropType.File);
                            e.Effects = DragDropEffects.Copy;
                        }
                    }
                }
            }
        }

        private bool IsValidAttachmentExtension(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            if (extension == ".jpg" || extension == ".png" || extension == ".gif" || extension == ".bmp")
                return true;
            return false;
        }

        private void OnDataGridRowDragLeave(object sender, DragEventArgs e)
        {
            DataGridRow row = (DataGridRow)sender;
            ClearDragDropStyles(row);
        }

        private bool isEditing;

        private bool IsEditing
        {
            get { return isEditing; }
            set
            {
                isEditing = value;
                ttf.IsEnabled = !value;
            }
        }

        private void OnBeginEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            IsEditing = true;
            DataGrid grid = (DataGrid)sender;
            DataGridColumn column = e.Column;
            DataGridRow row = e.Row;
            Transaction t = row.Item as Transaction;
            if (t != null && t.IsReadOnly)
            {
                e.Cancel = true;
                return;
            }
        }


        #endregion

        #region IView Interface implementation

        public MyMoney Money
        {
            get { return myMoney; }
            set
            {
                if (this.myMoney != null)
                {
                    myMoney.Changed -= new EventHandler<ChangeEventArgs>(OnMoneyChanged);
                }
                myMoney = value;
                if (value != null)
                {
                    myMoney.Changed += new EventHandler<ChangeEventArgs>(OnMoneyChanged);
                }
                this.TheActiveGrid.ClearItemsSource();

                if (this.IsVisible && currentDisplayName != TransactionViewName.None)
                {
                    InvalidateDisplay();
                }
            }
        }

        public void ActivateView()
        {

            this.Focus();
        }

        public event EventHandler BeforeViewStateChanged;
        public event EventHandler<AfterViewStateChangedEventArgs> AfterViewStateChanged;

        Account activeAccount;
        public Account ActiveAccount
        {
            get { return this.activeAccount; }
        }


        Payee activePayee;
        public Payee ActivePayee
        {
            get { return this.activePayee; }
        }

        Category activeCategory;
        public Category ActiveCategory
        {
            get { return this.activeCategory; }
        }

        Security activeSecurity;
        public Security ActiveSecurity
        {
            get { return this.activeSecurity; }
        }

        RentBuilding activeRental;
        public RentBuilding ActiveRental
        {
            get { return this.activeRental; }
        }

        public string Caption
        {
            get { return this.caption; }
            set
            {
                if (this.caption != value)
                {
                    this.caption = value;
                }
            }
        }

        public void Commit()
        {
            if (IsEditing)
            {
                TheActiveGrid.CommitEdit();
                // bugbug: change in behavior in Windows 8, you have to call commit twice to really commit!!!!!!!!!
                TheActiveGrid.CommitEdit();
            }
        }

        public bool CheckTransfers()
        {
            Commit();
            List<Transaction> dangling = myMoney.CheckTransfers();
            if (dangling.Count > 0)
            {
                if (MessageBoxEx.Show(
                            "Dangling transfers have been found, do you want to fix them now?",
                            "Dangling Transfers", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.OK)
                {
                    ShowDanglingTransfers(dangling);
                    return false; // tell caller we found transfers, and user wants to fix them.
                }
            }
            return true;
        }

        private void ShowDanglingTransfers(List<Transaction> dangling)
        {
            FireBeforeViewStateChanged();
            this.fixedList = null;
            this.lastQuery = null;
            SwitchLayout("TheGrid_TransactionFromDetails");
            SetActiveAccount(null, null, null, null, null);
            Account danglingAccount = new Account() { Type = AccountType.Cash };
            IList data = new TransactionCollection(myMoney, danglingAccount, dangling, true, false, null);
            Display(data, TransactionViewName.DanglingTransfers, "Dangling transfers", SelectedRowId);
            FireAfterViewStateChanged(SelectedRowId);
        }

        // request a new selected row on view refresh
        private long requestedRowId;

        public ViewState ViewState
        {
            get
            {
                TransactionViewState vs = new TransactionViewState();
                vs.ViewOneLineView = this.OneLineView;
                vs.ViewAllSplits = this.ViewAllSplits;
                vs.TransactionFilter = this.TransactionFilter;
                vs.QuickFilter = this.QuickFilter;
                vs.CurrentDisplayName = this.currentDisplayName;
                vs.Query = this.lastQuery;
                vs.CustomList = this.fixedList;

                if (this.TheActiveGrid != null)
                {
                    vs.SelectedRow = this.SelectedRowId;
                }

                if (this.ActiveAccount != null) vs.Account = this.ActiveAccount.Name;
                if (this.ActiveCategory != null) vs.Category = this.ActiveCategory.Name;
                if (this.ActivePayee != null) vs.Payee = this.ActivePayee.Name;
                if (this.ActiveSecurity != null) vs.Security = this.ActiveSecurity.Name;
                if (this.ActiveRental != null) vs.Rental = this.ActiveRental.Name;

                if (vs.Account == null && vs.Category == null && vs.Payee == null && vs.Security == null && vs.Rental == null)
                {
                    // then this is an uninitialized TransactionView, so return null!
                    return null;
                }

                vs.TabIndex = this.selectedTab;
                return vs;
            }

            set
            {
                TransactionViewState state = value as TransactionViewState;
                if (state != null)
                {
                    viewStateLock++;
                    this.OneLineView = state.ViewOneLineView;
                    this.ViewAllSplits = state.ViewAllSplits;
                    this.QuickFilterUX.FilterText = this.quickFilter = state.QuickFilter;
                    SetTransactionFilterNoUpdate(state.TransactionFilter);
                    this.InvestmentAccountTabs.SelectedIndex = this.selectedTab = state.TabIndex;

                    Account account = this.myMoney.Accounts.FindAccount(state.Account);
                    Payee payee = this.myMoney.Payees.FindPayee(state.Payee, false);
                    Security security = this.myMoney.Securities.FindSecurity(state.Security, false);
                    Category category = this.myMoney.Categories.FindCategory(state.Category);
                    RentBuilding building = this.myMoney.Buildings.FindByName(state.Rental);
                    SetActiveAccount(account, category, payee, security, building);

                    this.currentDisplayName = state.CurrentDisplayName;
                    this.fixedList = state.CustomList;
                    this.requestedRowId = state.SelectedRow;
                    this.lastQuery = state.Query;

                    if (state.Query != null)
                    {
                        IsQueryPanelDisplayed = true;
                        QueryPanel.Clear();
                        QueryPanel.AddQuery(state.Query);
                        QueryPanel.OnShow();
                    }

                    UpdateView();

                    viewStateLock--;
                }
            }
        }

        private void UpdateView()
        {
            switch (this.currentDisplayName)
            {
                case TransactionViewName.Account:
                    if (this.activeAccount != null)
                    {
                        this.ViewTransactionsForSingleAccount(this.activeAccount, TransactionSelection.Specific, requestedRowId);
                    }
                    break;
                case TransactionViewName.ByPayee:
                    if (this.activePayee != null)
                    {
                        this.ViewTransactionsForPayee(this.activePayee, requestedRowId);
                    }
                    break;
                case TransactionViewName.BySecurity:
                    if (this.activeSecurity != null)
                    {
                        this.ViewTransactionsForSecurity(this.activeSecurity, requestedRowId);
                    }
                    break;
                case TransactionViewName.ByCategory:
                case TransactionViewName.ByCategoryCustom:
                    if (this.activeCategory != null)
                    {
                        this.ViewTransactionsForCategory(this.activeCategory, requestedRowId);
                    }
                    break;
                case TransactionViewName.Rental:
                    if (this.activeRental != null)
                    {
                        this.ViewTransactionsByRental(this.activeRental);
                        this.SelectedRowId = requestedRowId;
                    }
                    break;
                case TransactionViewName.Portfolio:
                    ShowInvestmentPortfolio(this.activeAccount);
                    SwitchLayout("InvestmentPortfolioView");
                    break;
                case TransactionViewName.ByQuery:
                    if (this.lastQuery != null)
                    {
                        ViewTransactionsForAdvancedQuery(this.lastQuery);
                    }
                    else if (this.activeAccount != null)
                    {
                        this.ViewTransactionsForSingleAccount(this.activeAccount, TransactionSelection.Specific, requestedRowId);
                    }
                    break;
                case TransactionViewName.Custom:
                    ViewTransactions(this.fixedList);
                    break;
                case TransactionViewName.Transfers:
                    ViewTransfers(this.activeAccount);
                    break;
                case TransactionViewName.DanglingTransfers:
                    List<Transaction> dangling = myMoney.CheckTransfers();
                    ShowDanglingTransfers(dangling);
                    break;
                default:
                    break;
            }

            ShowBalance();
        }

        public bool IsReconciling { get { return this.reconciling; } }

        private ViewState beforeState;

        private Dictionary<Transaction, TransactionStatus> reconcilingTransactions;

        public void OnStartReconcile(Account a)
        {
            this.reconciling = true;
            beforeState = this.ViewState;
            SetTransactionFilterNoUpdate(TransactionFilter.Unreconciled);
            this.OneLineView = true;
            reconcilingTransactions = new Dictionary<Transaction, TransactionStatus>();
            // we normally scroll to the end, but this time we scroll to the top to show transactions most likely to be reconciled.
            this.ViewTransactionsForSingleAccount(a, TransactionSelection.First, 0);
        }

        public void OnEndReconcile(bool cancelled, bool hasStatement)
        {
            this.reconciling = false;
            this.ViewState = beforeState;
            SetReconciledState(cancelled);
            if (!cancelled)
            {
                SetHasStatement(hasStatement);
            }
            reconcilingTransactions = null;
            this.StatmentReconcileDateEnd = null;
        }

        private void SetHasStatement(bool hasStatement)
        {
            this.myMoney.Transactions.BeginUpdate(false);
            try
            {
                // Clear reconciling flags and set reconciled date.            
                foreach (Transaction t in reconcilingTransactions.Keys)
                {
                    t.HasStatement = hasStatement;
                }
            }
            finally
            {
                this.myMoney.Transactions.EndUpdate();
            }
        }

        private void SetReconciledState(bool cancelled)
        {
            this.myMoney.Transactions.BeginUpdate(false);
            try
            {
                // Clear reconciling flags and set reconciled date.            
                foreach (Transaction t in reconcilingTransactions.Keys)
                {
                    t.IsReconciling = false;
                    if (cancelled)
                    {
                        t.Status = reconcilingTransactions[t];
                        if (t.Status != TransactionStatus.Reconciled)
                        {
                            t.ReconciledDate = null;
                        }
                    }
                }
            }
            finally
            {
                this.myMoney.Transactions.EndUpdate();
            }
        }

        /// <summary>
        /// Show existing reconciled states for given statement date
        /// </summary>
        private void ShowReconciledState(DateTime statementDate)
        {
            SetReconciledState(false);
            reconcilingTransactions = new Dictionary<Transaction, TransactionStatus>();

            this.myMoney.Transactions.BeginUpdate(false);
            try
            {
                Account account = this.ActiveAccount;
                if (account != null)
                {
                    foreach (Transaction t in this.myMoney.Transactions.GetTransactionsFrom(this.ActiveAccount))
                    {
                        if (t.ReconciledDate.HasValue && t.Status == TransactionStatus.Reconciled)
                        {
                            DateTime dt = t.ReconciledDate.Value;

                            if (dt.Year == statementDate.Year && dt.Month == statementDate.Month)
                            {
                                // dt.Day == statementDate.Day
                                t.IsReconciling = true;
                                reconcilingTransactions[t] = t.Status;
                            }
                        }
                    }
                }
            }
            finally
            {
                this.myMoney.Transactions.EndUpdate();
            }
        }

        private DateTime? StatmentReconcileDateBegin;
        private DateTime? StatmentReconcileDateEnd;
        private bool IsLatestStatement;

        public void SetReconcileDateRange(DateTime begin, DateTime end, bool isLatest)
        {
            if (begin == DateTime.MinValue && end == DateTime.MinValue)
            {
                return;
            }
            IsLatestStatement = isLatest;
            StatmentReconcileDateBegin = begin;
            StatmentReconcileDateEnd = end;

            if (this.ActiveAccount != null)
            {
                // we normally scroll to the end, but this time we scroll to the top to show transactions most likely to be reconciled.
                ViewTransactionsForSingleAccount(this.ActiveAccount, TransactionSelection.First, 0);
            }

            ShowReconciledState(end);
        }

        public IEnumerable Rows
        {
            get
            {
                return this.TheActiveGrid.ItemsSource;
            }
        }

        public object SelectedRow
        {
            get { return this.TheActiveGrid.SelectedItem; }
            set { this.TheActiveGrid.SelectedItem = value; }
        }

        public int SelectedRowIndex
        {
            get { return this.TheActiveGrid.SelectedIndex; }
            set { this.TheActiveGrid.SelectedIndex = value; }
        }

        public Transaction SelectedTransaction
        {
            get { return this.SelectedRow as Transaction; }
            set
            {
                TransactionCollection c = TheActiveGrid.ItemsSource as TransactionCollection;
                if (c != null)
                {
                    if (!c.Contains(value))
                    {
                        c.Add(value);
                    }
                    this.SelectedRow = value;
                }
            }
        }

        public IServiceProvider ServiceProvider
        {
            get { return this.site; }
            set
            {
                if (this.site != value)
                {
                    this.site = value;
                    //TODO
                    //this.grid.ServiceProvider = value;
                    OnSiteChanged();
                }
            }
        }

        private void OnSiteChanged()
        {
            if (this.site != null)
            {
                this.myMoney = (MyMoney)this.site.GetService(typeof(MyMoney));
            }
        }


        private int viewStateLock;

        public ViewState DeserializeViewState(XmlReader reader)
        {
            return TransactionViewState.Deserialize(reader);
        }

        private void ShowStatus(string text)
        {
            if (this.site != null)
            {
                IStatusService status = (IStatusService)this.site.GetService(typeof(IStatusService));
                if (status != null)
                {
                    status.ShowMessage(text);
                }
            }
        }

        public void ViewTransfers(Account a)
        {
            this.lastQuery = null;
            this.fixedList = null;
            IList<Transaction> data = myMoney.Transactions.FindTransfersToAccount(a);
            SetActiveAccount(null, null, null, null, null);
            SwitchLayout("TheGrid_TransactionFromDetails");
            Display(new TransactionCollection(myMoney, a, data, true, false, null), TransactionViewName.Transfers, "Transfers to " + a.Name, SelectedRowId);
        }

        #region VIEWS

        private TransactionCollection GetTransactionsIncluding(Account a, bool accountChanged, bool filterOnInvestmentInfo, long selectedRowId)
        {
            var data = this.myMoney.Transactions.GetTransactionsFrom(a, GetTransactionIncludePredicate());
            bool found = (from t in data where t.Id == selectedRowId select t).Any();
            if (!found && accountChanged && TransactionFilter != TransactionFilter.All && !this.programaticFilterChange)
            {
                // remove transaction filter, we are probably jumping across accounts, and the filter on the target account makes the 
                // desired transaction unreachable.
                SetTransactionFilterNoUpdate(TransactionFilter.All);
                data = this.myMoney.Transactions.GetTransactionsFrom(a, GetTransactionIncludePredicate());
            }
            return new TransactionCollection(this.myMoney, this.ActiveAccount, data, false, filterOnInvestmentInfo, this.QuickFilter);
        }

        internal void ViewTransactionsForSingleAccount(Account a, TransactionSelection selection, long selectedRowId)
        {
            if (a != null)
            {
                if (a == this.activeAccount && selection == TransactionSelection.Current && this.currentDisplayName == TransactionViewName.Account &&
                    this.InvestmentAccountTabs.SelectedIndex == this.selectedTab)
                {
                    // already viewing this account.
                    return; 
                }

#if PerformanceBlocks
                using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.ViewTransactions))
                {
#endif
                    Commit();
                    FireBeforeViewStateChanged();

                    this.fixedList = null;
                    this.lastQuery = null;
                    bool accountChanged = this.ActiveAccount != a;
                    long currentRowId = this.SelectedRowId;

                    this.SetActiveAccount(a, null, null, null, null);
                    string layout = this.TheActiveGrid.Name;
                    IList data = null;

                    if (a.Type == AccountType.Brokerage || a.Type == AccountType.Retirement)
                    {
                        if (this.InvestmentAccountTabs.SelectedIndex == PortfolioTab)
                        {
                            layout = "InvestmentPortfolioView";
                            UpdatePortfolio(this.ActiveAccount);
                        }
                        else
                        {
                            layout = "TheGrid_InvestmentActivity";
                            data = GetTransactionsIncluding(a, accountChanged, true, selectedRowId);
                        }
                    }
                    else
                    {
                        layout = "TheGrid_BankTransactionDetails";
                        data = GetTransactionsIncluding(a, accountChanged, false, selectedRowId);
                    }

                    SwitchLayout(layout);

                    if (data != null)
                    {
                        switch (selection)
                        {
                            case TransactionSelection.Specific:
                                if (selectedRowId < 0)
                                {
                                    goto case TransactionSelection.Last;
                                }
                                break;
                            case TransactionSelection.First:
                                if (data.Count > 0)
                                {
                                    Transaction t = data[0] as Transaction;
                                    selectedRowId = t.Id;
                                }
                                break;
                            case TransactionSelection.Last:
                                if (data.Count > 0)
                                {
                                    Transaction t = data[data.Count - 1] as Transaction;
                                    selectedRowId = t.Id;
                                }
                                break;
                            case TransactionSelection.Current:
                                if (currentRowId < 0)
                                {
                                    goto case TransactionSelection.Last;
                                }
                                else
                                {
                                    bool exists = false;
                                    foreach (Transaction t in data)
                                    {
                                        if (t.Id == currentRowId)
                                        {
                                            exists = true;
                                            selectedRowId = currentRowId;
                                            break;
                                        }
                                    }
                                    if (!exists)
                                    {
                                        goto case TransactionSelection.Last;
                                    }
                                }
                                break;
                        }
                        Display(data, TransactionViewName.Account, a.Name, selectedRowId);
                    }

                    FireAfterViewStateChanged(selectedRowId == SelectedRowId ? SelectedRowId : -1);
#if PerformanceBlocks
                }
#endif
            }
        }


        internal void ViewTransactions(IEnumerable<Transaction> toView, string filter = "")
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.ViewTransactions))
            {
#endif
                FireBeforeViewStateChanged();

                List<Transaction> newList = new List<Transaction>();
                Account account = null;
                this.lastQuery = null;
                bool multiple = false;
                bool skipped = false;
                if (toView != null)
                {
                    foreach (Transaction t in toView)
                    {
                        if (t.IsDeleted)
                        {
                            skipped = true;
                            continue;
                        }
                        newList.Add(t);
                        if (account == null)
                        {
                            account = t.Account;
                        }
                        else if (account != t.Account)
                        {
                            multiple = true;
                        }
                    }
                }
                else
                {
                    //???
                }

                if (!skipped && this.fixedList == toView && this.programaticFilterChange)
                {
                    // then user is filtering a previously set fixed list.
                    var includeFilter = GetTransactionIncludePredicate();
                    newList = new List<Transaction>(from t in newList where includeFilter(t) && !t.IsDeleted select t);
                }

                TransactionViewName viewName = TransactionViewName.Custom;

                bool includeInvestmentInfo = false;

                this.fixedList = newList;
                this.quickFilter = filter;

                SetTransactionFilterNoUpdate(TransactionFilter.Custom);
                Commit();
                string layout = this.TheActiveGrid.Name;

                if (!multiple)
                {
                    if (account != null && (account.Type == AccountType.Brokerage || account.Type == AccountType.Retirement))
                    {
                        includeInvestmentInfo = true;
                        layout = "TheGrid_InvestmentActivity";
                        //data = GetTransactionsIncluding(account, accountChanged, includeInvestmentInfo, -1);
                    }
                    else
                    {
                        layout = "TheGrid_BankTransactionDetails";
                        //data = GetTransactionsIncluding(account, accountChanged, includeInvestmentInfo, -1);
                    }
                }
                else
                {
                    account = null;
                    layout = "TheGrid_TransactionFromDetails";
                }

                SwitchLayout(layout);
                SetActiveAccount(account, null, null, null, null);
                IList data = new TransactionCollection(myMoney, account, newList, true, includeInvestmentInfo, filter);
                Display(data, viewName, account != null ? account.Name : "Transactions", SelectedRowId);
                FireAfterViewStateChanged(SelectedRowId);

#if PerformanceBlocks
            }
#endif
        }

        internal void ViewTransactionsForPayee(Payee p, long selectedRowId)
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.ViewTransactions))
            {
#endif
                // make sure it's  real payee
                p = this.myMoney.Payees.FindPayee(p.Name, false);
                if (p != null)
                {
                    FireBeforeViewStateChanged();
                    this.fixedList = null;
                    this.lastQuery = null;
                    SwitchLayout("TheGrid_TransactionFromDetails");
                    SetActiveAccount(null, null, p, null, null);
                    IList<Transaction> transactions = myMoney.Transactions.GetTransactionsByPayee(p, GetTransactionIncludePredicate());
                    var data = new TransactionCollection(myMoney, null, transactions, true, false, this.QuickFilter);
                    Display(data, TransactionViewName.ByPayee, "Payments to " + p.Name, selectedRowId);
                    FireAfterViewStateChanged(selectedRowId);
                }
#if PerformanceBlocks
            }
#endif
        }

        internal void ViewTransactionsForSecurity(Security s, long selectedRowId)
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.ViewTransactions))
            {
#endif
                if (s != null)
                {
                    FireBeforeViewStateChanged();
                    this.fixedList = null;
                    this.lastQuery = null;
                    var transactions = RefreshViewBySecurity(s, selectedRowId);
                    FireAfterViewStateChanged(selectedRowId);

                    // Async load of security info.
                    var mgr = (StockQuoteManager)site.GetService(typeof(StockQuoteManager));
                    if (mgr != null)
                    {
                        mgr.HistoryAvailable -= OnStockHistoryAvailable;
                        mgr.HistoryAvailable += OnStockHistoryAvailable;
                        if (!string.IsNullOrEmpty(s.Symbol))
                        {
                            mgr.BeginDownloadHistory(s.Symbol);
                        }
                    }
                }
#if PerformanceBlocks
            }
#endif
        }

        public IList<Transaction> RefreshViewBySecurity(Security s, long selectedRowId)
        {
            SwitchLayout("TheGrid_BySecurity");
            SetActiveAccount(null, null, null, s, null);
            IList<Transaction> transactions = myMoney.Transactions.GetTransactionsBySecurity(s, GetTransactionIncludePredicate());
            var data = new TransactionCollection(myMoney, null, transactions, true, false, this.QuickFilter);
            Display(data, TransactionViewName.BySecurity, "Investments in " + s.Name, selectedRowId);
            return transactions;
        }

        private void OnStockHistoryAvailable(object sender, StockQuoteHistory e)
        {
            if (this.ActiveSecurity != null && e != null && this.ActiveSecurity.Symbol == e.Symbol)
            {
                TransactionCollection tc = this.TheActiveGrid.ItemsSource as TransactionCollection;
                if (tc != null)
                {
                    FillinMissingUnitPrices(this.ActiveSecurity, tc.GetOriginalTransactions());
                }
            }
        }

        async void FillinMissingUnitPrices(Security security, IEnumerable<Transaction> transactions)
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.UpdateStockQuoteHistory))
            {
#endif
                StockQuoteManager manager = (StockQuoteManager)this.ServiceProvider.GetService(typeof(StockQuoteManager));
                StockQuoteHistory history = await manager.GetCachedHistory(security.Symbol);
                if (history != null)
                {
                    List<StockQuote> sorted = history.GetSorted();

                    // Ok, so the list of transactions should all be investments related to this security and 
                    // we have a stock quote history which can be used to fill in any missing details on the
                    // UnitPrices associated with this stock (for example, Dividends may be missing UnitPrice).
                    int pos = 0;
                    bool changed = false;
                    try
                    {
                        this.myMoney.BeginUpdate(this);
                        StockQuote quote = sorted[0];
                        foreach (Transaction t in transactions)
                        {
                            Investment i = t.Investment;
                            if (i != null && i.Security == security)
                            {
                                while (pos < sorted.Count)
                                {
                                    StockQuote nextQuote = sorted[pos];
                                    if (nextQuote.Date > t.Date)
                                    {
                                        break;
                                    }
                                    quote = nextQuote;
                                    pos++;
                                }
                                if (t.Date >= quote.Date)
                                {
                                    if (i.UnitPrice == 0)
                                    {
                                        i.UnitPrice = quote.Close;
                                        changed = true;
                                    }
                                    else if (i.UnitPrice != quote.Close)
                                    {
                                        if (i.TradeType == InvestmentTradeType.Buy || i.TradeType == InvestmentTradeType.Sell)
                                        {
                                            // normal for this to be a bit different, should we do a a sanity check though?
                                        }
                                        else
                                        {
                                            Debug.WriteLine(string.Format("{0}: {1} close price {2} on {3} didn't match our transaction at {4} UnitPrice {5}",
                                                t.Account.Name, security.Symbol, quote.Close, quote.Date.ToShortDateString(), t.Date.ToShortDateString(), t.InvestmentUnitPrice));
                                        }
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        this.myMoney.EndUpdate();
                    }
                    if (changed && this.ActiveSecurity == security)
                    {
                        this.RefreshViewBySecurity(security, this.SelectedRowId);
                    }
                }
#if PerformanceBlocks
            }
#endif
        }

        internal void ViewTransactionsForCategory(Category c, long selectedRowId)
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.ViewTransactions))
            {
#endif
                if (c != null)
                {
                    FireBeforeViewStateChanged();
                    this.fixedList = null;
                    this.lastQuery = null;
                    SwitchLayout("TheGrid_TransactionFromDetails");
                    SetActiveAccount(null, c, null, null, null);
                    IList<Transaction> transactions = myMoney.Transactions.GetTransactionsByCategory(c, GetTransactionIncludePredicate());
                    var data = new TransactionCollection(myMoney, null, transactions, true, false, this.QuickFilter);
                    Display(data, TransactionViewName.ByCategory, "Transactions by Category " + c.Name, selectedRowId);
                    FireAfterViewStateChanged(selectedRowId);
                }
#if PerformanceBlocks
            }
#endif
        }

        internal void ViewTransactionsForCategory(Category c, IEnumerable<Transaction> list)
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.ViewTransactions))
            {
#endif
                if (c != null)
                {
                    FireBeforeViewStateChanged();
                    this.fixedList = null;
                    this.lastQuery = null;
                    SwitchLayout("TheGrid_TransactionFromDetails");
                    SetActiveAccount(null, c, null, null, null);
                    var data = new TransactionCollection(myMoney, null, list, true, false, this.QuickFilter);
                    Display(data, TransactionViewName.ByCategoryCustom, "Transactions by Category " + c.Name, SelectedRowId);
                    FireAfterViewStateChanged(SelectedRowId);
                }

#if PerformanceBlocks
            }
#endif
        }

        internal void ViewTransactionsForPayee(Payee p, IEnumerable<Transaction> list)
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.ViewTransactions))
            {
#endif
                if (p != null)
                {
                    FireBeforeViewStateChanged();
                    this.fixedList = null;
                    this.lastQuery = null;
                    SwitchLayout("TheGrid_TransactionFromDetails");
                    SetActiveAccount(null, null, p, null, null);
                    var data = new TransactionCollection(myMoney, null, list, true, false, this.QuickFilter);
                    Display(data, TransactionViewName.ByCategoryCustom, "Transactions by Payee " + p.Name, SelectedRowId);
                    FireAfterViewStateChanged(SelectedRowId);
                }

#if PerformanceBlocks
            }
#endif
        }

        QueryRow[] lastQuery;

        internal void ViewTransactionsForAdvancedQuery(QueryRow[] query)
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.ViewTransactions))
            {
#endif
                this.FireBeforeViewStateChanged();
                this.fixedList = null;
                this.SetActiveAccount(null, null, null, null, null);
                SwitchLayout("TheGrid_TransactionFromDetails");

                this.lastQuery = query;
                IList<Transaction> data = myMoney.Transactions.ExecuteQuery(query);
                this.Display(new TransactionCollection(myMoney, null, data, true, false, this.QuickFilter),
                    TransactionViewName.ByQuery, string.Empty, SelectedRowId);
                SelectedRowIndex = TheActiveGrid.Items.Count - 1;
                this.FireAfterViewStateChanged(SelectedRowId);

#if PerformanceBlocks
            }
#endif
        }

        internal void ViewInvestmentPortfolio()
        {
            // Show a special summary view of all investment positions.
            FireBeforeViewStateChanged();
            SwitchLayout("InvestmentPortfolioView");
            SetActiveAccount(null, null, null, null, null);
            ShowInvestmentPortfolio(null);
            FireAfterViewStateChanged(SelectedRowId);
        }

        PortfolioReport portfolioReport;

        internal DateTime GetReconiledExclusiveEndDate()
        {
            if (this.StatmentReconcileDateEnd.HasValue)
            {
                DateTime date = this.StatmentReconcileDateEnd.Value;
                return date.AddDays(1);
            }
            return DateTime.Now;
        }

        internal void ShowInvestmentPortfolio(Account account)
        {
            FireBeforeViewStateChanged();
            UpdatePortfolio(account);
            FireAfterViewStateChanged(SelectedRowId);
        }

        private void UpdatePortfolio(Account account)
        {
            currentDisplayName = TransactionViewName.Portfolio;
            layout = "InvestmentPortfolioView";

            FlowDocumentView view = this.InvestmentPortfolioView;
            HelpService.SetHelpKeyword(view, "Investment Portfolio");
            SetActiveAccount(account, null, null, null, null);
            // if we are reconciling then show the positions held at statement date so the stock balances can be reconciled also.
            DateTime reportDate = this.IsReconciling ? GetReconiledExclusiveEndDate() : DateTime.Now;
            PortfolioReport report = new PortfolioReport(view, this.myMoney, account, this.ServiceProvider, reportDate);
            report.DrillDown += OnReportDrillDown;
            _ = view.Generate(report);
            portfolioReport = report;
        }

        private void OnReportDrillDown(object sender, SecurityGroup e)
        {
            // create new report just for this drill down in security group.
            FireBeforeViewStateChanged();

            currentDisplayName = TransactionViewName.Portfolio;
            layout = "InvestmentPortfolioView";

            FlowDocumentView view = this.InvestmentPortfolioView;
            HelpService.SetHelpKeyword(view, "Investment Portfolio");
            DateTime reportDate = this.IsReconciling ? GetReconiledExclusiveEndDate() : DateTime.Now;
            PortfolioReport report = new PortfolioReport(view, this.myMoney, this.ServiceProvider, reportDate, e);
            _ = view.Generate(report);
            FireAfterViewStateChanged(SelectedRowId);
        }

        internal void ViewTransactionsByRental(RentBuilding building)
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.ViewTransactions))
            {
#endif
                if (building != null)
                {
                    this.FireBeforeViewStateChanged();
                    this.fixedList = null;
                    this.lastQuery = null;
                    SwitchLayout("TheGrid_TransactionFromDetails");
                    this.SetActiveAccount(null, null, null, null, building);
                    this.FireAfterViewStateChanged(SelectedRowId);
                }
#if PerformanceBlocks
            }
#endif
        }

        internal void ViewTransactionRentalBuildingSingleYearDepartment(RentalBuildingSingleYearSingleDepartment contextToView)
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.ViewTransactions))
            {
#endif
                this.FireBeforeViewStateChanged();
                this.fixedList = null;
                this.lastQuery = null;
                SwitchLayout("TheGrid_TransactionFromDetails");
                this.SetActiveAccount(null, null, null, null, contextToView.Building);
                IList<Transaction> data = myMoney.Transactions.GetTransactionsByCategory(contextToView.DepartmentCategory, contextToView.Year, false);
                this.Display(new TransactionCollection(myMoney, null, data, true, false, this.QuickFilter),
                    TransactionViewName.Rental, string.Format("{0} {1}", contextToView.Building.Name, contextToView.Year), SelectedRowId);
                this.FireAfterViewStateChanged(SelectedRowId);

#if PerformanceBlocks
            }
#endif
        }

        #endregion

        public Predicate<Transaction> GetTransactionIncludePredicate()
        {
            Predicate<Transaction> filter = null;
            switch (TransactionFilter)
            {
                case TransactionFilter.All:
                case TransactionFilter.Custom:
                    filter = new Predicate<Transaction>((t) => { return !t.Account.IsCategoryFund; });
                    break;
                case TransactionFilter.Accepted:
                    filter = new Predicate<Transaction>((t) => { return !t.Unaccepted && !t.Account.IsCategoryFund; });
                    break;
                case TransactionFilter.Unaccepted:
                    filter = new Predicate<Transaction>((t) => { return t.Unaccepted && !t.Account.IsCategoryFund; });
                    break;
                case TransactionFilter.Reconciled:
                    if (!this.IsReconciling)
                    {
                        // We are not in BALANCING mode so use the normal un-reconcile filter (show all transactions that are not reconciled)
                        filter = new Predicate<Transaction>((t) => { return t.Status == TransactionStatus.Reconciled && !t.Account.IsCategoryFund; });
                    }
                    else
                    {
                        // While balancing we need to see the reconciled transactions for the current statement date as well as any 
                        // before or after that are not reconciled.
                        filter = new Predicate<Transaction>((t) =>
                        {
                            return (t.Status == TransactionStatus.Reconciled && !t.Account.IsCategoryFund) ||
                                    t.IsReconciling || IsIncludedInCurrentStatement(t);
                        });
                    }
                    break;
                case TransactionFilter.Unreconciled:
                    if (!this.IsReconciling)
                    {
                        // We are not in BALANCING mode so use the normal un-reconcile filter (show all transactions that are not reconciled)
                        filter = new Predicate<Transaction>((t) => { return t.Status != TransactionStatus.Reconciled && t.Status != TransactionStatus.Void && !t.Account.IsCategoryFund; });
                    }
                    else
                    {
                        // While balancing we need to see the reconciled transactions for the current statement date as well as any 
                        // before or after that are not reconciled.
                        filter = new Predicate<Transaction>((t) =>
                        {
                            return (t.Status != TransactionStatus.Reconciled && t.Status != TransactionStatus.Void && !t.Account.IsCategoryFund) ||
                                    t.IsReconciling || IsIncludedInCurrentStatement(t);
                        });
                    }
                    break;
                case TransactionFilter.Categorized:
                    filter = new Predicate<Transaction>((t) =>
                    {
                        if (t.Status == TransactionStatus.Void)
                        {
                            return false; // no point seeing these
                        }
                        if (t.IsFakeSplit || t.Account.IsCategoryFund)
                        {
                            return false; // this represents a category by definition.
                        }
                        if (t.IsSplit)
                        {
                            return t.Splits.Unassigned == 0; // then all splits are good!
                        }
                        return t.Category != null;
                    });
                    break;
                case TransactionFilter.Uncategorized:
                    filter = new Predicate<Transaction>((t) =>
                    {
                        if (t.Status == TransactionStatus.Void)
                        {
                            return false; // no point seeing these
                        }
                        if (t.IsFakeSplit || t.Account.IsCategoryFund)
                        {
                            return false; // this represents a category by definition.
                        }
                        if (t.IsSplit)
                        {
                            return t.Splits.Unassigned > 0; // then there is more to categorize in the splits!
                        }
                        return (t.Category == null);
                    });
                    break;
            }
            return filter;
        }

        private bool IsIncludedInCurrentStatement(Transaction t)
        {
            if (this.StatmentReconcileDateBegin.HasValue)
            {
                return t.Date >= this.StatmentReconcileDateBegin.Value;
            }

            return true;
        }


        #endregion

        #region ICLIPBOARD
        public bool CanCut
        {
            get
            {
                if (InvestmentPortfolioView.Visibility == System.Windows.Visibility.Visible)
                {
                    return false;
                }
                return SelectedRow != null;
            }
        }
        public bool CanCopy
        {
            get
            {
                if (InvestmentPortfolioView.Visibility == System.Windows.Visibility.Visible)
                {
                    return true;
                }
                return SelectedRow != null;
            }
        }
        public bool CanPaste
        {
            get
            {
                if (InvestmentPortfolioView.Visibility == System.Windows.Visibility.Visible)
                {
                    return false;
                }
                return Clipboard.ContainsText() || (this.SelectedTransaction != null && Clipboard.ContainsImage());
            }
        }
        public bool CanDelete
        {
            get
            {
                if (InvestmentPortfolioView.Visibility == System.Windows.Visibility.Visible)
                {
                    return false;
                }
                return SelectedRow != null;
            }
        }

        public void Cut()
        {
            if (InvestmentPortfolioView.Visibility == System.Windows.Visibility.Visible)
            {
                return;
            }

            this.Commit();
            object selected = this.CopySelection();
            Transaction t = selected as Transaction;
            Split s = selected as Split;
            if (t != null)
            {
                DeleteTransaction(this.TheActiveGrid.SelectedIndex);
            }
            else if (s != null)
            {
                this.SelectedTransaction.NonNullSplits.Remove(s);
            }
        }

        private void DeleteTransaction(int i)
        {
            TransactionCollection data = this.TheActiveGrid.ItemsSource as TransactionCollection;
            if (i >= 0 && i < data.Count)
            {
                data.RemoveAt(i);
            }
        }

        private void DeleteTransaction(Transaction t, bool prompt)
        {
            TransactionCollection data = this.TheActiveGrid.ItemsSource as TransactionCollection;
            bool saved = data.Prompt;
            data.Prompt = prompt;
            data.Remove(t);
            data.Prompt = saved;
        }

        public void Copy()
        {
            if (QueryPanel.Visibility == Visibility.Visible &&
                QueryPanel.ContainsKeyboardFocus())
            {
                QueryPanel.Copy();
            }
            else if (InvestmentPortfolioView.Visibility == System.Windows.Visibility.Visible)
            {
                InvestmentPortfolioView.Copy();
            }
            else
            {
                this.Commit();
                this.CopySelection();
            }
        }

        public void Paste()
        {
            if (Clipboard.ContainsImage())
            {
                if (InvestmentPortfolioView.Visibility == System.Windows.Visibility.Visible)
                {
                    return;
                }

                PasteAttachment();
            }
            else if (QueryPanel.Visibility == Visibility.Visible &&
                QueryPanel.ContainsKeyboardFocus())
            {
                QueryPanel.Paste();
            }
            else if (InvestmentPortfolioView.Visibility == System.Windows.Visibility.Visible)
            {
                return;
            }
            else
            {
                this.Commit();
                this.PasteSelection();
            }
        }

        private void PasteAttachment()
        {
            var selected = this.SelectedTransaction;
            if (selected != null)
            {
                // add a new attachment containing this image.
                var bitmap = Clipboard.GetImage();

                try
                {
                    AttachmentManager mgr = this.ServiceProvider.GetService(typeof(AttachmentManager)) as AttachmentManager;
                    var fileName = mgr.GetUniqueFileName(selected, ".png");
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var stream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                    {
                        encoder.Save(stream);
                    }
                    selected.HasAttachment = true;
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.Message, "Paste Attachment Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var image = new Image() { Source = bitmap };
                double aspectRatio = (bitmap.Height / bitmap.Width);
                image.Width = bitmap.Width;
                image.Height = bitmap.Height;
                // scale it to fit the window.
                if (bitmap.Width > this.ActualWidth * 0.8)
                {
                    double w = this.ActualWidth * 0.8;
                    image.Width = w;
                    image.Height = w * aspectRatio;
                }

                if (bitmap.Height > this.ActualHeight * 0.8)
                {
                    double h = this.ActualHeight * 0.8;
                    image.Height = h;
                    image.Width = h / aspectRatio;
                }

                this.GridOverlayCanvas.Children.Clear();
                this.GridOverlayCanvas.Children.Add(image);
                var row = this.TheActiveGrid.GetRowFromItem(selected);
                if (row != null)
                {
                    Point pos = row.TransformToVisual(this.TransactionsGrid).Transform(new Point(0, 0));
                    // shrink image down to the attachment cell of this row.
                    TransformGroup g = new TransformGroup();
                    MatrixTransform mt = new MatrixTransform();
                    ScaleTransform st = new ScaleTransform();
                    g.Children.Add(st);
                    g.Children.Add(mt);
                    image.RenderTransform = g;

                    double x = (this.ActualWidth - image.Width) / 2;
                    double y = (this.ActualHeight - image.Height) / 2;
                    double dx = pos.X - x;
                    double dy = pos.Y - y;
                    Point start = new Point(x, y);
                    Point point1 = new Point(x + dx * 0.25, y + dy * 0.3);
                    Point point2 = new Point(x + dx * 0.75, y + dy * 0.8);
                    Point point3 = pos;
                    var duration = new Duration(TimeSpan.FromSeconds(0.5));

                    var animation = new System.Windows.Media.Animation.MatrixAnimationUsingPath();
                    animation.PathGeometry = new PathGeometry(new PathFigure[] {
                        new PathFigure(start,
                            new PathSegment[]
                            {
                                new BezierSegment(point1, point2, point3, true)
                            }, false)
                    });
                    animation.Duration = duration;
                    animation.AccelerationRatio = 1;

                    Storyboard.SetTarget(animation, image);
                    Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(MatrixTransform.Matrix)"));

                    var scaleXAnimation = new DoubleAnimation(1.0, 0, duration);
                    scaleXAnimation.AccelerationRatio = 1;
                    Storyboard.SetTarget(scaleXAnimation, image);
                    Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));

                    var scaleYAnimation = new DoubleAnimation(1.0, 0, duration);
                    scaleYAnimation.AccelerationRatio = 1;
                    Storyboard.SetTarget(scaleYAnimation, image);
                    Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));

                    Storyboard s = new Storyboard();
                    s.Children.Add(animation);
                    s.Children.Add(scaleXAnimation);
                    s.Children.Add(scaleYAnimation);
                    s.Begin(image);
                }
                else
                {
                    // hmmm, row is not visible, so the image should animate off the top or bottom of the screen...?
                }
            }
        }

        private void OnImageAnimationComplete(object sender, EventArgs e)
        {
            Debug.WriteLine("done!");
        }

        public void Delete()
        {
            if (InvestmentPortfolioView.Visibility == System.Windows.Visibility.Visible)
            {
                return;
            }
            this.Commit();

            object selected = selectedSplit;
            if (selected != null)
            {
                Transaction parent = this.SelectedTransaction;
                parent.NonNullSplits.Remove((Split)selectedSplit);
            }
            else
            {
                DeleteTransaction(this.TheActiveGrid.SelectedIndex);
            }
        }

        object CopySelection()
        {
            object selected = selectedSplit;
            if (selected == null)
            {
                selected = this.SelectedRow;
            }

            try
            {
                Exporters e = new Exporters();

                string xml = e.ExportString(new object[] { selected });

                if (!string.IsNullOrEmpty(xml))
                {
                    Clipboard.SetDataObject(xml, true);
                }
            }
            catch (Exception e)
            {
                MessageBoxEx.Show(e.Message, "Copy Error", MessageBoxButton.OK, MessageBoxImage.Error);
                selected = null;
            }
            return selected;
        }

        void PasteSelection()
        {

            IDataObject data = Clipboard.GetDataObject();
            if (data.GetDataPresent(typeof(string)))
            {
                string xml = (string)data.GetData(typeof(string));
                try
                {
                    if (this.activeAccount == null)
                    {
                        throw new Exception("Cannot paste transactions into this view because there is no single account associated with it");
                    }

                    TransactionCollection transactions = this.TheActiveGrid.ItemsSource as TransactionCollection;
                    if (transactions == null)
                    {
                        throw new Exception("Paste into investment portfolio is not supported");
                    }

                    Transaction selected = this.SelectedTransaction;
                    StringReader sr = new StringReader(xml);
                    XmlImporter importer = new XmlImporter(myMoney);
                    using (XmlReader r = XmlReader.Create(sr))
                    {
                        Transaction first = null;
                        foreach (object o in importer.ImportObjects(r, this.activeAccount, selected))
                        {
                            Transaction t = o as Transaction;
                            Investment i = o as Investment;
                            Split s = o as Split;

                            if (t != null)
                            {
                                if (first == null)
                                {
                                    first = t;
                                }
                            }
                            else if (i != null)
                            {
                                // not supported.
                            }
                            else if (s != null)
                            {
                                // already added.
                            }

                        }
                        if (first != null)
                        {
                            Refresh();
                            this.SelectedRowId = first.Id;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // clipboard must have contained something else then.
                    MessageBox.Show(ex.Message, "Paste Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        #region METHODS

        private void FireBeforeViewStateChanged()
        {
            if (viewStateLock == 0)
            {
                if (this.BeforeViewStateChanged != null)
                {
                    this.BeforeViewStateChanged(this, new EventArgs());
                }

                if (!quickFilterValueChanging && !refresh)
                {
                    // since we are switching views, clear out the quick filter
                    // (but only if this is not the result of the quick filter being set or simply a refresh!)
                    this.QuickFilterNoRefresh = string.Empty;
                }
            }
        }

        private void FireAfterViewStateChanged(long selectedRowId)
        {
            if (this.AfterViewStateChanged != null)
            {
                this.AfterViewStateChanged(this, new AfterViewStateChangedEventArgs(selectedRowId));
            }
            UpdateUX();
            ShowBalance();
            RemoveInvisibleContent(TheGrid_BankTransactionDetails);
            RemoveInvisibleContent(TheGrid_TransactionFromDetails);
            RemoveInvisibleContent(TheGrid_InvestmentActivity);
            RemoveInvisibleContent(TheGrid_BySecurity);
        }

        private void RemoveInvisibleContent(MoneyDataGrid grid)
        {
            if (grid.Visibility != Visibility.Visible)
            {
                grid.SetItemsSource(null);
            }
        }

        private void SetActiveAccount(Account a, Category c, Payee p, Security s, RentBuilding r)
        {
            this.activeAccount = a;
            this.activeCategory = c;
            this.activePayee = p;
            this.activeSecurity = s;
            this.activeRental = r;

            if (a == null && c == null && p == null && s == null && r == null)
            {
                this.Caption = null;
            }

            //
            // Hide / Show Tabs
            //
            if (a != null && (a.Type == AccountType.Brokerage || a.Type == AccountType.Retirement))
            {
                this.InvestmentAccountTabs.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                this.InvestmentAccountTabs.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        #region SEARCH & FILTERING

        public void FocusQuickFilter()
        {
            QuickFilterUX.FocusTextBox();
        }

        string quickFilter = string.Empty;

        public string QuickFilter
        {
            get { return this.quickFilter; }
            set
            {
                string trimmed = value == null ? null : value.Trim();

                if (InvestmentPortfolioView.Visibility == System.Windows.Visibility.Visible)
                {
                    InvestmentPortfolioView.QuickFilter = trimmed;
                }
                else if (this.quickFilter != trimmed)
                {

                    if (value.StartsWith("*"))
                    {
                        //-----------------------------------------------------
                        // Quick Search  across Multiple Account

                        // Strip away the asterix.
                        QuickFilterNoRefresh = trimmed.Remove(0, 1);

                        // Get all the transaction matching the quick search "text"
                        ViewTransactions(this.myMoney.Transactions.Items, this.quickFilter);
                    }
                    else
                    {
                        //-----------------------------------------------------
                        // Quick search using the current view
                        //
                        QuickFilterNoRefresh = value;

                        TransactionCollection tc = this.Rows as TransactionCollection;
                        // for DisplayName.ByCategoryCustom we need to call Refresh.
                        if (tc != null && currentDisplayName != TransactionViewName.ByCategoryCustom)
                        {
                            object selection = this.TheActiveGrid.SelectedItem;
                            tc.Filter = value;
                            this.TheActiveGrid.SelectedItem = selection;
                        }
                        else
                        {
                            Refresh();
                        }
                    }
                }
                OnQuickFilterChanged();
            }
        }

        public event EventHandler QuickFilterChanged;

        private void OnQuickFilterChanged()
        {
            if (QuickFilterChanged != null)
            {
                QuickFilterChanged(this, EventArgs.Empty);
            }
            ShowBalance();
        }


        public string QuickFilterNoRefresh
        {
            get { return this.QuickFilter; }
            set
            {
                if (this.quickFilter != value)
                {
                    this.quickFilter = value;
                    UpdateUX();
                    // No refresh invoked
                }
            }
        }

        bool quickFilterValueChanging;

        private void OnQuickFilterValueChanged(object sender, string filter)
        {
            quickFilterValueChanging = true;
            this.QuickFilter = filter;
            quickFilterValueChanging = false;
        }

        public readonly static DependencyProperty IsQueryPanelDisplayedProperty = DependencyProperty.Register("IsQueryPanelDisplayed", typeof(bool), typeof(TransactionsView),
          new FrameworkPropertyMetadata(false, OnIsQueryPanelDisplayed)
          );

        private static void OnIsQueryPanelDisplayed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TransactionsView tgv = d as TransactionsView;
            if (d != null)
            {
                bool showAdvancedSearch = (e.NewValue as bool?) == true ? true : false;

                if (showAdvancedSearch)
                {
                    // Set the grid Row definition to AUTO
                    tgv.SearchAreaRow.Height = GridLength.Auto;

                    // Show the main big Query Panel 
                    tgv.QueryPanel.Visibility = System.Windows.Visibility.Visible;
                    tgv.QueryPanel.MinHeight = 120;

                    // Show the RUN-SEARCH button
                    tgv.RunQuery.Visibility = System.Windows.Visibility.Visible;

                    // Show the splitter between the Advanced & Transactions
                    tgv.QueryPanelSplitter.Visibility = System.Windows.Visibility.Visible;
                    tgv.QuerySplitterGridRowHeight.Height = new GridLength(3);
                    tgv.QueryPanel.OnShow();
                }
                else
                {

                    // Hide the main big Query Panel 
                    tgv.QueryPanel.Visibility = System.Windows.Visibility.Collapsed;

                    // Place back the Row Definition height to Auto (we need this because the user may have used the splitter to change the height)
                    tgv.SearchAreaRow.Height = GridLength.Auto;

                    // We don't need the RUN-SEARCH button since the Advance query is now hidden
                    tgv.RunQuery.Visibility = System.Windows.Visibility.Collapsed;

                    // No splitter needed between the QuickFilter and the Transactions
                    tgv.QueryPanelSplitter.Visibility = System.Windows.Visibility.Collapsed;
                    tgv.QuerySplitterGridRowHeight.Height = new GridLength(0);

                    // Reset the list view (Chris: why? what if user wants to run it again in 5 minutes?)
                    // tgv.QueryPanel.Clear();
                }
            }
        }

        public bool IsQueryPanelDisplayed
        {

            get
            {
                return (GetValue(IsQueryPanelDisplayedProperty) as bool?) == true ? true : false;
            }

            set
            {
                SetValue(IsQueryPanelDisplayedProperty, value);
            }

        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            object selected = e.AddedItems[0];

            if (selected == FilterByNothing)
            {
                this.TransactionFilter = TransactionFilter.All;
            }
            else if (selected == FilterByReconciled)
            {
                this.TransactionFilter = TransactionFilter.Reconciled;
            }
            else if (selected == FilterByUnreconciled)
            {
                this.TransactionFilter = TransactionFilter.Unreconciled;
            }
            else if (selected == FilterByAccepted)
            {
                this.TransactionFilter = TransactionFilter.Accepted;
            }
            else if (selected == FilterByUnaccepted)
            {
                this.TransactionFilter = TransactionFilter.Unaccepted;
            }
            else if (selected == FilterByCategorized)
            {
                this.TransactionFilter = TransactionFilter.Categorized;
            }
            else if (selected == FilterByUncategorized)
            {
                this.TransactionFilter = TransactionFilter.Uncategorized;
            }
            else if (selected == FilterByCustom)
            {
                // do nothing - this one is usually selected programatically.
            }
        }

        private void Border_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            int minWidthForQuickFilter = 300;
            if (this.RunQuery.Visibility == System.Windows.Visibility.Visible)
            {
                minWidthForQuickFilter += 120;
            }

            this.SearchWidgetArea.Visibility = e.NewSize.Width < minWidthForQuickFilter ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

            if (this.IsQueryPanelDisplayed)
            {
                this.RunQuery.Visibility = e.NewSize.Width < 300 ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            }
        }
        #endregion

        private string layout = string.Empty;

        private Hashtable layoutInfo = new Hashtable();

        private bool SwitchLayout(string name)
        {
            this.Commit();

            switch (name)
            {
                case "TheGrid_TransactionFromDetails":
                case "TheGrid_BankTransactionDetails":
                    if (this.ActiveAccount != null && this.ActiveAccount.Type == AccountType.Credit)
                    {
                        HelpService.SetHelpKeyword(this, "Credit Card Accounts");
                    }
                    else
                    {
                        HelpService.SetHelpKeyword(this, "Bank Accounts");
                    }
                    break;
                case "InvestmentPortfolioView":
                    HelpService.SetHelpKeyword(InvestmentPortfolioView, "Investment Portfolio");
                    break;
                case "TheGrid_InvestmentActivity":
                case "TheGrid_BySecurity":
                    HelpService.SetHelpKeyword(this, "Investment Accounts");
                    break;
                default:
                    this.ClearValue(HelpService.HelpKeywordProperty);
                    break;
            }

            if (name == "InvestmentPortfolioView")
            {
                InvestmentPortfolioView.Visibility = Visibility.Visible;
                ToggleExpandAll.IsChecked = false;
                ToggleExpandAll.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                InvestmentPortfolioView.Visibility = Visibility.Collapsed;
                ToggleExpandAll.Visibility = System.Windows.Visibility.Collapsed;
                if (TheActiveGrid != null)
                {
                    TheActiveGrid.Visibility = System.Windows.Visibility.Visible;
                }
            }

            if (TheActiveGrid.Name == name)
            {
                if (TheActiveGrid.Visibility == System.Windows.Visibility.Visible)
                {
                    // We already have the correct Grid Layout and it is being displayed
                    return true;
                }
            }
            TheActiveGrid.ClearItemsSource();

            // Find the matching grid
            MoneyDataGrid dg = this.FindName(name) as MoneyDataGrid;
            if (dg != null)
            {
                bool hasKeyboardFocus = TheActiveGrid.IsKeyboardFocusWithin;
                if (TheActiveGrid != dg)
                {
                    // Hide the current grid
                    TheActiveGrid.Visibility = System.Windows.Visibility.Collapsed;
                    TheActiveGrid = dg;
                }
                // Ensure that the new active grid is visible
                TheActiveGrid.Visibility = System.Windows.Visibility.Visible;
                if (hasKeyboardFocus)
                {
                    TheActiveGrid.Focus(); // and keep the focus on it.
                }
                return true;
            }

            return false;
        }

        List<ChangeEventArgs> pendingUpdates = new List<ChangeEventArgs>();

        private void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
            if (rebalancing)
            {
                return;
            }
            // concatenate the updates so we can absorb an event storm here (e.g. OFX download).
            lock (pendingUpdates)
            {
                pendingUpdates.Add(args);
            }
            delayedUpdates.StartDelayedAction("ProcessUpdates", HandleChanges, TimeSpan.FromMilliseconds(100));
        }

        void HandleChanges()
        {
            bool rebalance = false;
            List<ChangeEventArgs> list;
            lock (pendingUpdates)
            {
                list = new List<ChangeEventArgs>(pendingUpdates);
                pendingUpdates.Clear();
            }
            MoneyDataGrid grid = TheActiveGrid;
            if (grid != null)
            {
                TransactionCollection tc = grid.ItemsSource as TransactionCollection;
                bool refresh = false;
                foreach (var item in list)
                {
                    var args = item;
                    while (args != null)
                    {
                        if (args.Item is Transaction t)
                        {
                            if (t != null && tc != null)
                            {
                                if (t.Account == this.ActiveAccount || this.ActiveAccount == null)
                                {
                                    if ((args.ChangeType == ChangeType.Deleted || args.ChangeType == ChangeType.Inserted))
                                    {
                                        // optimization tor TransactionCollection
                                        if (args.ChangeSource != null && args.ChangeSource.GetType() == typeof(TransactionCollection))
                                        {
                                            // TransactionCollection will already have taken care of inserts and deletes, so we
                                            // can optimize this case (refresh is slow).
                                            rebalance = t.Amount != 0;
                                        }
                                        else
                                        {
                                            // change came from somewhere else (like OFX import) so need a full refresh.
                                            refresh = true;
                                            rebalance = true;
                                        }
                                    }
                                    else if (args.ChangeType == ChangeType.Changed && args.Name == "Amount")
                                    {
                                        rebalance = true;
                                    }
                                }
                            }
                        }
                        else if (args.Item is Security s)
                        {
                            // ok, this might be the GetStock auto-update, see if there is anything to copy to 
                            // uncommitted payee.
                            if (!string.IsNullOrEmpty(s.Name) && 
                                this.SelectedTransaction != null && this.SelectedTransaction.Investment != null &&
                                this.SelectedTransaction.Investment.Security == s)
                            {
                                string payee = GetUncomittedPayee();
                                if (string.IsNullOrEmpty(payee) && this.SelectedTransaction != null)
                                {
                                    this.SelectedTransaction.Payee = this.myMoney.Payees.FindPayee(s.Name, true);
                                }
                            }
                        }
                        else if (args.Item is Category c)
                        {
                            if (args.Name == "Label" || args.Name == "Color" || args.ChangeType == ChangeType.Deleted)
                            {
                                // then any transactions using this category need to be refreshed..
                                refresh = true;
                            }
                        }
                        else if (args.Item is Account a)
                        {
                            // the "NewPlaceHolder" item may have just been changed into a real Transaction object.
                            // ChangeType.Changed would have already been handled by the INotifyPropertyChanged events, what we really care
                            // about here are transactions being inserted or removed which can happen if you do a background 'download'
                            // for example.
                            // These two change types are not structural, so the normal data binding update of the UI should be enough.
                            if (args.ChangeType != ChangeType.Rebalanced && args.ChangeType != ChangeType.TransientChanged)
                            {
                                if (a != null && a == this.ActiveAccount)
                                {
                                    if (args.ChangeType == ChangeType.Deleted)
                                    {
                                        // then this account is gone, so we need to clear the display.
                                        this.TheActiveGrid.ClearItemsSource();
                                    } 
                                    else if (args.ChangeType == ChangeType.Changed)
                                    {
                                        if (args.Name != "Unaccepted" && args.Name != "LastBalance")
                                        {
                                            // then we need a refresh, may have just loaded a bunch of new transactions from OFX.
                                            refresh = true;
                                            rebalance = true;
                                        }
                                    }
                                }
                                else if (a != null && args.ChangeType == ChangeType.Inserted)
                                {
                                    if (this.ActiveAccount == null && this.myMoney.Accounts.Count == 1)
                                    {
                                        // perhaps it's the first account and it's time to bind the transaction view to an account.
                                        this.currentDisplayName = TransactionViewName.Account;
                                        this.activeAccount = a;
                                        refresh = true;
                                    }
                                }
                            }
                        }
                        args = args.Next;
                    }
                }
                if (rebalance)
                {
                    this.Rebalance();
                }
                if (refresh)
                {
                    InvalidateDisplay();
                }
            }
        }

        private bool isDisplayInvalid;

        private void InvalidateDisplay()
        {
            if (!isDisplayInvalid)
            {
                isDisplayInvalid = true;
                if (!IsEditing)
                {
                    delayedUpdates.StartDelayedAction("Refresh", new Action(() =>
                    {
                        if (!IsEditing)
                        {
                            try
                            {
                                Refresh();
                            }
                            catch
                            {
                            }
                            isDisplayInvalid = false;
                        }
                    }), TimeSpan.FromMilliseconds(10));
                }
            }
        }



        private long GetRowId(object row)
        {
            Transaction t = row as Transaction;
            if (t != null) return t.Id;

            Investment i = row as Investment;
            if (i != null) return i.Id;

            return -1;
        }

        // Get the Id of the selected transaction
        public long SelectedRowId
        {
            get
            {
                object row = this.TheActiveGrid.SelectedItem;
                long id = GetRowId(row);

                // If we can't get a row id off the selected item (or there is no selected item) then
                // user might be off the bottom in the new blank row area, so search back for a row 
                // with an id and remember that one.
                int i = this.TheActiveGrid.Items.Count;
                while (id == -1 && i > 0)
                {
                    i--;
                    id = GetRowId(this.TheActiveGrid.Items[i]);
                }
                return id;
            }
            set
            {
                object row = FindItemById(value);
                if (row != null)
                {
                    // stop our event handler from doing a refresh which is not allowed in this scope.
                    this.TheActiveGrid.SelectionChanged -= new SelectionChangedEventHandler(TheGrid_SelectionChanged);
                    this.TheActiveGrid.SelectedItem = row;
                    this.TheActiveGrid.SelectionChanged += new SelectionChangedEventHandler(TheGrid_SelectionChanged);
                }
            }
        }

        internal object FindItemById(long id)
        {
            return FindItemById(this.TheActiveGrid.ItemsSource, id);
        }


        internal object FindItemById(IEnumerable data, long id)
        {
            if (data != null)
            {
                foreach (object obj in data)
                {
                    Transaction t = obj as Transaction;
                    if (t != null && t.Id == id) return t;
                    Investment i = obj as Investment;
                    if (i != null && i.Id == id) return i;
                }
            }
            return null;
        }

        private TransactionViewName currentDisplayName;

        public TransactionViewName ActiveViewName
        {
            get { return this.currentDisplayName; }
        }

        private void Display(IList data, TransactionViewName name, string caption, long selectedRowId)
        {
            currentDisplayName = name;
            string layout = TheActiveGrid.Name;

            if (ttf != null)
            {
                ttf.Dispose();
            }

            this.Commit();
            this.Caption = caption;


            this.TheActiveGrid.SetItemsSource(data);

            object selected = FindItemById(selectedRowId);
            if (selected != null)
            {
                this.TheActiveGrid.SelectedItem = selected;
            }
            else if (selectedRowId == 0)
            {
                SelectedRowIndex = 0;
            }
            else if (data != null && data.Count > 0)
            {
                this.TheActiveGrid.SelectedIndex = data.Count - 1;
            }

            if (data != null)
            {
                if (data is TransactionCollection)
                {
                    this.ViewModel = (TransactionCollection)data;
                    ttf = new TransactionTypeToFind(TheActiveGrid);
                }
                else
                {
                    this.ViewModel = null;
                }
            }
            else
            {
                this.ViewModel = null;
            }

            UpdateActivities();
        }

        private void OnInvestmentAccountTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            quickFilterValueChanging = true; // don't clear the filter on tab switches.
            ViewTransactionsForSingleAccount(this.activeAccount, TransactionSelection.Current, 0);
            ShowBalance();
            quickFilterValueChanging = false;
            this.TheActiveGrid.Focus();
            // Must update this field After we fire FireBeforeViewStateChanged.
            this.selectedTab = this.InvestmentAccountTabs.SelectedIndex;
        }


        private void ShowBalance()
        {
            if (this.Rows != null)
            {
                int count = 0;
                decimal salestax = 0;
                decimal investmentValue = 0;
                decimal balance;
                switch (this.ActiveViewName)
                {
                    case TransactionViewName.ByCategory:
                    case TransactionViewName.ByCategoryCustom:
                        balance = Transactions.GetBalance(this.myMoney, this.Rows, this.ActiveAccount, true, true, out count, out salestax, out investmentValue);
                        break;

                    case TransactionViewName.ByPayee:
                        balance = Transactions.GetBalance(this.myMoney, this.Rows, this.ActiveAccount, true, false, out count, out salestax, out investmentValue);
                        break;

                    default:
                        balance = Transactions.GetBalance(this.myMoney, this.Rows, this.ActiveAccount, false, false, out count, out salestax, out investmentValue);
                        break;
                }

                string msg = count + " rows, " + balance.ToString("C");
                if (salestax != 0)
                {
                    msg += ", taxes " + salestax.ToString("C");
                }
                if (investmentValue != 0)
                {
                    msg += ", investments " + investmentValue.ToString("C");
                }

                ShowStatus(msg);
            }
        }

        private bool refresh;

        /// <summary>
        /// call this method if you have changed the transaction entries and you want the items in the DataGrid to update the UI
        /// </summary>
        public void Refresh()
        {
            if (!refresh && this.myMoney != null)
            {
                refresh = true;

                this.requestedRowId = this.SelectedRowId;
                this.UpdateView();

                refresh = false;
            }
        }


        /// <summary>
        /// Get the un-committed date from the current row.
        /// </summary>
        /// <returns></returns>
        private DateTime? GetUncomittedDate()
        {
            DataGridRow row = TheActiveGrid.GetRowFromItem(TheActiveGrid.SelectedItem);
            string date = TheActiveGrid.GetUncommittedColumnText(row, "Date");
            if (date != null)
            {
                DateTime dt;
                if (DateTime.TryParse(date, out dt))
                {
                    return dt;
                }
            }
            return null;
        }

        /// <summary>
        /// Get the un-committed payee name for the current row.
        /// </summary>
        private string GetUncomittedPayee()
        {
            DataGridRow row = TheActiveGrid.GetRowFromItem(TheActiveGrid.SelectedItem);
            if (row != null)
            {
                return TheActiveGrid.GetUncommittedColumnText(row, "PayeeOrTransferCaption");
            }
            return null;
        }

        private void SetUncommittedPayee(string payee)
        {
            DataGridRow row = TheActiveGrid.GetRowFromItem(TheActiveGrid.SelectedItem);
            TheActiveGrid.SetUncommittedColumnText(row, "PayeeOrTransferCaption", 0, payee);
        }

        /// <summary>
        /// Get the un-committed category name for the current row.
        /// </summary>
        private string GetUncomittedCategory()
        {
            DataGridRow row = TheActiveGrid.GetRowFromItem(TheActiveGrid.SelectedItem);
            if (row != null)
            {
                return TheActiveGrid.GetUncommittedColumnText(row, "PayeeOrTransferCaption", 1);
            }
            return null;
        }

        #endregion

        #region UI EVENTS


        public static TObject FindVisualParent<TObject>(UIElement child) where TObject : UIElement
        {
            if (child == null)
            {
                return null;
            }

            UIElement parent = VisualTreeHelper.GetParent(child) as UIElement;

            while (parent != null)
            {
                TObject found = parent as TObject;
                if (found != null)
                {
                    return found;
                }
                else
                {
                    parent = VisualTreeHelper.GetParent(parent) as UIElement;
                }
            }

            return null;
        }

        internal void ToggleSplitDetails(long id)
        {
            var dataGrid = this.TheActiveGrid;

            if (dataGrid != null)
            {
                // Toggle the Detail Split View 
                if (id == splitVisibleRowId && dataGrid.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.VisibleWhenSelected)
                {
                    // Done editing the split. must be executed after any edit field is committed
                    // so we run this on the ContextIdle
                    Dispatcher.BeginInvoke(new Action(() => RestoreSplitViewMode()), DispatcherPriority.ContextIdle);
                }
                else
                {
                    // Show the Full Details Split inline DataGrid
                    TheActiveGrid.RowDetailsTemplate = this.TryFindResource("myDataGridDetailView") as DataTemplate;
                    dataGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected;
                    splitVisibleRowId = id;
                }
            }
        }


        private object lastSelectedItem;
        private long splitVisibleRowId;
        private object selectedSplit;

        private void TheGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1)
            {
                object selected = e.AddedItems[0];


                lastSelectedItem = selected;

                if (selected is Transaction)
                {
                    //-------------------------------------------------------------
                    // The user just changed the selection of the current Transaction
                    // We now need to decide if we went to hide the Detail view or leave it as is
                    if (TheActiveGrid.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Visible)
                    {
                        // Do not change any thing keep the Detail view in Mini mode 
                    }
                    else if (splitVisibleRowId != this.SelectedRowId)
                    {
                        if (TheActiveGrid.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.VisibleWhenSelected)
                        {
                            // VisibleWhenSelected can only have been set by the user action of clicking on the "SPLIT" button
                            // Since we are now selecting an different Transaction we can hide the Detail view

                            // In the case were the transaction split was created for the first time
                            // We need to refresh the items in order show the SPLIT button
                            // TheActiveGrid.Items.Refresh();

                            RestoreSplitViewMode();
                        }
                    }
                    selectedSplit = null;
                }
                else if (selected is Split)
                {
                    selectedSplit = (Split)selected;
                }

                if (lazyShowConnector == null)
                {
                    lazyShowConnector = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Normal, ShowPotentialDuplicates, this.Dispatcher);
                }
                lazyShowConnector.Stop();
                lazyShowConnector.Start();
            }
            else if (e.RemovedItems.Count > 0)
            {
                Transaction t = TheActiveGrid.SelectedItem as Transaction;
                if (e.RemovedItems[0] is Split)
                {
                    selectedSplit = null;
                }
                if (t == null)
                {
                    HideConnector();
                }
                else if (connector != null && connector.Source != t && connector.Target != t)
                {
                    HideConnector();
                }
            }
        }

        DispatcherTimer lazyShowConnector;

        private void ShowPotentialDuplicates(object sender, EventArgs e)
        {
            lazyShowConnector.Stop();
            lazyShowConnector = null;

            Transaction t = this.SelectedTransaction;
            if (t != null)
            {
                DataGridRow row = TheActiveGrid.GetRowFromItem(t);
                if (row != null)
                {
                    Settings settings = (Settings)this.site.GetService(typeof(Settings));
                    TimeSpan range = settings.DuplicateRange;
                    TransactionCollection tc = this.TheActiveGrid.ItemsSource as TransactionCollection;
                    Transaction u = Transactions.FindPotentialDuplicate(t, tc, range);
                    if (u != null)
                    {
                        ConnectAnchors(t, u);
                        return;
                    }
                }
            }
            HideConnector();
        }

        TransactionConnector connector;

        private void HideConnector()
        {
            if (connector != null)
            {
                bool hadFocus = connector.HasFocus;
                connector.Disconnect();
                connector = null;
                if (hadFocus)
                {
                    TheActiveGrid.SetFocusOnSelectedRow(TheActiveGrid.SelectedItem, "Payee");
                }
            }
        }

        private void ConnectAnchors(Transaction t, Transaction u)
        {
            if (connector != null)
            {
                this.connector.Connect(t, u);
            }
            else
            {
                this.connector = new TransactionConnector(TheActiveGrid);
                connector.Connect(t, u);
                connector.Clicked += OnMergeButtonClick;
                connector.Closed += OnConnectorClosed;
            }
        }

        void OnConnectorClosed(object sender, EventArgs e)
        {
            TransactionConnector connector = (TransactionConnector)sender;
            if (connector != null)
            {
                connector.Source.NotDuplicate = true;
                connector.Target.NotDuplicate = true;
            }
            HideConnector();
        }

        void OnMergeButtonClick(object sender, EventArgs e)
        {
            TransactionConnector connector = (TransactionConnector)sender;
            if (connector != null)
            {
                Merge(connector.Source, connector.Target, false);
                HideConnector();
                TheActiveGrid.ClearAutoEdit();
            }
        }

        private void RestoreSplitViewMode()
        {
            if (splitVisibleRowId != -1)
            {
                //
                // Split view is changing and we have a chance to cleanup any empty split that the user may have left behind
                //
                Transaction transactionLostSelection = this.Money.Transactions.FindTransactionById(splitVisibleRowId);

                if (transactionLostSelection != null)
                {
                    if (transactionLostSelection.Splits != null)
                    {
                        transactionLostSelection.Splits.RemoveEmptySplits();
                    }
                }

            }

            if (this.ViewAllSplits)
            {
                TheActiveGrid.RowDetailsTemplate = this.TryFindResource("myDataGridDetailMiniView") as DataTemplate;
                TheActiveGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Visible;
                splitVisibleRowId = this.SelectedRowId;
            }
            else
            {
                TheActiveGrid.RowDetailsTemplate = this.TryFindResource("myDataGridDetailView") as DataTemplate;
                TheActiveGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed;
                splitVisibleRowId = -1;
            }
        }

        #endregion

        #region MENUS COMMNANDS

        public readonly static RoutedUICommand CommandCopySplits = new RoutedUICommand("CopySplits", "CommandCopySplits", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandPasteSplits = new RoutedUICommand("PasteSplits", "CommandPasteSplits", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandAccept = new RoutedUICommand("Accept", "CommandAccept", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandVoid = new RoutedUICommand("Void", "CommandVoid", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandSplits = new RoutedUICommand("Splits", "CommandSplits", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandRenamePayee = new RoutedUICommand("RenamePayee", "CommandRenamePayee", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandLookupPayee = new RoutedUICommand("LookupPayee", "CommandLookupPayee", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandRecategorize = new RoutedUICommand("Recategorize", "CommandRecategorize", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandSetTaxDate = new RoutedUICommand("SetTaxDate", "CommandSetTaxDate", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandGotoRelatedTransaction = new RoutedUICommand("GotoRelatedTransaction", "CommandGotoRelatedTransaction", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandGotoStatement = new RoutedUICommand("GotoStatement", "CommandGotoStatement", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandViewTransactionsByAccount = new RoutedUICommand("ViewTransactionsByAccount", "CommandViewTransactionsByAccount", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandViewSimilarTransactions = new RoutedUICommand("ViewSimilarTransactions", "CommandViewSimilarTransactions", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandViewTransactionsByCategory = new RoutedUICommand("ViewTransactionsByCategory", "CommandViewTransactionsByCategory", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandViewTransactionsByPayee = new RoutedUICommand("ViewTransactionsByPayee", "CommandViewTransactionsByPayee", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandViewTransactionsBySecurity = new RoutedUICommand("ViewTransactionsBySecurity", "CommandViewTransactionsBySecurity", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandViewSecurity = new RoutedUICommand("CommandViewSecurity", "CommandViewSecurity", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandViewCategory = new RoutedUICommand("CommandViewCategory", "CommandViewCategory", typeof(TransactionsView));

        public readonly static RoutedUICommand CommandViewToggleOneLineView = new RoutedUICommand("View ToggleOneLineView", "ViewToggleOneLineView", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandViewToggleAllSplits = new RoutedUICommand("View Toggle View All Splits", "ViewToggleViewAllSplits", typeof(TransactionsView));

        public readonly static RoutedUICommand CommandViewExport = new RoutedUICommand("Export", "CommandViewExport", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandScanAttachment = new RoutedUICommand("ScanAttachment", "CommandScanAttachment", typeof(TransactionsView));

        #region CAN EXECUTE

        bool HasNonReadonlySelectedTransaction
        {
            get
            {
                return this.SelectedTransaction != null && !this.SelectedTransaction.IsReadOnly;
            }
        }

        TransactionCollection viewModel;

        /// <summary>
        /// The currently visible set of transactions.
        /// </summary>
        public TransactionCollection ViewModel
        {
            get
            {
                return viewModel;
            }
            set
            {
                viewModel = value;
                if (ViewModelChanged != null)
                {
                    ViewModelChanged(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler ViewModelChanged;

        private void CanExecute_Accept(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = HasNonReadonlySelectedTransaction;
            e.Handled = true;
        }

        private void CanExecute_Void(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = HasNonReadonlySelectedTransaction;
            e.Handled = true;
        }
        private void CanExecute_Budgeted(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = HasNonReadonlySelectedTransaction;
            e.Handled = true;
        }
        private void CanExecute_Splits(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = HasNonReadonlySelectedTransaction;
            e.Handled = true;
        }
        private void CanExecute_RenamePayee(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = HasNonReadonlySelectedTransaction;
            e.Handled = true;
        }
        private void CanExecute_LookupPayee(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }
        private void CanExecute_Recategorize(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void CanExecute_SetTaxDate(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = HasNonReadonlySelectedTransaction;
            e.Handled = true;
        }

        private void CanExecute_GotoStatement(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.SelectedTransaction != null && this.SelectedTransaction.HasStatement;
            e.Handled = true;
        }

        private void CanExecute_GotoRelatedTransaction(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.SelectedTransaction != null;
            e.Handled = true;
        }
        private void CanExecute_ViewTransactionsByAccount(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.SelectedTransaction != null;
            e.Handled = true;
        }
        private void CanExecute_ViewSimilarTransactions(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }
        private void CanExecute_ViewTransactionsByCategory(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (this.SelectedTransaction != null && !this.SelectedTransaction.IsSplit && this.SelectedTransaction.Category != null);
            e.Handled = true;
        }
        private void CanExecute_ViewTransactionsByPayee(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (this.SelectedTransaction != null && this.SelectedTransaction.Payee != null);
            e.Handled = true;
        }

        private void CanExecute_ViewTransactionsBySecurity(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void CanExecute_DeleteThisTransaction(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = TheActiveGrid.SelectedItem != null;
            e.Handled = true;
        }


        #endregion

        #region ON COMMAND

        void OnCommandAccept(object sender, RoutedEventArgs e)
        {
            ToggleTransactionStateAccept(this.SelectedTransaction);
        }

        void OnCommandVoid(object sender, RoutedEventArgs e)
        {
            Transaction t = this.TheActiveGrid.SelectedItem as Transaction;
            if (t != null && t.Status != TransactionStatus.Reconciled && !t.IsReadOnly)
            {
                t.Status = (t.Status == TransactionStatus.Void) ? TransactionStatus.None : TransactionStatus.Void;
            }
        }

        private void OnCopySplits(object sender, ExecutedRoutedEventArgs e)
        {
            this.Commit();
            Transaction t = this.SelectedTransaction;
            if (t != null && t.IsSplit)
            {
                string xml = t.Splits.Serialize();
                Clipboard.Clear();
                Clipboard.SetText(xml);
            }
        }

        private void CanCopySplits(object sender, CanExecuteRoutedEventArgs e)
        {
            Transaction t = this.SelectedTransaction;
            e.CanExecute = (t != null && t.IsSplit && t.Splits.Count > 0);
        }

        private void OnPasteSplits(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                Transaction t = this.SelectedTransaction;
                if (t != null)
                {
                    string xml = Clipboard.GetText();
                    t.NonNullSplits.DeserializeInto(this.myMoney, xml);
                }
            }
            catch (Exception ex)
            {
                MessageBoxEx.Show(ex.Message, "Error pasting splits", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CanPasteSplits(object sender, CanExecuteRoutedEventArgs e)
        {
            Transaction t = this.SelectedTransaction;
            e.CanExecute = (t != null && Clipboard.ContainsText());
        }

        void OnCommandSplits(object sender, RoutedEventArgs e)
        {
            Transaction t = this.SelectedTransaction;
            if (t != null && !t.IsReadOnly)
            {

                Splits splits = t.NonNullSplits;

                if (t.NonNullSplits.Count == 0)
                {
                    // must have something in there so grid shows one row.
                    Split s = splits.NewSplit();
                    s.Amount = t.Amount;
                    s.Category = t.Category;
                    splits.AddSplit(s);
                }
                t.Category = myMoney.Categories.Split;
                this.TheActiveGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected;
                t.OnChanged("NonNullSplits");
                splitVisibleRowId = this.SelectedRowId;
            }
        }

        void OnCommandRenamePayee(object sender, RoutedEventArgs e)
        {
            this.TheActiveGrid.CommitEdit();
            Transaction t = this.SelectedTransaction;
            if (t != null && !t.IsReadOnly)
            {
                this.RenamePayee(t.Payee);
            }
        }

        void OnCommandLookupPayee(object sender, RoutedEventArgs e)
        {
            this.TheActiveGrid.CommitEdit();
            Transaction t = this.SelectedTransaction;
            if (t != null && !t.IsReadOnly)
            {
                // Start Web "t.Payee"
                string url = "http://www.bing.com/search?q=";
                url += t.Payee;
                Uri webSite = new Uri(url, UriKind.Absolute);
                InternetExplorer.OpenUrl(IntPtr.Zero, webSite);
            }
        }



        void RenamePayee(Payee payee)
        {
            RenamePayeeDialog dialog = RenamePayeeDialog.ShowDialogRenamePayee(site, this.myMoney, payee);
            dialog.Owner = App.Current.MainWindow;
            dialog.ShowDialog();
        }

        private void OnRecategorizeAll(object sender, RoutedEventArgs e)
        {
            this.TheActiveGrid.CommitEdit();
            RecategorizeDialog dialog = new Dialogs.RecategorizeDialog(this.myMoney);
            dialog.Owner = App.Current.MainWindow;
            Category category = null;
            if (this.SelectedTransaction != null)
            {
                category = this.SelectedTransaction.Category;
            }
            else if (this.viewModel.Count > 0)
            {
                category = this.viewModel[0].Category;
            }
            dialog.FromCategory = dialog.ToCategory = category;
            if (dialog.ShowDialog() == true)
            {
                // do it !!
                Category to = dialog.ToCategory;
                if (to != category)
                {
                    this.myMoney.BeginUpdate(this);
                    try
                    {
                        foreach (Transaction t in this.ViewModel)
                        {
                            t.Category = to;
                        }
                    }
                    finally
                    {
                        this.myMoney.EndUpdate();
                    }
                }
            }
        }

        private void OnSetTaxDate(object sender, ExecutedRoutedEventArgs e)
        {
            this.TheActiveGrid.CommitEdit();
            Transaction t = this.SelectedTransaction;
            if (t != null && !t.IsReadOnly)
            {
                var extra = this.myMoney.TransactionExtras.FindByTransaction(t.Id);
                var dialog = new PickYearDialog();
                dialog.Owner = Application.Current.MainWindow;
                dialog.SetTitle("Select Tax Date");
                dialog.SetPrompt("Set the tax date for this transaction:");
                if (extra != null && extra.TaxDate.HasValue)
                {
                    dialog.SelectedDate = extra.TaxDate.Value;
                }
                else
                {
                    dialog.SelectedDate = t.Date;
                }

                if (dialog.ShowDialog() == true)
                {
                    var date = dialog.SelectedDate;
                    if (date.HasValue && date != t.Date)
                    {
                        if (extra == null)
                        {
                            extra = new TransactionExtra()
                            {
                                Transaction = t.Id
                            };
                            this.myMoney.TransactionExtras.AddExtra(extra);
                            t.Extra = extra;
                        }
                        extra.TaxDate = date.Value;
                    }
                    else if (extra != null)
                    {
                        extra.TaxDate = null;
                        if (extra.IsEmpty)
                        {
                            this.myMoney.TransactionExtras.RemoveExtra(extra);
                            t.Extra = null;
                        }                        
                    }
                }
            }
        }

        void OnCommandGotoRelatedTransaction(object sender, RoutedEventArgs e)
        {
            if (isEditing)
            {
                this.Commit();
                Dispatcher.BeginInvoke(new Action(JumpToRelatedTransaction), DispatcherPriority.Background);
            }
            else
            {
                JumpToRelatedTransaction();
            }
        }

        private void OnCommandGotoStatement(object sender, ExecutedRoutedEventArgs e)
        {
            if (isEditing)
            {
                this.Commit();
                Dispatcher.BeginInvoke(new Action(OpenStatement), DispatcherPriority.Background);
            }
            else
            {
                OpenStatement();
            }
        }


        private void JumpToRelatedTransaction()
        {
            Split split = lastSelectedItem as Split;
            if (split != null)
            {
                GotoRelated(split);
            }
            else
            {
                Transaction t = this.SelectedTransaction;
                if (t != null)
                {
                    GotoRelated(t);
                }
            }
        }

        private void OpenStatement()
        {
            Transaction t = this.SelectedTransaction;
            StatementManager sm = (StatementManager)this.site.GetService(typeof(StatementManager));
            if (sm != null && t != null && t.ReconciledDate.HasValue)
            {
                var fileName = sm.GetStatementFullPath(t.Account, t.ReconciledDate.Value);
                if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
                {
                    InternetExplorer.OpenUrl(IntPtr.Zero, fileName);
                }
            }
        }

        void GotoRelated(Split split)
        {
            Transfer u = split.Transfer;
            if (u != null)
            {
                GotoTransaction(u.Transaction);
            }
        }

        void GotoRelated(Transaction t)
        {
            Transfer u = t.Transfer;
            if (u != null)
            {
                GotoTransaction(u.Transaction);
            }
            else if (t.Related != null)
            {
                GotoTransaction(t.Related);
            }
            else if (t.IsSplit)
            {
                foreach (Split s in t.Splits)
                {
                    if (s.Transfer != null)
                    {
                        GotoTransaction(s.Transfer.Transaction);
                        break;
                    }
                }
            }
            else
            {
                AttempToMatchAndConvertPossibleTransfer(t);
            }
        }

        private void GotoTransaction(Transaction t)
        {
            // Must use this method in order to coordinate the jump with MainWindow otherwise MainWindow
            // tries to do some things that interfere with this navigation during it's implementation of
            // RestorePreviouslySavedSelection.
            IViewNavigator n = (IViewNavigator)this.site.GetService(typeof(IViewNavigator));
            n.NavigateToTransaction(t);
        }

        private void AttempToMatchAndConvertPossibleTransfer(Transaction t)
        {
            //
            // We are searching for the potential transfer transaction in another account.
            //
            Settings settings = (Settings)this.site.GetService(typeof(Settings));
            int days = settings.TransferSearchDays; // starting point, but can grow from there (hence the loop).
            while (true)
            {
                List<QueryRow> queryRows = new List<QueryRow>();
             
                // Set the acceptable Date range to consider the other transaction a possible match
                var dateMin = t.Date.AddDays(-days);
                var dateMax = t.Date.AddDays(days);

                queryRows.Add(new QueryRow(Conjunction.And, Field.Date, Operation.GreaterThanEquals, dateMin.ToString()));
                queryRows.Add(new QueryRow(Conjunction.And, Field.Date, Operation.LessThanEquals, dateMax.ToString()));
                
                // If this was a payment we are looking for a deposit
                // if this was a deposit we are looking for a payment

                Field DepositOrPayment = t.amount > 0 ? Field.Payment : Field.Deposit;
                queryRows.Add(new QueryRow(Conjunction.And, DepositOrPayment, Operation.Equals, Math.Abs(t.amount).ToString()));
               
                // Execute the search, this a blocking call
                IList<Transaction> list = myMoney.Transactions.ExecuteQuery(queryRows.ToArray());
                IList<Transaction> free = new List<Transaction>(from u in list where u.Transfer == null select u);
                if (free.Count > 0)
                {
                    // only look at transactions that don't already have a Transfer.
                    list = free; 
                }

                switch (list.Count)
                {
                    // Best case scenario we found only one match, offer to the user to convert this to a transfer 
                    case 1:
                        {
                            var found = list[0];
                            if (found.Transfer != null)
                            {
                                MessageBoxEx.Show("The matching transfer is already a linked Transfer, please check if you have a duplicate on this side that needs to be merged.",
                                    "Transfer exists already", MessageBoxButton.OK, MessageBoxImage.Information);
                                return;
                            }
                            String message = "Account:  " + found.AccountName + '\n';
                            message += "Date:  " + found.Date + '\n';
                            message += "Amount:  " + found.amount + '\n';
                            message += "Category:  " + found.CategoryFullName + '\n';
                            message += "Memo:  " + found.Memo + '\n';
                            message += "\nMerge into a transfer transaction?";

                            if (MessageBoxEx.Show(message, "Found a matching transfer", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                            {
                                if (t.amount > 0)
                                {
                                    // Money was transfered from the external account to this account
                                    TransformTwoTrasactionIntoTransfer(found, t);
                                }
                                else
                                {
                                    // Money was transfered from to external account
                                    TransformTwoTrasactionIntoTransfer(t, found);
                                }
                            }
                            return;
                        }

                    // There no Match, let the user know
                    case 0:
                        {
                            days *= 2;
                            var rc = MessageBoxEx.Show($"No matching transaction found, would you like to try a wider search with ± {days} days?", 
                                "Transfer Search Failed", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (rc == MessageBoxResult.No)
                            {
                                return;
                            }
                            break;
                        }

                    // two or more matching transactions were found, let the user know
                    default:
                        {
                            String foundThese = "";
                            foreach (var found in list)
                            {
                                foundThese += found.AccountName + ' ' + found.Date + ' ' + found.amount;
                            }

                            MessageBoxEx.Show(foundThese, "Found " + list.Count + " matching transfers", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                }
            }
        }

        void TransformTwoTrasactionIntoTransfer(Transaction transferFrom, Transaction transferTo)
        {
            // keep the payees to store them in the transfer memo fields
            // Its use a clue of the two original disconnected transactions
            var newMemoForBothSide = transferFrom.PayeeName + ">" + transferTo.PayeeName;
            
            // From
            {
                transferFrom.Transfer = new Transfer(0, transferFrom, transferTo);
                if (string.IsNullOrEmpty(transferFrom.Memo))
                {
                    transferFrom.Memo = newMemoForBothSide;
                }
            }

            // To
            {
                transferTo.Transfer = new Transfer(0, transferTo, transferFrom);
                if (string.IsNullOrEmpty(transferTo.Memo))
                {
                    transferTo.Memo = newMemoForBothSide;
                }
            }

            transferFrom.OnChanged("PayeeOrTransferCaption");
            transferFrom.OnChanged("Memo");

            transferTo.OnChanged("PayeeOrTransferCaption");
            transferTo.OnChanged("Memo");
        }

        void OnCommandViewTransactionsByAccount(object sender, RoutedEventArgs e)
        {
            this.Commit();
            Transaction t = this.SelectedTransaction;
            if (t != null && t.Account != null)
            {
                ViewTransactionsForSingleAccount(t.Account, TransactionSelection.Specific, t.Id);
            }
        }

        void OnCommandViewSimilarTransactions(object sender, RoutedEventArgs e)
        {
        }

        void OnCommandViewTransactionsByCategory(object sender, RoutedEventArgs e)
        {
            this.Commit();
            Transaction t = this.SelectedTransaction;
            if (t != null && t.Category != null)
            {
                ViewTransactionsForCategory(t.Category, t.Id);
            }
        }

        void OnCommandViewTransactionsByPayee(object sender, RoutedEventArgs e)
        {
            this.Commit();
            Transaction t = this.SelectedTransaction;
            if (t != null && t.Payee != null)
            {
                ViewTransactionsForPayee(t.Payee, t.Id);
            }
        }

        void OnCommandViewTransactionsBySecurity(object sender, RoutedEventArgs e)
        {
            Transaction t = TheActiveGrid.SelectedItem as Transaction;
            Investment i = TheActiveGrid.SelectedItem as Investment;
            if (t != null)
            {
                i = t.Investment;
            }
            if (i != null)
            {
                ViewTransactionsForSecurity(i.Security, i.Id);
            }
        }



        void OnCommandViewSecurity(object sender, RoutedEventArgs e)
        {
            Security s = null;
            Transaction t = this.SelectedTransaction;
            if (t != null && t.Investment != null && t.Investment.Security != null)
            {
                s = t.Investment.Security;
            }
            Investment i = this.SelectedRow as Investment;
            if (i != null)
            {
                s = i.Security;
            }
            if (s != null)
            {
                IViewNavigator n = (IViewNavigator)this.site.GetService(typeof(IViewNavigator));
                n.NavigateToSecurity(s);
            }
        }

        private void CanExecute_ViewSecurity(object sender, CanExecuteRoutedEventArgs e)
        {
            Transaction t = this.SelectedTransaction;
            if (t != null)
            {
                e.CanExecute = t.Investment != null && t.Investment.Security != null;
                return;
            }
            Investment i = this.SelectedRow as Investment;
            if (i != null)
            {
                e.CanExecute = i.Security != null;
                return;
            }
        }

        private void CanExecute_ViewCategory(object sender, CanExecuteRoutedEventArgs e)
        {
            Transaction t = this.SelectedTransaction;
            if (t != null)
            {
                e.CanExecute = (t.Category != null) && t.Transfer == null && !t.IsSplit;
                return;
            }
        }

        private void OnCommandViewCategory(object sender, ExecutedRoutedEventArgs e)
        {
            Transaction t = this.SelectedTransaction;
            if (t != null && t.Category != null && t.Transfer == null && !t.IsSplit)
            {
                Category c = t.Category;
                CategoryDialog dialog = CategoryDialog.ShowDialogCategory(this.myMoney, c.Name);
                dialog.Owner = App.Current.MainWindow;
                dialog.ShowDialog();
            }
        }


        private void OnCommandViewToggleOneLineView(object sender, ExecutedRoutedEventArgs e)
        {
            this.OneLineView = !this.OneLineView;
        }

        private void OnCommandViewToggleAllSplits(object sender, ExecutedRoutedEventArgs e)
        {
            this.ViewAllSplits = !this.ViewAllSplits;
        }

        private void CanExecute_ToggleOneLineView(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.IsVisible;
            e.Handled = true;
        }

        private void CanExecute_ToggleViewsAllSplits(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!IsEditing)
            {
                //
                // Only allow to toggle "show split view" when we are not in editing mode
                //
                e.CanExecute = this.IsVisible;
                e.Handled = true;
            }
        }

        private void CanExecute_AllTransactions(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.IsVisible;
            e.Handled = true;
        }

        private void CanExecute_UnacceptedTransactions(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.IsVisible;
            e.Handled = true;
        }

        private void CanExecute_UnreconciledTransactions(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.IsVisible;
            e.Handled = true;
        }

        private void CanExecute_BudgetedTransactions(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.IsVisible;
            e.Handled = true;
        }

        private void CanExecute_UncategorizedTransactions(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.IsVisible;
            e.Handled = true;
        }

        private void OnCommandViewExport(object sender, ExecutedRoutedEventArgs e)
        {
            Exporters exporter = new Exporters();
            List<object> list = new List<object>();
            foreach (object o in this.Rows)
            {
                list.Add(o);
            }
            exporter.ExportPrompt(list);
        }

        #endregion


        /// <summary>
        /// Toggles the transaction State "ACCEPT" to "UNACCEPTED"
        /// </summary>
        /// <param name="t"></param>
        private void ToggleTransactionStateAccept(Transaction t)
        {
            if (t != null && !t.IsReadOnly)
            {
                t.Unaccepted = !t.Unaccepted;
            }
        }

        /// <summary>
        /// Toggle the transaction state from RECONCILED to UNRECONCILED
        /// </summary>
        /// <param name="t"></param>
        internal void ToggleTransactionStateReconciled(Transaction t)
        {
            if (t == null || t.Parent == null)
            {
                // if the parent is null then this is one of those gray fake transactions representing
                // a split line pulled out for a "bycategory" view.
                return;
            }

            t.Parent.BeginUpdate(true);
            try
            {

                // toggle it.
                if (this.reconciling)
                {
                    if (t.Status != TransactionStatus.Reconciled)
                    {
                        ReconcileThisTransaction(t);
                    }
                    else
                    {
                        if (reconcilingTransactions.ContainsKey(t))
                        {
                            t.Status = reconcilingTransactions[t];
                            if (t.Status == TransactionStatus.Reconciled)
                            {
                                t.Status = TransactionStatus.Cleared;
                            }
                        }
                        else
                        {
                            t.Status = TransactionStatus.Cleared;
                        }
                        t.IsReconciling = false;
                        t.ReconciledDate = null;
                    }
                }
                else
                {
                    if (t.Status == TransactionStatus.None)
                    {
                        t.Status = TransactionStatus.Cleared;
                    }
                    else if (t.Status == TransactionStatus.Cleared)
                    {
                        t.Status = TransactionStatus.None;
                    }
                }
            }
            finally
            {
                t.Parent.EndUpdate();
            }
        }

        private void ReconcileThisTransaction(Transaction t)
        {
            if (t.Status != TransactionStatus.Reconciled)
            {
                reconcilingTransactions[t] = t.Status;
                t.Status = TransactionStatus.Reconciled;
                t.IsReconciling = true;
                t.ReconciledDate = this.StatmentReconcileDateEnd;
            }
        }

        #endregion

        #region EDITING CONTROL EVENTS

        private void TheGridForAmountSplit_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            Split s = e.Row.DataContext as Split;
            if (s != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    s.Transaction.NonNullSplits.Rebalance(); // update split message.
                }), DispatcherPriority.Background);
            }
        }

        void OnDataGridCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            MoneyDataGrid grid = sender as MoneyDataGrid;
            Transaction t = this.SelectedTransaction;
            if (t != null && t.IsSplit && grid != null && (e.Column.SortMemberPath == "SalesTax" || e.Column.SortMemberPath == "Debit" || e.Column.SortMemberPath == "Credit"))
            {
                decimal salesTax = t.SalesTax;
                decimal amount = t.Amount;

                bool editedTax = TryGetDecimal(grid.GetUncommittedColumnText(e.Row, "SalesTax"), ref salesTax);
                bool editedAmount = false;
                if (TryGetDecimal(grid.GetUncommittedColumnText(e.Row, "Debit"), ref amount))
                {
                    amount = -amount;
                    editedAmount = true;
                }
                else
                {
                    editedAmount = TryGetDecimal(grid.GetUncommittedColumnText(e.Row, "Credit"), ref amount);
                }

                if (amount < 0)
                {
                    amount += salesTax;
                }
                else
                {
                    amount -= salesTax; // then it was a refund, so sales tax was refunded also!
                }

                if (editedAmount || editedTax)
                {
                    t.NonNullSplits.AmountMinusSalesTax = amount;
                }
                else
                {
                    t.NonNullSplits.AmountMinusSalesTax = null;
                }
                t.NonNullSplits.Rebalance();
            }
        }

        bool TryGetDecimal(string s, ref decimal value)
        {
            if (!string.IsNullOrEmpty(s))
            {
                decimal v;
                if (decimal.TryParse(s, out v))
                {
                    value = v;
                    return true;
                }
            }
            return false;
        }

        private void ComboBoxForPayee_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.OldFocus is TextBox && sender is FrameworkElement)
            {
                ComboBox combo = sender as System.Windows.Controls.ComboBox;

                object value = combo.SelectedItem;

                string text = combo.Text;
                bool attemptingTransfer = Transaction.IsTransferCaption(text);

                Split s = combo.DataContext as Split;
                if (s != null)
                {
                    Transaction t = s.Transaction;
                    if (attemptingTransfer && t.Transfer != null && t.Transfer.Split != null)
                    {
                        if (MessageBoxEx.Show(string.Format("This transaction is already the target of a transfer from a split in account {0}.\n" +
                                         "MyMoney doesn't support splits being on both sides of a transfer so do you " +
                                         "want to remove the split transfer in account {0} ?", t.Transfer.Transaction.AccountName), "Transfer Not Supported", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.OK)
                        {
                            // tear down the split in a way that doesn't result in this side being deleted!
                            t.Transfer.Split.ClearTransfer();
                            t.Transfer.Split = null;
                            t.Transfer = null;
                        }
                        else
                        {
                            combo.Text = "Cancelled";
                        }
                    }
                    else if (!attemptingTransfer)
                    {
                        // Clear out any residual transfer information in this split.
                        // BUGBUG: this shouldn't be here, it is a nasty hack to modify the Transaction before editing is committed.
                        s.Transfer = null;
                        s.Payee = this.myMoney.Payees.FindPayee(text, true);
                    }
                }
                else
                {
                    Transaction t = combo.DataContext as Transaction;
                    if (t != null)
                    {
                        if (!attemptingTransfer)
                        {
                            // BUGBUG: this shouldn't be here, it is a nasty hack to modify the Transaction before editing is committed.
                            t.Payee = this.myMoney.Payees.FindPayee(text, true);

                            // Clear out any residual transfer information
                            t.Transfer = null;
                        }
                    }
                }
            }
        }

        private void ComboBoxForPayee_FilterChanged(object sender, RoutedEventArgs e)
        {
            FilteringComboBox combo = sender as FilteringComboBox;
            combo.FilterPredicate = new Predicate<object>((o) => { return o.ToString().IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0; });
        }

        bool promptingNewCategory;

        private void ComboBoxForCategory_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.OldFocus is TextBox)
            {
                ComboBox combo = sender as System.Windows.Controls.ComboBox;
                if (combo == null)
                {
                    return;
                }

                if (promptingNewCategory)
                {
                    e.Handled = true;
                    return;
                }

                object value = combo.SelectedItem;
                string text = ("" + combo.Text).Trim();
                Transaction t = combo.DataContext as Transaction;
                if (t != null && t.Category != null)
                {
                    // category might have just been entered and we are coming through here again because
                    // user typed TAB, but the combo box items haven't been updated yet.
                    if (t.Category == this.myMoney.Categories.FindCategory(text))
                    {
                        return;
                    }
                }

                if (value == null && !string.IsNullOrEmpty(text) && this.isEditing)
                {
                    promptingNewCategory = true;
                    try
                    {
                        e.Handled = true;
                        value = EnterNewCategoryAsync(combo, text);
                    }
                    finally
                    {
                        promptingNewCategory = false;
                    }
                }

                if (value is Category || string.IsNullOrEmpty(text))
                {
                    if (t != null)
                    {
                        this.myMoney.Categorize(t, (Category)value);
                        t.UpdateCategoriesView();
                    }
                    Split s = combo.DataContext as Split;
                    if (s != null)
                    {
                        s.Category = (Category)value;
                        s.UpdateCategoriesView();
                    }
                }
            }
        }

        private Category EnterNewCategoryAsync(ComboBox combo, string text)
        {
            Category newCategory = null;
            CategoryDialog dialog = CategoryDialog.ShowDialogCategory(this.myMoney, text);
            if (dialog.ShowDialog() == true)
            {
                newCategory = dialog.Category;
            }

            TextBox box = (TextBox)combo.Template.FindName("PART_EditableTextBox", combo);
            if (box != null)
            {
                // move focus back to this box
                box.SelectAll();
                box.Focus();
                if (newCategory != null)
                {
                    box.Text = newCategory.Name;
                }
            }

            return newCategory;
        }

        private void ComboBoxForCategory_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ComboBox combo = (ComboBox)sender;
            if (!string.IsNullOrEmpty(combo.Text))
            {
                return;
            }

            Split s = combo.DataContext as Split;
            if (s != null)
            {
                s.UpdateCategoriesView();
            }

            Transaction t = combo.DataContext as Transaction;
            if (t != null)
            {
                t.UpdateCategoriesView();
                // Auto-populate category (and/or Splits) from previous similar transaction.
                // This saves a LOT of typing.
                AutoPopulateCategory(t);
            }

        }

        private void AutoPopulateCategory(Transaction t)
        {
            string payeeOrTransfer = GetUncomittedPayee();

            if (string.IsNullOrEmpty(payeeOrTransfer))
            {
                payeeOrTransfer = t.PayeeOrTransferCaption;
            }

            // don't blow away any pending category that the user has entered already.
            string hasCategoryPending = GetUncomittedCategory();

            if (t != null && t.Category == null && string.IsNullOrEmpty(hasCategoryPending) && !string.IsNullOrEmpty(payeeOrTransfer))
            {
                object u = AutoCategorization.AutoCategoryMatch(t, payeeOrTransfer);
                if (u != null)
                {
                    try
                    {
                        DateTime? date = GetUncomittedDate();
                        if (date.HasValue)
                        {
                            t.Date = date.Value;
                        }
                        if (u is Split s)
                        {
                            t.Category = s.Category;
                        }
                        else if (u is Transaction ut)
                        {
                            this.myMoney.CopyCategory(ut, t);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBoxEx.Show(ex.ToString(), "Unexpected Internal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

        }

        private void ComboBoxForCategory_FilterChanged(object sender, RoutedEventArgs e)
        {
            FilteringComboBox combo = sender as FilteringComboBox;
            combo.Items.Filter = new Predicate<object>((o) => { return ((Category)o).GetFullName().IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0; });
        }

        private void ComboBoxSymbol_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.OldFocus is TextBox)
            {
                Transaction t = this.TheActiveGrid.SelectedItem as Transaction;
                if (t != null)
                {
                    ComboBox combo = sender as System.Windows.Controls.ComboBox;
                    object value = combo.SelectedItem;
                    Security s = value as Security;
                    if (s == null)
                    {
                        string text = combo.Text;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            s = this.myMoney.Securities.FindSymbol(text, false);
                            if (s == null)
                            {
                                s = this.myMoney.Securities.FindSecurityById(text);
                                if (s == null)
                                {
                                    // add a new one!
                                    s = this.myMoney.Securities.FindSymbol(text, true);
                                }
                            }
                            t.InvestmentSecurity = s;
                        }
                    }
                }
            }
        }

        private void ComboBoxForSymbols_FilterChanged(object sender, RoutedEventArgs e)
        {
            FilteringComboBox combo = sender as FilteringComboBox;
            string userString = combo.Text;
            combo.Items.Filter = new Predicate<object>((o) =>
            {
                Security s = (Security)o;
                if (combo.Filter != null)
                {
                    if ((s.Name != null && s.Name.IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (s.Symbol != null && s.Symbol.IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (s.Symbol != null && s.Symbol.IndexOf(userString, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return true;
                    }
                }
                return false;
            });
        }

        private void TheGridForAmountSplit_Loaded(object sender, RoutedEventArgs e)
        {
            MoneyDataGrid grid = (MoneyDataGrid)sender;
            SetupGrid(grid, false);
        }

        private void TheGridForAmountSplit_Unloaded(object sender, RoutedEventArgs e)
        {
            MoneyDataGrid grid = (MoneyDataGrid)sender;
            TearDownGrid(grid);
        }

        private void OnScanAttachment(object sender, ExecutedRoutedEventArgs e)
        {
            Transaction t = this.SelectedTransaction;
            if (t != null)
            {
                AttachmentManager rm = (AttachmentManager)this.site.GetService(typeof(AttachmentManager));
                AttachmentDialog.ScanAttachments(t, rm, (Settings)this.site.GetService(typeof(Settings)));
            }
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);
        }

        #endregion

    }

    //==========================================================================================
    public class TransactionViewState : ViewState
    {

        private bool viewAllSplits;
        public bool ViewAllSplits
        {
            get { return viewAllSplits; }
            set { viewAllSplits = value; }
        }

        public TransactionFilter TransactionFilter { get; set; }

        private bool viewOneLineView;
        public bool ViewOneLineView
        {
            get { return viewOneLineView; }
            set { viewOneLineView = value; }
        }

        public TransactionViewName CurrentDisplayName;


        public string Account;  // view transactions in account
        public string Payee;    // view by payee
        public string Category; // view by category
        public string Security; // view by security
        public string Rental;   // view by rental

        public long SelectedRow;
        public string QuickFilter;

        // not persisted, but it is handy in memory to remember these lists.
        public IEnumerable<Transaction> CustomList { get; set; }

        public QueryRow[] Query;

        private int tabIndex;
        public int TabIndex
        {
            get { return tabIndex; }
            set { tabIndex = value; }
        }

        public override void ReadXml(XmlReader r)
        {

            if (r.IsEmptyElement) return;

            List<QueryRow> tempQuery = new List<QueryRow>();

            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    switch (r.Name)
                    {
                        case "ViewAllSplits":
                            this.ViewAllSplits = ReadBoolean(r);
                            break;
                        case "TransactionFilter":
                            this.TransactionFilter = ReadEnum<TransactionFilter>(r);
                            break;
                        case "ViewOneLineView":
                            this.ViewOneLineView = ReadBoolean(r);
                            break;
                        case "Account":
                            this.Account = r.ReadString();
                            break;
                        case "Payee":
                            this.Payee = r.ReadString();
                            break;
                        case "Category":
                            this.Category = r.ReadString();
                            break;
                        case "Security":
                            this.Security = r.ReadString();
                            break;
                        case "Rental":
                            this.Rental = r.ReadString();
                            break;
                        case "SelectedRow":
                            this.SelectedRow = ReadInt(r, -1);
                            break;
                        case "TabIndex":
                            this.TabIndex = ReadInt(r, 0);
                            break;
                        case "CurrentDisplayName":
                            this.CurrentDisplayName = ReadEnum<TransactionViewName>(r);
                            break;
                        case "QuickFilter":
                            this.QuickFilter = r.ReadString();
                            break;
                        case "QueryRow":
                            tempQuery.Add(QueryRow.ReadQuery(r));
                            break;

                    }
                }
            }
            if (tempQuery.Count > 0)
            {
                Query = tempQuery.ToArray();
            }
        }

        public override void WriteXml(XmlWriter writer)
        {
            if (writer != null)
            {
                writer.WriteElementString("ViewAllSplits", this.ViewAllSplits.ToString());
                writer.WriteElementString("TransactionFilter", this.TransactionFilter.ToString());
                writer.WriteElementString("ViewOneLineView", this.ViewOneLineView.ToString());

                if (this.Account != null)
                {
                    writer.WriteElementString("Account", this.Account);
                }
                if (this.Payee != null)
                {
                    writer.WriteElementString("Payee", this.Payee);
                }
                if (this.Category != null)
                {
                    writer.WriteElementString("Category", this.Category);
                }
                if (this.Security != null)
                {
                    writer.WriteElementString("Security", this.Security);
                }
                if (this.Rental != null)
                {
                    writer.WriteElementString("Rental", this.Rental);
                }
                writer.WriteElementString("SelectedRow", this.SelectedRow.ToString());
                writer.WriteElementString("TabIndex", this.TabIndex.ToString());

                writer.WriteElementString("QuickFilter", this.QuickFilter);
                writer.WriteElementString("CurrentDisplayName", this.CurrentDisplayName.ToString());

                if (this.Query != null)
                {
                    writer.WriteStartElement("Query");
                    foreach (QueryRow row in Query)
                    {
                        writer.WriteStartElement("QueryRow");
                        row.WriteXml(writer);
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
            }
        }

        public static TransactionViewState Deserialize(XmlReader r)
        {
            TransactionViewState state = new TransactionViewState();
            state.ReadXml(r);
            return state;
        }

        public void Serialize(XmlWriter w)
        {
            w.WriteStartElement("ViewState");
            WriteXml(w);
            w.WriteEndElement();
        }


    }


    public abstract class TypeToFind : IDisposable
    {
        uint start;
        protected DataGrid grid;
        string typedSoFar;
        int resetDelay;

        public bool IsEnabled { get; set; }

        protected TypeToFind(DataGrid grid)
        {
            this.grid = grid;
            this.resetDelay = 500;
            RegisterEvents(true);
            IsEnabled = true;

            foreach (DataGridColumn c in grid.Columns)
            {
                if (c.CanUserSort && c.SortDirection.HasValue)
                {
                    sorted = c;
                    break;
                }
            }
        }

        void grid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            IsEnabled = true;
        }

        void grid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            IsEnabled = false;
        }

        void OnTextInput(object sender, TextCompositionEventArgs e)
        {
            if (IsEnabled)
            {
                uint tick = NativeMethods.TickCount;
                string text = e.Text;
                foreach (char ch in text)
                {
                    if (ch < 0x20) return; // don't process control characters
                    if (tick < start || tick < this.resetDelay || start < tick - this.resetDelay)
                    {
                        typedSoFar = ch.ToString();
                    }
                    else
                    {
                        typedSoFar += ch.ToString();
                    }
                }
                int index = Find(typedSoFar);
                if (index >= 0)
                {
                    grid.SelectedIndex = index;
                    grid.ScrollIntoView(grid.SelectedItem);
                }
                start = tick;
            }
        }

        protected abstract int Find(string text);

        protected DataGridColumn sorted;

        void grid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            this.sorted = e.Column;
        }

        void RegisterEvents(bool register)
        {
            if (register)
            {
                this.grid.BeginningEdit += new EventHandler<DataGridBeginningEditEventArgs>(grid_BeginningEdit);
                this.grid.RowEditEnding += new EventHandler<DataGridRowEditEndingEventArgs>(grid_RowEditEnding);
                this.grid.Sorting += new DataGridSortingEventHandler(grid_Sorting);
                this.grid.PreviewTextInput += new TextCompositionEventHandler(OnTextInput);
            }
            else
            {
                this.grid.BeginningEdit -= new EventHandler<DataGridBeginningEditEventArgs>(grid_BeginningEdit);
                this.grid.RowEditEnding -= new EventHandler<DataGridRowEditEndingEventArgs>(grid_RowEditEnding);
                this.grid.Sorting -= new DataGridSortingEventHandler(grid_Sorting);
                this.grid.PreviewTextInput -= new TextCompositionEventHandler(OnTextInput);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                RegisterEvents(false);
            }
        }
    }

    public class TransactionTypeToFind : TypeToFind
    {
        public TransactionTypeToFind(DataGrid grid)
            : base(grid)
        {
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        protected override int Find(string text)
        {
            if (sorted != null && !string.IsNullOrEmpty(text))
            {
                int i = 0;
                foreach (object o in grid.Items)
                {
                    string value = string.Empty;

                    Transaction t = o as Transaction;
                    if (t != null)
                    {
                        switch (sorted.SortMemberPath)
                        {
                            case "Number":
                                value = (t.Number == null) ? string.Empty : t.Number.ToString();
                                break;
                            case "Date":
                                value = t.Date.ToShortDateString();
                                break;
                            case "Caption":
                                value = t.Payee != null ? t.Payee.Name : string.Empty;
                                break;
                            case "StatusString":
                                value = t.StatusString;
                                break;
                            case "SalesTax":
                                value = t.SalesTax.ToString();
                                break;
                            case "Debit":
                                value = t.Debit.IsNull ? string.Empty : t.Debit.Value.ToString();
                                break;
                            case "Credit":
                                value = t.Credit.IsNull ? string.Empty : t.Credit.Value.ToString();
                                break;
                            case "Investment.Type":
                                if (t.Investment != null)
                                {
                                    value = t.Investment.Type.ToString();
                                }
                                break;
                            case "Investment.Security":
                                if (t.Investment != null && t.Investment.Security != null)
                                {
                                    value = t.Investment.Security.Name;
                                }
                                break;
                            case "Investment.Units":
                                if (t.Investment != null)
                                {
                                    value = t.Investment.Units.ToString();
                                }
                                break;
                            case "Investment.UnitPrice":
                                if (t.Investment != null)
                                {
                                    value = t.Investment.UnitPrice.ToString();
                                }
                                break;
                        }
                    }
                    else
                    {
                        Investment it = o as Investment;
                        if (it != null)
                        {
                            switch (sorted.SortMemberPath)
                            {
                                case "Security":
                                    value = it.Security == null ? string.Empty : it.Security.Name;
                                    break;
                                case "Type":
                                    value = it.Type.ToString();
                                    break;
                                case "Price":
                                    value = it.Price.ToString();
                                    break;
                                case "CostBasis":
                                    value = it.CostBasis.ToString();
                                    break;
                                case "MarketValue":
                                    value = it.MarketValue.ToString();
                                    break;
                                case "GainLoss":
                                    value = it.GainLoss.ToString();
                                    break;
                                case "Units":
                                    value = it.Units.ToString();
                                    break;
                                case "UnitPrice":
                                    value = it.UnitPrice.ToString();
                                    break;
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(value) && value.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                    i++;
                }
            }
            return -1;
        }
    }


    #region ObservableCollections

    public class TransactionCollection : FilteredObservableCollection<Transaction>
    {
        private MyMoney money;
        private Account account;
        private bool filterOnInvestmentInfo; // whether to include investment info in the quick filtering
        private bool filterOnAccountName; // whether to include account name in the quick filtering.
        private bool constructing = true;
        private IEnumerable<Transaction> transactions;

        public TransactionCollection(MyMoney money, Account a, IEnumerable<Transaction> data, bool filterOnAccountName, bool filterOnInvestmentInfo, string filter)
            : base(data)
        {
            this.money = money;
            this.account = a;
            this.filterOnAccountName = filterOnAccountName;
            this.filterOnInvestmentInfo = filterOnInvestmentInfo;
            this.Filter = filter;
            this.transactions = data;
            constructing = false;
        }

        // return the original unfiltered transactions
        public IEnumerable<Transaction> GetOriginalTransactions()
        {
            return this.transactions;
        }

        bool prompt;

        public bool Prompt
        {
            get { return this.prompt; }
            set { this.prompt = value; }
        }

        public Account Account { get => account; set => account = value; }

        protected override void InsertItem(int index, Transaction t)
        {
            base.InsertItem(index, t);
            if (t.Parent == null && !t.IsReadOnly)
            {
                t.Parent = money.Transactions;
                t.Id = -1; // Let the data model take care of transaction Id
                t.Date = DateTime.Today;

                if (t.Account == null)
                {
                    t.Account = account;
                }
                // The Begin/EndUpdate stops the TransactionControl from doing a Refresh which is what we want.
                if (!constructing)
                {
                    this.money.BeginUpdate(this);
                    this.money.Transactions.AddTransaction(t);
                    this.money.EndUpdate();
                }
            }
        }

        private bool ConfirmDelete()
        {
            return MessageBoxEx.Show("Do you want to delete this transaction?", string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes;
        }

        protected override void RemoveItem(int index)
        {
            Transaction t = this[index];
            if (t != null)
            {
                if (t.Status == TransactionStatus.Reconciled && t.Amount != 0)
                {
                    MessageBoxEx.Show("You cannot remove Reconciled transactions", string.Empty, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift) || !Prompt || ConfirmDelete())
                    {
                        this.money.BeginUpdate(this);
                        try
                        {
                            this.money.RemoveTransaction(t);
                            base.RemoveItem(index);
                        }
                        catch (Exception e)
                        {
                            MessageBoxEx.Show(e.Message, "Remove transaction", MessageBoxButton.OK, MessageBoxImage.Information);
                        }

                        this.money.EndUpdate();

                    }
                }
            }
        }

        public override bool IsMatch(Transaction t, FilterLiteral filterToken)
        {
            if (filterToken == null)
            {
                return true;
            }
            return Transactions.IsAnyFieldsMatching(t, filterToken, filterOnAccountName) || Transactions.IsSplitsMatching(t.Splits, filterToken) ||
                        (filterOnInvestmentInfo && t.Investment != null && IsMatch(t.Investment, filterToken));
        }

        internal static bool IsMatch(Investment i, FilterLiteral filter)
        {
            return (i.Security != null && (filter.MatchSubstring(i.Security.Name) || filter.MatchSubstring(i.Security.Symbol))) ||
                    filter.MatchDecimal(i.Price) ||
                    filter.MatchDecimal(i.UnitPrice);
        }
    }

    #endregion

    /// <summary>
    /// This class is here for performance reasons only.  The DataGridCell Template was too slow otherwise.
    /// </summary>    
    public class TransactionCell : Border
    {
        /* This class replaces the following XAML.
         * 
        <ControlTemplate TargetType="{x:Type DataGridCell}">
            <Border x:Name="CellBorder"
                        BorderBrush="{TemplateBinding BorderBrush}"  
                        BorderThickness="0" 
                        SnapsToDevicePixels="True">

                <ContentPresenter  SnapsToDevicePixels="{TemplateBinding SnapsToDevicePixels}"/>

            </Border>
         * 
            <ControlTemplate.Triggers>
                <DataTrigger Binding="{Binding Path=IsReconciling}" Value="True">
                    <Setter Property="Background" Value="{DynamicResource WalkaboutReconciledRowBackgroundBrush}" TargetName="CellBorder"/>
                </DataTrigger>

                <Trigger Property="IsSelected" Value="true">
                    <Setter Property="Background" Value="{DynamicResource ListItemSelectedBackgroundBrush}" TargetName="CellBorder"/>
                    <Setter Property="Foreground" Value="{DynamicResource ListItemSelectedForegroundBrush}"/>
                </Trigger>

                <DataTrigger Binding="{Binding Path=Unaccepted}" Value="True">
                    <Setter Property="FontWeight" Value="Bold"/>
                    <Setter Property="TextBlock.FontWeight" Value="Bold"/>
                </DataTrigger>

                <DataTrigger Binding="{Binding Path=Unaccepted}" Value="False">
                    <Setter Property="FontWeight" Value="Normal"/>
                    <Setter Property="TextBlock.FontWeight" Value="Normal"/>
                </DataTrigger>

                <DataTrigger Binding="{Binding Path=IsDown}" Value="True">
                    <Setter Property="Foreground" Value="{DynamicResource NegativeCurrencyForegroundBrush}"/>
                </DataTrigger>

                <DataTrigger Binding="{Binding Path=IsReadOnly}" Value="True">
                    <Setter Property="Foreground" Value="Gray"/>
                </DataTrigger>

            </ControlTemplate.Triggers>
        </ControlTemplate>
         */

        public TransactionCell()
        {
            this.DataContextChanged += new DependencyPropertyChangedEventHandler(OnDataContextChanged);
            this.Unloaded += (s, e) =>
            {
                OnUnloaded();
            };
            this.Loaded += (s, e) =>
            {
                OnLoaded();
            };
        }

        void OnUnloaded()
        {
            // stop listening
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
            }
        }

        void OnLoaded()
        {
            // start listening
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
                this.context.PropertyChanged += OnPropertyChanged;
                UpdateUI();
            }
        }

        Transaction context;
        DataGridCell cell;
        bool mouseOver;

        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            DataGridCell cell = this.GetParentObject() as DataGridCell;
            SetCell(cell);
            base.OnVisualParentChanged(oldParent);
        }

        void SetCell(DataGridCell newCell)
        {
            if (this.cell != null)
            {
                cell.Selected -= new RoutedEventHandler(OnCellSelectionChanged);
                cell.Unselected -= new RoutedEventHandler(OnCellSelectionChanged);
            }
            this.cell = newCell;
            if (cell != null)
            {
                cell.Selected += new RoutedEventHandler(OnCellSelectionChanged);
                cell.Unselected += new RoutedEventHandler(OnCellSelectionChanged);
            }
        }

        void OnCellSelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateBackground();
            UpdateForeground();
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Transaction t = e.NewValue as Transaction;
            if (t == null)
            {
                Investment i = e.NewValue as Investment;
                if (i != null)
                {
                    t = i.Transaction;
                }
            }
            if (t != this.context)
            {
                SetContext(t);
                UpdateUI();
            }
        }

        void UpdateUI() 
        { 
            UpdateBackground();
            UpdateForeground();
            UpdateFontWeight();
            UpdateBorder();
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            mouseOver = true;
            UpdateBackground();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            mouseOver = false;
            UpdateBackground();
            base.OnMouseLeave(e);
        }

        void SetContext(Transaction transaction)
        {
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
            }
            this.context = transaction;
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
                this.context.PropertyChanged += OnPropertyChanged;
            }
        }

        void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "IsReconciling":
                    UpdateBackground();
                    break;
                case "Unaccepted":
                    UpdateFontWeight();
                    break;
                case "IsDown":
                    UpdateForeground();
                    break;
                case "IsReadOnly":
                    UpdateForeground();
                    break;
                case "AttachmentDropTarget":
                    UpdateBorder();
                    break;
            }
        }

        void UpdateBackground()
        {
            /*
               <DataTrigger Binding="{Binding Path=IsReconciling}" Value="True">
                    <Setter Property="Background" Value="{DynamicResource WalkaboutReconciledRowBackgroundBrush}" TargetName="CellBorder"/>
                </DataTrigger>

                <Trigger Property="IsSelected" Value="true">
                    <Setter Property="Background" Value="{DynamicResource ListItemSelectedBackgroundBrush}" TargetName="CellBorder"/>
                </Trigger>
             */
            bool isReconciling = false;
            bool isSelected = IsSelected;
            if (this.context != null)
            {
                isReconciling = this.context.IsReconciling;
            }
            Brush backgroundBrush = null;
            if (isSelected)
            {
                if (mouseOver)
                {
                    backgroundBrush = AppTheme.Instance.GetThemedBrush("ListItemSelectedBackgroundMouseOverBrush");
                }
                else
                {
                    backgroundBrush = AppTheme.Instance.GetThemedBrush("ListItemSelectedBackgroundBrush");
                }
            }
            else if (isReconciling)
            {
                backgroundBrush = AppTheme.Instance.GetThemedBrush("ListItemReconcilingBackgroundBrush");
            }

            if (backgroundBrush == null)
            {
                // allow to go back to "AlternatingColor" mode.
                this.ClearValue(Border.BackgroundProperty);
            }
            else
            {
                this.SetValue(Border.BackgroundProperty, backgroundBrush);
            }
        }

        bool IsSelected
        {
            get
            {
                return (cell != null) ? cell.IsSelected : false;
            }
        }

        void UpdateForeground()
        {
            /* 
               <Trigger Property="IsSelected" Value="true">
                    <Setter Property="Foreground" Value="{DynamicResource ListItemSelectedForegroundBrush}"/>
                </Trigger>
                <DataTrigger Binding="{Binding Path=IsDown}" Value="True">
                    <Setter Property="Foreground" Value="{DynamicResource NegativeCurrencyForegroundBrush}"/>
                </DataTrigger>

                <DataTrigger Binding="{Binding Path=IsReadOnly}" Value="True">
                    <Setter Property="Foreground" Value="Gray"/>
                </DataTrigger>
             */

            bool isSelected = IsSelected;
            bool isDown = false;
            bool isReadOnly = false;
            bool isUnaccepted = false;

            if (this.context != null)
            {
                isDown = this.context.IsDown;
                isReadOnly = this.context.IsReadOnly;
                isUnaccepted = this.context.Unaccepted;
            }
            string foregroundBrushName = "ListItemForegroundBrush";

            if (isReadOnly)
            {
                foregroundBrushName = "ListItemSelectedForegroundDisabledBrush";
            }
            else if (isDown)
            {
                foregroundBrushName = "ListItemSelectedForegroundNegativeBrush";
            }
            else if (isSelected)
            {
                if (isUnaccepted)
                {
                    foregroundBrushName = "ListItemSelectedForegroundBrush";
                }
                else
                {
                    foregroundBrushName = "ListItemSelectedForegroundLowBrush";
                }
            }
            else if (isUnaccepted)
            {
                foregroundBrushName = "ListItemForegroundUnacceptedBrush";
            }

            // establish the new color.
            var brush = AppTheme.Instance.GetThemedBrush(foregroundBrushName);
            SetValue(TextBlock.ForegroundProperty, brush);
            if (cell != null)
            {
                cell.SetValue(TextBlock.ForegroundProperty, brush);
            }
        }

        void UpdateFontWeight()
        {
            /*              
                <DataTrigger Binding="{Binding Path=Unaccepted}" Value="True">
                    <Setter Property="TextBlock.FontWeight" Value="Bold"/>
                </DataTrigger>

                <DataTrigger Binding="{Binding Path=Unaccepted}" Value="False">
                    <Setter Property="TextBlock.FontWeight" Value="Normal"/>
                </DataTrigger>
             */

            bool unaccepted = false;

            if (this.context != null)
            {
                unaccepted = this.context.Unaccepted;
            }
            if (unaccepted)
            {
                this.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
                if (cell != null)
                {
                    cell.SetValue(DataGridCell.FontWeightProperty, FontWeights.Bold);
                }
            }
            else
            {
                ClearValue(TextBlock.FontWeightProperty);
                if (cell != null)
                {
                    cell.ClearValue(DataGridCell.FontWeightProperty);
                }
            }
        }

        void UpdateBorder()
        {
            if (this.context != null && this.cell != null)
            {
                if (cell.Column.SortMemberPath == "HasAttachment")
                {
                    if (this.context.AttachmentDropTarget)
                    {
                        var brush = AppTheme.Instance.GetThemedBrush("ValidDropTargetFeedbackBrush");
                        this.SetValue(DataGridCell.BorderBrushProperty, brush);
                        this.BorderThickness = new Thickness(1);
                    }
                    else
                    {
                        this.BorderThickness = new Thickness(0);
                    }
                }
            }
        }
    }

    public class TransactionAttachmentIcon : Border
    {
        Transaction context;

        public TransactionAttachmentIcon()
        {
            this.DataContextChanged += OnDataContextChanged;
            this.Unloaded += (s, e) =>
            {
                OnUnloaded();
            };
            this.Loaded += (s, e) =>
            {
                OnLoaded();
            };
        }

        void OnUnloaded()
        {
            // stop listening
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnContextPropertyChanged;
            }
        }

        void OnLoaded()
        {
            // start listening
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnContextPropertyChanged;
                this.context.PropertyChanged += OnContextPropertyChanged;
                UpdateIcon();
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue != this.context)
            {
                // WPF is recycling this object for a different row!
                SetContext(e.NewValue as Transaction);
            }
        }

        void SetContext(Transaction t)
        {
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnContextPropertyChanged;
            }
            this.context = t;
            if (this.context != null)
            {
                this.context.PropertyChanged += OnContextPropertyChanged;
            }
            UpdateIcon();
        }

        private void OnContextPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "HasAttachment")
            {
                UpdateIcon();
            }
        }

        void UpdateIcon()
        {
            if (this.context == null || !this.context.HasAttachment)
            {
                this.Child = null;
            }
            else if (this.Child == null)
            {
                var icon = new SymbolIcon()
                {
                    Symbol = Symbol.Attach,
                    Foreground = AppTheme.Instance.GetThemedBrush("ListItemForegroundBrush")
                };

                SymbolIconHackery.SetFontSize(icon, 16.0); // reduce fontsize from default 20.0.
                this.Child = icon;
            }
        }
    }

    internal static class SymbolIconHackery {
        internal static Action<SymbolIcon, double> setter;

        internal static void SetFontSize(SymbolIcon icon, double fontSize)
        {
            if (setter == null)
            {
                setter = CompiledPropertySetter.CompileSetter<SymbolIcon, double>("FontSize",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            }
            setter(icon, fontSize);
        }
    }

    public class TransactionAttachmentColumn : DataGridColumn
    {

        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            var icon = new SymbolIcon()
            {
                Symbol = Symbol.Attach,
                Foreground = AppTheme.Instance.GetThemedBrush("ListItemForegroundBrush")
            };
            SymbolIconHackery.SetFontSize(icon, 16.0); // reduce fontsize from default 20.0.
            return new Button()
            {
                Command = TransactionsView.CommandScanAttachment,
                Padding = new Thickness(1),
                Focusable = false,
                Style = (Style)cell.FindResource("DefaultButtonStyle"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Content = icon
            };
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            return new TransactionAttachmentIcon();
        }
    }

    /*
        <DataGridTemplateColumn Header="Num" CellTemplate="{StaticResource myTemplateNumber}"  CellEditingTemplate="{StaticResource myTemplateNumberEdit}"
                                        SortMemberPath="Number" />     
        <!-- NUMBER -->
        <DataTemplate x:Key="myTemplateNumber">
            <TextBlock Text="{Binding Number, Converter={StaticResource NullableValueConverter}}" VerticalAlignment="Top"/>
        </DataTemplate>

        <DataTemplate x:Key="myTemplateNumberEdit">
            <TextBox Style="{StaticResource GridTextBoxStyle}" Text="{Binding Number, StringFormat={}{0:N}, Converter={StaticResource NullableValueConverter}, Mode=TwoWay, ValidatesOnDataErrors=True, ValidatesOnExceptions=True}" 
                VerticalAlignment="Top"/>
        </DataTemplate>
    */
    public class TransactionNumberColumn : DataGridColumn
    {
        const string FieldName = "Number";

        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            TextBox box = new TextBox()
            {
                VerticalAlignment = VerticalAlignment.Top,
                Name = "EditorFor" + FieldName
            };
            box.SetResourceReference(TextBox.StyleProperty, "GridTextBoxStyle");
            box.SetBinding(TextBox.TextProperty, new Binding("Number")
            {
                StringFormat = "N",
                Converter = new NullableValueConverter(),
                Mode = BindingMode.TwoWay,
                ValidatesOnDataErrors = true,
                ValidatesOnExceptions = true
            });
            return box;
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            TextBox box = cell.Content as TextBox;
            Binding binding = null;
            if (box != null)
            {
                var e = box.GetBindingExpression(TextBox.TextProperty);
                if (e != null)
                {
                    binding = e.ParentBinding;
                }
            }

            return new TransactionTextField(FieldName, (t) => t.Number, binding, dataItem)
            {
                VerticalAlignment = VerticalAlignment.Top
            };
        }
    }

    /*
     <StackPanel MinWidth="300" VerticalAlignment="Top" Focusable="false" Margin="2,0,0,0">
                <Border BorderThickness="0,0,0,1" BorderBrush="Transparent" Focusable="false">
                    <TextBlock Text="{Binding PayeeOrTransferCaption, Converter={StaticResource NullableValueConverter}}" 
                               VerticalAlignment="Top" />
                </Border>
                <Border BorderThickness="0,0,0,1" BorderBrush="Transparent" Focusable="false">
                    <TextBlock Text="{Binding Category.Name, Converter={StaticResource NullableValueConverter}}" 
                           Visibility="{Binding RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type views:TransactionsView}}, Path=OneLineView, Converter={StaticResource FalseToVisible}}"/>
                </Border>
                <Border BorderThickness="0,0,0,1" BorderBrush="Transparent" Focusable="false">
                    <TextBlock Text="{Binding Memo, Converter={StaticResource NullableValueConverter}}"  FontStyle="Italic" Opacity=".7"                            
                           Visibility="{Binding RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type views:TransactionsView}}, Path=OneLineView, Converter={StaticResource FalseToVisible}}"/>
                </Border>
            </StackPanel>
     */
    public class TransactionPayeeCategoryMemoColumn : DataGridColumn
    {
        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            DataTemplate template = (DataTemplate)cell.FindResource("myTemplatePayeeCategoryMemoEdit");
            FrameworkElement element = template.LoadContent() as FrameworkElement;
            return element;
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            StackPanel stack = cell.Content as StackPanel;
            Binding payee = null;
            Binding category = null;
            Binding memo = null;
            if (stack != null)
            {
                payee = new Binding("PayeeOrTransferCaption");
                category = new Binding("CategoryName");
                memo = new Binding("Memo");
            }
            TransactionsView view = WpfHelper.FindAncestor<TransactionsView>(cell);
            if (view == null)
            {
                // has this item been deleted?
                return null;
            }
            return new TransactionPayeeCategoryMemoField(view, payee, category, memo, dataItem);
        }
    }

    public class TransactionPayeeCategoryMemoField : StackPanel
    {
        TransactionsView view;
        TransactionTextField payeeField;
        TransactionTextField categoryField;
        TransactionTextField memoField;


        public TransactionPayeeCategoryMemoField(TransactionsView view, Binding payee, Binding category, Binding memo, object dataItem)
        {
            // Visibility="{Binding RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type views:TransactionsView}}, 
            //          Path=OneLineView, Converter={StaticResource FalseToVisible}}"
            this.view = view;
            view.OneLineViewChanged -= new EventHandler(OnOneLineViewChanged);
            view.OneLineViewChanged += new EventHandler(OnOneLineViewChanged);

            this.MinWidth = 300;
            this.VerticalAlignment = VerticalAlignment.Top;
            this.Focusable = false;
            this.Margin = new Thickness(2, 1, 0, 0);

            this.payeeField = new TransactionTextField("PayeeOrTransferCaption", (t) => t.PayeeOrTransferCaption, payee, dataItem);

            this.Children.Add(new Border()
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = Brushes.Transparent,
                Focusable = false,
                Child = payeeField
            });

            if (!view.OneLineView || category != null || memo != null)
            {
                CreateCategoryMemo(category, memo, dataItem);
                if (view.OneLineView)
                {
                    // hide the text blocks that contain these edited values for now.
                    OnOneLineViewChanged(this, EventArgs.Empty);
                }
            }

        }

        public TransactionTextField PayeeField
        {
            get { return payeeField; }
            set { payeeField = value; }
        }

        public TransactionTextField CategoryField
        {
            get { return categoryField; }
            set { categoryField = value; }
        }

        public TransactionTextField MemoField
        {
            get { return memoField; }
            set { memoField = value; }
        }

        void CreateCategoryMemo(Binding category, Binding memo, object dataItem)
        {
            if (this.categoryField == null)
            {
                this.categoryField = new TransactionTextField("CategoryName", (t) => t.Category == null ? "" : t.Category.Name, null, dataItem);
                this.memoField = new TransactionTextField("Memo", (t) => t.Memo, null, dataItem);

                this.Children.Add(new Border()
                {
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = Brushes.Transparent,
                    Focusable = false,
                    Child = categoryField
                });
                this.Children.Add(new Border()
                {
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = Brushes.Transparent,
                    Focusable = false,
                    Child = memoField
                });
            }
        }

        void OnOneLineViewChanged(object sender, EventArgs e)
        {
            if (view.OneLineView)
            {
                if (this.categoryField != null) this.categoryField.Visibility = System.Windows.Visibility.Collapsed;
                if (this.memoField != null) this.memoField.Visibility = System.Windows.Visibility.Collapsed;
            }
            else
            {
                if (this.categoryField == null)
                {
                    CreateCategoryMemo(null, null, null);
                }
                else
                {
                    this.categoryField.Visibility = System.Windows.Visibility.Visible;
                    this.memoField.Visibility = System.Windows.Visibility.Visible;
                }
            }
        }

    }

    public class TransactionCategoryColorColumn : DataGridColumn
    {
        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            throw new NotImplementedException();
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            /*
             <Border Margin="2" BorderThickness="0,0,0,0" Height="8" Width="8" VerticalAlignment="Center"  HorizontalAlignment="Center" 
                    Background="{Binding Path=CategoryNonNull.InheritedColor, Converter={StaticResource CategoryToBrush}}" 
                    CornerRadius="0" Focusable="false" />
             */
            Border border = new Border()
            {
                Margin = new Thickness(2),
                BorderThickness = new Thickness(0),
                Height = 8,
                Width = 8,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                CornerRadius = new CornerRadius(0),
                Focusable = false
            };
            border.SetBinding(Border.BackgroundProperty, new Binding("CategoryNonNull.InheritedColor")
            {
                Converter = new CategoryToBrush()
            });
            return border;
        }
    }

    public class TransactionStatusColumn : DataGridColumn
    {
        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            throw new NotImplementedException();
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            return new TransactionStatusButton();
        }
    }

    public class TransactionAnchorColumn : DataGridColumn
    {
        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            throw new NotImplementedException();
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            return new TransactionAnchor();
        }
    }

    public class TransactionConnectorAdorner : Adorner
    {
        const double connectorSize = 30;
        const double penWidth = 5;
        PathGeometry connector;
        RoundedButton mainButton;
        CloseBox closeBox;
        Point connectorCenter;
        Point startPoint;
        Point endPoint;
        bool startOffscreen;
        bool endOffscreen;

        public TransactionConnectorAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
            mainButton = new RoundedButton();
            mainButton.Content = "Merge";
            mainButton.ToolTip = "These transactions appear to be duplicates, click this button to merge them";
            mainButton.CornerRadius = new CornerRadius(5);
            mainButton.BorderThickness = new Thickness(3);
            mainButton.FontWeight = FontWeights.Bold;

            this.AddVisualChild(mainButton);
            closeBox = new CloseBox();
            this.AddVisualChild(closeBox);
            closeBox.Opacity = 1;
        }

        public Point StartPoint
        {
            get => this.startPoint;
            set
            {
                if (this.startPoint != value)
                {
                    this.startPoint = value;
                    InvalidateVisual();
                }
            }
        }

        public Point EndPoint
        {
            get => this.endPoint;
            set
            {
                if (this.endPoint != value)
                {
                    this.endPoint = value;
                    InvalidateVisual();
                }
            }
        }

        public bool StartOffscreen
        {
            get => startOffscreen;
            set
            {
                if (this.startOffscreen != value)
                {
                    this.startOffscreen = value;
                    InvalidateVisual();
                }
            }
        }

        public bool EndOffscreen
        {
            get => endOffscreen;
            set
            {
                if (this.endOffscreen != value)
                {
                    this.endOffscreen = value;
                    InvalidateVisual();
                }
            }
        }

        internal Button MergeButton { get { return this.mainButton; } }

        internal Button CloseBox { get { return this.closeBox; } }

        protected override Size MeasureOverride(Size constraint)
        {
            mainButton.Measure(constraint);
            closeBox.Measure(constraint);

            CreateConnectorGeometry();

            if (connector != null)
            {
                InvalidateVisual();

                return connector.Bounds.Size;
            }
            return constraint;
        }

        const double strokeThickness = 3;

        protected override Size ArrangeOverride(Size finalSize)
        {
            Brush stroke = (Brush)FindResource("TransactionConnectorBrush");
            Brush foreground = (Brush)FindResource("TransactionConnectorForegroundBrush");
            Brush background = (Brush)FindResource("TransactionConnectorButtonBackground"); 
            Brush pressed = (Brush)FindResource("TransactionConnectorButtonPressedBackground");
            Brush mouseOver = (Brush)FindResource("TransactionConnectorButtonMouseOverBackground");

            mainButton.Background = background;
            mainButton.BorderBrush = stroke;
            mainButton.Foreground = stroke;
            mainButton.BorderThickness = new Thickness(strokeThickness);
            mainButton.MouseOverBackground = mouseOver;
            mainButton.MouseOverBorder = stroke;
            mainButton.MouseOverForeground = foreground;
            mainButton.MousePressedBackground = pressed;
            mainButton.MousePressedBorder = stroke;
            mainButton.MousePressedForeground = foreground;

            closeBox.Foreground = stroke;
            closeBox.BorderThickness = new Thickness(1);
            closeBox.Background = background;
            closeBox.MouseOverBackground = mouseOver;
            closeBox.MouseOverForeground = foreground;
            closeBox.MousePressedBackground = pressed;
            closeBox.MousePressedForeground = foreground;

            CreateConnectorGeometry();

            Size buttonSize = mainButton.DesiredSize;
            Point pos = this.connectorCenter;
            pos.X -= (buttonSize.Width / 2);
            pos.Y -= (buttonSize.Height / 2);

            mainButton.Arrange(new Rect(pos.X, pos.Y, buttonSize.Width + (2 * strokeThickness), buttonSize.Height + (2 * strokeThickness)));

            pos.X += buttonSize.Width + (closeBox.DesiredSize.Width / 2) - strokeThickness;
            pos.Y -= (closeBox.DesiredSize.Height / 2) - strokeThickness;
            closeBox.Arrange(new Rect(pos.X, pos.Y, closeBox.DesiredSize.Width, closeBox.DesiredSize.Height));

            return base.ArrangeOverride(finalSize);
        }

        protected override int VisualChildrenCount
        {
            get
            {
                return 2;
            }
        }

        protected override Visual GetVisualChild(int index)
        {
            if (index == 0)
            {
                return mainButton;
            }
            else if (index == 1)
            {
                return closeBox;
            }
            return null;
        }

        private Geometry CreateConnectorGeometry()
        {
            double arcSize = 10;
            double arcY = arcSize;
            double arcAngle = 90;
            double offsetY = startPoint.Y;
            double height = endPoint.Y - startPoint.Y;
            SweepDirection direction = SweepDirection.Clockwise;
            if (startPoint.Y > endPoint.Y)
            {
                // then the connector is going up, which means arcs are anti-clockwise.
                arcY = -arcSize;
                arcAngle = -90;
                direction = SweepDirection.Counterclockwise;
            }
            double arcX = arcSize;
            double x = startPoint.X;
            PathFigure connector;
            if (startOffscreen && endOffscreen)
            {
                connector = new PathFigure(new Point(x + connectorSize, offsetY), new PathSegment[] {
                    new LineSegment(new Point(x + connectorSize, height + offsetY - arcY), true)
                }, false);
            }
            else if (startOffscreen)
            {
                connector = new PathFigure(new Point(x + connectorSize, offsetY), new PathSegment[] {
                    new LineSegment(new Point(x + connectorSize, height + offsetY - arcY), true),
                    new ArcSegment(new Point(x + connectorSize - arcX, height + offsetY), new Size(arcSize, arcSize), arcAngle, false, direction, true),
                    new LineSegment(new Point(x, height + offsetY), true),
                }, false);
            }
            else if (endOffscreen)
            {
                connector = new PathFigure(new Point(x, offsetY), new PathSegment[] {
                    new LineSegment(new Point(x + connectorSize - arcX, offsetY), true),
                    new ArcSegment(new Point(x + connectorSize, offsetY + arcY), new Size(arcSize, arcSize), arcAngle, false, direction, true),
                    new LineSegment(new Point(x + connectorSize, height + offsetY - arcY), true)
                }, false);
            }
            else
            {
                connector = new PathFigure(new Point(x, offsetY), new PathSegment[] {
                    new LineSegment(new Point(x + connectorSize - arcX, offsetY), true),
                    new ArcSegment(new Point(x + connectorSize, offsetY + arcY), new Size(arcSize, arcSize), arcAngle, false, direction, true),
                    new LineSegment(new Point(x + connectorSize, height + offsetY - arcY), true),
                    new ArcSegment(new Point(x + connectorSize - arcX, height + offsetY), new Size(arcSize, arcSize), arcAngle, false, direction, true),
                    new LineSegment(new Point(x, height + offsetY), true),
                }, false);
            }

            var path = new PathGeometry();
            path.Figures.Add(connector);

            Brush brush = (Brush)FindResource("TransactionConnectorBrush");

            this.connectorCenter = new Point(x + connectorSize, startPoint.Y + (height + penWidth) / 2);
            this.connector = path;

            return path;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            Geometry path = CreateConnectorGeometry();
            if (path == null)
            {
                return;
            }

            Brush brush = (Brush)FindResource("TransactionConnectorBrush");

            drawingContext.DrawGeometry(null, new Pen(brush, penWidth) { LineJoin = PenLineJoin.Round }, path);
        }

    }

    public class TransactionConnector
    {
        Transaction sourceTransaction;
        Transaction targetTransaction;
        TransactionAnchor sourceAnchor;
        TransactionAnchor targetAnchor;
        TransactionConnectorAdorner adorner;
        AdornerLayer layer;
        DataGridRow sourceRow;
        DataGridRow targetRow;
        MoneyDataGrid grid;

        public TransactionConnector(MoneyDataGrid grid)
        {
            this.grid = grid;
            this.grid.LoadingRow += OnRowLoadUnload;
            this.grid.UnloadingRow += OnRowLoadUnload;
            // we also have to watch scrolling in case the anchor moves too far offscreen
            // such that it becomes invalid...
            grid.ScrollChanged += OnGridScrollChanged;            
        }

        void OnRowLoadUnload(object sender, DataGridRowEventArgs e)
        {
            if (e.Row == sourceRow || e.Row == targetRow)
            {
                // might need to reposition the adorner...
                Connect(sourceTransaction, targetTransaction);
            }
        }

        void OnGridScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // might need to reposition the adorner...
            Connect(sourceTransaction, targetTransaction);
        }

        public Transaction Source { get { return this.sourceTransaction; } }

        public Transaction Target { get { return this.targetTransaction; } }

        public void Connect(Transaction t, Transaction u)
        {
            this.sourceTransaction = t;
            this.targetTransaction = u;

            TransactionCollection tc = (TransactionCollection)this.grid.ItemsSource;
            int i = tc.IndexOf(this.sourceTransaction);
            int j = tc.IndexOf(this.targetTransaction);

            var visibleRows = this.grid.GetVisibleRows();
            if ((i < visibleRows.Item1 && j < visibleRows.Item1 ) ||
                (i > visibleRows.Item2 && j > visibleRows.Item2))
            {
                // then the merged range is entirely out of view.
                if (this.adorner != null)
                {
                    this.adorner.Visibility = Visibility.Collapsed;
                }
                return;
            }

            sourceRow = this.grid.GetRowFromItem(t);
            targetRow = this.grid.GetRowFromItem(u);

            Point startPoint = (this.adorner != null) ? this.adorner.StartPoint : new Point(0, 0);
            Point endPoint = (this.adorner != null) ? this.adorner.EndPoint : new Point(0, 0);
            bool startOffscreen = true;
            bool endOffscreen = true;

            if (sourceRow != null)
            {
                sourceAnchor = sourceRow.FindFirstDescendantOfType<TransactionAnchor>();
                if (sourceAnchor != null)
                {
                    startOffscreen = false;
                    startPoint = sourceAnchor.TransformToAncestor(this.grid).Transform(new Point(0, sourceRow.ActualHeight / 2));
                    if (startPoint.Y > this.grid.ActualHeight)
                    {
                        startOffscreen = true;
                        startPoint.Y = this.grid.ActualHeight;
                    }
                    if (startPoint.Y < 0)
                    {
                        startOffscreen = true;
                        startPoint.Y = 0;
                    }
                    endPoint.X = startPoint.X;
                }
            }
            if (startOffscreen)
            {
                startPoint.Y = (i < j) ? 0 : this.grid.ActualHeight;
            }
            if (targetRow != null)
            {
                targetAnchor = WpfHelper.FindFirstDescendantOfType<TransactionAnchor>(targetRow);
                if (targetAnchor != null)
                {
                    endOffscreen = false;
                    endPoint = targetAnchor.TransformToAncestor(this.grid).Transform(new Point(0, targetRow.ActualHeight / 2));

                    if (endPoint.Y > this.grid.ActualHeight)
                    {
                        endOffscreen = true;
                        endPoint.Y = this.grid.ActualHeight;
                    }
                    if (endPoint.Y < 0)
                    {
                        endOffscreen = true;
                        endPoint.Y = 0;
                    }
                    if (sourceRow == null)
                    {
                        startPoint.X = endPoint.X;
                    }
                }
            }
            if (endOffscreen)
            {
                endPoint.Y = (i < j) ? this.grid.ActualHeight : 0;
            }

            if (this.adorner == null)
            {
                this.adorner = new TransactionConnectorAdorner(this.grid);
                layer = AdornerLayer.GetAdornerLayer(this.grid);
                layer.Add(this.adorner);
                // relay these events up
                this.adorner.MergeButton.Click += OnMainButtonClick;
                this.adorner.CloseBox.Click += OnCloseBoxClick;
            }

            this.adorner.StartOffscreen = startOffscreen;
            this.adorner.EndOffscreen = endOffscreen;
            this.adorner.StartPoint = startPoint;
            this.adorner.EndPoint = endPoint;
            this.adorner.Visibility = Visibility.Visible;
        }

        void OnCloseBoxClick(object sender, RoutedEventArgs e)
        {
            OnClosed();
        }

        void OnMainButtonClick(object sender, RoutedEventArgs e)
        {
            OnClicked();
        }

        public event EventHandler Clicked;

        private void OnClicked()
        {
            if (Clicked != null)
            {
                Clicked(this, EventArgs.Empty);
            }
            Disconnect();
        }

        public event EventHandler Closed;

        private void OnClosed()
        {
            if (Closed != null)
            {
                Closed(this, EventArgs.Empty);
            }
            Disconnect();
        }

        internal void Disconnect()
        {
            if (this.sourceAnchor != null)
            {
                sourceAnchor.DataContextChanged -= OnDataContextChanged;
            }
            if (this.targetAnchor != null)
            {
                targetAnchor.DataContextChanged -= OnDataContextChanged;
            }
            this.sourceAnchor = this.targetAnchor = null;

            if (this.layer != null)
            {
                layer.Remove(this.adorner);
                this.layer = null;
                this.adorner = null;
            }

            grid.ScrollChanged -= OnGridScrollChanged;

            this.sourceRow = this.targetRow = null;
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Disconnect();
        }


        public bool HasFocus
        {
            get
            {
                return (adorner != null && adorner.IsKeyboardFocusWithin);
            }
        }
    }


    /// <summary>
    /// This class is a place holder for the end of the transaction grid row so we can position things like
    /// the TransactionConnector.
    /// </summary>
    public class TransactionAnchor : Border
    {
        public TransactionAnchor()
        {
            this.Width = 10;
        }
    }

    public class PreserveDecimalDigitsValueConverter : IValueConverter
    {
        public int GetDecimalDigits(decimal d)
        {
            int digits = 0;
            decimal x = d - (int)d;
            while (x != 0)
            {
                digits++;
                x *= 10;
                x = x - (int)x;
            }
            return Math.Max(Math.Min(digits, 5), 2);
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(string))
            {
                throw new Exception("Unexpected target type passed to PreserveDecimalDigitsValueConverter.Convert : " + targetType.Name);
            }
            if (value == null)
            {
                return "";
            }

            Type valueType = value.GetType();
            if (valueType == typeof(SqlDecimal))
            {
                SqlDecimal d = (SqlDecimal)value;
                if (d.IsNull)
                {
                    return "";
                }
                return d.Value.ToString("N" + GetDecimalDigits(d.Value));
            }
            else if (valueType == typeof(Decimal))
            {
                decimal d = (decimal)value;
                return d.ToString("N" + GetDecimalDigits(d));
            }
            else if (valueType == typeof(DateTime))
            {
                return ((DateTime)value).ToString("d");
            }
            else
            {
                return value.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && value.GetType() != typeof(string))
            {
                throw new Exception("Unexpected value type passed to PreserveDecimalDigitsValueConverter.ConvertBack : " + value.GetType().Name);
            }
            string s = (string)value;

            if (targetType == typeof(SqlDecimal))
            {
                if (string.IsNullOrWhiteSpace(s))
                {
                    return new SqlDecimal();
                }
                return SqlDecimal.Parse(s);
            }
            else if (targetType == typeof(Decimal))
            {
                if (string.IsNullOrWhiteSpace(s)) return 0D;
                return decimal.Parse(s);
            }
            else if (targetType == typeof(DateTime))
            {
                if (string.IsNullOrWhiteSpace(s)) return DateTime.Now;
                return DateTime.Parse(s);
            }
            else if (targetType == typeof(string))
            {
                return s;
            }
            else
            {
                throw new Exception("Unexpected target type passed to PreserveDecimalDigitsValueConverter.ConvertBack : " + value.GetType().Name);
            }
        }
    }

    public class TransactionNumericColumn : DataGridColumn
    {
        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            /*
             <TextBox Style="{StaticResource NumericTextBoxStyle}" 
             *     Text="{Binding Tax, StringFormat={}{0:N}, Converter={StaticResource SqlDecimalToDecimalConverter}, 
             *            Mode=TwoWay, ValidatesOnDataErrors=True, ValidatesOnExceptions=True}" VerticalAlignment="Top"/>
             */
            TextBox box = new TextBox()
            {
                VerticalAlignment = VerticalAlignment.Top,
                TextAlignment = TextAlignment.Right,
                Style = (Style)cell.FindResource("DefaultTextBoxStyle"),
                Name = this.SortMemberPath
            };
            ModernWpf.Controls.Primitives.TextBoxHelper.SetIsEnabled(box, false);

            if (this.SortMemberPath == "Debit" || this.SortMemberPath == "Credit")
            {
                throw new Exception("Should be using TransactionAmountColumn for these");
            }

            box.SetResourceReference(TextBox.StyleProperty, "NumericTextBoxStyle");
            box.SetBinding(TextBox.TextProperty, new Binding(this.SortMemberPath)
            {
                StringFormat = "N5",
                Converter = new PreserveDecimalDigitsValueConverter(),
                Mode = BindingMode.TwoWay,
                ValidatesOnDataErrors = true,
                ValidatesOnExceptions = true
            });
            return box;
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            Binding binding = null;
            TextBox box = cell.Content as TextBox;
            if (box != null)
            {
                binding = new Binding(this.SortMemberPath)
                {
                    StringFormat = "N"
                };
            }

            Func<Transaction, string> f = null;
            switch(this.SortMemberPath)
            {
                case "SalesTax":
                    f = (t) => GetStringValue(t.SalesTax);
                    break;
                case "Balance":
                    f = (t) => t.Balance.ToString("N"); // show zeros
                    break;
                case "InvestmentUnits":
                    f = (t) => GetStringValue(t.InvestmentUnits);
                    break;
                case "InvestmentUnitPrice":
                    f = (t) => GetStringValue(t.InvestmentUnitPrice);
                    break;
                case "CurrentUnits":
                    f = (t) => t.Investment == null ? "" : GetStringValue(t.Investment.CurrentUnits);
                    break;
                case "RunningUnits":
                    f = (t) => GetStringValue(t.RunningUnits, "N0");
                    break;
                case "CurrentUnitPrice":
                    f = (t) => t.Investment == null ? "" : GetStringValue(t.Investment.CurrentUnitPrice);
                    break;
                case "RunningBalance":
                    f = (t) => GetStringValue(t.RunningBalance);
                    break;
                default:
                    throw new NotImplementedException("unexpected name " + this.SortMemberPath);
            }

            return new TransactionTextField(this.SortMemberPath, f, binding, dataItem)
            {
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Style = this.TextBlockStyle
            };

        }

        private static string GetStringValue(SqlDecimal s)
        {
            if (s.IsNull)
            {
                return "";
            }
            return GetStringValue(s.Value);
        }

        private string GetStringValue(decimal d, string format = "N")
        {
            if (d == 0)
            {
                return "";
            }
            return d.ToString(format);
        }

        public Style TextBlockStyle
        {
            get { return (Style)GetValue(TextBlockStyleProperty); }
            set { SetValue(TextBlockStyleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for TextBlockStyle.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextBlockStyleProperty =
            DependencyProperty.Register("TextBlockStyle", typeof(Style), typeof(TransactionNumericColumn));

        protected override bool CommitCellEdit(FrameworkElement editingElement)
        {
            try
            {
                return base.CommitCellEdit(editingElement);
            }
            catch (Exception ex)
            {
                TextBox box = editingElement as TextBox;
                if (box != null)
                {
                    var binding = box.BindingGroup.BindingExpressions.FirstOrDefault();
                    if (binding != null)
                    {
                        Validation.MarkInvalid(binding, new ValidationError(new ExceptionValidationRule(), binding, ex.Message, ex));
                    }
                }
                return false;
            }
        }

    }

    public class TransactionTextField : TextBlock
    {
        Transaction context;
        string fieldName;
        bool hasBinding;
        Func<Transaction, string> getter;

        public TransactionTextField(string name, Func<Transaction, string> getter, Binding binding, object dataItem)
        {
            this.getter = getter;
            this.Margin = new Thickness(2, 1, 3, 0);
            this.fieldName = name;
            if (binding != null)
            {
                this.hasBinding = true;
                // inherit the binding from the editor control so we display the same uncomitted value.
                this.SetBinding(TextBlock.TextProperty, binding);
            }

            SetContext(dataItem as Transaction);
            this.DataContextChanged += new DependencyPropertyChangedEventHandler(OnDataContextChanged);
            this.Unloaded += (s, e) =>
            {
                OnUnloaded();
            };
            this.Loaded += (s, e) =>
            {
                OnLoaded();
            };
        }

        void OnUnloaded()
        {
            // stop listening
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
            }
        }

        void OnLoaded()
        {
            // start listening
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
                this.context.PropertyChanged += OnPropertyChanged;
                UpdateLabel();
            }
        }

        public string FieldName => this.fieldName;

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Transaction t = e.NewValue as Transaction;
            if (t == null)
            {
                Investment i = e.NewValue as Investment;
                if (i != null)
                {
                    t = i.Transaction;
                }
            }
            if (t != this.context)
            {
                SetContext(t);
            }
        }

        void SetContext(Transaction transaction)
        {
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
            }
            this.context = transaction;
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
                this.context.PropertyChanged += OnPropertyChanged;
            }
            UpdateLabel();
        }

        private void SetText(string text)
        {
            if (text != this.Text)
            {
                this.Text = text;
            }
        }

        private void UpdateLabel()
        {
            if (this.hasBinding)
            {
                // already taken care of.
                return;
            }
            if (this.context != null)
            {
                string value = this.getter(this.context);
                if (value == null)
                {
                    value = string.Empty;
                }
                this.SetText(value);
            }
            else if (!string.IsNullOrEmpty(this.Text))
            {
                SetText(string.Empty);
            }
        }

        void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            string name = e.PropertyName;
            if (name == this.fieldName)
            {
                UpdateLabel();
            }
        }
    }

    /*
        <DataGridTemplateColumn Header="Date"  CellTemplate="{StaticResource myTemplateDate}" CellEditingTemplate="{StaticResource myTemplateDateEdit}"
                    SortMemberPath="Date" SortDirection="Ascending" />
        
        <!-- DATE -->
        <DataTemplate x:Key="myTemplateDate">
            <TextBlock Text="{Binding Date,StringFormat={}{0:d}}" MinWidth="100" VerticalAlignment="Top" Margin="4,0,0,0"/>
        </DataTemplate>

        <DataTemplate x:Key="myTemplateDateEdit">
            <local:MoneyDatePicker SelectedDate="{Binding Date}" MinWidth="100" VerticalAlignment="Top" BorderThickness="0" Margin="0,-2,0,0" />
        </DataTemplate>
    */
    public class TransactionDateColumn : DataGridColumn
    {
        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            MoneyDatePicker picker = new MoneyDatePicker()
            {
                VerticalAlignment = VerticalAlignment.Top,
                MinWidth = 100,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, -2, 0, 0),
                Name = "EditorForDate"
            };            
            picker.SetBinding(MoneyDatePicker.SelectedDateProperty, new Binding("Date"));
            return picker;
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            Binding binding = null;
            MoneyDatePicker picker = cell.Content as MoneyDatePicker;
            if (picker != null)
            {
                binding = new Binding("Date")
                {
                    StringFormat = "d"
                };
            }
            return new TransactionTextField("Date", (t) => t.Date.ToString("d"), binding, dataItem)
            {
                Margin = new Thickness(2, 1, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                MinWidth = 100
            };
        }
    }

    /// <summary>
    /// This class is here for performance reasons only.  The complex DataGridCell Template was too slow otherwise.
    /// In code we can optimize away the construction of the CheckBox which is only used during budget balancing.
    /// This code is replacing the following XAML:
    /*
     *   <StackPanel Orientation="Vertical" VerticalAlignment="Top">
                <Button BorderBrush="Transparent" Background="Transparent" 
                            PreviewMouseLeftButtonDown="OnButtonClick_StatusColumn">
                    <TextBlock Text="{Binding StatusString}"/>
                </Button>
        </StackPanel>
     */
    /// </summary>  
    public class TransactionStatusButton : Border
    {
        Button button;
        TextBlock label;
        Transaction context;
        TransactionsView view;

        /// <summary>
        /// This button presents the transaction status
        /// </summary>
        public TransactionStatusButton()
        {
            this.Child = this.button = new Button() { Padding = new Thickness(8, 0, 8, 0) };
            this.button.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(OnStatusButtonClick);
            this.DataContextChanged += new DependencyPropertyChangedEventHandler(OnDataContextChanged);
            this.button.Content = label = new TextBlock();
            this.button.BorderBrush = Brushes.Transparent;
            this.button.Background = Brushes.Transparent;
            this.Unloaded += (s, e) =>
            {
                OnUnloaded();
            };
            this.Loaded += (s, e) =>
            {
                OnLoaded();
            };
        }

        void OnUnloaded()
        {
            // stop listening
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
            }
        }

        void OnLoaded()
        {
            // start listening
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
                this.context.PropertyChanged += OnPropertyChanged;
                UpdateLabel();
            }
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            SetView(WpfHelper.FindAncestor<TransactionsView>(this));
            base.OnVisualParentChanged(oldParent);
        }

        void SetView(TransactionsView newView)
        {
            this.view = newView;
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Transaction t = e.NewValue as Transaction;
            if (t == null)
            {
                Investment i = e.NewValue as Investment;
                if (i != null)
                {
                    t = i.Transaction;
                }
            }
            if (t != this.context)
            {
                SetContext(t);
            }

            UpdateLabel();
        }

        void SetContext(Transaction transaction)
        {
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
            }
            this.context = transaction;
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
                this.context.PropertyChanged += OnPropertyChanged;
            }
        }

        void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "StatusString":
                    UpdateLabel();
                    break;
            }
        }

        void UpdateLabel()
        {
            label.Text = (this.context != null) ? this.context.StatusString : "";

            if (this.context != null)
            {
                switch (this.context.Status)
                {
                    case TransactionStatus.None:
                        ToolTip = "";
                        break;
                    case TransactionStatus.Electronic:
                        ToolTip = Walkabout.Properties.Resources.StatusElectronicTip;
                        break;
                    case TransactionStatus.Cleared:
                        ToolTip = Walkabout.Properties.Resources.StatusClearedTip;
                        break;
                    case TransactionStatus.Reconciled:
                        ToolTip = Walkabout.Properties.Resources.StatusReconciledTip;
                        break;
                    case TransactionStatus.Void:
                        ToolTip = Walkabout.Properties.Resources.StatusVoidTip;
                        break;
                    default:
                        break;
                }
            }
        }

        void OnStatusButtonClick(object sender, MouseButtonEventArgs e)
        {
            // Since this is a preview event handler, we need to do dispatch invoke to make
            // this wait for row selection change to catch up, then toggle the status of the right
            // transaction.
            Transaction t = this.context;
            if (t != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (view != null)
                    {
                        view.ToggleTransactionStateReconciled(t);
                    }
                }));
            }
        }
    }

    /// <summary>
    /// Construct a single TransactionAmountControl object which is shared between readonly and editing modes.
    /// </summary>
    public class TransactionAmountColumn : DataGridColumn
    {
        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            TransactionAmountControl ctrl = cell.Content as TransactionAmountControl;
            if (ctrl != null)
            {
                ctrl.IsEditing = true;
                ctrl.Type = this.SortMemberPath;
            }
            else
            {
                ctrl = new TransactionAmountControl();
                ctrl.IsEditing = true;
                ctrl.Type = this.SortMemberPath;
            }

            return ctrl;
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            TransactionAmountControl ctrl = cell.Content as TransactionAmountControl;
            if (ctrl != null)
            {
                ctrl.IsEditing = false;
                ctrl.Type = this.SortMemberPath;
            }
            else
            {
                ctrl = new TransactionAmountControl();
                ctrl.Type = this.SortMemberPath;
            }
            return ctrl;
        }

        protected override void CancelCellEdit(FrameworkElement editingElement, object uneditedValue)
        {
            TransactionAmountControl edit = editingElement as TransactionAmountControl;
            if (edit != null)
            {
                edit.OnEditCanceled();
            }
            base.CancelCellEdit(editingElement, uneditedValue);
        }


        protected override bool CommitCellEdit(FrameworkElement editingElement)
        {
            try
            {
                return base.CommitCellEdit(editingElement);
            }
            catch (Exception ex)
            {
                TransactionAmountControl edit = editingElement as TransactionAmountControl;
                if (edit != null)
                {
                    edit.OnValidationError(ex);
                }
                return false;
            }
        }
    }

    /// <summary>
    /// This class is here for performance reasons only.  Turns out the XAML for displaying the TextBlock and optional split button
    /// is too slow.
    /// </summary>
    // THis class replaces the following XAML
    /*
        <Grid VerticalAlignment="Stretch" Background="Transparent" Focusable="False">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <Button     Style="{DynamicResource ButtonStyleSplitAmount}"                            
                        Visibility="{Binding HasDebitAndIsSplit, Converter={StaticResource TrueToVisible}, FallbackValue=Collapsed}"
                        Tag="{Binding Id}"
                        PreviewMouseLeftButtonDown="OnButtonSplitClicked" VerticalAlignment="Top"/>

            <TextBlock  Style="{StaticResource DataTextBlockStyle}" Margin="0,0,2,0" 
                        Grid.Column="1" Text="{Binding Debit, StringFormat={}{0:N}, Converter={StaticResource SqlDecimalToDecimalConverter}}"                            
                        TextAlignment="Right" VerticalAlignment="Top" />
        </Grid>
    */
    public class TransactionAmountControl : UserControl
    {
        bool editing;
        string type;
        Button button;
        TextBlock label;
        Transaction context;
        TextBox editbox;
        string editedValue;
        bool? editFieldEmpty;
        bool isDebit;

        /// <summary>
        /// This button presents the transaction status
        /// </summary>
        public TransactionAmountControl()
        {
            var grid = new Grid()
            {
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Background = Brushes.Transparent,
                Focusable = false
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

            this.Content = grid;

            this.DataContextChanged += new DependencyPropertyChangedEventHandler(OnDataContextChanged);
            this.Unloaded += (s, e) =>
            {
                OnUnloaded();
            };
            this.Loaded += (s, e) =>
            {
                OnLoaded();
            };
        }

        void OnUnloaded()
        {
            // stop listening
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
            }
        }

        void OnLoaded()
        {
            // start listening
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
                this.context.PropertyChanged += OnPropertyChanged;
                UpdateButton();
                UpdateLabel();
            }
        }

        // whether to show TextBox or TextLabel.
        public bool IsEditing
        {
            get { return editing; }
            set { editing = value; }
        }

        private void FinishConstruction()
        {
            if (this.context != null && this.type != null)
            {
                UpdateButton();
                UpdateEditing();
                SetBindings();
            }
            if (this.context == null)
            {
                UpdateLabel();
            }
        }

        // Debit or Credit
        public string Type
        {
            get { return type; }
            set
            {
                type = value;
                isDebit = value == "Debit";
                editFieldEmpty = null;
                FinishConstruction();
            }
        }

        protected void SetBindings()
        {
            if (this.editbox != null)
            {
                // Text="{Binding Debit, StringFormat={}{0:N}, Converter={StaticResource SqlDecimalToDecimalConverter}, 
                // Mode=TwoWay, ValidatesOnDataErrors=True, ValidatesOnExceptions=True}"
                var binding = new Binding(this.Type)
                {
                    StringFormat = "{0:N}",
                    Converter = new Walkabout.WpfConverters.SqlDecimalToDecimalConverter(),
                    Mode = BindingMode.TwoWay,
                    ValidatesOnDataErrors = true,
                    ValidatesOnExceptions = true
                };
                editbox.SetBinding(TextBox.TextProperty, binding);
            }
            if (this.label != null)
            {
                // it is faster to bind the label manually.
                UpdateLabel();
            }
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            base.OnVisualParentChanged(oldParent);
            if (this.label == null && this.editbox == null)
            {
                FinishConstruction();
            }
        }

        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Transaction t = e.NewValue as Transaction;
            if (t == null)
            {
                Investment i = e.NewValue as Investment;
                if (i != null)
                {
                    t = i.Transaction;
                }
            }
            if (t != this.context)
            {
                SetContext(t);
            }

            if (t == null)
            {
                // recycling of last row.
                UpdateLabel();
            }
        }

        void SetContext(Transaction transaction)
        {
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
            }
            this.context = transaction;
            if (this.context != null)
            {
                this.context.PropertyChanged -= OnPropertyChanged;
                this.context.PropertyChanged += OnPropertyChanged;
                FinishConstruction();
            }
        }

        private void UpdateLabel()
        {
            if (this.label != null)
            {
                this.label.Text = GetCurrentValue();
            }
        }

        private string GetCurrentValue()
        {
            if (this.context == null)
            {
                return string.Empty;
            }
            if (editedValue != null)
            {
                decimal v;
                if (decimal.TryParse(editedValue, out v))
                {
                    if (v != 0)
                    {
                        return v.ToString("N");
                    }
                }
            }
            else
            {
                SqlDecimal value = (this.isDebit) ? this.context.Debit : this.context.Credit;
                if (!value.IsNull)
                {
                    return value.Value.ToString("N");
                }
            }
            return string.Empty;
        }

        private void UpdateEditing()
        {
            Grid grid = (Grid)this.Content;
            if (IsEditing)
            {
                if (editbox == null)
                {
                    editbox = new TextBox()
                    {
                        TextAlignment = TextAlignment.Right,
                        VerticalAlignment = System.Windows.VerticalAlignment.Top,
                    };
                    editbox.TextChanged += new TextChangedEventHandler(OnEditTextChanged);
                    Grid.SetColumn(editbox, 1);
                    grid.Children.Add(editbox);
                    editbox.SetResourceReference(TextBlock.StyleProperty, "NumericTextBoxStyle");
                }

                if (label != null)
                {
                    // cleanup from previous recycling.                    
                    grid.Children.Remove(label);
                    label = null;
                    editedValue = null;
                }
            }
            else
            {
                if (label == null)
                {
                    label = new TextBlock()
                    {
                        Margin = new Thickness(10, 1, 3, 0), // ensures room for the split button.
                        TextAlignment = TextAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    label.SetResourceReference(TextBlock.StyleProperty, "DataTextBlockStyle");

                    Grid.SetColumn(label, 1);
                    grid.Children.Add(label);
                }

                if (editbox != null)
                {
                    // cleanup from previous recycling.
                    editedValue = editbox.Text;
                    editbox.TextChanged -= new TextChangedEventHandler(OnEditTextChanged);
                    grid.Children.Remove(editbox);
                    editbox = null;
                }
            }
        }

        void OnEditTextChanged(object sender, TextChangedEventArgs e)
        {
            string value = this.editbox.Text;
            SetEditFieldEmpty(string.IsNullOrEmpty(value));
        }

        public void OnEditCanceled()
        {
            Restore();
            SetEditFieldEmpty(string.IsNullOrEmpty(GetCurrentValue()));
        }

        public void OnValidationError(Exception ex)
        {
            if (this.editbox != null)
            {
                var binding = editbox.BindingGroup.BindingExpressions.FirstOrDefault();
                Validation.MarkInvalid(binding, new ValidationError(new ExceptionValidationRule(), binding, ex.Message, ex));
            }
        }

        private void SetEditFieldEmpty(bool empty)
        {
            // Ok, now we want the Debit field to take over from the Credit field and vice versa.
            if (!editFieldEmpty.HasValue || editFieldEmpty.Value != empty)
            {
                editFieldEmpty = empty;
                TransactionAmountControl opposition = GetOpposingField();
                if (opposition != null)
                {
                    if (empty)
                    {
                        // restore opposing field to it's value
                        opposition.Restore();
                        SwitchTransferCaption(opposition.isDebit ? false : true);
                    }
                    else
                    {
                        // clear opposing field.   
                        opposition.Clear();
                        SwitchTransferCaption(opposition.isDebit ? true : false);
                    }
                }
            }
        }

        private TransactionAmountControl GetOpposingField()
        {
            string oppositeName = (this.isDebit) ? "Credit" : "Debit";
            MoneyDataGrid grid = WpfHelper.FindAncestor<MoneyDataGrid>(this);
            if (grid != null)
            {
                DataGridColumn column = grid.FindColumn(oppositeName);
                if (column != null)
                {
                    FrameworkElement content = column.GetCellContent(this.DataContext);
                    TransactionAmountControl e = content as TransactionAmountControl;
                    return e;
                }
            }
            return null;
        }

        private void SwitchTransferCaption(bool isFrom)
        {
            Transaction t = this.DataContext as Transaction;
            if (t == null)
            {
                return;
            }

            MoneyDataGrid grid = WpfHelper.FindAncestor<MoneyDataGrid>(this);
            if (grid != null)
            {
                DataGridColumn column = grid.FindColumn("PayeeOrTransferCaption");
                if (column != null)
                {
                    TransactionPayeeCategoryMemoField content = column.GetCellContent(this.DataContext) as TransactionPayeeCategoryMemoField;
                    if (content != null)
                    {
                        var field = content.PayeeField;
                        string payee = field.Text;
                        if (Transaction.IsTransferCaption(payee))
                        {
                            string accountName = Transaction.ExtractTransferAccountName(payee);
                            MyMoney money = t.MyMoney;
                            Account account = money.Accounts.FindAccount(accountName);

                            // ok, then figure out what it needs switching to...
                            field.Text = Transaction.GetTransferCaption(account, isFrom);
                        }
                    }
                }
            }
        }

        private void Restore()
        {
            UpdateLabel();
        }

        private void Clear()
        {
            if (label != null)
            {
                label.Text = "";
            }
        }

        DispatcherTimer lazyButtonTimer;

        private void UpdateButton()
        {
            if ((this.isDebit && context != null && context.HasDebitAndIsSplit) ||
                (!this.isDebit && context != null && context.HasCreditAndIsSplit))
            {
                if (this.button == null)
                {
                    if (this.IsEditing)
                    {
                        // create it right away so it doesn't flash when we enter edit mode.
                        OnCreateButton(this, EventArgs.Empty);
                        StopLazyButtonTimer();
                    }
                    else
                    {
                        if (lazyButtonTimer == null)
                        {
                            lazyButtonTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Normal, OnCreateButton, this.Dispatcher);
                        }
                        lazyButtonTimer.Stop();
                        lazyButtonTimer.Start();
                    }
                }
                else
                {
                    button.Visibility = Visibility.Visible;
                }
            }
            else if (context != null && this.type != null)
            {
                if (button != null)
                {
                    // The button's OnDataCOntextChanged is very expensive, so it is cheaper to just remove
                    // the button than to try and recycle it since there are usually a lot more rows without
                    // splits than there are with splits.
                    Grid grid = (Grid)this.Content;
                    grid.Children.Remove(button);
                    button = null;
                }
                StopLazyButtonTimer();
            }
        }

        private void StopLazyButtonTimer()
        {
            if (lazyButtonTimer != null)
            {
                lazyButtonTimer.Stop();
                lazyButtonTimer = null;
            }
        }

        /// <summary>
        /// We lazily create the button so that scrolling is smoother :-)
        /// </summary>
        private void OnCreateButton(object sender, EventArgs e)
        {
            StopLazyButtonTimer();
            if ((this.isDebit && context != null && context.HasDebitAndIsSplit) ||
                (!this.isDebit && context != null && context.HasCreditAndIsSplit))
            {
                if (button == null)
                {
                    Grid grid = (Grid)this.Content;
                    button = new Button();
                    button.FontSize = 8.0;
                    button.SetResourceReference(Button.StyleProperty, "ButtonStyleSplitAmount");
                    button.PreviewMouseLeftButtonDown += OnButtonSplitClicked;
                    button.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                    grid.Children.Add(button);
                }
            }
        }

        /// <summary>
        /// The user has click the toggle Split button, either for editing or for viewing the split details
        /// One click will show the split for editing.
        /// One more click will hide the split.
        /// </summary>
        private void OnButtonSplitClicked(object sender, MouseButtonEventArgs e)
        {
            TransactionsView view = WpfHelper.FindAncestor<TransactionsView>(this);
            if (view != null && this.context != null)
            {
                Button button = (Button)e.Source;
                view.ToggleSplitDetails(this.context.Id);
            }
        }

        void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "HasDebitAndIsSplit":
                case "HasCreditAndIsSplit":
                    UpdateButton();
                    break;
                case "Credit":
                case "Debit":
                case "Amount":
                    editedValue = null; // backing store has been updated after edit.
                    UpdateLabel();
                    break;
            }
        }


    }

    #region XAML CONVERTERS

    public class TransactionFilterToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            TransactionFilter filter = (TransactionFilter)value;
            switch (parameter.ToString())
            {
                case "All":
                    return filter == TransactionFilter.All;
                case "Accepted":
                    return filter == TransactionFilter.Accepted;
                case "Unaccepted":
                    return filter == TransactionFilter.Unaccepted;
                case "Reconciled":
                    return filter == TransactionFilter.Reconciled;
                case "Unreconciled":
                    return filter == TransactionFilter.Unreconciled;
                case "Categorized":
                    return filter == TransactionFilter.Categorized;
                case "Uncategorized":
                    return filter == TransactionFilter.Uncategorized;
                case "Custom":
                    return filter == TransactionFilter.Custom;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not a valid method to call
            return 0;
        }
    }

    public class ValidationErrorGetErrorMessageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            StringBuilder sb = new StringBuilder();
            ReadOnlyObservableCollection<ValidationError> errors = value as ReadOnlyObservableCollection<ValidationError>;
            if (errors != null)
            {
                foreach (ValidationError e in errors)
                {
                    if (e.Exception == null || e.Exception.InnerException == null)
                    {
                        sb.AppendLine(e.ErrorContent.ToString());
                    }
                    else
                    {
                        sb.AppendLine(e.Exception.InnerException.ToString());
                    }
                }
            }
            return sb.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }




    public class ShowSplitForDebit : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            Transaction t = value as Transaction;
            if (t != null)
            {
                if (!t.Debit.IsNull)
                {
                    if (t.IsSplit)
                    {
                        return Visibility.Visible;
                    }
                }
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not a valid method to call
            return 0;
        }
    }

    public class ShowSplitForCredit : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Transaction t = value as Transaction;
            if (t != null)
            {
                if (!t.Credit.IsNull)
                {
                    if (t.IsSplit)
                    {
                        return Visibility.Visible;
                    }
                }
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not a valid method to call
            return 0;
        }
    }

    #endregion
}
