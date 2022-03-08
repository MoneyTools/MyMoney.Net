using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Interfaces.Views;
using Walkabout.Utilities;

namespace Walkabout.Views
{
    /// <summary>
    /// Interaction logic for SecuritiesView.xaml
    /// </summary>
    public partial class SecuritiesView : UserControl, IView
    {
        public readonly static RoutedUICommand CommandToggleAllSplits = new RoutedUICommand("View Toggle View All Splits", "CommandToggleAllSplits", typeof(SecuritiesView));
        public readonly static RoutedUICommand CommandShowRelatedTransactions = new RoutedUICommand("Show Related Transactions", "CommandShowRelatedTransactions", typeof(TransactionsView));

        public SecuritiesView()
        {
            InitializeComponent();
            SetupGrid(this.SecuritiesDataGrid);
        }

        public event EventHandler<SecuritySelectionEventArgs> SecurityNavigated;
        public event EventHandler<SecuritySelectionEventArgs> SecuritySelected;

        internal void OnSecurityNavigated(Security s)
        {
            if (SecurityNavigated != null)
            {
                SecurityNavigated(this, new SecuritySelectionEventArgs(s));
            }
        }
        internal void OnSecuritySelected(Security s)
        {
            if (SecuritySelected != null)
            {
                SecuritySelected(this, new SecuritySelectionEventArgs(s));
            }
        }

        DataGrid stockSplitGrid;

        void SetupGrid(DataGrid grid)
        {
            stockSplitGrid = grid;
            grid.BeginningEdit += OnBeginEdit;
            grid.RowEditEnding += OnDataGridCommit;
            grid.CellEditEnding += OnDataGridCellEditEnding;
            grid.PreviewKeyDown += new KeyEventHandler(OnDataGridPreviewKeyDown);
            grid.SelectionChanged += new SelectionChangedEventHandler(OnGridSelectionChanged);
        }

        void TearDownGrid(DataGrid grid)
        {
            grid.BeginningEdit -= OnBeginEdit;
            grid.RowEditEnding -= OnDataGridCommit;
            grid.PreviewKeyDown -= new KeyEventHandler(OnDataGridPreviewKeyDown);
            grid.SelectionChanged -= new SelectionChangedEventHandler(OnGridSelectionChanged);
        }

        public Security SelectedSecurity
        {
            get { return this.SecuritiesDataGrid.SelectedItem as Security; }
        }


        public ObservableCollection<SecurityType> SecurityTypes
        {
            get
            {
                ObservableCollection<SecurityType> list = new ObservableCollection<SecurityType>();
                foreach (System.Reflection.FieldInfo field in typeof(SecurityType).GetFields())
                {
                    if (field.IsStatic)
                    {
                        object value = field.GetValue(null);
                        if (value is SecurityType)
                        {
                            list.Add((SecurityType)value);
                        }
                    }
                }
                return list;
            }
        }

        public ObservableCollection<YesNo> TaxableTypes
        {
            get
            {
                ObservableCollection<YesNo> list = new ObservableCollection<YesNo>();
                list.Add(YesNo.No);
                list.Add(YesNo.Yes);
                return list;
            }
        }

        object lastSelectedItem;
        int splitVisibleRowId;

        private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataGrid grid = (DataGrid)sender;
            if (grid.Name == "TheGridForStockSplit")
            {
                return;
            }

