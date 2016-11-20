using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Interfaces;
using Walkabout.Help;
using Walkabout.Utilities;
using System.Windows.Threading;
using Walkabout.Interfaces.Views;

namespace Walkabout.Views
{
    /// <summary>
    /// Interaction logic for AliasesView.xaml
    /// </summary>
    public partial class AliasesView : UserControl, IView
    {
        bool payeesDirty;
        bool dirty;
        DispatcherTimer timer;
        Alias rowEdit;

        public AliasesView()
        {
            InitializeComponent();
            this.IsVisibleChanged += new DependencyPropertyChangedEventHandler(AliasesView_IsVisibleChanged);
            this.AliasDataGrid.RowEditEnding += AliasDataGrid_RowEditEnding;
        }

        void AliasDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            rowEdit = e.Row.DataContext as Alias;
        }

        void AliasesView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                OnBeforeViewStateChanged();
                OnAfterViewStateChanged();
                if (dirty)
                {
                    // catch up now that we need to be visible.
                    ShowAliases();
                }               
            }
        }


        void ShowAliases()
        {
            this.AliasDataGrid.SetItemsSource(new AliasCollection(this.Money, this.quickFilter));
            this.PayeeList = new ListCollectionView(GetPayeesList());
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
            get { return aliasTypes; }
        }



        public ListCollectionView PayeeList
        {
            get { return (ListCollectionView)GetValue(PayeeListProperty); }
            set { SetValue(PayeeListProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PayeeList.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PayeeListProperty =
            DependencyProperty.Register("PayeeList", typeof(ListCollectionView), typeof(AliasesView), new PropertyMetadata(null));


        private List<Payee> GetPayeesList()
        {
            List<Payee> names = new List<Payee>();
            foreach (Payee p in Money.Payees.GetPayees())
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
                :base(money.Aliases.GetAliases(), filter)
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
                    money.Aliases.AddAlias(alias);
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
                }

                this.money = value;

                if (this.money != null)
                {
                    this.money.Changed += OnMoneyChanged;
                }
                ShowAliases();
            }
        }

        void OnMoneyChanged(object sender, ChangeEventArgs e)
        {
            AliasCollection view = this.AliasDataGrid.ItemsSource as AliasCollection;

            while (e != null)
            {
                if (e.Item is Payee)
                {
                    payeesDirty = true;
                    break;
                }
                else if (e.Item is Alias && e.Item != rowEdit)
                {
                    if (e.ChangeType == ChangeType.Inserted && view != null && view.Contains((Alias)e.Item))
                    {
                        // ignore newly inserted items
                    }
                    else if (!this.AliasDataGrid.IsEditing)  // ignore changes if grid is being edited.
                    {
                        dirty = true;
                        break;
                    }
                }
                e = e.Next;
            }
            if (dirty || payeesDirty && this.IsVisible)
            {
                // start delayed update
                StartTimer();
            }
        }
        
        private void StartTimer()
        {            
            StopTimer();
            timer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            timer.Tick += OnTimerTick;
            timer.Start();
        }

        private void StopTimer()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= OnTimerTick;
                timer = null;
            }
        }

        void OnTimerTick(object sender, object e)
        {
            if (payeesDirty)
            {
                this.PayeeList = new ListCollectionView(GetPayeesList());
                payeesDirty = false;
            }

            StopTimer();

            if (dirty && this.IsVisible)
            {
                if (this.AliasDataGrid.IsEditing)
                {
                    this.AliasDataGrid.CommitEdit();
                }
                // show the new list
                ShowAliases();
            }
        }


        public void ActivateView()
        {
            Focus();
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
            QuickFilterUX.FocusTextBox();
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
                    ShowAliases();
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
