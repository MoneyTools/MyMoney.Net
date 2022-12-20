using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Interfaces.Views;
using Walkabout.Utilities;

namespace Walkabout.Views
{
    /// <summary>
    /// Interaction logic for AliasesView.xaml
    /// </summary>
    public partial class AliasesView : UserControl, IView
    {
        bool payeesDirty;
        bool dirty;
        Alias rowEdit;
        DelayedActions delayedActions = new DelayedActions();

        public AliasesView()
        {
            this.InitializeComponent();
            IsVisibleChanged += new DependencyPropertyChangedEventHandler(this.AliasesView_IsVisibleChanged);
            this.AliasDataGrid.RowEditEnding += this.AliasDataGrid_RowEditEnding;
            Unloaded += (s, e) =>
            {
                if (this.money != null)
                {
                    this.money.Changed += this.OnMoneyChanged;
                }
            };
        }

        void AliasDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            // There seems to be a bug in WPF where this is called before the final editing cell is committed!
            // For example, I change Alias type to Regex, hit ENTER and this is called before Alias.Type is changed to Regex!
            this.rowEdit = e.Row.DataContext as Alias;
            this.delayedActions.StartDelayedAction("FindConflicts", this.FindConflicts, TimeSpan.FromMilliseconds(30));
        }

        void FindConflicts()
        {
            if (this.rowEdit != null)
            {
                IEnumerable<Alias> conflicts = this.money.FindSubsumedAliases(this.rowEdit);
                foreach (Alias conflict in conflicts)
                {
                    if (conflict != this.rowEdit)
                    {
                        conflict.OnDelete();
                    }
                }
            }
        }

        void AliasesView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                this.OnBeforeViewStateChanged();
                this.OnAfterViewStateChanged();
                if (this.dirty)
                {
                    // catch up now that we need to be visible.
                    this.ShowAliases();
                }
            }
        }


        void ShowAliases()
        {
            this.AliasDataGrid.SetItemsSource(new AliasCollection(this.Money, this.quickFilter));
            this.PayeeList = new ListCollectionView(this.GetPayeesList());
            this.dirty = false;
            this.payeesDirty = false;
        }


        private void ComboBoxForPayee_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.OldFocus is TextBox)
            {
                Alias t = this.AliasDataGrid.SelectedItem as Alias;
                if (t != null)
                {
                    ComboBox combo = sender as System.Windows.Controls.ComboBox;
                    object value = combo.SelectedItem;
                    string selected = value == null ? string.Empty : value.ToString();
                    string text = combo.Text;
                    if (selected != text && text != null && text.Length > 0)
                    {
                        // then we need to add the Payee for this item (so long as user is not trying to add a transfer.
                        if (!Walkabout.Data.Transaction.IsTransferCaption(text))
                        {
                            t.Payee = this.Money.Payees.FindPayee(text, true);
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

        List<AliasType> aliasTypes = new List<AliasType>(new AliasType[] { AliasType.None, AliasType.Regex });

        public IList<AliasType> AliasTypes
        {
            get { return this.aliasTypes; }
        }



        public ListCollectionView PayeeList
        {
            get { return (ListCollectionView)this.GetValue(PayeeListProperty); }
            set { this.SetValue(PayeeListProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PayeeList.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PayeeListProperty =
            DependencyProperty.Register("PayeeList", typeof(ListCollectionView), typeof(AliasesView), new PropertyMetadata(null));


        private List<Payee> GetPayeesList()
        {
            List<Payee> names = new List<Payee>();
            foreach (Payee p in this.Money.Payees.GetPayees())
            {
                names.Add(p);
            }
            names.Sort(new Comparison<Payee>((p1, p2) => { return string.Compare(p1.Name, p2.Name); }));
            return names;
        }

        /// <summary>
        /// Manages the collection of aliases displayed in the grid.
        /// </summary>
        class AliasCollection : FilteredObservableCollection<Alias>
        {
            MyMoney money;

            public AliasCollection(MyMoney money, string filter)
                : base(money.Aliases.GetAliases(true), filter)
            {
                this.money = money;
            }

            public override bool IsMatch(Alias item, FilterLiteral filterToken)
            {
                if (filterToken == null)
                {
                    return true;
                }

                string payeeName = (item.Payee != null) ? item.Payee.Name : null;

                return filterToken.MatchSubstring(item.AliasType.ToString()) || filterToken.MatchSubstring(item.Pattern) || filterToken.MatchSubstring(payeeName);
            }


            protected override void InsertItem(int index, Alias alias)
            {
                base.InsertItem(index, alias);

                if (alias.Id == -1)
                {
                    this.money.Aliases.AddAlias(alias);
                }
            }

            protected override void RemoveItem(int index)
            {
                Alias alias = (Alias)this[index];

                base.RemoveItem(index);

                if (alias.Id != -1)
                {
                    this.money.Aliases.RemoveAlias(alias);
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
                if (this.money != null)
                {
                    this.money.Changed -= this.OnMoneyChanged;
                }

                this.money = value;

                if (this.money != null)
                {
                    this.money.Changed += this.OnMoneyChanged;
                    this.ShowAliases();
                }
            }
        }

        void OnMoneyChanged(object sender, ChangeEventArgs e)
        {
            AliasCollection view = this.AliasDataGrid.ItemsSource as AliasCollection;

            while (e != null)
            {
                if (e.Item is Payee)
                {
                    this.payeesDirty = true;
                    break;
                }
                else if (e.Item is Alias && e.Item != this.rowEdit)
                {
                    if (e.ChangeType == ChangeType.Inserted && view != null && view.Contains((Alias)e.Item))
                    {
                        // ignore newly inserted items
                    }
                    else if (!this.AliasDataGrid.IsEditing)  // ignore changes if grid is being edited.
                    {
                        this.dirty = true;
                        break;
                    }
                }
                e = e.Next;
            }
            if (this.dirty || this.payeesDirty && this.IsVisible)
            {
                // start delayed update
                this.delayedActions.StartDelayedAction("RefreshList", this.RefreshList, TimeSpan.FromMilliseconds(50));
            }
        }

        void RefreshList()
        {
            if (this.payeesDirty)
            {
                this.PayeeList = new ListCollectionView(this.GetPayeesList());
                this.payeesDirty = false;
            }

            if (this.dirty && this.IsVisible)
            {
                if (this.AliasDataGrid.IsEditing)
                {
                    this.AliasDataGrid.CommitEdit();
                }
                // show the new list
                this.ShowAliases();
            }
        }


        public void ActivateView()
        {
            this.Focus();
            // re-wire events
            this.Money = this.money;
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
            get { return this.sp; }
            set { this.sp = value; }
        }

        public void Commit()
        {
            //todo
        }

        public string Caption
        {
            get { return "Aliases"; }
        }

        public object SelectedRow
        {
            get { return this.AliasDataGrid.SelectedItem; }
            set { this.AliasDataGrid.SelectedItem = value; }
        }

        public ViewState ViewState
        {
            get
            {
                // todo;
                return null;
            }
            set
            {
                // todo;
            }
        }

        public ViewState DeserializeViewState(System.Xml.XmlReader reader)
        {
            // todo;
            return null;
        }

        public void FocusQuickFilter()
        {
            this.QuickFilterUX.FocusTextBox();
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
                    this.ShowAliases();
                }
            }
        }

        public bool IsQueryPanelDisplayed { get; set; }

        private void OnQuickFilterValueChanged(object sender, string filter)
        {
            this.QuickFilter = filter;
        }

        #endregion


    }
}