            if (e.AddedItems.Count == 1)
            {
                object selected = e.AddedItems[0];

                if (selected is Security)
                {
                    //-------------------------------------------------------------
                    // The user just changed the selection of the current Transaction
                    // We now need to decide if we went to hide the Detail view or leave it as is


                    if (SecuritiesDataGrid.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Visible)
                    {
                        // Do not change any thing keep the Detail view in Mini mode 
                    }
                    else if (splitVisibleRowId != this.SelectedRowId)
                    {
                        if (SecuritiesDataGrid.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.VisibleWhenSelected)
                        {
                            // VisibleWhenSelected can only have been set by the user action of clicking on the "SPLIT" button
                            // Since we are now selecting an different Transaction we can hide the Detail view

                            // In the case were the transaction split was created for the first time
                            // We need to refresh the items in order show the SPLIT button
                            // TheActiveGrid.Items.Refresh();

                            RestoreSplitViewMode();
                        }
                    }

                    Security s = (Security)selected;
                    s.IsExpanded = (SecuritiesDataGrid.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Collapsed) ? false : true;
                }

                if (lastSelectedItem != null && lastSelectedItem != selected)
                {
                    Security s = lastSelectedItem as Security;
                    if (s != null)
                    {
                        s.IsExpanded = (SecuritiesDataGrid.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Visible) ? true : false;
                    }
                }
                    

                lastSelectedItem = selected;
                OnSelectionChanged(selected as Security);
            }
        }

        void OnSelectionChanged(Security security)
        {
            OnSecuritySelected(security);
        }


        DataGrid FindDataGridContainingFocus()
        {
            DependencyObject e = Keyboard.FocusedElement as DependencyObject;
            while (e != null && !(e is DataGrid))
            {
                e = VisualTreeHelper.GetParent(e);
            }
            return e as DataGrid;
        }

        bool IsKeyboardFocusInsideSplitsDataGrid
        {
            get
            {
                DataGrid focus = FindDataGridContainingFocus();
                if (focus != null && focus.Name == "TheGridForStockSplit")
                {
                    return true;
                }
                return false;
            }
        }

