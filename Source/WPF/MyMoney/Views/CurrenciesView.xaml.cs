using System;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Interfaces.Views;
using Walkabout.Utilities;

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
            this.InitializeComponent();
            this.SetupGrid(this.CurrenciesDataGrid);
            Unloaded += (s, e) =>
            {
                if (this.money != null)
                {
                    this.money.Changed -= new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);
                }
            };
        }

        public void FocusQuickFilter()
        {
        }

        private void SetupGrid(DataGrid grid)
        {
            grid.BeginningEdit += this.OnBeginEdit;
            grid.RowEditEnding += this.OnDataGridCommit;
            grid.PreviewKeyDown += new KeyEventHandler(this.OnDataGridPreviewKeyDown);
            grid.SelectionChanged += new SelectionChangedEventHandler(this.OnGridSelectionChanged);
        }

        private void TearDownGrid(DataGrid grid)
        {
            grid.BeginningEdit -= this.OnBeginEdit;
            grid.RowEditEnding -= this.OnDataGridCommit;
            grid.PreviewKeyDown -= new KeyEventHandler(this.OnDataGridPreviewKeyDown);
            grid.SelectionChanged -= new SelectionChangedEventHandler(this.OnGridSelectionChanged);
        }

        public Security SelectedSecurity
        {
            get { return this.CurrenciesDataGrid.SelectedItem as Security; }
        }

        private object lastSelectedItem;

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


                    if (this.CurrenciesDataGrid.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Visible)
                    {
                        // Do not change any thing keep the Detail view in Mini mode 
                    }

                    Security s = (Security)selected;
                    s.IsExpanded = (this.CurrenciesDataGrid.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Collapsed) ? false : true;
                }

                if (this.lastSelectedItem != null && this.lastSelectedItem != selected)
                {
                    Security s = this.lastSelectedItem as Security;
                    if (s != null)
                    {
                        s.IsExpanded = (this.CurrenciesDataGrid.RowDetailsVisibilityMode == DataGridRowDetailsVisibilityMode.Visible) ? true : false;
                    }
                }


                this.lastSelectedItem = selected;

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

        private void OnDataGridPreviewKeyDown(object sender, KeyEventArgs e)
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
                        if (grid != null && !this.IsEditing)
                        {
                            grid.BeginEdit();
                            e.Handled = true;
                        }
                        break;
                    }


            }
        }

        private bool IsEditing { get; set; }

        private void OnBeginEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            this.IsEditing = true;
        }

        private void OnDataGridCommit(object sender, DataGridRowEditEndingEventArgs e)
        {
            this.IsEditing = false;
        }

        public int SelectedRowId
        {
            get
            {
                Security s = this.CurrenciesDataGrid.SelectedItem as Security;
                if (s != null)
                {
                    return s.Id;
                }

                return -1;
            }
        }

        private void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
        }

        private void ShowCurrencies()
        {
            try
            {
                // dumb thing doesn't let us update the list while sorting.
                DataGridColumn sort = this.RemoveSort(this.CurrenciesDataGrid);

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

        private DataGridColumn RemoveSort(DataGrid grid)
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

        private void ComboBoxForName_GotFocus(object sender, RoutedEventArgs e)
        {
            ComboBox box = (ComboBox)sender;
            // auto populate if possible.
            if (this.CurrenciesDataGrid.SelectedItem is Currency c && string.IsNullOrEmpty(c.Name))
            {
                DataGridRow row = this.CurrenciesDataGrid.GetRowFromItem(this.CurrenciesDataGrid.SelectedItem);
                if (row != null)
                {
                    var symbol = this.CurrenciesDataGrid.GetUncommittedColumnText(row, "Symbol");
                    if (!string.IsNullOrEmpty(symbol))
                    {
                        var ci = Currency.GetCultureForCurrency(symbol);
                        var ri = new RegionInfo(ci.Name);
                        box.Text = ri.CurrencyEnglishName;
                    }
                }
            }
        }

        private void ComboBoxForCultureCode_GotFocus(object sender, RoutedEventArgs e)
        {
            // auto populate if possible.
            ComboBox box = (ComboBox)sender;
            if (this.CurrenciesDataGrid.SelectedItem is Currency c && string.IsNullOrEmpty(c.Name))
            {
                DataGridRow row = this.CurrenciesDataGrid.GetRowFromItem(this.CurrenciesDataGrid.SelectedItem);
                if (row != null)
                {
                    var symbol = this.CurrenciesDataGrid.GetUncommittedColumnText(row, "Symbol");
                    if (!string.IsNullOrEmpty(symbol))
                    {
                        var ci = Currency.GetCultureForCurrency(symbol);
                        box.Text = ci.Name;
                    }
                }
            }
        }


        /// <summary>
        /// Manages the collection of securities displayed in the grid.
        /// </summary>
        private class CurrencyCollection : FilteredObservableCollection<Currency>
        {
            private readonly MyMoney money;
            private readonly CurrenciesView view;

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
                        filterToken.MatchSubstring(item.CultureCode) ||
                        filterToken.MatchDecimal(item.Ratio) ||
                        filterToken.MatchDecimal(item.LastRatio);
                }
            }

            protected override void InsertItem(int index, Currency currency)
            {
                base.InsertItem(index, currency);

                if (currency.Id == -1)
                {
                    this.money.Currencies.AddCurrency(currency);
                }
            }

            protected override void RemoveItem(int index)
            {
                Currency currency = this[index];

                base.RemoveItem(index);

                if (currency.Id != -1)
                {
                    this.money.Currencies.RemoveCurrency(currency);
                }
            }
        }



        #region IView

        private MyMoney money;

        public MyMoney Money
        {

            get { return this.money; }

            set
            {
                if (this.money != null)
                {
                    this.money.Changed -= new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);
                }
                this.money = value;
                if (this.money != null)
                {
                    this.money.Changed += new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);
                    this.ShowCurrencies();
                }
            }

        }


        public void ActivateView()
        {
            this.Focus();
            this.Money = this.money;
        }

        public event EventHandler BeforeViewStateChanged;

        private void OnBeforeViewStateChanged()
        {
            if (BeforeViewStateChanged != null)
            {
                BeforeViewStateChanged(this, EventArgs.Empty);
            }
        }

        public event EventHandler<AfterViewStateChangedEventArgs> AfterViewStateChanged;

        private void OnAfterViewStateChanged()
        {
            if (AfterViewStateChanged != null)
            {
                AfterViewStateChanged(this, new AfterViewStateChangedEventArgs(0));
            }
        }

        private IServiceProvider sp;

        public IServiceProvider ServiceProvider
        {
            get { return this.sp; }
            set { this.sp = value; }
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
                foreach (DataGridColumn c in this.CurrenciesDataGrid.Columns)
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
                            this.CurrenciesDataGrid.SelectedItem = s;
                            this.CurrenciesDataGrid.ScrollIntoView(s);
                        }
                    }
                    if (svs.SortedColumn != -1 && svs.SortedColumn < this.CurrenciesDataGrid.Columns.Count && svs.SortDirection.HasValue)
                    {
                        DataGridColumn c = this.CurrenciesDataGrid.Columns[svs.SortedColumn];
                        c.SortDirection = svs.SortDirection.Value;
                    }
                }
            }
        }


        public ViewState DeserializeViewState(System.Xml.XmlReader reader)
        {
            return CurrenciesViewState.Deserialize(reader);
        }

        private string quickFilter;

        public string QuickFilter
        {
            get { return this.quickFilter; }
            set
            {
                if (this.quickFilter != value)
                {
                    this.quickFilter = value;
                    this.ShowCurrencies();
                }
            }
        }

        public bool IsQueryPanelDisplayed { get; set; }

        #endregion

    }

    public class CurrenciesViewState : ViewState
    {
        public string SelectedSecurity { get; set; }

        private int sort = -1;
        public int SortedColumn
        {
            get { return this.sort; }
            set { this.sort = value; }
        }

        private ListSortDirection? direction;

        public ListSortDirection? SortDirection
        {
            get { return this.direction; }
            set { this.direction = value; }
        }

        public override void ReadXml(XmlReader r)
        {
            if (r.IsEmptyElement)
            {
                return;
            }

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
                if (!string.IsNullOrEmpty(this.SelectedSecurity))
                {
                    writer.WriteElementString("SelectedSecurity", this.SelectedSecurity);
                }
                if (this.SortedColumn != -1)
                {
                    writer.WriteElementString("SortedColumn", this.SortedColumn.ToString());
                }
                if (this.SortDirection.HasValue)
                {
                    writer.WriteElementString("SortDirection", this.SortDirection.Value.ToString());
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

    public class CulturePickerConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || (value is INullable && value.ToString() == "Null"))
            {
                return string.Empty;
            }

           
            return value;
        }

        // Given this as Value >>>> "ANG :  Dutch (Sint Maarten) = nl-SX"
        // Return this "nl-SX"
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var cp = (CulturePicker)value;
            if (cp != null)
            {
                return cp.CultureInfoName;
            }
            return value;
        }
    }
}

