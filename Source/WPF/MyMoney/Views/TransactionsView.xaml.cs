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
using Walkabout.Views.Controls;
using System.ComponentModel;
using System.Data.SqlTypes;
using Walkabout.WpfConverters;
using System.Windows.Media.Imaging;
using Walkabout.Configuration;
using Walkabout.Interfaces.Views;
using Walkabout.StockQuotes;

#if PerformanceBlocks
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
using System.Threading.Tasks;

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
            ToggleShowLines.IsChecked = !OneLineView;
            ToggleShowLinesImage.Source = (OneLineView) ? (ImageSource)FindResource("ThreeLines") : (ImageSource)FindResource("OneLine");
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

        private void OnToggleLines_Checked(object sender, RoutedEventArgs e)
        {
            OneLineView = false;
        }

        private void OnToggleLines_Unchecked(object sender, RoutedEventArgs e)
        {
            OneLineView = true;
        }

        private void OnToggleShowSplits_Checked(object sender, RoutedEventArgs e)
        {
            ViewAllSplits = true;
        }

        private void OnToggleShowSplits_Unchecked(object sender, RoutedEventArgs e)
        {
            ViewAllSplits = false;
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


        public static readonly DependencyProperty BalancingBudgetProperty = DependencyProperty.Register("BalancingBudget", typeof(bool), typeof(TransactionsView), new FrameworkPropertyMetadata(new PropertyChangedCallback(OnBalancingBudgetChanged)));

        static void OnBalancingBudgetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TransactionsView view = (TransactionsView)d;
            view.OnBalancingBudgetChanged();
        }

        private void OnBalancingBudgetChanged()
        {
            if (BalancingBudgetChanged != null)
            {
                BalancingBudgetChanged(this, EventArgs.Empty);
            }
        }

        public bool BalancingBudget
        {
            get { return (bool)GetValue(BalancingBudgetProperty); }
            set { SetValue(BalancingBudgetProperty, value); }
        }

        public event EventHandler BalancingBudgetChanged;

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

                ToggleShowSplits.IsChecked = value;

                ToggleShowSplits.ToolTip = value ? "Hide All Splits" : "Show All Splits";

                ToggleShowSplitsImage.Source = value ? (ImageSource)FindResource("SplitIconFilled") : (ImageSource)FindResource("SplitIcon");
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
                TheGrid_TransactionFromDetails.ParentMenu = this.ContextMenu;
                TheGrid_InvestmentActivity.ParentMenu = this.ContextMenu;
                TheGrid_BySecurity.ParentMenu = this.ContextMenu;

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


            //// We want to insert the transaction after the one currently selected one
            //// TODO most of the transaction date are set to 00:00:00 
            //// in the future we need to "reSort" all the seconds of all the transaction for that date for this account
            //// For now we add 1 second to ensure that it is placed after the Date selected
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
                // Note: this must be DispatcherPriority.Background otherwise Rebalance() happens too soon and
                // doesn't see the new value!
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

                if (transaction != null && transaction.HasAttachment == false)
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files.Length == 1)
                    {
                        var extension = Path.GetExtension(files[0]);
                        var settings = (Settings)this.site.GetService(typeof(Settings));
                        var directory = Path.Combine(settings.AttachmentDirectory, NativeMethods.GetValidFileName(transaction.AccountName));

                        try
                        {
                            if (!Directory.Exists(directory))
                                Directory.CreateDirectory(directory);

                            var attachmentFullPath = Path.Combine(directory, $"{transaction.Id}{extension}");

                            if (!File.Exists(attachmentFullPath))
                            {
                                File.Copy(files[0], attachmentFullPath);
                                transaction.HasAttachment = true;
                            }
                        }
                        catch { }
                    }
                }
            }
            else
            {
                Transaction t = e.Data.GetData(typeof(Transaction)) as Transaction;
                if (t != null)
                {
                    Transaction u = row.Item as Transaction;
                    if (t != u && t.Amount == u.Amount)
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
                    var proceed = false;
                    var transaction = row.Item as Transaction;
                    if(transaction != null && transaction.HasAttachment == false)
                    {
                        var files = (string[])e.Data.GetData(DataFormats.FileDrop);

                        if (files.Length == 1 && IsValidAttachmentExtension(files[0]))
                        {
                            proceed = true;
                            SetDragDropStyles(row, DropType.File);
                            e.Effects = DragDropEffects.Copy;
                        }
                    }
                    
                    if(!proceed)
                    {
                        e.Effects = DragDropEffects.None;
                        e.Handled = true;
                    }
                }
                else
                {
                    e.Handled = true;
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
            IList data = new TransactionCollection(myMoney, null, dangling, true, false, null);
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

                vs.TabIndex = this.InvestmentAccountTabs.SelectedIndex;
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
                    this.InvestmentAccountTabs.SelectedIndex = state.TabIndex;

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

        public void OnEndReconcile(bool cancelled)
        {
            this.reconciling = false;
            this.ViewState = beforeState;
            SetReconciledState(cancelled);
            reconcilingTransactions = null;
            this.StatmentReconcileDateEnd = null;
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
                            ShowInvestmentPortfolio(this.ActiveAccount);
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

            currentDisplayName = TransactionViewName.Portfolio;
            layout = "InvestmentPortfolioView";

            FlowDocumentView view = this.InvestmentPortfolioView;
            SetActiveAccount(account, null, null, null, null);
            // if we are reconciling then show the positions held at statement date so the stock balances can be reconciled also.
            DateTime reportDate = this.IsReconciling ? GetReconiledExclusiveEndDate() : DateTime.Now;
            PortfolioReport report = new PortfolioReport(view, this.myMoney, account, this.ServiceProvider, reportDate);
            view.Generate(report);
            portfolioReport = report;
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
                IList<Transaction> data = myMoney.Transactions.GetTransactionsByCategory(contextToView.DepartmentCategory, contextToView.Year, true);
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
                    filter = new Predicate<Transaction>((t) => {
                        if (t.Status != TransactionStatus.Void)
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
                        return t.Category != null || t.Transfer != null;
                    });
                    break;
                case TransactionFilter.Uncategorized:
                    filter = new Predicate<Transaction>((t) => {
                        if (t.Status != TransactionStatus.Void)
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
                        return (t.Category == null && t.Transfer == null);
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
                return Clipboard.ContainsText();
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
            if (QueryPanel.Visibility == Visibility.Visible &&
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
                    Importer importer = new Importer(myMoney);
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
                this.TransactionFilter = TransactionFilter.Uncategorized;
            }
            else if (selected == FilterByCategorized)
            {
                this.TransactionFilter = TransactionFilter.Categorized;
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
                        Security s = args.Item as Security;
                        Transaction t = args.Item as Transaction;
                        Investment i = args.Item as Investment;
                        Account a = args.Item as Account;
                        Category c = args.Item as Category;

                        if (s != null && !string.IsNullOrEmpty(s.Name))
                        {
                            // ok, this might be the GetStock auto-update, see if there is anything to copy to 
                            // uncommitted payee.
                            if (this.SelectedTransaction != null && this.SelectedTransaction.Investment != null &&
                                this.SelectedTransaction.Investment.Security == s)
                            {
                                string payee = GetUncomittedPayee();
                                if (string.IsNullOrEmpty(payee) && this.SelectedTransaction != null)
                                {
                                    this.SelectedTransaction.Payee = this.myMoney.Payees.FindPayee(s.Name, true);
                                }
                            }
                        }

                        // the "NewPlaceHolder" item may have just been changed into a real Transaction object.
                        // ChangeType.Changed would have already been handled by the INotifyPropertyChanged events, what we really care
                        // about here are transactions being inserted or removed which can happen if you do a background 'download'
                        // for example.
                        // These two change types are not structural, so the normal data binding update of the UI should be enough.
                        else if (args.ChangeType != ChangeType.Rebalanced && args.ChangeType != ChangeType.TransientChanged)
                        {
                            if (t != null && tc != null)
                            {
                                if (t.Account == this.ActiveAccount || this.ActiveAccount == null)
                                {
                                    if (args.ChangeType == ChangeType.Deleted || args.ChangeType == ChangeType.Inserted)
                                    {
                                        rebalance = true;
                                        // optimization tor TransactionCollection
                                        if (args.ChangeSource != null && args.ChangeSource.GetType() == typeof(TransactionCollection))
                                        {
                                            // TransactionCollection will already have taken care of inserts and deletes, so we
                                            // can optimize this case (refresh is slow).
                                            rebalance = true;
                                        }
                                        else
                                        { 
                                            // change came from somewhere else (like OFX import) so need a full refresh.
                                            refresh = true;
                                            rebalance = true;
                                        }
                                    }
                                }
                            }
                            else if (a != null && a == this.ActiveAccount && args.ChangeType == ChangeType.Deleted)
                            {
                                // then this account is gone, so we need to clear the display.
                                this.TheActiveGrid.ClearItemsSource();
                            }
                            else if (a != null && a == this.ActiveAccount && args.ChangeType == ChangeType.Changed)
                            {
                                if (args.Name != "Unaccepted" && args.Name != "LastBalance")
                                {
                                    // then we need a refresh, may have just loaded a bunch of new transactions from OFX.
                                    refresh = true;
                                    rebalance = true;
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
                    Dispatcher.BeginInvoke(new Action(() =>
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
                    }), DispatcherPriority.Render);
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
                    Transaction u = FindPotentialDuplicate(t);
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

        private bool IsPotentialDuplicate(Transaction t, Transaction u, int dayRange)
        {
            return !u.IsFake && !t.IsFake &&
                u != t && u.amount == t.amount && u.PayeeName == t.PayeeName &&
                // they must be in the same account (which they may not be if on the multi-account view).
                u.Account == t.Account &&
                // ignore transfers for now
                (t.Transfer == null && u.Transfer == null) &&
                // if user has already marked both as not duplicates, then skip it.
                (!t.NotDuplicate || !u.NotDuplicate) &&
                // and if they are investment transactions the stock type and unit quanities have to be the same
                IsPotentialDuplicate(t.Investment, u.Investment) &&
                // if they both have unique FITID fields, then the bank is telling us these are not duplicates.
                (string.IsNullOrEmpty(t.FITID) || string.IsNullOrEmpty(u.FITID) || t.FITID == u.FITID) &&
                // they can't be both reconciled, because then we can't merge them!
                (u.Status != TransactionStatus.Reconciled || t.Status != TransactionStatus.Reconciled) &&
                // within specified date range
                Math.Abs((u.Date - t.Date).Days) < dayRange;
        }

        private bool IsPotentialDuplicate(Investment u, Investment v)
        {
            if (u != null && v == null) return false;
            if (u == null && v != null) return false;
            if (u == null && v == null) return true;

            return u.TradeType == v.TradeType &&
                u.Units == v.Units &&
                u.UnitPrice == v.UnitPrice &&
                u.SecurityName == v.SecurityName;
        }

        private Transaction FindPotentialDuplicate(Transaction t)
        {
            Settings settings = (Settings)this.site.GetService(typeof(Settings));
            TimeSpan range = settings.DuplicateRange;
            int days = range.Days;
            int[] indices = new int[2]; // one forward index, and one backward index.

            TransactionCollection tc = this.TheActiveGrid.ItemsSource as TransactionCollection;
            if (tc != null)
            {
                int i = tc.IndexOf(t);
                if (i > 0)
                {
                    int count = tc.Count;
                    DateTime now = DateTime.Now;

                    // ok, find nearby transactions that have the same amount, searching
                    // out from closest first, since the closest is the most likely one.
                    for (int j = 1; j < count; j++)
                    {
                        indices[0] = i - j;
                        indices[1] = i + j;

                        foreach (int k in indices)
                        {
                            if (k >= 0 && k < count)
                            {
                                Transaction u = tc[j];
                                if (IsPotentialDuplicate(t, u, days) && !IsRecurring(t, u, tc))
                                {
                                    // ok, this is the closest viable duplicate...
                                    return u;
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        private bool IsRecurring(Transaction t, Transaction u, TransactionCollection tc)
        {
            // if we find prior transactions that match (or very closely match) that are on the 
            // same (or similar) TimeSpan then this might be a recurring transaction.
            TimeSpan span = t.Date - u.Date;
            int days = (int)Math.Abs(span.TotalDays);
            if (days == 0)
            {
                // unlikely to have a recurring payment on the same day.
                return false;
            }
            int found = 0;
            int i = Math.Min(tc.IndexOf(t), tc.IndexOf(u));
            for (--i; i > 0; i--)
            {
                Transaction w = tc[i];
                if (w.amount == 0 || w.Status == TransactionStatus.Void)
                {
                    continue;
                }

                // if they are within 1% of each other and within 3 days of the prior time span
                // then it is probably a recurring instance.
                if (w.PayeeName == t.PayeeName && Math.Abs((w.Amount - t.amount) * 100 / w.Amount) < 1)
                {
                    TimeSpan spanw = u.Date - w.Date;
                    int daysw = (int)Math.Abs(spanw.TotalDays);
                    if (Math.Abs(daysw - days) < 3)
                    {
                        found++;
                        if (found == 3)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
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
        public readonly static RoutedUICommand CommandBudgeted = new RoutedUICommand("Budgeted", "CommandBudgeted", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandSplits = new RoutedUICommand("Splits", "CommandSplits", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandRenamePayee = new RoutedUICommand("RenamePayee", "CommandRenamePayee", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandLookupPayee = new RoutedUICommand("LookupPayee", "CommandLookupPayee", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandRecategorize = new RoutedUICommand("Recategorize", "CommandRecategorize", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandGotoRelatedTransaction = new RoutedUICommand("GotoRelatedTransaction", "CommandGotoRelatedTransaction", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandViewTransactionsByAccount = new RoutedUICommand("ViewTransactionsByAccount", "CommandViewTransactionsByAccount", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandViewSimilarTransactions = new RoutedUICommand("ViewSimilarTransactions", "CommandViewSimilarTransactions", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandViewTransactionsByCategory = new RoutedUICommand("ViewTransactionsByCategory", "CommandViewTransactionsByCategory", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandViewTransactionsByPayee = new RoutedUICommand("ViewTransactionsByPayee", "CommandViewTransactionsByPayee", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandViewTransactionsBySecurity = new RoutedUICommand("ViewTransactionsBySecurity", "CommandViewTransactionsBySecurity", typeof(TransactionsView));
        public readonly static RoutedUICommand CommandViewSecurity = new RoutedUICommand("CommandViewSecurity", "CommandViewSecurity", typeof(TransactionsView));

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

        private void BudgetTransaction(Transaction t)
        {
            if (t != null)
            {
                try
                {
                    this.myMoney.BeginUpdate(this);
                    t.SetBudgeted(!t.IsBudgeted, null);
                }
                catch (Exception ex)
                {
                    UiDispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBoxEx.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }));
                }
                finally
                {
                    this.myMoney.EndUpdate();
                }
            }
        }

        void OnCommandBudgeted(object sender, RoutedEventArgs e)
        {
            Transaction t = this.SelectedTransaction;
            if (t != null && !t.IsReadOnly)
            {
                BudgetTransaction(t);
            }
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

        void OnRecategorizeAll(object sender, RoutedEventArgs e)
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
        }

        private void GotoTransaction(Transaction t)
        {
            // Must use this method in order to coordinate the jump with MainWindow otherwise MainWindow
            // tries to do some things that interfere with this navigation during it's implementation of
            // RestorePreviouslySavedSelection.
            IViewNavigator n = (IViewNavigator)this.site.GetService(typeof(IViewNavigator));
            n.NavigateToTransaction(t);
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
            else
            {
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    TextBox box = (TextBox)combo.Template.FindName("PART_EditableTextBox", combo);
                    if (box != null)
                    {
                        // move focus back to this box
                        box.SelectAll();
                        box.Focus();
                    }
                }));
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
                Transaction u = this.myMoney.FindPreviousTransactionByPayee(t, payeeOrTransfer);

                if (u != null)
                {
                    try
                    {
                        DateTime? date = GetUncomittedDate();
                        if (date.HasValue)
                        {
                            t.Date = date.Value;
                        }
                        this.myMoney.CopyCategory(u, t);
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
        int start;
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
                int tick = Environment.TickCount;
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
                    <Setter Property="Background" Value="{DynamicResource WalkaboutToolBoxListBoxItemBrushWhenSelected}" TargetName="CellBorder"/>
                    <Setter Property="Foreground" Value="{DynamicResource WalkaboutSelectedTextBrush}"/>
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
                    <Setter Property="Foreground" Value="Red"/>
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
        }

        Transaction context;
        DataGridCell cell;

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
            ClearContext();

            Transaction t = e.NewValue as Transaction;
            if (t == null)
            {
                Investment i = e.NewValue as Investment;
                if (i != null)
                {
                    t = i.Transaction;
                }
            }
            if (t != null)
            {
                SetContext(t);
            }

            UpdateBackground();
            UpdateForeground();
            UpdateFontWeight();
            UpdateBorder();
        }

        void ClearContext()
        {
            if (this.context != null)
            {
                this.context.PropertyChanged -= new PropertyChangedEventHandler(OnPropertyChanged);
            }
            this.context = null;
        }

        void SetContext(Transaction transaction)
        {
            this.context = transaction;
            if (this.context != null)
            {
                this.context.PropertyChanged += new PropertyChangedEventHandler(OnPropertyChanged);
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
                    <Setter Property="Background" Value="{DynamicResource WalkaboutToolBoxListBoxItemBrushWhenSelected}" TargetName="CellBorder"/>
                </Trigger>
             */
            bool isReconciling = false;
            bool isSelected = IsSelected;

            if (this.context != null)
            {
                isReconciling = this.context.IsReconciling;
            }
            if (isSelected)
            {
                this.SetResourceReference(Border.BackgroundProperty, "WalkaboutToolBoxListBoxItemBrushWhenSelected");
            }
            else if (isReconciling)
            {
                this.SetResourceReference(Border.BackgroundProperty, "WalkaboutReconciledRowBackgroundBrush");
            }
            else
            {
                //this.SetResourceReference(Border.BackgroundProperty, null);
                this.ClearValue(Border.BackgroundProperty);
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
                    <Setter Property="Foreground" Value="{DynamicResource WalkaboutSelectedTextBrush}"/>
                </Trigger>
                <DataTrigger Binding="{Binding Path=IsDown}" Value="True">
                    <Setter Property="Foreground" Value="Red"/>
                </DataTrigger>

                <DataTrigger Binding="{Binding Path=IsReadOnly}" Value="True">
                    <Setter Property="Foreground" Value="Gray"/>
                </DataTrigger>
             */

            bool isSelected = IsSelected;
            bool isDown = false;
            bool isReadOnly = false;

            if (this.context != null)
            {
                isDown = this.context.IsDown;
                isReadOnly = this.context.IsReadOnly;
            }
            if (isReadOnly)
            {
                this.SetValue(TextBlock.ForegroundProperty, Brushes.Gray);
                if (cell != null)
                {
                    cell.SetValue(DataGridCell.ForegroundProperty, Brushes.Gray);
                }
            }
            else if (isDown)
            {
                this.SetValue(TextBlock.ForegroundProperty, Brushes.Red);
                if (cell != null)
                {
                    cell.SetValue(DataGridCell.ForegroundProperty, Brushes.Red);
                }
            }
            else if (isSelected)
            {
                ClearValue(TextBlock.ForegroundProperty);
                this.SetResourceReference(TextBlock.ForegroundProperty, "WalkaboutSelectedTextBrush");
                if (cell != null)
                {
                    cell.SetResourceReference(DataGridCell.ForegroundProperty, "WalkaboutSelectedTextBrush");
                }
            }
            else
            {
                ClearValue(TextBlock.ForegroundProperty);
                //this.SetResourceReference(TextBlock.ForegroundProperty, null);
                if (cell != null)
                {
                    cell.ClearValue(DataGridCell.ForegroundProperty);
                }
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
                        this.SetResourceReference(DataGridCell.BorderBrushProperty, "ValidDropTargetFeedbackBrush");
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

    /*
      <DataTemplate x:Key="myTemplateAttachment">
          <Image Source="{Binding HasAttachment, Converter={StaticResource AttachmentIconConverter}}" VerticalAlignment="Top" Width="16" Height="16"/>
      </DataTemplate>

      <DataTemplate x:Key="myTemplateAttachmentEdit">
            <Button Command="views:TransactionsView.CommandScanAttachment" Padding="0" Focusable="False">
                <Image Source="/MyMoney;component/Icons/SmallScanner.png" Width="16" Height="16" VerticalAlignment="Top" HorizontalAlignment="Left"/>
            </Button>
      </DataTemplate>
    */
    public class TransactionAttachmentColumn : DataGridColumn
    {

        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            return new Button()
            {
                Command = TransactionsView.CommandScanAttachment,
                Padding = new Thickness(0),
                Focusable = false,
                Content = new Image()
                {
                    Width = 16,
                    Height = 16,
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Source = BitmapFrame.Create(new Uri("pack://application:,,,/MyMoney;component/Icons/SmallScanner.png"))
                }
            };
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            Image img = new Image()
            {
                VerticalAlignment = VerticalAlignment.Top,
                Width = 16,
                Height = 16
            };

            img.SetBinding(Image.SourceProperty, new Binding("HasAttachment")
            {
                Converter = new AttachmentIconConverter()
            });
            return img;
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
        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            TextBox box = new TextBox()
            {
                VerticalAlignment = VerticalAlignment.Top
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

            return new TransactionTextField("Number", binding, dataItem)
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
            view.OneLineViewChanged += new EventHandler(OnOneLineViewChanged);

            this.MinWidth = 300;
            this.VerticalAlignment = VerticalAlignment.Top;
            this.Focusable = false;
            this.Margin = new Thickness(2, 0, 0, 0);

            this.payeeField = new TransactionTextField("PayeeOrTransferCaption", payee, dataItem);

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
                this.categoryField = new TransactionTextField("CategoryName", category, dataItem);
                this.memoField = new TransactionTextField("Memo", memo, dataItem);

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
        double height;
        double rowHeight;

        public TransactionConnectorAdorner(TransactionAnchor visibleAnchor, double height, double rowHeight)
            : base(visibleAnchor)
        {
            this.height = height;
            this.rowHeight = rowHeight;

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
            Brush background = Brushes.LightGray;
            Brush pressed = new SolidColorBrush(Color.FromRgb(0xc4, 0xe5, 0xf6));

            mainButton.Background = background;
            mainButton.BorderBrush = stroke;
            mainButton.Foreground = stroke;
            mainButton.BorderThickness = new Thickness(strokeThickness);
            mainButton.MouseOverForeground = Brushes.Black;
            mainButton.MouseOverBackground = Brushes.White;
            mainButton.MouseOverBorder = stroke;

            mainButton.MousePressedForeground = Brushes.Black;
            mainButton.MousePressedBackground = pressed;
            mainButton.MousePressedBorder = stroke;

            closeBox.Foreground = stroke;
            closeBox.BorderThickness = new Thickness(1);
            closeBox.Background = Brushes.LightGray;
            closeBox.MouseOverBackground = Brushes.White;
            closeBox.MouseOverForeground = Brushes.Black;
            closeBox.MousePressedBackground = pressed;
            closeBox.MousePressedForeground = Brushes.Black;

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
            double offsetY = rowHeight / 2;
            double arcSize = 10;
            double arcY = arcSize;
            double arcAngle = 90;
            SweepDirection direction = SweepDirection.Clockwise;
            if (height < 0)
            {
                arcY = -arcSize;
                arcAngle = -90;
                direction = SweepDirection.Counterclockwise;
            }
            double arcX = arcSize;

            PathFigure connector = new PathFigure(new Point(0, offsetY), new PathSegment[] {
                new LineSegment(new Point(connectorSize - arcX, offsetY), true),
                new ArcSegment(new Point(connectorSize, offsetY + arcY), new Size(arcSize, arcSize), arcAngle, false, direction, true),
                new LineSegment(new Point(connectorSize, height + offsetY - arcY), true),
                new ArcSegment(new Point(connectorSize - arcX, height + offsetY), new Size(arcSize, arcSize), arcAngle, false, direction, true),
                new LineSegment(new Point(0, height + offsetY), true),
            }, false);

            var path = new PathGeometry();
            path.Figures.Add(connector);

            Brush brush = (Brush)FindResource("TransactionConnectorBrush");

            this.connectorCenter = new Point(connectorSize, (height + offsetY + penWidth) / 2);
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
            this.grid.UnloadingRow += OnUnloadingRow;
        }

        void OnUnloadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row == sourceRow || e.Row == targetRow)
            {
                // then the row we are anchored to is unreliable, so we have to disconnect.
                Disconnect();
            }
        }

        void OnGridScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // might need to reposition the adorner...
            DataGridRow s = this.grid.GetRowFromItem(sourceTransaction);
            DataGridRow t = this.grid.GetRowFromItem(targetTransaction);

            if (s == null && t == null)
            {
                // both rows have been virtualized, so we cannot show anything.
                Disconnect();
            }
            else if (s != sourceRow || t != targetRow)
            {
                Connect(sourceTransaction, targetTransaction);
            }
        }

        public Transaction Source { get { return this.sourceTransaction; } }

        public Transaction Target { get { return this.targetTransaction; } }

        public void Connect(Transaction t, Transaction u)
        {
            Disconnect();

            this.sourceTransaction = t;
            this.targetTransaction = u;

            TransactionCollection tc = (TransactionCollection)this.grid.ItemsSource;
            int i = tc.IndexOf(this.sourceTransaction);
            int j = tc.IndexOf(this.targetTransaction);

            sourceRow = this.grid.GetRowFromItem(t);
            targetRow = this.grid.GetRowFromItem(u);

            var visibleAnchor = sourceAnchor;
            double height = 0;
            double rowHeight = 0;

            if (sourceRow != null && targetRow == null)
            {
                sourceAnchor = sourceRow.FindFirstDescendantOfType<TransactionAnchor>();
                if (sourceAnchor != null)
                {
                    targetAnchor = CreateFakeAnchor(sourceAnchor, u, j - i);
                    Point pos = targetAnchor.RenderTransform.Transform(new Point(0, 0));
                    visibleAnchor = sourceAnchor;
                    height = pos.Y;
                    rowHeight = sourceRow.ActualHeight;
                }
            }
            else if (sourceRow == null && targetRow != null)
            {
                targetAnchor = WpfHelper.FindFirstDescendantOfType<TransactionAnchor>(targetRow);
                if (targetAnchor != null)
                {
                    sourceAnchor = CreateFakeAnchor(targetAnchor, t, i - j);
                    visibleAnchor = targetAnchor;
                    Point pos = sourceAnchor.RenderTransform.Transform(new Point(0, 0));
                    height = pos.Y;
                    rowHeight = targetAnchor.ActualHeight;
                }
            }
            else if (sourceRow == null && targetRow == null)
            {
                // now what? Neither row is really anchored so we can't inject our adorner...
                return;
            }
            else
            {
                sourceAnchor = WpfHelper.FindFirstDescendantOfType<TransactionAnchor>(sourceRow);
                targetAnchor = WpfHelper.FindFirstDescendantOfType<TransactionAnchor>(targetRow);
                if (sourceAnchor != null && targetAnchor != null)
                {
                    Point pos = targetAnchor.TransformToVisual(sourceAnchor).Transform(new Point(0, 0));
                    visibleAnchor = sourceAnchor;
                    rowHeight = sourceRow.ActualHeight;
                    height = pos.Y;
                }
            }

            if (visibleAnchor != null)
            {
                this.adorner = new TransactionConnectorAdorner(visibleAnchor, height, rowHeight);

                layer = AdornerLayer.GetAdornerLayer(visibleAnchor);
                layer.Add(this.adorner);

                // relay these events up
                this.adorner.MergeButton.Click += OnMainButtonClick;
                this.adorner.CloseBox.Click += OnCloseBoxClick;

                // and watch in case adorners are recycled to new row...
                sourceAnchor.DataContextChanged += OnDataContextChanged;
                targetAnchor.DataContextChanged += OnDataContextChanged;

                // we also have to watch scrolling in case the anchor moves too far offscreen
                // such that it becomes invalid...
                grid.ScrollChanged += OnGridScrollChanged;
            }
        }

        private TransactionAnchor CreateFakeAnchor(TransactionAnchor realAnchor, Transaction dataContext, int offset)
        {
            // the DataGrid is very annoying, it sometimes chooses to return null rows for rows that are
            // virtualized.  But we need a real anchor even if it is way offscreen otherwise we can't 
            // draw our connector.  So here we have to fake it.
            double offsetY = offset * sourceRow.ActualHeight;

            var fakeAnchor = new TransactionAnchor();
            fakeAnchor.RenderTransform = new TranslateTransform(0, offsetY);

            // we have to put it in the tree so that the connector can get valid transform.
            realAnchor.Child = fakeAnchor;

            fakeAnchor.DataContext = dataContext;
            return fakeAnchor;
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
                TextAlignment = TextAlignment.Right
            };

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

            return new TransactionTextField(this.SortMemberPath, binding, dataItem)
            {
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Style = this.TextBlockStyle
            };

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
        Binding binding;

        public TransactionTextField(string name, Binding binding, object dataItem)
        {
            this.fieldName = name;
            this.binding = binding;
            if (binding != null)
            {
                // inherit the binding from the editor control so we display the same uncomitted value.
                this.SetBinding(TextBlock.TextProperty, binding);
            }

            SetContext(dataItem as Transaction);
            this.DataContextChanged += new DependencyPropertyChangedEventHandler(OnDataContextChanged);
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
                ClearContext();
                if (t != null)
                {
                    SetContext(t);
                }
            }
            if (t == null)
            {
                UpdateLabel();
            }
        }

        void ClearContext()
        {
            if (this.context != null)
            {
                this.context.PropertyChanged -= new PropertyChangedEventHandler(OnPropertyChanged);
            }
            this.context = null;
            this.binding = null;
        }

        void SetContext(Transaction transaction)
        {
            this.context = transaction;
            if (this.context != null)
            {
                this.context.PropertyChanged += new PropertyChangedEventHandler(OnPropertyChanged);
                UpdateLabel();
            }
            else
            {
                this.Text = string.Empty;
            }
        }

        private string GetStringValue(SqlDecimal s)
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

        private void UpdateLabel()
        {
            if (this.binding != null)
            {
                // already taken care of.
                return;
            }
            if (this.context != null)
            {
                string value = string.Empty;
                var investment = this.context.Investment;

                switch (this.fieldName)
                {
                    case "Number":
                        value = this.context.Number;
                        break;
                    case "Date":
                        value = this.context.Date.ToString("d");
                        break;
                    case "SalesTax":
                        value = GetStringValue(this.context.SalesTax);
                        break;
                    case "Debit":
                        value = GetStringValue(this.context.Debit);
                        break;
                    case "Credit":
                        value = GetStringValue(this.context.Credit);
                        break;
                    case "Balance":
                        value = this.context.Balance.ToString("N"); // show zeros
                        break;
                    case "PayeeOrTransferCaption":
                        value = this.context.PayeeOrTransferCaption;
                        break;
                    case "CategoryName":
                        value = this.context.Category == null ? "" : this.context.Category.Name;
                        break;
                    case "Memo":
                        value = this.context.Memo;
                        break;
                    case "RunningBalance":
                        value = GetStringValue(this.context.RunningBalance);
                        break;
                    case "InvestmentUnitPrice":
                        value = GetStringValue(this.context.InvestmentUnitPrice);
                        break;
                    case "RunningUnits":
                        value = GetStringValue(this.context.RunningUnits, "N0");
                        break;
                    case "InvestmentUnits":
                        value = GetStringValue(this.context.InvestmentUnits);
                        break;
                    case "CurrentUnitPrice":
                        if (investment != null)
                        {
                            value = GetStringValue(investment.CurrentUnitPrice);
                        }
                        break;
                    case "CurrentUnits":
                        if (investment != null)
                        {
                            value = GetStringValue(investment.CurrentUnits);
                        }
                        break;

                }
                if (value == null)
                {
                    value = string.Empty;
                }
                this.Text = value;
            }
            else
            {
                this.Text = string.Empty;
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
                Margin = new Thickness(0, -2, 0, 0)
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
            return new TransactionTextField("Date", binding, dataItem)
            {
                VerticalAlignment = VerticalAlignment.Top,
                MinWidth = 100,
                Margin = new Thickness(4, 0, 0, 0)
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
                <CheckBox Style="{DynamicResource SimpleCheckBox}" IsChecked="{Binding IsBudgeted}"    
                              Checked="OnBudgetCheckboxChecked"
                              Unchecked="OnBudgetCheckboxUnchecked"
                              ToolTip="Whether transaction is included in the budget or not" 
                              Visibility="{Binding RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type views:TransactionsView}}, Path=BalancingBudget, Converter={StaticResource TrueToVisible}}"/>
        </StackPanel>
     */
    /// </summary>  
    public class TransactionStatusButton : Border
    {
        Button button;
        TextBlock label;
        Transaction context;
        TransactionsView view;
        CheckBox checkbox;

        /// <summary>
        /// This button presents the transaction status
        /// </summary>
        public TransactionStatusButton()
        {
            this.Child = this.button = new Button();
            this.button.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(OnStatusButtonClick);
            this.DataContextChanged += new DependencyPropertyChangedEventHandler(OnDataContextChanged);
            this.button.Content = label = new TextBlock();
            this.button.BorderBrush = Brushes.Transparent;
            this.button.Background = Brushes.Transparent;
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            SetView(WpfHelper.FindAncestor<TransactionsView>(this));
            base.OnVisualParentChanged(oldParent);
        }

        void SetView(TransactionsView newView)
        {
            if (this.view != null)
            {
                this.view.BalancingBudgetChanged -= new EventHandler(OnBalancingBudgetChanged);
            }
            this.view = newView;
            if (this.view != null)
            {
                this.view.BalancingBudgetChanged += new EventHandler(OnBalancingBudgetChanged);

                if (this.view.BalancingBudget)
                {
                    OnBalancingBudget();
                }
            }
        }

        void OnBalancingBudgetChanged(object sender, EventArgs e)
        {
            if (this.view != null && this.view == sender)
            {
                OnBalancingBudget();
            }
            else
            {
                this.Child = this.button;
            }
        }

        void OnBalancingBudget()
        {
            if (this.view.BalancingBudget)
            {
                if (this.checkbox == null)
                {
                    /*
                     *  <CheckBox Style="{DynamicResource SimpleCheckBox}" IsChecked="{Binding IsBudgeted}"    
                          Checked="OnBudgetCheckboxChecked"
                          Unchecked="OnBudgetCheckboxUnchecked"
                          ToolTip="Whether transaction is included in the budget or not" 
                          Visibility="{Binding RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type views:TransactionsView}}, Path=BalancingBudget, 
                               Converter={StaticResource TrueToVisible}}"/>
                     */
                    this.checkbox = new CheckBox();
                    this.checkbox.SetResourceReference(CheckBox.StyleProperty, "SimpleCheckBox");
                    this.checkbox.ToolTip = "Whether transaction is included in the budget or not";
                    this.checkbox.Margin = new Thickness(1, 2, 0, 0);
                    RegisterCheckboxEvents(true);
                }
                this.Child = this.checkbox;
                UpdateCheckbox();
            }
            else
            {
                this.Child = this.button;
            }
        }

        private void RegisterCheckboxEvents(bool register)
        {
            if (register)
            {
                this.checkbox.Checked += OnBudgetCheckboxChecked;
                this.checkbox.Unchecked += OnBudgetCheckboxUnchecked;
            }
            else
            {
                this.checkbox.Checked -= OnBudgetCheckboxChecked;
                this.checkbox.Unchecked -= OnBudgetCheckboxUnchecked;
            }
        }

        private void OnBudgetCheckboxChecked(object sender, RoutedEventArgs e)
        {
            CheckBox box = (CheckBox)sender;
            Transaction t = box.DataContext as Transaction;
            if (t != null)
            {
                t.BeginUpdate(this);
                if (!t.IsBudgeted)
                {
                    t.IsBudgeted = true;
                    if (this.view != null && this.view.BalancingBudget)
                    {
                        // user is manually adding this transaction into this month's budget for
                        // whatever reason, independent of transaction date.
                        t.BudgetBalanceDate = this.view.BudgetDate;
                    }
                }
                t.EndUpdate();
            }
        }

        private void OnBudgetCheckboxUnchecked(object sender, RoutedEventArgs e)
        {
            CheckBox box = (CheckBox)sender;
            Transaction t = box.DataContext as Transaction;
            if (t != null)
            {
                t.IsBudgeted = false;
            }
        }


        void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ClearContext();
            Transaction t = e.NewValue as Transaction;
            if (t == null)
            {
                Investment i = e.NewValue as Investment;
                if (i != null)
                {
                    t = i.Transaction;
                }
            }
            if (t != null)
            {
                SetContext(t);
            }

            UpdateLabel();
            UpdateCheckbox();
        }

        void ClearContext()
        {
            if (this.context != null)
            {
                this.context.PropertyChanged -= new PropertyChangedEventHandler(OnPropertyChanged);
            }
            this.context = null;
        }

        void SetContext(Transaction transaction)
        {
            this.context = transaction;
            if (this.context != null)
            {
                this.context.PropertyChanged += new PropertyChangedEventHandler(OnPropertyChanged);
            }
        }

        void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "StatusString":
                    UpdateLabel();
                    break;
                case "IsBudgeted":
                    UpdateCheckbox();
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

        void UpdateCheckbox()
        {
            if (this.checkbox != null)
            {
                RegisterCheckboxEvents(false); // don't let this trigger a model change, we are trying to update the UI to reflect the model...
                this.checkbox.IsChecked = (this.context != null) ? this.context.IsBudgeted : false;
                RegisterCheckboxEvents(true);
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
                ClearContext();
                if (t != null)
                {
                    SetContext(t);
                }
            }

            if (t == null)
            {
                // recycling of last row.
                UpdateLabel();
            }
        }

        void ClearContext()
        {
            if (this.context != null)
            {
                this.context.PropertyChanged -= new PropertyChangedEventHandler(OnPropertyChanged);
            }
            this.context = null;
        }

        void SetContext(Transaction transaction)
        {
            this.context = transaction;
            if (this.context != null)
            {
                this.context.PropertyChanged += new PropertyChangedEventHandler(OnPropertyChanged);
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
                SqlDecimal value = (this.type == "Debit") ? this.context.Debit : this.context.Credit;
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
                        Margin = new Thickness(0, 0, 2, 0),
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
                        SwitchTransferCaption(opposition.Type == "Debit" ? false : true);
                    }
                    else
                    {
                        // clear opposing field.   
                        opposition.Clear();
                        SwitchTransferCaption(opposition.Type == "Debit" ? true : false);
                    }
                }
            }
        }

        private TransactionAmountControl GetOpposingField()
        {
            string oppositeName = (type == "Debit") ? "Credit" : "Debit";
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
            if ((this.type == "Debit" && context != null && context.HasDebitAndIsSplit) ||
                (this.type == "Credit" && context != null && context.HasCreditAndIsSplit))
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
            if ((this.Type == "Debit" && context != null && context.HasDebitAndIsSplit) ||
                (this.Type == "Credit" && context != null && context.HasCreditAndIsSplit))
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



    public class ZeroToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            decimal currentValue = (decimal)value;
            if (currentValue == 0)
            {
                return Brushes.Gray;
            }
            return Brushes.Red;
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