        void OnDataGridPreviewKeyDown(object sender, KeyEventArgs e)
        {
            MoneyDataGrid grid = (MoneyDataGrid)sender;

            if (IsKeyboardFocusInsideSplitsDataGrid)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Tab:
                    if (grid.CurrentColumn != null)
                    {
                        if (e.KeyboardDevice.IsKeyDown(Key.LeftShift) || e.KeyboardDevice.IsKeyDown(Key.RightShift))
                        {
                            e.Handled = SecuritiesDataGrid.MoveFocusToPreviousEditableField();
                        }
                        else
                        {
                            e.Handled = SecuritiesDataGrid.MoveFocusToNextEditableField();
                        }
                    }
                    break;

                case Key.Enter:
                    {
                        if (grid != null && grid.CurrentCell != null && !this.IsEditing)
                        {
                            grid.BeginEdit();
                            e.Handled = true;
                        }
                        break;
                    }


            }
        }

        bool IsEditing { get; set; }

        void OnBeginEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            IsEditing = true;
        }

        void OnDataGridCommit(object sender, DataGridRowEditEndingEventArgs e)
        {
            this.IsEditing = false;
        }

        private void OnDataGridCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if ((string)e.Column.Header == "Symbol" && e.EditAction == DataGridEditAction.Commit)
            {
                Security s = e.Row.DataContext as Security;

                TextBox box = e.EditingElement.FindFirstDescendantOfType<TextBox>();
                try
                {
                    ValidateSymbol(s, box.Text);
                }
                catch (Exception ex)
                {
                    e.Cancel = true;
                    MessageBoxEx.Show(ex.Message, "Symbol Error", MessageBoxButton.OK);
                }
            }
        }

        private void ValidateSymbol(Security edited, string newSymbol)
        {
            if (string.IsNullOrEmpty(newSymbol))
            {
                return;
            }
            if (newSymbol.IndexOfAny(new char[] { ' ', '\t', '\r', '\n' }) >= 0)
            {
                throw new Exception("Symbol '" + newSymbol + "' should not contain any spaces.");
            }

            foreach (Security s in this.money.Securities.AllSecurities)
            {
                if (!s.IsDeleted && s != edited && !string.IsNullOrEmpty(s.Symbol)  &&
                    string.Compare(s.Symbol, newSymbol, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    throw new Exception("Symbol '" + newSymbol + "' is already being used elsewhere");
                }
            }
        }

        public static readonly DependencyProperty ViewAllSplitsProperty = DependencyProperty.Register("ViewAllSplits", typeof(bool), typeof(SecuritiesView), new FrameworkPropertyMetadata(new PropertyChangedCallback(OnViewAllSplitsChanged)));

        public bool ViewAllSplits
        {
            get { return (bool)GetValue(ViewAllSplitsProperty); }
            set { SetValue(ViewAllSplitsProperty, value); }
        }

        static void OnViewAllSplitsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SecuritiesView tv = (SecuritiesView)d;
            tv.OnViewAllSplitsChanged();
        }
        
        void OnViewAllSplitsChanged()
        {
            OnBeforeViewStateChanged();

            // Toggle the Detail Split View 
            if (ViewAllSplits == true)
            {
                SecuritiesDataGrid.RowDetailsTemplate = TryFindResource("StockSplitMiniView") as DataTemplate;
                SecuritiesDataGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Visible;
                splitVisibleRowId = this.SelectedRowId;
            }
            else
            {
                SecuritiesDataGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed;
                splitVisibleRowId = -1;
            }

            ToggleShowSplits.IsChecked = ViewAllSplits;
        }

        public static readonly DependencyProperty ViewAllSecuritiesProperty = DependencyProperty.Register("ViewAllSecurities", typeof(bool), typeof(SecuritiesView), new FrameworkPropertyMetadata(true, new PropertyChangedCallback(OnViewAllSecuritiesChanged)));

        public bool ViewAllSecurities
        {
            get { return (bool)GetValue(ViewAllSecuritiesProperty); }
            set { SetValue(ViewAllSecuritiesProperty, value); }
        }

        static void OnViewAllSecuritiesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SecuritiesView view = (SecuritiesView)d;
            view.OnViewAllSecuritiesChanged();
        }

        void OnViewAllSecuritiesChanged()
        {
            ToggleShowAllSecurities.IsChecked = !ViewAllSecurities;
            ShowSecurities();
        }

        private void TheGridForStockSplit_Loaded(object sender, RoutedEventArgs e)
        {
            DataGrid grid = (DataGrid)sender;
            SetupGrid(grid);
        }

        private void TheGridForStockSplit_Unloaded(object sender, RoutedEventArgs e)
        {
            DataGrid grid = (DataGrid)sender;
            TearDownGrid(grid);
        }

        public int SelectedRowId
        {
            get
            {
                Security s = this.SecuritiesDataGrid.SelectedItem as Security;
                if (s != null) return s.Id;
                return -1;
            }
        }

        internal void GotoSecurity(Security security)
        {
            SecurityCollection sc = (SecurityCollection)this.SecuritiesDataGrid.ItemsSource;
            if (!sc.Contains(security))
            {
                if (sc.Filter != null)
                {
                    sc.Filter = null;
                }
                if (!sc.Contains(security))
                {
                    if (!ViewAllSecurities)
                    {
                        // add all securities so we have better chance of showing the security in question.
                        ViewAllSecurities = true;
                    }
                }
            }

            this.SecuritiesDataGrid.SelectedItem = security;
            this.SecuritiesDataGrid.ScrollIntoView(security);
        }

        private void ShowSecurities()
        {
            try
            {
                // dumb thing doesn't let us update the list while sorting.
                DataGridColumn sort = RemoveSort(this.SecuritiesDataGrid);

                this.SecuritiesDataGrid.ItemsSource = new SecurityCollection(this, this.Money, this.quickFilter, this.ViewAllSecurities);
                if (sort != null)
                {
                    // now put it back!
                    sort.SortDirection = System.ComponentModel.ListSortDirection.Ascending;
                }
            }
            catch (Exception e)
            {
                MessageBoxEx.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        DataGridColumn RemoveSort(DataGrid grid)
        {
            DataGridColumn result = null;
            foreach (DataGridColumn c in grid.Columns)
            {
                if (c.SortDirection.HasValue)
                {
                    result = c;
                    c.SortDirection = null;
                }
            }
            return result;
        }

        private void ComboBoxForSymbol_FilterChanged(object sender, RoutedEventArgs e)
        {
            FilteringComboBox combo = sender as FilteringComboBox;
            combo.FilterPredicate = new Predicate<object>((o) => { return o.ToString().IndexOf(combo.Filter, StringComparison.OrdinalIgnoreCase) >= 0; });
        }


        public ListCollectionView AllSymbols
        {
            get 
            { 
                return new ListCollectionView(((List<string>)this.money.Securities.AllSymbols).ToArray()); 
            }
        }

        private void ComboBoxForSymbol_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.OldFocus is TextBox)
            {
                Security t = this.SecuritiesDataGrid.SelectedItem as Security;
                if (t != null)
                {
                    ComboBox combo = sender as System.Windows.Controls.ComboBox;
                    object value = combo.SelectedItem;
                    string selected = value == null ? "" : value.ToString();
                    string text = combo.Text;
                    if (selected != text && text != null && text.Length > 0)
                    {
                        // then we need to add the Symbol to the list.
                        t.Symbol = text;
                    }
                }
            }
        }


        #region IView

        MyMoney money;

        public MyMoney Money
        {

            get { return this.money; }

            set
            {
                this.money = value;
                ShowSecurities();
            }

        }

        public void ActivateView()
        {
            Focus();
            ShowSecurities();
        }

        public event EventHandler BeforeViewStateChanged;

        void OnBeforeViewStateChanged()
        {
            if (BeforeViewStateChanged != null)
            {
                BeforeViewStateChanged(this, EventArgs.Empty);
            }
        }

        public event EventHandler<AfterViewStateChangedEventArgs> AfterViewStateChanged;

        void OnAfterViewStateChanged()
        {
            if (AfterViewStateChanged != null)
            {
                AfterViewStateChanged(this, new AfterViewStateChangedEventArgs(0));
            }
        }

        IServiceProvider sp;

        public IServiceProvider ServiceProvider
        {
            get { return sp; }
            set { sp = value; }
        }

        public void Commit()
        {
            //todo
        }

        public string Caption
        {
            get { return "Securities"; }
        }

        public object SelectedRow
        {
            get { return this.SecuritiesDataGrid.SelectedItem; }
            set
            {
                this.SecuritiesDataGrid.SelectedItem = value;
            }
        }

        public ViewState ViewState
        {
            get 
            {
                Security s = this.SelectedSecurity;
                string name = s == null ? string.Empty : s.Name;
                SecuritiesViewState state = new SecuritiesViewState()
                {
                    SelectedSecurity = name,
                    ViewAllSplits = this.ViewAllSplits,
                    ViewAllSecurities = this.ViewAllSecurities
                };

                int column = 0;
                foreach (DataGridColumn c in SecuritiesDataGrid.Columns)
                {
                    if (c.SortDirection.HasValue)
                    {
                        state.SortDirection = c.SortDirection.Value;
                        state.SortedColumn = column;
                        break;
                    }
                    column++;
                }
                return state;
            }

            set
            {
                SecuritiesViewState svs = value as SecuritiesViewState;
                if (svs != null)
                {
                    this.ViewAllSplits = svs.ViewAllSplits;
                    this.ViewAllSecurities = svs.ViewAllSecurities;

                    string security = svs.SelectedSecurity;
                    if (!string.IsNullOrEmpty(security))
                    {
                        Security s = this.money.Securities.FindSecurity(security, false);
                        if (s != null)
                        {
                            SecuritiesDataGrid.SelectedItem = s;
                            SecuritiesDataGrid.ScrollIntoView(s);
                        }
                    }
                    if (svs.SortedColumn != -1 && svs.SortedColumn < SecuritiesDataGrid.Columns.Count && svs.SortDirection.HasValue)
                    {
                        DataGridColumn c = SecuritiesDataGrid.Columns[svs.SortedColumn];
                        c.SortDirection = svs.SortDirection.Value;
                    }
                }
            }
        }


        public ViewState DeserializeViewState(System.Xml.XmlReader reader)
        {
            return SecuritiesViewState.Deserialize(reader);
        }

        public bool IsQueryPanelDisplayed { get; set; }

        #endregion

        #region Context Menu Commands

        private void OnCommandToggleAllSplits(object sender, ExecutedRoutedEventArgs e)
        {
            this.ViewAllSplits = !this.ViewAllSplits;
        }

        private void CanExecute_ToggleAllSplits(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void OnShowRelatedTransactions(object sender, ExecutedRoutedEventArgs e)
        {
            Security s = this.SecuritiesDataGrid.SelectedItem as Security;
            if (s != null)
            {
                // navigate to transaction view showing all transactions involving the selected security
                OnSecurityNavigated(s);
            }
        }

        private void CanExecute_ShowRelatedTransactions(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.SecuritiesDataGrid.SelectedItem is Security;
        }

        private void OnButtonSplitClicked(object sender, MouseButtonEventArgs e)
        {
            ToggleButton button = (ToggleButton)sender;
            Security s = button.Tag as Security;
            int id = -1;
            if (s != null) id = s.Id;

            // Toggle the Detail Split View 
            if (id == splitVisibleRowId && this.SecuritiesDataGrid.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.VisibleWhenSelected)
            {

                // Done editing the split. must be executive after the any edit filed are committed
                // so we run this on the ContextIdle
                Dispatcher.BeginInvoke(new Action(() => RestoreSplitViewMode()), DispatcherPriority.ContextIdle);
            }
            else
            {
                // Show the Full Details Split inline DataGrid
                this.SecuritiesDataGrid.RowDetailsTemplate = this.TryFindResource("StockSplitDetailView") as DataTemplate;
                this.SecuritiesDataGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected;
                splitVisibleRowId = id;
            }
        }


        private void RestoreSplitViewMode()
        {
            if (splitVisibleRowId != -1)
            {
                //
                // Split view is changing and we have a chance to cleanup any empty split that the user may have left behind
                //
                Security securityLostSelection = this.Money.Securities.FindSecurityAt(splitVisibleRowId);

                if (securityLostSelection != null)
                {
                    if (securityLostSelection.StockSplits != null)
                    {
                        securityLostSelection.StockSplits.RemoveEmptySplits();
                    }
                }

            }

            if (this.ViewAllSplits)
            {
                this.SecuritiesDataGrid.RowDetailsTemplate = this.TryFindResource("StockSplitMiniView") as DataTemplate;
                this.SecuritiesDataGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Visible;
                splitVisibleRowId = this.SelectedRowId;
            }
            else
            {
                this.SecuritiesDataGrid.RowDetailsTemplate = this.TryFindResource("StockSplitDetailView") as DataTemplate;
                this.SecuritiesDataGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed;
                splitVisibleRowId = -1;
            }
        }
        #endregion 
    
        #region SEARCH & FILTERING

        public void FocusQuickFilter()
        {
            QuickFilterUX.FocusTextBox();
        }

        private string quickFilter = string.Empty;

        public string QuickFilter
        {
            get { return this.quickFilter; }
            set
            {
                if (this.quickFilter != value)
                {
                    this.quickFilter = value;
                    Refresh();
                }
            }
        }

        void Refresh()
        {
            ShowSecurities();
        }

        private void OnQuickFilterValueChanged(object sender, string filter)
        {
            this.QuickFilter = filter;
        }

        #endregion   

        private void OnShowAllSecurities_Checked(object sender, RoutedEventArgs e)
        {
            ViewAllSecurities = false; // filter is in effect
        }

        private void OnShowAllSecurities_Unchecked(object sender, RoutedEventArgs e)
        {
            ViewAllSecurities = true; // filter is not in effect
        }

        private void OnToggleShowSplits_Checked(object sender, RoutedEventArgs e)
        {
            ViewAllSplits = true;
        }

        private void OnToggleShowSplits_Unchecked(object sender, RoutedEventArgs e)
        {
            ViewAllSplits = false;
        }
        
    }

    public class SecuritiesViewState : ViewState
    {
        public string SelectedSecurity { get; set; }

        private bool viewAllSplits;
        public bool ViewAllSplits
        {
            get { return viewAllSplits; }
            set { viewAllSplits = value; }
        }

        private bool viewAllSecurities = true; // default true
        public bool ViewAllSecurities
        {
            get { return viewAllSecurities; }
            set { viewAllSecurities = value; }
        }

        int sort = -1;
        public int SortedColumn
        {
            get { return this.sort; }
            set { this.sort = value; }
        }

        ListSortDirection? direction;

        public ListSortDirection? SortDirection
        {
            get { return this.direction; }
            set { this.direction = value; }
        }

        public override void ReadXml(XmlReader r)
        {
            if (r.IsEmptyElement) return;

            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    switch (r.Name)
                    {
                        case "ViewAllSplits":
                            this.ViewAllSplits = ReadBoolean(r);
                            break;
                        case "ViewAllSecurities":
                            this.ViewAllSecurities = ReadBoolean(r);
                            break;
                        case "SelectedSecurity":
                            this.SelectedSecurity = r.ReadString();
                            break;
                        case "SortedColumn":
                            this.SortedColumn = ReadInt(r, -1);
                            break;
                        case "SortDirection":
                            this.SortDirection = ReadEnum<ListSortDirection>(r);
                            break;

                    }
                }
            }
        }

        public override void WriteXml(XmlWriter writer)
        {
            if (writer != null)
            {
                writer.WriteElementString("ViewAllSplits", this.ViewAllSplits.ToString());
                writer.WriteElementString("ViewAllSecurities", this.ViewAllSecurities.ToString());
                if (!string.IsNullOrEmpty(SelectedSecurity))
                {
                    writer.WriteElementString("SelectedSecurity", SelectedSecurity);
                }
                if (this.SortedColumn != -1)
                {
                    writer.WriteElementString("SortedColumn", SortedColumn.ToString());
                }
                if (this.SortDirection.HasValue)
                {
                    writer.WriteElementString("SortDirection", SortDirection.Value.ToString());
                }
            }
        }


        public static SecuritiesViewState Deserialize(XmlReader r)
        {
            SecuritiesViewState state = new SecuritiesViewState();
            state.ReadXml(r);
            return state;
        }

    }

    /// <summary>
    /// Manages the collection of securities displayed in the grid, and the filtering of that collection.
    /// </summary>
    internal class SecurityCollection : FilteredObservableCollection<Security>
    {
        MyMoney money;
        SecuritiesView view;

        public SecurityCollection(SecuritiesView view, MyMoney money, string filter, bool showAllSecurities)
            : base(showAllSecurities ? money.Securities.GetSecurities() : money.GetOwnedSecurities(), filter)
        {
            this.view = view;
            this.money = money;
        }

        public override bool IsMatch(Security item, FilterLiteral filter)
        {
            if (filter == null)
            {
                return true;
            }
            else
            {
                return (filter.MatchSubstring(item.CuspId) || filter.MatchSubstring(item.Name) || filter.MatchSubstring(item.Symbol) || filter.MatchDecimal(item.Price));
            }
        }

        protected override void InsertItem(int index, Security security)
        {
            base.InsertItem(index, security);

            if (security.Id == -1)
            {
                money.Securities.AddSecurity(security);
            }
        }

        protected override void RemoveItem(int index)
        {
            Security security = (Security)this[index];

            foreach (Transaction t in money.Transactions)
            {
                if (t.Investment != null && t.Investment.Security == security)
                {
                    if (MessageBoxEx.Show("This security is being used, do you want to see which transactions are using it?", "Security in use", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                    {
                        view.OnSecurityNavigated(security);
                    }
                    return;
                }
            }

            base.RemoveItem(index);

            if (security.Id != -1)
            {
                this.money.Securities.RemoveSecurity(security);
            }
        }
    }

    public class SecuritySelectionEventArgs : EventArgs
    {
        Security security;
        public SecuritySelectionEventArgs(Security s)
        {
            security = s;
        }
        public Security Security { get { return this.security; } }
    }

}
