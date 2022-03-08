using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Walkabout.Data;
using Walkabout.Interfaces.Views;

namespace Walkabout.Views
{
    /// <summary>
    /// This class tracks changes made to the Money objects and provides various
    /// accessors that provide information about those changes and a Summary UI
    /// that shows the changes with links that can navigate to them.
    /// </summary>
    public class ChangeTracker
    {
        MyMoney myMoney;
        Dictionary<Type, ChangeList> changes;
        bool isDirty;
        IViewNavigator navigator;

        public ChangeTracker(MyMoney money, IViewNavigator navigator)
        {
            myMoney = money;
            myMoney.Changed += new EventHandler<ChangeEventArgs>(OnMoneyChanged);
            this.navigator = navigator;
            Clear();
        }

        public event EventHandler DirtyChanged;

        public bool IsDirty
        {
            get { return isDirty; }
            set
            {
                if (isDirty != value)
                {
                    isDirty = value;
                    OnDirtyChanged();
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
                owner = t;
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
                        if (!inserted.Contains(item) && !deleted.Contains(item))
                        {
                            changed.Add(item);
                        }
                        break;
                    case ChangeType.Inserted:
                        deleted.Remove(item);
                        changed.Remove(item);
                        inserted.Add(item);
                        break;
                    case ChangeType.Deleted:
                        inserted.Remove(item);
                        deleted.Add(item);
                        break;
                }
            }
        }

        public void Clear()
        {
            changes = new Dictionary<Type, ChangeList>();
            IsDirty = false;
        }

        public int ChangeCount
        {
            get
            {
                int total = 0;
                foreach (ChangeList list in changes.Values)
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
                        if (!changes.TryGetValue(t, out list))
                        {
                            list = new ChangeList(t);
                            changes[t] = list;
                        }
                        list.Add(args);
                    }
                }
                args = args.Next;
            }
            if (changes.Count > 0)
            {
                IsDirty = true;
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

            if (changes.Count == 0)
            {
                AddColumn(summary, "No changes pending", 0, 0).FontStyle = FontStyles.Italic;
                return summary;
            }

            AddColumn(summary, "Inserted", 1, 0).FontStyle = FontStyles.Italic;
            AddColumn(summary, "Deleted", 2, 0).FontStyle = FontStyles.Italic;
            AddColumn(summary, "Changed", 3, 0).FontStyle = FontStyles.Italic;

            int row = 1;

            foreach (ChangeList list in from list in changes.Values orderby list.owner.Name select list)
            {
                Type owner = list.owner;

                summary.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
                    
                AddColumn(summary, owner.Name, 0, row);
                AddLink(summary, list.inserted, 1, row);
                AddLink(summary, list.deleted, 2, row);
                AddLink(summary, list.changed, 3, row);
                row++;
            }

            return summary;
        }

        private TextBlock AddColumn(Grid grid, string text, int column, int row)
        {
            TextBlock block = new TextBlock();
            block.Margin = new Thickness(4,2,4,2);
            block.Text = text;
            Grid.SetColumn(block, column);
            Grid.SetRow(block, row);
            grid.Children.Add(block);
            return block;
        }

        private TextBlock AddLink(Grid grid, HashSet<object> list, int column, int row)
        {
            TextBlock block = AddColumn(grid, list.Count.ToString(), column, row);
            if (list.Count > 0)
            {
                if (list.FirstOrDefault() is Transaction)
                {
                    block.Foreground = Brushes.Blue;
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
                        NavigateTo(list);
                    });
                }
                else 
                {
                    List<string> tooltip = new List<string>();

                    foreach (object i in list) 
                    {
                        if (i is Security)
                        {
                            AppendNonEmptyLine(tooltip, ((Security)i).Name);
                        }
                        else if (i is Account)
                        {
                            AppendNonEmptyLine(tooltip, ((Account)i).Name);
                        }
                        else if (i is Payee)
                        {
                            AppendNonEmptyLine(tooltip, ((Payee)i).Name);
                        }
                        else if (i is Category)
                        {
                            AppendNonEmptyLine(tooltip, ((Category)i).Name);
                        }
                        else if (i is Alias)
                        {
                            AppendNonEmptyLine(tooltip, ((Alias)i).Pattern);
                        }
                        else if (i is Investment)
                        {
                            // these we can navigate to also...? tooltip.AppendLine(((Investment)i).Pattern);
                        }
                        else if (i is RentBuilding)
                        {
                            AppendNonEmptyLine(tooltip, ((RentBuilding)i).Name);
                        }
                        else if (i is RentUnit)
                        {
                            AppendNonEmptyLine(tooltip, ((RentUnit)i).Name);
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
            if (list.Count == 0) {
                return;
            }
            object first = list.FirstOrDefault();
            if (first is Transaction)
            {
                navigator.ViewTransactions(list.Cast<Transaction>());
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
                changes.TryGetValue(typeof(Account), out list);
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
                changes.TryGetValue(typeof(Account), out list);
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
                changes.TryGetValue(typeof(Account), out list);
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
                changes.TryGetValue(typeof(Transaction), out list);
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
                changes.TryGetValue(typeof(Transaction), out list);
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
                changes.TryGetValue(typeof(Transaction), out list);
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
                changes.TryGetValue(typeof(Split), out list);
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
                changes.TryGetValue(typeof(Split), out list);
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
                changes.TryGetValue(typeof(Split), out list);
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
                changes.TryGetValue(typeof(Payee), out list);
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
                changes.TryGetValue(typeof(Payee), out list);
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
                changes.TryGetValue(typeof(Payee), out list);
                return GetInserted<Payee>(list);
            }
        }


        /// <summary>
        /// Return a list of the Categories that have unsaved changes.
        /// </summary>
        public IEnumerable<Category> ChangedCategories
        {
            get {
                ChangeList list = null;
                changes.TryGetValue(typeof(Category), out list);
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
                changes.TryGetValue(typeof(Category), out list);
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
                changes.TryGetValue(typeof(Category), out list);
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
