using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Walkabout.Data;
using Walkabout.Interfaces.Views;
using Walkabout.Utilities;

namespace Walkabout.Views
{
    /// <summary>
    /// This class tracks changes made to the Money objects and provides various
    /// accessors that provide information about those changes and a Summary UI
    /// that shows the changes with links that can navigate to them.
    /// </summary>
    public class ChangeTracker : IDisposable
    {
        MyMoney myMoney;
        Dictionary<Type, ChangeList> changes;
        bool isDirty;
        IViewNavigator navigator;

        public ChangeTracker(MyMoney money, IViewNavigator navigator)
        {
            this.myMoney = money;
            this.myMoney.Changed += new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);
            this.navigator = navigator;
            this.Clear();
        }

        public void Dispose()
        {
            if (this.myMoney != null)
            {
                this.myMoney.Changed -= new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);
                this.myMoney = null;
            }
        }

        public event EventHandler DirtyChanged;

        public bool IsDirty
        {
            get { return this.isDirty; }
            set
            {
                if (this.isDirty != value)
                {
                    this.isDirty = value;
                    this.OnDirtyChanged();
                }
            }
        }

        void OnDirtyChanged()
        {
            if (DirtyChanged != null)
            {
                DirtyChanged(this, EventArgs.Empty);
            }
        }

        class ChangeList
        {
            public Type owner;
            public HashSet<object> inserted = new HashSet<object>();
            public HashSet<object> changed = new HashSet<object>();
            public HashSet<object> deleted = new HashSet<object>();

            public ChangeList(Type t)
            {
                this.owner = t;
            }

            public void Add(ChangeEventArgs args)
            {
                object item = args.Item;
                switch (args.ChangeType)
                {
                    case ChangeType.None:
                    case ChangeType.TransientChanged:
                        break;
                    case ChangeType.Changed:
                        if (!this.inserted.Contains(item) && !this.deleted.Contains(item))
                        {
                            this.changed.Add(item);
                        }
                        break;
                    case ChangeType.Inserted:
                        this.deleted.Remove(item);
                        this.changed.Remove(item);
                        this.inserted.Add(item);
                        break;
                    case ChangeType.Deleted:
                        this.inserted.Remove(item);
                        this.deleted.Add(item);
                        break;
                }
            }
        }

        public void Clear()
        {
            this.changes = new Dictionary<Type, ChangeList>();
            this.IsDirty = false;
        }

        public int ChangeCount
        {
            get
            {
                int total = 0;
                foreach (ChangeList list in this.changes.Values)
                {
                    total += list.changed.Count + list.deleted.Count + list.inserted.Count;
                }
                return total;
            }
        }

        void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
            while (args != null)
            {
                object item = args.Item;
                if (item != null)
                {
                    Type t = item.GetType();
                    if (t != typeof(MyMoney))
                    {
                        ChangeList list;
                        if (!this.changes.TryGetValue(t, out list))
                        {
                            list = new ChangeList(t);
                            this.changes[t] = list;
                        }
                        list.Add(args);
                    }
                }
                args = args.Next;
            }
            if (this.changes.Count > 0)
            {
                this.IsDirty = true;
            }
        }

        public Grid GetSummary()
        {
            Grid summary = new Grid();

            summary.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            summary.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            summary.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            summary.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });

            summary.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });

            if (this.changes.Count == 0)
            {
                this.AddColumn(summary, "No changes pending", 0, 0).FontStyle = FontStyles.Italic;
                return summary;
            }

            this.AddColumn(summary, "Inserted", 1, 0).FontStyle = FontStyles.Italic;
            this.AddColumn(summary, "Deleted", 2, 0).FontStyle = FontStyles.Italic;
            this.AddColumn(summary, "Changed", 3, 0).FontStyle = FontStyles.Italic;

            int row = 1;

            foreach (ChangeList list in from list in this.changes.Values orderby list.owner.Name select list)
            {
                Type owner = list.owner;

                summary.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });

                this.AddColumn(summary, owner.Name, 0, row);
                this.AddLink(summary, list.inserted, 1, row);
                this.AddLink(summary, list.deleted, 2, row);
                this.AddLink(summary, list.changed, 3, row);
                row++;
            }

            return summary;
        }

        private TextBlock AddColumn(Grid grid, string text, int column, int row)
        {
            TextBlock block = new TextBlock();
            block.Margin = new Thickness(4, 2, 4, 2);
            block.Text = text;
            Grid.SetColumn(block, column);
            Grid.SetRow(block, row);
            grid.Children.Add(block);
            return block;
        }

        private TextBlock AddLink(Grid grid, HashSet<object> list, int column, int row)
        {
            TextBlock block = this.AddColumn(grid, list.Count.ToString(), column, row);
            if (list.Count > 0)
            {
                if (list.FirstOrDefault() is Transaction)
                {
                    block.Foreground = AppTheme.Instance.GetThemedBrush("HyperlinkForeground");
                    block.Background = Brushes.Transparent; // makes the text more hittable.
                    block.MouseEnter += new System.Windows.Input.MouseEventHandler((s, e) =>
                    {
                        block.TextDecorations.Add(TextDecorations.Underline);
                    });
                    block.MouseLeave += new System.Windows.Input.MouseEventHandler((s, e) =>
                    {
                        block.TextDecorations.Clear();
                    });
                    block.MouseDown += new System.Windows.Input.MouseButtonEventHandler((s, e) =>
                    {
                        this.NavigateTo(list);
                    });
                }
                else
                {
                    List<string> tooltip = new List<string>();

                    foreach (object i in list)
                    {
                        if (i is Security)
                        {
                            this.AppendNonEmptyLine(tooltip, ((Security)i).Name);
                        }
                        else if (i is Account)
                        {
                            this.AppendNonEmptyLine(tooltip, ((Account)i).Name);
                        }
                        else if (i is Payee)
                        {
                            this.AppendNonEmptyLine(tooltip, ((Payee)i).Name);
                        }
                        else if (i is Category)
                        {
                            this.AppendNonEmptyLine(tooltip, ((Category)i).Name);
                        }
                        else if (i is Alias)
                        {
                            this.AppendNonEmptyLine(tooltip, ((Alias)i).Pattern);
                        }
                        else if (i is Investment)
                        {
                            // these we can navigate to also...? tooltip.AppendLine(((Investment)i).Pattern);
                        }
                        else if (i is RentBuilding)
                        {
                            this.AppendNonEmptyLine(tooltip, ((RentBuilding)i).Name);
                        }
                        else if (i is RentUnit)
                        {
                            this.AppendNonEmptyLine(tooltip, ((RentUnit)i).Name);
                        }
                        else if (i is LoanPayment)
                        {
                            //
                        }
                        if (tooltip.Count > 10)
                        {
                            break;
                        }
                    }

                    if (tooltip.Count > 0)
                    {
                        tooltip.Sort();
                        block.ToolTip = string.Join("\n", tooltip.ToArray());
                    }
                }
            }
            return block;
        }

        private void AppendNonEmptyLine(List<string> names, string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                names.Add(text);
            }
        }

        private void NavigateTo(HashSet<object> list)
        {
            if (list.Count == 0)
            {
                return;
            }
            object first = list.FirstOrDefault();
            if (first is Transaction)
            {
                this.navigator.ViewTransactions(list.Cast<Transaction>());
            }
        }

        /// <summary>
        /// Return a list of the accounts that have unsaved changes.
        /// </summary>
        public IEnumerable<Account> ChangedAccounts
        {
            get
            {
                ChangeList list = null;
                this.changes.TryGetValue(typeof(Account), out list);
                return GetChanged<Account>(list);
            }
        }

        /// <summary>
        /// Return a list of the accounts that have been deleted since last save.
        /// </summary>
        public IEnumerable<Account> DeletedAccounts
        {
            get
            {
                ChangeList list = null;
                this.changes.TryGetValue(typeof(Account), out list);
                return GetDeleted<Account>(list);
            }
        }

        /// <summary>
        /// Return a list of the accounts that have been inserted since last save.
        /// </summary>
        public IEnumerable<Account> InsertedAccounts
        {
            get
            {
                ChangeList list = null;
                this.changes.TryGetValue(typeof(Account), out list);
                return GetInserted<Account>(list);
            }
        }

        /// <summary>
        /// Return a list of the Transactions that have unsaved changes 
        /// </summary>
        public IEnumerable<Transaction> ChangedTransactions
        {
            get
            {
                ChangeList list = null;
                this.changes.TryGetValue(typeof(Transaction), out list);
                return GetChanged<Transaction>(list);
            }
        }

        /// <summary>
        /// Return a list of the Transactions that have been deleted since last save.
        /// </summary>
        public IEnumerable<Transaction> DeletedTransactions
        {
            get
            {
                ChangeList list = null;
                this.changes.TryGetValue(typeof(Transaction), out list);
                return GetDeleted<Transaction>(list);
            }
        }

        /// <summary>
        /// Return a list of the Transactions that have been inserted since last save.
        /// </summary>
        public IEnumerable<Transaction> InsertedTransaction
        {
            get
            {
                ChangeList list = null;
                this.changes.TryGetValue(typeof(Transaction), out list);
                return GetInserted<Transaction>(list);
            }
        }

        /// <summary>
        /// Return a list of the Splits that have unsaved changes.
        /// </summary>
        public IEnumerable<Split> ChangedSplits
        {
            get
            {
                ChangeList list = null;
                this.changes.TryGetValue(typeof(Split), out list);
                return GetChanged<Split>(list);
            }
        }

        /// <summary>
        /// Return a list of the Transactions that have been deleted since last save.
        /// </summary>
        public IEnumerable<Split> DeletedSplits
        {
            get
            {
                ChangeList list = null;
                this.changes.TryGetValue(typeof(Split), out list);
                return GetDeleted<Split>(list);
            }
        }

        /// <summary>
        /// Return a list of the Transactions that have been inserted since last save.
        /// </summary>
        public IEnumerable<Split> InsertedSplits
        {
            get
            {
                ChangeList list = null;
                this.changes.TryGetValue(typeof(Split), out list);
                return GetInserted<Split>(list);
            }
        }

        /// <summary>
        /// Return a list of the Payees that have unsaved changes.
        /// </summary>
        public IEnumerable<Payee> ChangedPayees
        {
            get
            {
                ChangeList list = null;
                this.changes.TryGetValue(typeof(Payee), out list);
                return GetChanged<Payee>(list);
            }
        }

        /// <summary>
        /// Return a list of the Payees that have been deleted since last save.
        /// </summary>
        public IEnumerable<Payee> DeletedPayees
        {
            get
            {
                ChangeList list = null;
                this.changes.TryGetValue(typeof(Payee), out list);
                return GetDeleted<Payee>(list);
            }
        }

        /// <summary>
        /// Return a list of the Payees that have been inserted since last save.
        /// </summary>
        public IEnumerable<Payee> InsertedPayees
        {
            get
            {
                ChangeList list = null;
                this.changes.TryGetValue(typeof(Payee), out list);
                return GetInserted<Payee>(list);
            }
        }


        /// <summary>
        /// Return a list of the Categories that have unsaved changes.
        /// </summary>
        public IEnumerable<Category> ChangedCategories
        {
            get
            {
                ChangeList list = null;
                this.changes.TryGetValue(typeof(Category), out list);
                return GetChanged<Category>(list);
            }
        }

        /// <summary>
        /// Return a list of the Categories that have been deleted since last save.
        /// </summary>
        public IEnumerable<Category> DeletedCategories
        {
            get
            {
                ChangeList list = null;
                this.changes.TryGetValue(typeof(Category), out list);
                return GetDeleted<Category>(list);
            }
        }

        /// <summary>
        /// Return a list of the Categories that have been inserted since last save.
        /// </summary>
        public IEnumerable<Category> InsertedCategories
        {
            get
            {
                ChangeList list = null;
                this.changes.TryGetValue(typeof(Category), out list);
                return GetInserted<Category>(list);
            }
        }


        private static IEnumerable<T> GetChanged<T>(ChangeList list) where T : class
        {
            if (list != null)
            {
                foreach (ChangeList a in list.changed)
                {
                    if (a is T)
                    {
                        yield return a as T;
                    }
                }
            }
        }

        private static IEnumerable<T> GetDeleted<T>(ChangeList list) where T : class
        {
            if (list != null)
            {
                foreach (ChangeList a in list.deleted)
                {
                    if (a is T)
                    {
                        yield return a as T;
                    }
                }
            }
        }

        private static IEnumerable<T> GetInserted<T>(ChangeList list) where T : class
        {
            if (list != null)
            {
                foreach (PersistentObject a in list.inserted)
                {
                    if (a is T)
                    {
                        yield return a as T;
                    }
                }
            }
        }

    }
}
