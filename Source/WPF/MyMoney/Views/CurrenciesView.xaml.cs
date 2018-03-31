using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Configuration;
using Walkabout.Controls;
using Walkabout.Data;
using System.Windows.Threading;
using System.Xml;
using Walkabout.Interfaces;
using Walkabout.Help;
using Walkabout.Utilities;
using Walkabout.Interfaces.Views;

namespace Walkabout.Views
{
    //public class SecuritySelectionEventArgs : EventArgs
    //{
    //    Security security;
    //    public SecuritySelectionEventArgs(Security s)
    //    {
    //        security = s;
    //    }
    //    public Security Security { get { return this.security; } }
    //}


    /// <summary>
    /// Interaction logic for CurrenciesView.xaml
    /// </summary>
    public partial class CurrenciesView : UserControl, IView
    {

        public CurrenciesView()
        {
            InitializeComponent();
            SetupGrid(this.CurrenciesDataGrid);
        }

        public void FocusQuickFilter()
        {
        }

        void SetupGrid(DataGrid grid)
        {
            grid.BeginningEdit += OnBeginEdit;
            grid.RowEditEnding += OnDataGridCommit;
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
            get { return this.CurrenciesDataGrid.SelectedItem as Security; }
        }


        object lastSelectedItem;

        private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataGrid grid = (DataGrid)sender;

            if (e.AddedItems.Count == 1)
            {
                object selected = e.AddedItems[0];

                if (selected is Security)
                {
                    //-------------------------------------------------------------
                    // The user just changed the selection of the current Transaction
                    // We now need to decide if we went to hide the Detail view or leave it as is


                    if (CurrenciesDataGrid.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Visible)
                    {
                        // Do not change any thing keep the Detail view in Mini mode 
                    }

                    Security s = (Security)selected;
                    s.IsExpanded = (CurrenciesDataGrid.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Collapsed) ? false : true;
                }

                if (lastSelectedItem != null && lastSelectedItem != selected)
                {
                    Security s = lastSelectedItem as Security;
                    if (s != null)
                    {
                        s.IsExpanded = (CurrenciesDataGrid.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Visible) ? true : false;
                    }
                }


                lastSelectedItem = selected;

            }
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

        void OnDataGridPreviewKeyDown(object sender, KeyEventArgs e)
        {
            MoneyDataGrid grid = (MoneyDataGrid)sender;


            switch (e.Key)
            {
                case Key.Tab:
                    if (grid.CurrentColumn != null)
                    {
                        if (e.KeyboardDevice.IsKeyDown(Key.LeftShift) || e.KeyboardDevice.IsKeyDown(Key.RightShift))
                        {
                            e.Handled = grid.MoveFocusToPreviousEditableField();
                        }
                        else
                        {
                            e.Handled = grid.MoveFocusToNextEditableField();
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

        public int SelectedRowId
        {
            get
            {
                Security s = this.CurrenciesDataGrid.SelectedItem as Security;
                if (s != null) return s.Id;
                return -1;
            }
        }


        void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
        }

        void ShowCurrencies()
        {
            try
            {
                // dumb thing doesn't let us update the list while sorting.
                DataGridColumn sort = RemoveSort(this.CurrenciesDataGrid);

                this.CurrenciesDataGrid.ItemsSource = new CurrencyCollection(this, this.Money, this.quickFilter);
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




        /// <summary>
        /// Manages the collection of securities displayed in the grid.
        /// </summary>
        class CurrencyCollection : FilteredObservableCollection<Currency>
        {
            MyMoney money;
            CurrenciesView view;

            public CurrencyCollection(CurrenciesView view, MyMoney money, string filter)
                : base(money.Currencies.GetCurrencies(), filter)
            {
                this.view = view;
                this.money = money;
            }


            public override bool IsMatch(Currency item, FilterLiteral filterToken)
            {
                if (filterToken == null)
                {
                    return true;
                }
                else
                {
                    return filterToken.MatchSubstring(item.Name) || 
                        filterToken.MatchSubstring(item.Symbol) || 
                        filterToken.MatchDecimal(item.Ratio) ||
                        filterToken.MatchDecimal(item.LastRatio);
                }
            }

            protected override void InsertItem(int index, Currency currency)
            {
                base.InsertItem(index, currency);

                if (currency.Id == -1)
                {
                    money.Currencies.AddCurrency(currency);
                }
            }

            protected override void RemoveItem(int index)
            {
                Currency currency = (Currency)this[index];

                base.RemoveItem(index);

                if (currency.Id != -1)
                {
                    this.money.Currencies.RemoveCurrency(currency);
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
                this.money.Changed += new EventHandler<ChangeEventArgs>(OnMoneyChanged);
                ShowCurrencies();
            }

        }



        public void ActivateView()
        {
            Focus();
            this.Money.Changed += new EventHandler<ChangeEventArgs>(OnMoneyChanged);
            ShowCurrencies();
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
            get { return this.CurrenciesDataGrid.SelectedItem; }
            set { this.CurrenciesDataGrid.SelectedItem = value; }
        }

        public ViewState ViewState
        {
            get
            {

                Security s = this.SelectedSecurity;
                string name = s == null ? string.Empty : s.Name;
                CurrenciesViewState state = new CurrenciesViewState()
                {
                    SelectedSecurity = name,
                };

                int column = 0;
                foreach (DataGridColumn c in CurrenciesDataGrid.Columns)
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
                CurrenciesViewState svs = value as CurrenciesViewState;
                if (svs != null)
                {

                    string security = svs.SelectedSecurity;
                    if (!string.IsNullOrEmpty(security))
                    {
                        Security s = this.money.Securities.FindSecurity(security, false);
                        if (s != null)
                        {
                            CurrenciesDataGrid.SelectedItem = s;
                            CurrenciesDataGrid.ScrollIntoView(s);
                        }
                    }
                    if (svs.SortedColumn != -1 && svs.SortedColumn < CurrenciesDataGrid.Columns.Count && svs.SortDirection.HasValue)
                    {
                        DataGridColumn c = CurrenciesDataGrid.Columns[svs.SortedColumn];
                        c.SortDirection = svs.SortDirection.Value;
                    }
                }
            }
        }


        public ViewState DeserializeViewState(System.Xml.XmlReader reader)
        {
            return CurrenciesViewState.Deserialize(reader);
        }

        string quickFilter;

        public string QuickFilter
        {
            get { return this.quickFilter; }
            set
            {
                if (this.quickFilter != value)
                {
                    this.quickFilter = value;
                    ShowCurrencies();
                }
            }
        }

        public bool IsQueryPanelDisplayed { get; set; }

        #endregion

    }

    public class CurrenciesViewState : ViewState
    {
        public string SelectedSecurity { get; set; }


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


        public static CurrenciesViewState Deserialize(XmlReader r)
        {
            CurrenciesViewState state = new CurrenciesViewState();
            state.ReadXml(r);
            return state;
        }

    }
}
