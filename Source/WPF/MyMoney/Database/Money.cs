using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Walkabout.Utilities;
using System.Threading;
using Walkabout.Database;
using System.Collections.Specialized;

namespace Walkabout.Data
{
    public enum ChangeType { None, Changed, Inserted, Deleted, Reloaded, Rebalanced, ChildChanged, TransientChanged };

    public delegate void ErrorHandler(string errorMessage);

    public delegate bool AccountFilterPredicate(Account a);


    public class ChangeEventArgs : EventArgs
    {
        object item;
        string name;
        ChangeType type;
        ChangeEventArgs next;
        object source;

        public ChangeEventArgs(object item, string name, ChangeType type)
        {
            this.item = item;
            this.type = type;
            this.name = name;
        }

        public object ChangeSource
        {
            get { return this.source; }
            set { this.source = value; }
        }

        public ChangeType ChangeType
        {
            get { return this.type; }
        }

        public object Item
        {
            get { return this.item; }
        }

        public string Name
        {
            get { return this.name; }
        }

        public ChangeEventArgs Next
        {
            get { return this.next; }
            set { this.next = value; }
        }
    }

    public class ThreadSafeObservableCollection<T> : ObservableCollection<T>
    {
        public ThreadSafeObservableCollection()
        {
        }

        protected override void OnCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                base.OnCollectionChanged(e);
            }));
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                base.OnPropertyChanged(e);
            }));
        }

    }

    class BatchSync
    {
        object syncObject = new object();
        int refCount;

        public int Increment()
        {
            int b = 0;
            lock (this.syncObject)
            {
                this.refCount++;
                b = this.refCount;
            }
            return b;
        }

        public int Decrement()
        {
            int b = 0;
            lock (this.syncObject)
            {
                if (this.refCount > 0)
                {
                    this.refCount--;
                }
                b = this.refCount;
            }
            return b;
        }

        internal int Read()
        {
            int b = 0;
            lock (this.syncObject)
            {
                b = this.refCount;
            }
            return b;
        }
    }

    // For change tracking.
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public abstract class PersistentContainer : INotifyPropertyChanged, IEnumerable<PersistentObject>
    {
        BatchSync batched;
        bool changePending;
        bool saveChangeHistory;
        ChangeEventArgs head;
        ChangeEventArgs tail;
        PersistentObject parent;
        EventHandlerCollection<ChangeEventArgs> handlers;

        BatchSync GetBatched()
        {
            if (batched == null)
            {
                batched = new BatchSync();
            }
            return batched;
        }


        protected PersistentContainer()
        {
        }

        protected PersistentContainer(PersistentObject parent)
        {
            this.parent = parent;
        }

        public PersistentObject Parent { get { return parent; } set { this.parent = value; } }

        public event EventHandler<ChangeEventArgs> Changed
        {
            add
            {
                if (handlers == null)
                {
                    handlers = new EventHandlerCollection<ChangeEventArgs>();
                }
                handlers.AddHandler(value);
            }
            remove
            {
                if (handlers != null)
                {
                    handlers.RemoveHandler(value);
                }
            }
        }

        /// <summary>
        /// Fire a change event 
        /// </summary>
        /// <param name="sender">Object that owns the changed item</param>
        /// <param name="item">The item that was changed</param>
        /// <param name="name">The property that was changed </param>
        /// <param name="type">The type of change being made</param>
        /// <returns>Returns true if the parent container or this container are in batching mode</returns>
        public virtual bool FireChangeEvent(Object sender, object item, string name, ChangeType type)
        {
            return FireChangeEvent(sender, new ChangeEventArgs(item, name, type));
        }

        /// <summary>
        /// Fire a change event 
        /// </summary>
        /// <param name="sender">Object that owns the changed item</param>
        /// <param name="item">The item that was changed</param>
        /// <param name="type">The type of change being made</param>
        /// <returns>Returns true if the parent container or this container are in batching mode</returns>
        public virtual bool FireChangeEvent(Object sender, ChangeEventArgs args)
        {
            if (GetBatched().Read() == 0)
            {
                if (this.parent != null)
                {
                    if (parent.FireChangeEvent(sender, args))
                    {
                        return true;// parent is batching.
                    }
                }

                SendEvent(sender, args);
                return false;
            }
            else
            {
                changePending = true;
                if (this.saveChangeHistory)
                {
                    if (tail != null)
                    {
                        this.tail.Next = args;
                        this.tail = args;
                    }
                    else
                    {
                        this.head = this.tail = args;
                    }
                }
                return true;
            }
        }

        void SendEvent(object sender, ChangeEventArgs args)
        {
            if (this.handlers != null && this.handlers.HasListeners)
            {
                this.handlers.RaiseEvent(sender, args);
            }

            PersistentObject.RaisePropertyChangeEvents(args);
        }

        public bool IsUpdating
        {
            get { return GetBatched().Read() > 0; }
        }

        public virtual void BeginUpdate(bool saveChangeHistory)
        {
            // batched updates
            if (IsUpdating && !this.saveChangeHistory)
                saveChangeHistory = this.saveChangeHistory;
            GetBatched().Increment();
            this.saveChangeHistory = saveChangeHistory;
        }

        public virtual void EndUpdate()
        {
            if (GetBatched().Decrement() == 0 && this.handlers != null && this.handlers.HasListeners && changePending)
            {
                changePending = false;
                if (this.head != null)
                {
                    SendEvent(this, head);
                }
                else
                {
                    SendEvent(this, new ChangeEventArgs(this, null, ChangeType.Changed));
                }
                this.head = this.tail = null;
            }            
        }

        public virtual void OnNameChanged(object o, string oldName, string newName)
        {
        }

        public virtual string Serialize()
        {
            DataContractSerializer xs = new DataContractSerializer(this.GetType());
            using (StringWriter sw = new StringWriter())
            {
                XmlTextWriter w = new XmlTextWriter(sw);
                w.Formatting = Formatting.Indented;
                xs.WriteObject(w, this);
                w.Close();
                return sw.ToString();
            }
        }

        public void RemoveDeleted()
        {
            // Cleanup deleted objects
            List<PersistentObject> list = new List<PersistentObject>();
            foreach (PersistentObject pe in this)
            {
                if (pe.IsDeleted)
                    list.Add(pe);
            }
            foreach (PersistentObject pe in list)
            {
                this.RemoveChild(pe);
            }
        }

        public abstract void Add(object child);

        public abstract void RemoveChild(PersistentObject pe);

        public event PropertyChangedEventHandler PropertyChanged;

        internal void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        protected abstract IEnumerator<PersistentObject> InternalGetEnumerator();

        public IEnumerator<PersistentObject> GetEnumerator()
        {
            return InternalGetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return InternalGetEnumerator();
        }

        internal virtual void MarkAllNew()
        {
            foreach (PersistentObject o in this)
            {
                if (!o.IsDeleted)
                {
                    o.OnInserted();
                }
            }
        }
    }

    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class PersistentObject : INotifyPropertyChanged
    {
        EventHandlerCollection<ChangeEventArgs> handlers;

        [XmlIgnore]
        public bool BatchMode;

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public event EventHandler<ChangeEventArgs> Changed
        {
            add
            {
                if (handlers == null)
                {
                    handlers = new EventHandlerCollection<ChangeEventArgs>();
                }
                handlers.AddHandler(value);
            }
            remove
            {
                if (handlers != null)
                {
                    handlers.RemoveHandler(value);
                }
            }
        }

        ChangeType change = ChangeType.Inserted;

        [XmlIgnore]
        public PersistentContainer Parent { get; set; }

        public PersistentObject()
        { // for serialization
        }

        public PersistentObject(PersistentContainer container)
        {
            this.Parent = container;
        }

        public virtual void OnUpdated()
        {
            // This is done when database is updated.
            if (this.change != ChangeType.Deleted)
                this.change = ChangeType.None;
        }

        public virtual void OnInserted()
        {
            if (this.change != ChangeType.Inserted)
            {
                this.change = ChangeType.Inserted;
                this.FireChangeEvent(this, null, ChangeType.Inserted);
            }
        }

        public virtual void OnDelete()
        {
            if (this.change != ChangeType.Deleted)
            {
                this.change = ChangeType.Deleted;
                this.FireChangeEvent(this, null, ChangeType.Deleted);
            }
        }

        public virtual void OnChanged(string name)
        {
            if (BatchMode == false)
            {
                // Insert or Delete take precedence over Changed.
                if (this.change == ChangeType.None)
                {
                    this.change = ChangeType.Changed;
                }
                FireChangeEvent(this, name, ChangeType.Changed);
            }
        }

        /// <summary>
        /// This method is called when a computed property changes.
        /// This type of change is not persisted.
        /// </summary>
        /// <param name="name"></param>
        public virtual void OnTransientChanged(string name)
        {
            FireChangeEvent(this, name, ChangeType.TransientChanged);
        }

        public virtual void OnNameChanged(string oldName, string newName)
        {
            if (this.Parent != null)
                this.Parent.OnNameChanged(this, oldName, newName);

        }

        [XmlIgnore]
        public bool IsInserted { get { return this.change == ChangeType.Inserted; } }

        [XmlIgnore]
        public bool IsDeleted { get { return this.change == ChangeType.Deleted; } }

        [XmlIgnore]
        public bool IsChanged { get { return this.change == ChangeType.Changed; } }

        public void Undelete()
        {
            this.change = ChangeType.Changed;
        }

        BatchSync batched;
        bool changePending;
        ChangeEventArgs head;
        ChangeEventArgs tail;
        object changeSource;

        BatchSync GetBatched()
        {
            if (batched == null)
            {
                batched = new BatchSync();
            }
            return batched;
        }

        public bool IsUpdating
        {
            get { return GetBatched().Read() > 0; }
        }

        public virtual void BeginUpdate(object source)
        {
            // batched updates
            changeSource = source;
            GetBatched().Increment();
        }

        public virtual void EndUpdate()
        {
            if (GetBatched().Decrement() == 0 && changePending)
            {
                changeSource = null;
                changePending = false;
                if (this.head != null)
                {
                    FireChangeEvent(this, head);
                }
                else
                {
                    FireChangeEvent(this, new ChangeEventArgs(this, null, ChangeType.Changed));
                }

                this.head = this.tail = null;
            }
        }
    

        internal void FlushUpdates()
        {
            changePending = false;
            changeSource = null;
            this.head = this.tail = null;
        }

        protected void FireChangeEvent(object item, string name, ChangeType type)
        {
            FireChangeEvent(this, new ChangeEventArgs(item, name, type));
        }

        public virtual bool FireChangeEvent(Object sender, ChangeEventArgs args)
        {
            if (GetBatched().Read() > 0)
            {
                args.ChangeSource = changeSource;
                changePending = true;
                if (head == null)
                {
                    head = tail = args;
                }
                else
                {
                    tail.Next = args;
                    tail = args;
                }
                return true;
            }
            else if (this.Parent != null)
            {
                if (this.Parent.FireChangeEvent(this.Parent, args))
                {
                    return true; // parent is batching.
                }
            }
            else
            {
                SendEvent(sender, args);
            }
            return false;
        }

        void SendEvent(object sender, ChangeEventArgs args)
        {
            if (this.handlers != null && this.handlers.HasListeners)
            {
                this.handlers.RaiseEvent(sender, args);
            }
            RaisePropertyChangeEvents(args);
        }

        internal static void RaisePropertyChangeEvents(ChangeEventArgs args)
        {
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                RaisePropertyChangeEventsOnUIThread(args);
            }));
        }

        private static void RaisePropertyChangeEventsOnUIThread(ChangeEventArgs args)
        {
            while (args != null)
            {
                string name = args.Name;
                if (name != null)
                {
                    PersistentObject o = args.Item as PersistentObject;
                    if (o != null)
                    {
                        o.RaisePropertyChanged(name);
                    }
                    else
                    {
                        PersistentContainer c = args.Item as PersistentContainer;
                        if (c != null)
                        {
                            c.RaisePropertyChanged(name);
                        }
                    }
                }
                args = args.Next;
            }
        }

        public XmlElement ToXml(XmlDocument context)
        {
            string xml = this.Serialize();
            XmlElement te = context.CreateElement("Container");
            te.InnerXml = xml;
            XmlElement node = te.SelectSingleNode("*") as XmlElement;
            return node;
        }

        public virtual string Serialize()
        {
            using (StringWriter sw = new StringWriter())
            {
                XmlTextWriter w = new XmlTextWriter(sw);
                w.Formatting = Formatting.Indented;
                Serialize(w);
                w.Close();
                return sw.ToString();
            }
        }

        public virtual void Serialize(XmlWriter w)
        {
            DataContractSerializer xs = new DataContractSerializer(this.GetType());
            xs.WriteObject(w, this);
        }


        protected static string Truncate(string s, int length)
        {
            if (s == null) return s;
            if (s.Length > length)
            {
                return s.Substring(0, length);
            }
            return s;
        }
    }

    //================================================================================
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public partial class MyMoney : PersistentObject
    {
        private Accounts accounts;
        private OnlineAccounts onlineAccounts;
        private Payees payees;
        private Aliases aliases;
        private Currencies currencies;
        private Categories categories;
        private Transactions transactions;
        private Securities securities;
        private StockSplits stockSplits;
        private RentBuildings buildings;


        public MyMoney()
        {
            Accounts = new Accounts(this);
            OnlineAccounts = new OnlineAccounts(this);
            Payees = new Payees(this);
            Aliases = new Aliases(this);
            Categories = new Categories(this);
            Currencies = new Currencies(this);
            Transactions = new Transactions(this);
            Securities = new Securities(this);
            StockSplits = new StockSplits(this);
            Buildings = new RentBuildings(this);
            LoanPayments = new LoanPayments(this);

            EventHandler<ChangeEventArgs> handler = new EventHandler<ChangeEventArgs>(OnChanged);
            Accounts.Changed += handler;
            OnlineAccounts.Changed += handler;
            Payees.Changed += handler;
            Categories.Changed += handler;
            Currencies.Changed += handler;
            Transactions.Changed += handler;
            Securities.Changed += handler;
            Buildings.Changed += handler;
        }

        [DataMember]
        public Accounts Accounts
        {
            get { return accounts; }
            set { accounts = value; accounts.Parent = this; }
        }

        [DataMember]
        public OnlineAccounts OnlineAccounts
        {
            get { return onlineAccounts; }
            set { onlineAccounts = value; onlineAccounts.Parent = this; }
        }

        [DataMember]
        public Payees Payees
        {
            get { return payees; }
            set { payees = value; payees.Parent = this; }
        }

        [DataMember]
        public Aliases Aliases
        {
            get { return aliases; }
            set { aliases = value; aliases.Parent = this; }
        }

        [DataMember]
        public Categories Categories
        {
            get { return categories; }
            set { categories = value; categories.Parent = this; }
        }

        [DataMember]
        public Currencies Currencies
        {
            get { return currencies; }
            set { currencies = value; currencies.Parent = this; }
        }

        [DataMember]
        public Transactions Transactions
        {
            get { return transactions; }
            set { transactions = value; transactions.Parent = this; }
        }

        [DataMember]
        public Securities Securities
        {
            get { return securities; }
            set { securities = value; securities.Parent = this; }
        }


        [DataMember]
        public StockSplits StockSplits
        {
            get { return stockSplits; }
            set { stockSplits = value; stockSplits.Parent = this; }
        }

        [DataMember]
        public RentBuildings Buildings
        {
            get { return buildings; }
            set { buildings = value; buildings.Parent = this; }
        }

        private EventHandlerCollection<ChangeEventArgs> balanceHandlers;

        public event EventHandler<ChangeEventArgs> Rebalanced
        {
            add
            {
                if (balanceHandlers == null)
                {
                    balanceHandlers = new EventHandlerCollection<ChangeEventArgs>();
                }
                balanceHandlers.AddHandler(value);
            }
            remove
            {
                if (balanceHandlers != null)
                {
                    balanceHandlers.RemoveHandler(value);
                }
            }
        }


        internal void OnChanged(object sender, ChangeEventArgs e)
        {
            FireChangeEvent(sender, e);
        }

        public void Clear()
        {
            this.Accounts.Clear();
            this.OnlineAccounts.Clear();
            this.Payees.Clear();
            this.Categories.Clear();
            this.Currencies.Clear();
            this.Transactions.Clear();
            this.Securities.Clear();
            this.Buildings.Clear();
        }

        // Make sure securities are unique by Symbol
        public void CheckSecurities()
        {
            Hashtable<string, Security> map = new Hashtable<string, Security>();
            foreach (Security s in this.Securities.GetSecurities())
            {
                if (string.IsNullOrEmpty(s.Symbol)) continue;

                if (map.ContainsKey(s.Symbol))
                {
                    Security original = map[s.Symbol];
                    // found a duplicate!
                    // So remove it and fix up all transactions to point to the original
                    foreach (Transaction t in Transactions.GetAllTransactions())
                    {
                        Investment investment = t.Investment;
                        if (investment != null)
                        {
                            if (investment.Security == s)
                            {
                                investment.Security = original;
                            }
                        }
                    }
                    s.OnDelete();
                }
                else
                {
                    map[s.Symbol] = s;
                }
            }

            // make sure we don't have any dangling stock splits
            foreach (StockSplit split in this.StockSplits)
            {
                if (split.Security == null)
                {
                    split.OnDelete();
                }
            }
        }

        public void CheckCategoryFunds()
        {
            foreach (Account a in this.Accounts.GetCategoryFunds())
            {
                // ensure category is initialized.
                a.GetFundCategory();
            }
        }

        public void Save(IDatabase database)
        {
            BeginUpdate(this);
            RemoveUnusedPayees();
            RemoveUnusedOnlineAccounts();

            database.Save(this);

            EndUpdate();
        }

        private void RemoveUnusedOnlineAccounts()
        {
            HashSet<OnlineAccount> used = new HashSet<OnlineAccount>();
            foreach (Account a in this.Accounts.GetAccounts())
            {
                if (a.OnlineAccount != null)
                {
                    used.Add(a.OnlineAccount);
                }
            }
            List<OnlineAccount> toRemove = new List<OnlineAccount>();
            foreach (OnlineAccount oa in OnlineAccounts.GetOnlineAccounts())
            {
                if (!used.Contains(oa))
                {
                    toRemove.Add(oa);
                }
            }
            foreach (OnlineAccount oa in toRemove)
            {
                OnlineAccounts.Remove(oa);
            }
        }

        /// <summary>
        /// Get list of all Payees that are actually referenced from a Transaction or a Split.
        /// </summary>
        /// <returns></returns>
        internal HashSet<Payee> GetUsedPayees()
        {
            HashSet<Payee> used = new HashSet<Payee>();
            foreach (Transaction t in Transactions.GetAllTransactions())
            {
                Payee p = t.Payee;
                if (p != null)
                {
                    used.Add(p);
                }
                if (t.IsSplit)
                {
                    foreach (Split s in t.Splits)
                    {
                        p = s.Payee;
                        if (p != null)
                        {
                            used.Add(p);
                        }
                    }
                }
            }
            return used;
        }

        public void RemoveUnusedPayees()
        {
            HashSet<Payee> used = GetUsedPayees();

            // make sure payee is not being used by an Alias.
            Dictionary<string, Alias> payeesReferencedByAliases = new Dictionary<string, Alias>();
            foreach (Alias a in Aliases.GetAliases())
            {
                payeesReferencedByAliases[a.Payee.Name] = a;
            }

            List<Payee> toRemove = new List<Payee>();
            foreach (Payee p in Payees.GetPayees())
            {
                if (!used.Contains(p))
                {
                    if (payeesReferencedByAliases.ContainsKey(p.Name))
                    {
                        continue; // need to keep this one then.
                    }
                    toRemove.Add(p);
                }
            }

            foreach (Payee p in toRemove)
            {
                Payees.RemovePayee(p);
            }
        }

        public List<Security> GetUnusedSecurities()
        {
            HashSet<Security> used = new HashSet<Security>();
            foreach (Transaction t in Transactions.GetAllTransactions())
            {
                if (t.Investment != null && t.Investment.Security != null)
                {
                    used.Add(t.Investment.Security);
                }
            }
            List<Security> unused = new List<Security>();
            foreach (Security s in Securities.GetSecurities())
            {
                if (!used.Contains(s))
                {
                    unused.Add(s);
                }
            }
            return unused;
        }

        public List<Security> GetUsedSecurities(AccountFilterPredicate pred)
        {
            HashSet<Security> used = new HashSet<Security>();
            foreach (Transaction t in Transactions.GetAllTransactions())
            {
                if (t.Investment != null && t.Investment.Security != null && pred(t.Account))
                {
                    used.Add(t.Investment.Security);
                }
            }
            List<Security> result = new List<Security>();
            foreach (Security s in Securities.GetSecurities())
            {
                if (used.Contains(s))
                {
                    result.Add(s);
                }
            }
            return result;
        }

        public void RemoveUnusedSecurities()
        {
            foreach (Security s in GetUnusedSecurities())
            {
                Securities.RemoveSecurity(s);
            }
        }

        public static void EnsureInvestmentAccount(Account a, string line, int lineNumber)
        {
            if (a.Type != AccountType.Brokerage && a.Type != AccountType.Retirement)
            {
                throw new MoneyException(String.Format("Received information for investment account on line {0}: {1}, but account {2} is of type {3}",
                    line, lineNumber, a.Name, a.Type.ToString()));
            }
        }

        public List<Transaction> CheckTransfers()
        {
            List<Transaction> dangling = new List<Transaction>();
            List<Account> deletedaccounts = new List<Account>();
            this.Transactions.CheckTransfers(this, dangling, deletedaccounts);
            foreach (Account a in deletedaccounts)
            {
                this.Accounts.RemoveAccount(a);
            }
            return dangling;
        }

        public void Rebalance(Account a)
        {
            CostBasisCalculator calculator = new CostBasisCalculator(this, DateTime.Now);
            Rebalance(calculator, a);
        }

        public void Rebalance(CostBasisCalculator calculator, Account a)
        {
            if (this.Transactions.Rebalance(calculator, a) && this.balanceHandlers != null && this.balanceHandlers.HasListeners)
            {
                var args = new ChangeEventArgs(a, "Balance", ChangeType.Rebalanced);
                this.balanceHandlers.RaiseEvent(this, args);
            }
        }

        public void Rebalance(RentBuilding rental)
        {
            if (this.balanceHandlers != null && this.balanceHandlers.HasListeners)
            {
                var args = new ChangeEventArgs(rental, "Balance", ChangeType.Rebalanced);
                this.balanceHandlers.RaiseEvent(this, args);
            }
        }

        public void RebalanceInvestments()
        {
            CostBasisCalculator calculator = new CostBasisCalculator(this, DateTime.Now);

            foreach (Account a in this.Accounts.GetAccounts())
            {
                if (a.Type == AccountType.Brokerage || a.Type == AccountType.Retirement)
                {
                    Rebalance(calculator, a);
                }
            }
        }

        HashSet<Account> balancePending = new HashSet<Account>();

        public override void EndUpdate()
        {
            base.EndUpdate();

            if (!this.IsUpdating)
            {
                ApplyPendingBalance();
            }
        }

        private void ApplyPendingBalance()
        {
            bool changed = false;
            if (balancePending != null && balancePending.Count > 0)
            {
                CostBasisCalculator calculator = new CostBasisCalculator(this, DateTime.Now);
                this.Transactions.BeginUpdate(false);
                try
                {
                    foreach (Account account in new List<Account>(balancePending))
                    {
                        changed = this.Transactions.Rebalance(calculator, account);
                        if (changed && this.balanceHandlers != null && this.balanceHandlers.HasListeners)
                        {
                            this.balanceHandlers.RaiseEvent(this, new ChangeEventArgs(account, "Balance", ChangeType.Rebalanced));
                        }
                    }
                }
                finally
                {
                    this.Transactions.EndUpdate();
                }
                balancePending.Clear();
            }

        }

        public bool Rebalance(Transaction t)
        {
            bool changed = false;
            if (this.IsUpdating)
            {
                balancePending.Add(t.Account);
                if (t.Transfer != null)
                {
                    Transaction u = t.Transfer.Transaction;
                    balancePending.Add(u.Account);
                }
            }
            else
            {
                this.Transactions.BeginUpdate(false);
                try
                {
                    CostBasisCalculator calculator = new CostBasisCalculator(this, DateTime.Now);
                    changed = this.Transactions.Rebalance(calculator, t.Account);
                    if (t.Transfer != null)
                    {
                        Transaction u = t.Transfer.Transaction;
                        changed |= this.Transactions.Rebalance(calculator, u.Account);
                    }
                }
                finally
                {
                    this.Transactions.EndUpdate();
                }
                if (changed && this.balanceHandlers != null && this.balanceHandlers.HasListeners)
                {
                    this.balanceHandlers.RaiseEvent(this, new ChangeEventArgs(t.Account, "Balance", ChangeType.Rebalanced));
                }
            }
            return changed;
        }

        public decimal ReconciledBalance(Account a, DateTime statementDate)
        {
            return this.Transactions.ReconciledBalance(a, statementDate);
        }

        public decimal EstimatedBalance(Account a, DateTime est)
        {
            return this.Transactions.EstimatedBalance(a, est);
        }

        public bool RemoveTransaction(Transaction t)
        {
            if (t.Status == TransactionStatus.Reconciled && t.Amount != 0)
            {
                throw new MoneyException("Cannot removed reconciled transaction");
            }
            RemoveTransfer(t);

            this.Transactions.RemoveTransaction(t);
            if (t.Unaccepted)
            {
                if (t.Account != null)
                {
                    t.Account.Unaccepted--;
                }
                if (t.Payee != null)
                {
                    t.Payee.UnacceptedTransactions--;
                }
            }

            if (t.Category == null && t.Transfer == null && !t.IsSplit)
            {
                if (t.Payee != null)
                {
                    t.Payee.UncategorizedTransactions--;
                }
            }

            Rebalance(t);
            return true;
        }

        public void ClearTransferToAccount(Transaction t, Account a)
        {
            if (t.IsSplit)
            {
                foreach (Split s in t.Splits.GetSplits())
                {
                    if (s.Transfer != null && s.Transfer.Transaction.Account == a)
                    {
                        ClearTransferToAccount(s.Transfer);
                        s.ClearTransfer();
                        s.Category = s.Amount < 0 ? Categories.TransferToDeletedAccount :
                                Categories.TransferFromDeletedAccount;
                        if (string.IsNullOrEmpty(s.Memo))
                        {
                            s.Memo = a.Name;
                        }
                    }
                }
            }

            if (t.Transfer != null && t.Transfer.Transaction.Account == a)
            {
                ClearTransferToAccount(t.Transfer);
                t.Transfer = null;
                if (!t.IsSplit)
                {
                    t.Category = t.Amount < 0 ? Categories.TransferToDeletedAccount :
                                Categories.TransferFromDeletedAccount;
                }
                if (string.IsNullOrEmpty(t.Memo))
                {
                    t.Memo = a.Name;
                }
            }
        }

        public void ClearTransferToAccount(Transfer t)
        {
            if (t.Split != null)
            {
                Split s = t.Split;
                s.ClearTransfer();
                s.Payee = this.Payees.Transfer;
            }
            else
            {
                Transaction u = t.Transaction;
                u.Payee = this.Payees.Transfer;
                u.Transfer = null;
            }
        }

        // for re-entrancy guard.
        HashSet<Transfer> removing = new HashSet<Transfer>();

        public bool RemoveTransfer(Transfer t)
        {
            if (t != null)
            {
                if (removing == null)
                {
                    // bugbug: is this a C# bug???
                    removing = new HashSet<Transfer>();
                }
                if (!removing.Contains(t))
                {
                    removing.Add(t);

                    Transaction source = t.Owner;
                    Transaction target = t.Transaction;
                    bool sourceIsBeingDeleted = source.Account.IsDeleted;
                    try
                    {
                        if (t.Split != null)
                        {
                            // the target of this transfer is a split.
                            Split s = t.Split;
                            Transfer other = s.Transfer;
                            if (other != null && other.Transaction == source)
                            {
                                if (sourceIsBeingDeleted)
                                {
                                    s.ClearTransfer();
                                    s.Payee = null;
                                    s.Category = s.Amount < 0 ? Categories.TransferToDeletedAccount :
                                        Categories.TransferFromDeletedAccount;
                                }
                                else
                                {
                                    // todo: split is now incomplete...
                                    s.ClearTransfer(); // break circularity
                                    target.NonNullSplits.RemoveSplit(s);
                                }
                            }
                            else
                            {
                                // then this was a dangling transfer
                            }
                        }
                        else if (target.Transfer != null && target.Transfer.Transaction == source)
                        {
                            removing.Add(target.Transfer);
                            if (sourceIsBeingDeleted)
                            {
                                target.Transfer = null;
                                target.Payee = Payees.Transfer;
                                target.Category = target.Amount < 0 ? Categories.TransferToDeletedAccount :
                                        Categories.TransferFromDeletedAccount;
                            }
                            else
                            {
                                if (target.Status == TransactionStatus.Reconciled)
                                {
                                    throw new MoneyException("Transfer is reconciled on the other side and cannot be modified outside of balancing the target account.");
                                }
                                target.Transfer = null; // break circular reference.
                                this.RemoveTransaction(target);
                            }
                        }
                        else
                        {
                            // then this was a dangling transfer!
                        }
                    }
                    finally
                    {
                        removing.Remove(t);
                        removing.Remove(t.Transaction.Transfer);
                    }
                    return true;
                }
            }
            return false;
        }


        public void RemoveTransfer(Transaction t)
        {
            if (t.Transfer != null)
            {
                if (RemoveTransfer(t.Transfer))
                {
                    t.Transfer = null;
                }
            }
            //if (t.IsSplit)
            //{
            //    foreach (Split s in t.Splits.Items)
            //    {
            //        this.RemoveTransfer(s);
            //    }
            //}
        }

        public void RemoveTransfer(Split s)
        {
            if (s.Transfer != null)
            {
                if (RemoveTransfer(s.Transfer))
                {
                    s.Transfer = null;
                }
            }
        }

        public void Transfer(Transaction t, Account to)
        {
            if (t.Account == to)
            {
                throw new MoneyException("Cannot transfer to same account");
            }

            RemoveTransfer(t.Transfer);

            Transaction u = this.Transactions.NewTransaction(to);
            u.Amount = this.Currencies.GetTransferAmount(-t.Amount, t.Account.Currency, to.Currency);
            u.Category = t.Category;
            u.Date = t.Date;
            u.FITID = t.FITID;
            u.Number = t.Number;
            u.Memo = t.Memo;
            //u.Status = t.Status; no !!!

            Investment i = t.Investment;
            if (i != null)
            {
                Investment j = u.GetOrCreateInvestment();
                j.Units = i.Units;
                j.UnitPrice = i.UnitPrice;
                j.Security = i.Security;
                switch (i.Type)
                {
                    case InvestmentType.Add:
                        j.Type = InvestmentType.Remove;
                        break;
                    case InvestmentType.Remove:
                        j.Type = InvestmentType.Add;
                        break;
                    case InvestmentType.None: // assume it's a remove
                        i.Type = InvestmentType.Remove;
                        j.Type = InvestmentType.Add;
                        break;
                    case InvestmentType.Buy:
                    case InvestmentType.Sell:
                        throw new MoneyException("Transfer must be of type 'Add' or 'Remove'.");
                }
                u.Investment = j;
            }
            // must have a valid transaction id before we assign the transfers.
            this.Transactions.AddTransaction(u);

            u.Transfer = new Transfer(0, u, t);
            t.Transfer = new Transfer(0, t, u);
        }

        public void Transfer(Split s, Account to)
        {
            Transaction t = s.Transaction;
            if (t.Account == to)
            {
                throw new MoneyException("Cannot transfer to same account");
            }

            if (t.Transfer != null && t.Transfer.Split != null)
            {
                throw new MoneyException("This transaction is already the target of a split transfer.\n" +
                                         "MyMoney doesn't support splits being on both sides of a transfer\n" +
                                         "So if you really want to add a transfer to ths Splits in this transaction\n" +
                                         "then please remove the split transfer that is pointing to this transaction"
                            );
            }

            // try the remove transfer first, because it will throw if the other side is reconciled.
            RemoveTransfer(t.Transfer);
            t.Transfer = null;
            RemoveTransfer(s.Transfer);
            s.Transfer = null;

            Transaction u = this.Transactions.NewTransaction(to);
            u.Date = t.Date;
            u.FITID = t.FITID;
            u.Investment = t.Investment;
            u.Number = t.Number;
            u.Memo = s.Memo;
            u.Category = s.Category;
            //u.Status = t.Status; // no !!!
            u.Amount = this.Currencies.GetTransferAmount(-s.Amount, t.Account.Currency, to.Currency);
            u.Transfer = new Transfer(0, u, t, s);
            s.Transfer = new Transfer(0, t, s, u);
            this.Transactions.AddTransaction(u);
            Rebalance(to);
        }

        public event EventHandler<TransferChangedEventArgs> BeforeTransferChanged;

        bool ignoreOnTransferChanged;

        internal void OnTransferChanged(Transaction transaction, Transfer value)
        {
            if (ignoreOnTransferChanged)
            {
                return;
            }
            if (BeforeTransferChanged != null)
            {
                BeforeTransferChanged(this, new TransferChangedEventArgs(transaction, value));
            }
            if (transaction.Transfer != null && transaction.Transfer != value)
            {
                ignoreOnTransferChanged = true;
                try
                {
                    RemoveTransfer(transaction);
                }
                finally
                {
                    ignoreOnTransferChanged = false;
                }
            }
        }

        public event EventHandler<SplitTransferChangedEventArgs> BeforeSplitTransferChanged;

        bool ignoreOnSplitTransferChanged;

        internal void OnSplitTransferChanged(Split split, Transfer value)
        {
            if (ignoreOnSplitTransferChanged)
            {
                return;
            }
            if (BeforeSplitTransferChanged != null)
            {
                BeforeSplitTransferChanged(this, new SplitTransferChangedEventArgs(split, value));
            }
            if (split.Transfer != null && split.Transfer != value)
            {
                ignoreOnSplitTransferChanged = true;
                try
                {
                    RemoveTransfer(split);
                }
                finally
                {
                    ignoreOnSplitTransferChanged = false;
                }
            }
        }

        public void Categorize(Transaction t, Category cat)
        {
            if (cat != this.Categories.Split && t.IsSplit)
            {
                t.Splits.RemoveAll();
            }
            t.Category = cat;
        }

        static public void Categorize(Split s, Category cat)
        {
            s.Category = cat;
        }

        public void CopyCategory(Transaction from, Transaction to)
        {
            if (from.Transfer != null)
            {
                if (to.Transfer == null)
                {
                    if (from.Transfer.Split != null)
                    {
                        // throw new MoneyException("Don't know how to copy a transfer to a split");
                        return;
                    }
                    this.Transfer(to, from.Transfer.Transaction.Account);
                }
            }
            else if (from.IsSplit)
            {
                // copy the splits
                string xml = from.Splits.Serialize();
                to.NonNullSplits.DeserializeInto(this, xml);
            }
            to.Category = from.Category;
        }

        public Transaction FindPreviousTransactionByPayee(Transaction t, string payeeOrTransferCaption)
        {
            Transaction found = null;
            Account a = t.Account;
            if (a != null)
            {
                found = FindPreviousTransactionByPayee(a, t, payeeOrTransferCaption);
                if (found == null)
                {
                    // try other accounts;
                    foreach (Account other in this.Accounts.GetAccounts())
                    {
                        if (other != a)
                        {
                            found = FindPreviousTransactionByPayee(other, t, payeeOrTransferCaption);
                            if (found != null)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            return found;
        }

        Transaction FindPreviousTransactionByPayee(Account a, Transaction t, string payeeOrTransferCaption)
        {
            int ucount = 0;
            Transaction u = FindPreviousTransactionByPayee(a, t, payeeOrTransferCaption, /*searchSplits*/ false, out ucount);
            int splitcount = 0;
            Transaction v = FindPreviousTransactionByPayee(a, t, payeeOrTransferCaption, /*searchSplits*/ true, out splitcount);

            if (splitcount == 0 || splitcount < ucount / 2)
            {
                return u;
            }
            else
            {
                return v;
            }
        }

        Transaction FindPreviousTransactionByPayee(Account a, Transaction t, string payeeOrTransferCaption, bool searchSplits, out int count)
        {
            IList<Transaction> list = this.Transactions.GetTransactionsFrom(a);
            int len = list.Count;

            if (len == 0)
            {
                // Nothing to do here
                count = 0;
                return null;
            }

            // Tally of how close the current transaction is to the amounts for a given category.
            CategoryProbabilies<Transaction> probabilities = new CategoryProbabilies<Transaction>();

            Transaction closestByDate = null;
            long ticks = 0;

            decimal amount = t.Amount;
            for (int i = 0; i < len; i++)
            {
                Transaction u = list[i] as Transaction;
                if (searchSplits == u.IsSplit)
                {
                    u = AddPossibility(t, u, payeeOrTransferCaption, probabilities);
                    if (u != null && amount == 0)
                    {
                        // we can't use the probabilities when the amount is zero, so we just return
                        // the closest transaction by date because in the case of something like a paycheck
                        // the most recent paycheck usually has the closed numbers on the splits.
                        long newTicks = Math.Abs((u.Date - t.Date).Ticks);
                        if (closestByDate == null || newTicks < ticks)
                        {
                            closestByDate = u;
                            ticks = newTicks;
                        }
                    }
                }

            }

            if (closestByDate != null)
            {
                count = 1;
                return closestByDate;
            }

            count = probabilities.Count;
            return probabilities.GetBestMatch(t.Amount);
        }

        private Transaction AddPossibility(Transaction t, Transaction u, string payeeOrTransferCaption, CategoryProbabilies<Transaction> probabilities)
        {
            if (u != t && u.Category != null && u.Payee != null && string.Compare(u.PayeeOrTransferCaption, payeeOrTransferCaption, true) == 0)
            {
                probabilities.Add(u, u.Category.GetFullName(), u.Amount);
                return u;
            }
            return null;
        }

        public IEnumerable<PersistentObject> FindAliasMatches(Alias alias, IEnumerable<Transaction> transactions)
        {
            Payee np = alias.Payee;
            foreach (Transaction t in transactions)
            {
                Payee p = t.Payee;
                if (p != null && np != p &&
                    t.Transfer == null) // should not be changing transfers this way!
                {
                    string name = p.Name;
                    if (alias.Matches(name))
                    {
                        yield return t;
                    }
                }
                if (t.IsSplit)
                {
                    foreach (Split s in t.Splits)
                    {
                        Payee sp = s.Payee;
                        if (sp != null & sp != np &&
                            s.Transfer == null) // don't mess with transfers.
                        {
                            string name = sp.Name;
                            if (alias.Matches(name))
                            {
                                yield return s;
                            }
                        }
                    }
                }
            }
        }

        public int ApplyAlias(Alias alias, IEnumerable<Transaction> transactions)
        {
            int count = 0;
            Payee np = alias.Payee;
            this.Transactions.BeginUpdate(true);
            try
            {
                foreach (PersistentObject po in FindAliasMatches(alias, transactions))
                {
                    Transaction t = po as Transaction;
                    Split s = po as Split;
                    if (t != null)
                    {
                        t.Payee = np;
                        count++;
                    }
                    else if (s != null)
                    {
                        Payee sp = s.Payee;
                        s.Payee = np;
                        count++;
                    }
                }
            }
            finally
            {
                this.Transactions.EndUpdate();
            }
            return count;
        }


        public event EventHandler<ErrorEventArgs> ErrorLog;

        public void LogError(Exception e)
        {
            if (ErrorLog != null)
            {
                ErrorLog(this, new ErrorEventArgs(e));
            }
        }

        #region CostBasis

        /// <summary>
        /// Get a list of all Investment transactions grouped by security
        /// </summary>
        /// <param name="filter">The account filter or null if you want them all</param>
        /// <param name="toDate">Get all transactions up to but not including this date</param>
        /// <returns></returns>
        public IDictionary<Security, List<Investment>> GetTransactionsGroupedBySecurity(Predicate<Account> filter, DateTime toDate)
        {
            SortedDictionary<Security, List<Investment>> transactionsBySecurity = new SortedDictionary<Security, List<Investment>>(new SecurityComparer());

            // Sort all add, remove, buy, sell transactions by date and by security.
            foreach (Transaction t in Transactions.GetAllTransactionsByDate())
            {
                if (t.Date < toDate && (filter == null || filter(t.Account)) &&
                    t.Investment != null && t.Investment.Type != InvestmentType.None)
                {
                    Investment i = t.Investment;
                    Security s = i.Security;
                    if (s != null)
                    {
                        List<Investment> list = null;
                        if (!transactionsBySecurity.TryGetValue(s, out list))
                        {
                            list = new List<Investment>();
                            transactionsBySecurity[s] = list;
                        }
                        list.Add(i);
                    }
                }
            }
            return transactionsBySecurity;
        }

        /// <summary>
        /// Given a list of transactions against the same security, this method will compute
        /// the CurrentUnits and CurrentUnitPrice fields, adjusting for any stock splits that
        /// have happened in the date range of the transactions.
        /// </summary>
        /// <param name="transactions">The list of transactions, must all have the same Security</param>
        public void ApplyStockSplits(List<Investment> transactions)
        {
            Security security = null;
            IList<StockSplit> splits = null;

            foreach (Investment t in transactions)
            {
                if (splits == null)
                {
                    security = t.Transaction.Investment.Security;
                    splits = this.StockSplits.GetStockSplitsForSecurity(security);
                }
                else
                {
                    Debug.Assert(t.Transaction.Investment.Security.Name == security.Name);
                }
                t.ResetCostBasis();

                // Note: ApplySplit is commutative, so we can apply them in any order.
                foreach (StockSplit s in splits)
                {
                    t.ApplySplit(s);
                }

            }

        }


        #endregion

        internal int RemoveDuplicateSecurities()
        {
            List<Security> toRemove = new List<Security>();
            Hashtable<string, Security> index = new Hashtable<string, Security>();

            foreach (Security s in this.Securities)
            {
                string name = s.Name;
                if (string.IsNullOrEmpty(name))
                {
                }
                else
                {
                    Security s2;
                    if (index.TryGetValue(s.Name, out s2))
                    {
                        // merge...
                        if (s.Merge(s2))
                        {
                            toRemove.Add(s2);
                        }
                    }
                    else
                    {
                        index[s.Name] = s;
                    }
                }
            }

            foreach (Security s in toRemove)
            {
                Security s2;
                if (index.TryGetValue(s.Name, out s2))
                {
                    foreach (Transaction t in this.Transactions)
                    {
                        Investment i = t.Investment;
                        if (i != null && i.Security == s)
                        {
                            i.Security = s2;
                        }
                    }
                }
                this.Securities.RemoveSecurity(s);
            }
            return toRemove.Count;
        }

        internal int RemoveDuplicatePayees()
        {
            List<Payee> toRemove = new List<Payee>();
            Hashtable<string, Payee> index = new Hashtable<string, Payee>();

            foreach (Payee p in this.Payees)
            {
                string name = p.Name;
                if (string.IsNullOrEmpty(name))
                {
                    // ignore
                }
                else
                {
                    Payee existing;
                    if (index.TryGetValue(p.Name, out existing))
                    {
                        existing.Merge(p);
                        toRemove.Add(p);
                    }
                    else
                    {
                        index[p.Name] = p;
                    }
                }
            }

            foreach (Payee p in toRemove)
            {
                Payee original;
                if (index.TryGetValue(p.Name, out original))
                {
                    foreach (Transaction t in this.Transactions)
                    {
                        if (t.Payee == p)
                        {
                            t.Payee = original;
                        }
                        if (t.IsSplit)
                        {
                            foreach (Split s in t.Splits)
                            {
                                if (s.Payee == p)
                                {
                                    s.Payee = original;
                                }
                            }
                        }
                    }

                    foreach (Alias a in this.Aliases)
                    {
                        if (a.Payee == p)
                        {
                            a.Payee = original;
                        }

                    }
                }

                this.Payees.RemovePayee(p);
            }

            return toRemove.Count;
        }


        /// <summary>
        /// Add all the parent pointers we need for eventing to work properly.
        /// </summary>
        internal void PostDeserializeFixup()
        {
            this.BeginUpdate(this);

            // rebuild category hierarchy
            this.Categories.FixParents();

            foreach (Account a in this.Accounts)
            {
                a.PostDeserializeFixup(this);
            }

            foreach (Alias a in this.Aliases)
            {
                a.PostDeserializeFixup(this);
            }

            foreach (Transaction t in this.Transactions)
            {
                Account a = this.Accounts.FindAccount(t.AccountName);
                t.PostDeserializeFixup(this, this.Transactions, a, false);
            }

            CostBasisCalculator calculator = new CostBasisCalculator(this, DateTime.Now);

            // Now we can rebalance the accounts.
            foreach (Account a in this.Accounts)
            {
                this.Rebalance(calculator, a);
            }
            MarkAllUpToDate();

            this.EndUpdate();
        }

        private void MarkAllUpToDate()
        {
            this.BeginUpdate(this);

            // Mark all objects as up to date.
            foreach (Account a in this.Accounts) { a.OnUpdated(); }
            foreach (Transaction t in this.Transactions)
            {
                t.OnUpdated();
                if (t.IsSplit)
                {
                    foreach (Split s in t.Splits)
                    {
                        s.OnUpdated();
                    }
                }
            }
            foreach (Category c in this.Categories) { c.OnUpdated(); }
            foreach (Payee p in this.Payees) { p.OnUpdated(); }
            foreach (Alias a in this.Aliases) { a.OnUpdated(); }
            foreach (Security s in this.Securities) { s.OnUpdated(); }
            foreach (Currency c in this.Currencies) { c.OnUpdated(); }
            foreach (StockSplit s in this.StockSplits) { s.OnUpdated(); }
            foreach (OnlineAccount a in this.OnlineAccounts) { a.OnUpdated(); }
            foreach (RentBuilding b in this.Buildings) { b.OnUpdated(); }
            foreach (LoanPayment l in this.LoanPayments) { l.OnUpdated(); }
            this.EndUpdate();
        }

        internal void MarkAllNew()
        {
            this.BeginUpdate(this);

            // Mark all objects as being new (needing to be saved).
            this.OnlineAccounts.MarkAllNew();
            this.Accounts.MarkAllNew();
            this.Payees.MarkAllNew();
            this.Aliases.MarkAllNew();
            this.Categories.MarkAllNew();
            this.Securities.MarkAllNew();
            this.StockSplits.MarkAllNew();
            this.Buildings.MarkAllNew();
            this.Buildings.Units.MarkAllNew();
            this.LoanPayments.MarkAllNew();
            this.Transactions.MarkAllNew();
            this.EndUpdate();
        }

        /// <summary>
        /// Return list of securities that are currently still owned.
        /// </summary>
        /// <returns></returns>
        internal List<Security> GetOwnedSecurities()
        {
            HashSet<Security> unique = new HashSet<Security>();

            CostBasisCalculator calc = new CostBasisCalculator(this, DateTime.Now);

            foreach (var pair in calc.GetHoldingsBySecurityType(null))
            {
                foreach (SecurityPurchase sp in pair.Value)
                {
                    unique.Add(sp.Security);
                }
            }

            List<Security> sorted = new List<Security>(unique);
            sorted.Sort(Security.Compare);
            return sorted;
        }

        /// <summary>
        /// Get the cash balance of the given investment account.  If the account is null
        /// it returns the cash balance of all investment accounts.
        /// </summary>
        /// <param name="account">The account or null</param>
        /// <returns>Cash balance</returns>
        public decimal GetInvestmentCashBalance(Predicate<Account> filter)
        {
            decimal cash = 0;
            foreach (Account a in this.Accounts.GetAccounts(false))
            {
                if (filter(a))
                {
                    cash += a.OpeningBalance;
                    foreach (Transaction t in this.Transactions.GetTransactionsFrom(a))
                    {
                        cash += t.Amount;
                    }
                }
            }

            return cash;
        }



        internal void SwitchSecurities(Security fromSecurity, Security moveToSecurity)
        {
            foreach (Transaction t in this.transactions.GetTransactionsBySecurity(fromSecurity, null))
            {
                t.Investment.Security = moveToSecurity;
            }
        }
    }

    public class ErrorEventArgs : EventArgs
    {
        Exception error;

        public Exception Exception { get { return error; } }

        public ErrorEventArgs(Exception error)
        {
            this.error = error;
        }
    }

    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Accounts : PersistentContainer, ICollection<Account>
    {
        int NextAccount = 0;
        Hashtable<int, Account> accounts = new Hashtable<int, Account>(); // id->Account
        Hashtable<string, Account> accountIndex = new Hashtable<string, Account>(); // name->Account

        // for serialization only
        public Accounts()
        {
        }

        public Accounts(PersistentObject parent)
            : base(parent)
        {
        }

        [XmlIgnore]
        public int Count
        {
            get { return this.accounts.Count; }
        }

        public Account GetFirstAccount()
        {
            foreach (Account a in accounts.Values)
            {
                return a;
            }
            return null;
        }

        public override void OnNameChanged(object o, string oldName, string newName)
        {
            if (oldName != null && accountIndex.ContainsKey(oldName))
                accountIndex.Remove(oldName);
            accountIndex[newName] = (Account)o;
        }

        public void Clear()
        {
            if (this.NextAccount != 0 || this.accounts.Count != 0 || this.accountIndex.Count != 0)
            {
                NextAccount = 0;
                this.accounts = new Hashtable<int, Account>();
                this.accountIndex = new Hashtable<string, Account>();
                FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }

        public Account AddAccount(int id)
        {
            Account a = new Account(this);
            a.Id = id;
            if (this.NextAccount <= id) this.NextAccount = id + 1;
            this.accounts[id] = a;
            return a;
        }

        public Account AddAccount(string name)
        {
            Debug.Assert(name != null);
            Account a = FindAccount(name);
            if (a == null)
            {
                a = AddAccount(this.NextAccount);
                a.Name = name;
                FireChangeEvent(this, a, null, ChangeType.Inserted);
            }
            this.accountIndex[name] = a;
            return a;
        }

        public void AddAccount(Account a)
        {
            if (a.Id == -1)
            {
                a.Id = this.NextAccount++;
            }
            else if (this.NextAccount <= a.Id)
            {
                this.NextAccount = a.Id;
            }
            a.OnInserted();
            a.Parent = this;
            this.accounts[a.Id] = a;
            if (!string.IsNullOrWhiteSpace(a.Name))
            {
                this.accountIndex[a.Name] = a;
            }
            FireChangeEvent(this, new ChangeEventArgs(a, null, ChangeType.Inserted));
        }

        public Account FindAccount(string name)
        {
            if (name == null || name.Length == 0) return null;
            return (Account)this.accountIndex[name];
        }

        public Account FindAccountAt(int id)
        {
            return (Account)this.accounts[id];
        }

        public Account FindAccountByAccountId(string accountId)
        {
            foreach (Account a in accounts.Values)
            {
                if (a.AccountId == accountId)
                {
                    return a;
                }
            }
            return null;
        }

        const string CategoryAccountPrefix = "Category: ";

        public Account FindCategoryFund(Category c)
        {
            Debug.Assert(c != null);
            Account a = FindAccount(CategoryAccountPrefix + c.Name);
            if (a != null && a.IsCategoryFund)
            {
                return a;
            }
            return null;
        }

        public Account AddCategoryFund(Category c)
        {
            Debug.Assert(c != null);
            string name = CategoryAccountPrefix + c.Name;
            Account a = FindAccount(name);
            if (a != null)
            {
                if (a.Type != AccountType.CategoryFund)
                {
                    throw new MoneyException("Cannot create category account because account with conflicting name already exists");
                }
                return a;
            }

            a = AddAccount(this.NextAccount);
            a.Type = AccountType.CategoryFund;
            a.Flags = AccountFlags.Budgeted;
            a.Name = name;
            this.accountIndex[name] = a;
            FireChangeEvent(this, a, null, ChangeType.Inserted);
            return a;
        }

        public IList<Account> GetCategoryFunds()
        {
            List<Account> list = new List<Account>(this.accounts.Count);
            foreach (Account a in this.accounts.Values)
            {
                if (!a.IsDeleted && a.IsCategoryFund) // category fund accounts are special.
                {
                    list.Add(a);
                }
            }
            list.Sort(new AccountComparer());
            return list;
        }

        public Category GetFundCategory(Account a)
        {
            if (a.IsCategoryFund)
            {
                MyMoney m = this.Parent as MyMoney;
                if (a.Name.StartsWith(CategoryAccountPrefix))
                {
                    string name = a.Name.Substring(CategoryAccountPrefix.Length);
                    return m.Categories.FindCategory(name);
                }

                return null;
            }
            return null;
        }

        public override void Add(object child)
        {
            Add((Category)child);
        }

        public override void RemoveChild(PersistentObject pe)
        {
            RemoveAccount((Account)pe);
        }

        public bool RemoveAccount(Account a)
        {
            if (a.IsInserted)
            {
                if (this.accounts.ContainsKey(a.Id))
                    this.accounts.Remove(a.Id);
                string name = a.Name;
                if (name != null && this.accountIndex.ContainsKey(name))
                    this.accountIndex.Remove(name);
            }
            a.OnDelete();

            MyMoney myMoney = this.Parent as MyMoney;
            myMoney.BeginUpdate(this);

            // Fix up any transfers that are pointing to this account.
            IList<Transaction> view = myMoney.Transactions.FindTransfersToAccount(a);
            if (view.Count != 0)
            {
                foreach (Transaction u in view)
                {
                    myMoney.ClearTransferToAccount(u, a);
                }
            }

            view = myMoney.Transactions.GetTransactionsFrom(a);
            if (view.Count != 0)
            {
                foreach (Transaction t in view)
                {
                    myMoney.Transactions.RemoveTransaction(t);
                    if (t.Unaccepted && t.Account != null)
                    {
                        t.Account.Unaccepted--;
                    }
                }
            }
            myMoney.EndUpdate();
            return true;
        }

        public IList<Account> GetAccounts()
        {
            return GetAccounts(false);
        }

        public IList<Account> GetAccounts(bool filterOutClosed)
        {
            // we have to clone the array so that when it is
            // sorted we don't mess up our copy of the list 
            // since the position in the Accounts list is
            // the account id referenced from the Transaction
            // when the data is persisted.  Same goes for the
            // Payees and Categories.  We also filter out deleted
            // accounts.
            List<Account> list = new List<Account>(this.accounts.Count);
            foreach (Account a in this.accounts.Values)
            {
                if (!a.IsDeleted && (!filterOutClosed || !a.IsClosed) &&
                    a.Type != AccountType.CategoryFund) // category fund accounts are special.
                {
                    list.Add(a);
                }
            }
            list.Sort(new AccountComparer());
            return list;
        }

        [XmlIgnore]
        public Collection<Account> AllAccounts
        {
            get
            {
                Collection<Account> l = new Collection<Account>();
                foreach (Account a in GetAccounts())
                {
                    l.Add(a);
                }
                return l;
            }
        }

        #region ICollection

        public void Add(Account item)
        {
            AddAccount(item);
        }

        public bool Contains(Account item)
        {
            return this.accounts.ContainsKey(item.Id);
        }

        public void CopyTo(Account[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(Account item)
        {
            return RemoveAccount(item);
        }

        public new IEnumerator<Account> GetEnumerator()
        {
            foreach (Account a in this.accounts.Values)
            {
                yield return a;
            }
        }

        protected override IEnumerator<PersistentObject> InternalGetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion

    }

    public enum AccountType
    {
        Savings = 0,
        Checking = 1,
        MoneyMarket = 2,
        Cash = 3,
        Credit = 4,
        Brokerage = 5,
        Retirement = 6,
        // There is a hole here from deleted type which we can fill when we invent new types, but the types 8-10 have to keep those numbers        
        // or else we mess up the existing databases.
        Asset = 8,              // Used for tracking Assets like "House, Car, Boat, Jewelry, this helps to make NetWorth more accurate
        CategoryFund = 9,       // a pseudo account for managing category budgets
        Loan = 10,
        CreditLine = 11
    }

    public enum TaxableIncomeType
    {
        All = 0,
        None = 1,
        Gains = 2
    }
    public enum AccountFlags
    {
        None = 0,
        Budgeted = 1,
        Closed = 2,
        TaxDeferred = 4
    }


    public class AccountSectionHeader
    {
        public string Title { get; set; }
        public decimal BalanceInNormalizedCurrencyValue { get; set; }
        public List<Account> Accounts { get; set; }
        public event EventHandler Clicked;
        public void OnClick()
        {
            if (Clicked != null)
            {
                Clicked(this, EventArgs.Empty);
            }
        }
    }

    //================================================================================
    [TableMapping(TableName = "Accounts")]
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Account : PersistentObject
    {
        int id = -1;
        string accountId;
        string ofxAccountId;
        string name;
        string description;
        AccountType type;
        decimal openingBalance;
        decimal balance;
        string currency;
        decimal accountCurrencyRatio;
        int onlineAccountId;
        OnlineAccount onlineAccount;
        string webSite;
        int reconcileWarning;
        DateTime lastSync;
        DateTime lastBalance;
        int unaccepted;
        SqlGuid syncGuid;
        AccountFlags flags;
        Category category; // for category funds.

        Category categoryForPrincipal;  // For Loan accounts
        Category categoryForInterest;   // For Loan accounts

        public Account()
        { // for serialization
        }

        public Account(Accounts container)
            : base(container)
        {
            this.lastSync = DateTime.MinValue;
        }

        [DataMember]
        [XmlAttribute]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true)]
        public int Id
        {
            get { return this.id; }
            set { if (this.id != value) { this.id = value; OnChanged("Id"); } }
        }

        [DataMember]
        [XmlAttribute]
        [ColumnMapping(ColumnName = "AccountId", MaxLength = 20, AllowNulls = true)]
        public string AccountId
        {
            get { return this.accountId; }
            set { if (this.accountId != value) { this.accountId = Truncate(value, 20); OnChanged("AccountId"); } }
        }

        [DataMember]
        [XmlAttribute]
        [ColumnMapping(ColumnName = "OfxAccountId", MaxLength = 50, AllowNulls = true)]
        public string OfxAccountId
        {
            get { return this.ofxAccountId == null ? this.accountId : this.ofxAccountId; }
            set { if (this.ofxAccountId != value) { this.ofxAccountId = Truncate(value, 50); OnChanged("OfxAccountId"); } }
        }

        [XmlIgnore]
        public bool IsClosed
        {
            get { return (this.flags & AccountFlags.Closed) != 0; }
            set
            {
                if (this.IsClosed != value)
                {
                    if (value) flags |= AccountFlags.Closed;
                    else flags = flags & ~AccountFlags.Closed;
                    OnChanged("IsClosed");
                }
            }
        }

        [XmlIgnore]
        public bool IsBudgeted
        {
            get { return (this.flags & AccountFlags.Budgeted) != 0; }
            set
            {
                if (this.IsBudgeted != value)
                {
                    if (value) flags |= AccountFlags.Budgeted;
                    else flags = flags & ~AccountFlags.Budgeted;
                    OnChanged("IsBudgeted");
                }
            }
        }

        [XmlIgnore]
        public bool IsTaxDeferred
        {
            get { return (this.flags & AccountFlags.TaxDeferred) != 0; }
            set
            {
                if (this.IsTaxDeferred != value)
                {
                    if (value) flags |= AccountFlags.TaxDeferred;
                    else flags = flags & ~AccountFlags.TaxDeferred;
                    OnChanged("IsTaxDeferred");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Name", MaxLength = 80)]
        public string Name
        {
            get { return this.name; }
            set
            {
                if (this.name != value)
                {
                    string old = this.name;
                    this.name = Truncate(value, 80);
                    OnChanged("Name");
                    OnNameChanged(old, value);
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Description", MaxLength = 255, AllowNulls = true)]
        public string Description
        {
            get { return this.description; }
            set
            {
                if (this.description != value)
                {
                    this.description = Truncate(value, 255);
                    OnChanged("Description");
                }
            }
        }

        [XmlIgnore]
        [ColumnMapping(ColumnName = "Type")]
        public AccountType Type
        {
            get { return this.type; }
            set { if (this.type != value) { this.type = value; OnChanged("Type"); } }
        }

        [DataMember]
        [XmlAttribute]
        public int SerializedAccountType
        {
            get { return (int)this.type; }
            set { this.Type = (AccountType)value; }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "OpeningBalance", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal OpeningBalance
        {
            get { return this.openingBalance; }
            set { if (this.openingBalance != value) { this.openingBalance = value; OnChanged("OpeningBalance"); } }
        }

        [XmlIgnore]
        public decimal Balance
        {
            get
            {
                if (this.IsClosed)
                {
                    return 0; // can't owe something on a closed account.
                }
                return this.balance;
            }
            set
            {
                if (this.balance != value)
                {
                    this.balance = value;
                    OnTransientChanged("Balance");
                }
            }
        }


        /// <summary>
        /// Return the Balance in USD currency
        /// </summary>
        [XmlIgnore]
        public decimal BalanceNormalized
        {
            get
            {
                MyMoney money = this.Parent.Parent as MyMoney;
                if (money != null)
                {
                    Currency c = money.Currencies.FindCurrency(this.currency);
                    if (c != null)
                    {
                        //-----------------------------------------------------
                        // Apply ratio of conversion
                        // for example USA 2,000 * CAN .95 = 1,900 (in USA currency)
                        return this.Balance * c.Ratio;
                    }

                }
                return this.Balance;
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Currency", MaxLength = 3, AllowNulls = true)]
        public string Currency
        {
            get { return this.currency; }
            set
            {
                if (this.currency != value)
                {
                    this.currency = value; OnChanged("Currency");
                }
            }
        }

        [XmlIgnore]
        public string NonNullCurrency
        {
            get
            {
                if (string.IsNullOrEmpty(this.Currency))
                {
                    return "USD";
                }
                return this.currency;
            }
        }

        [DataMember]
        private int OnlineAccountId
        {
            get { return this.onlineAccountId; }
            set { this.onlineAccountId = value; }
        }

        public decimal AccountCurrencyRatio
        {
            get { return this.accountCurrencyRatio; }
            set { this.accountCurrencyRatio = value; }
        }

        [ColumnObjectMapping(ColumnName = "OnlineAccount", KeyProperty = "Id", AllowNulls = true)]
        public OnlineAccount OnlineAccount
        {
            get { return this.onlineAccount; }
            set
            {
                if (this.onlineAccount != value)
                {
                    onlineAccountId = value == null ? -1 : value.Id;
                    this.onlineAccount = value; OnChanged("OnlineAccount");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "WebSite", MaxLength = 512, AllowNulls = true)]
        public string WebSite
        {
            get { return this.webSite; }
            set { if (this.webSite != value) { this.webSite = value; OnChanged("WebSite"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "ReconcileWarning", AllowNulls = true)]
        public int ReconcileWarning
        {
            get { return this.reconcileWarning; }
            set { if (this.reconcileWarning != value) { this.reconcileWarning = value; OnChanged("ReconcileWarning"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "LastSync", AllowNulls = true)]
        public DateTime LastSync
        {
            get { return this.lastSync; }
            set { if (this.lastSync != value) { this.lastSync = value; OnChanged("LastSync"); } }
        }

        [XmlIgnore]
        public string LastSyncDate
        {
            get { return this.lastSync.ToShortDateString(); }
            set
            {
                DateTime date;
                if (DateTime.TryParse(value, out date))
                {
                    this.LastSync = date;
                }
            }
        }

        // for some stupid reason DataContractSerializer can't serialize null SqlGuid...
        [ColumnMapping(ColumnName = "SyncGuid", AllowNulls = true)]
        public SqlGuid SyncGuid
        {
            get { return this.syncGuid; }
            set
            {
                if (this.syncGuid.ToString() != value.ToString())
                {
                    this.syncGuid = value; OnChanged("SyncGuid");
                }
            }
        }

        [DataMember(Name = "SyncGuid")]
        public string SerializedSyncGuid
        {
            get { return syncGuid.IsNull ? "" : syncGuid.ToString(); }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    this.syncGuid = SqlGuid.Null;
                }
                else
                {
                    this.syncGuid = new SqlGuid(value);
                }
                OnChanged("SyncGuid");
            }
        }

        [ColumnMapping(ColumnName = "Flags", SqlType = typeof(SqlInt32), AllowNulls = true)]
        [XmlIgnore]// for some stupid reason the XmlSerializer can't serialize this field.
        public AccountFlags Flags
        {
            get { return this.flags; }
            set { if (this.flags != value) { this.flags = value; OnChanged("Flags"); } }
        }

        [DataMember]
        public int FlagsAsInt
        {
            get { return (int)this.Flags; }
            set { this.Flags = (AccountFlags)value; }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "LastBalance", AllowNulls = true)]
        public DateTime LastBalance
        {
            get { return this.lastBalance; }
            set { if (this.lastBalance != value) { this.lastBalance = value; OnChanged("LastBalance"); } }
        }

        [DataMember]
        [XmlAttribute]
        public int Unaccepted
        {
            get { return this.unaccepted; }
            set { if (this.unaccepted != value) { this.unaccepted = value; OnChanged("Unaccepted"); } }
        }

        // This is for serialization
        [DataMember]
        public int SerializedFlags
        {
            get { return (int)this.flags; }
            set { if ((int)this.flags != value) { this.flags = (AccountFlags)value; OnChanged("Flags"); } }
        }

        public override string ToString()
        {
            return Name;
        }

        public Account ShallowCopy()
        {
            return (Account)this.MemberwiseClone();
        }

        [XmlIgnore]
        public bool IsCategoryFund { get { return this.Type == AccountType.CategoryFund; } }

        public Category GetFundCategory()
        {
            if (this.category == null)
            {
                Accounts parent = this.Parent as Accounts;
                this.category = parent.GetFundCategory(this);
            }
            return this.category;
        }

        [IgnoreDataMember]
        [ColumnObjectMapping(ColumnName = "CategoryIdForPrincipal", KeyProperty = "Id", AllowNulls = true)]
        public Category CategoryForPrincipal
        {
            get
            {
                return this.categoryForPrincipal;
            }

            set
            {
                this.categoryForPrincipal = value;
                OnChanged("CategoryForPrincipal");
            }
        }

        [IgnoreDataMember]
        [ColumnObjectMapping(ColumnName = "CategoryIdForInterest", KeyProperty = "Id", AllowNulls = true)]
        public Category CategoryForInterest
        {
            get
            {
                return this.categoryForInterest;
            }

            set
            {
                this.categoryForInterest = value;
                OnChanged("CategoryForInterest");
            }
        }

        #region Serialization Hack

        string categoryForPrincipalName;

        /// <summary>
        /// for serialization only
        /// </summary>
        [DataMember]
        public string CategoryForPrincipalName
        {
            get { return this.categoryForPrincipal == null ? null : this.categoryForPrincipal.Name; }
            set { categoryForPrincipalName = value; }
        }

        string categoryForInterestName;

        /// <summary>
        /// for serialization only
        /// </summary>
        [DataMember]
        public string CategoryForInterestName
        {
            get { return this.categoryForInterest == null ? null : this.categoryForInterest.Name; }
            set { categoryForInterestName = value; }
        }

        internal void PostDeserializeFixup(MyMoney myMoney)
        {
            this.OnlineAccount = myMoney.OnlineAccounts.FindOnlineAccountAt(this.OnlineAccountId);

            if (!string.IsNullOrEmpty(categoryForPrincipalName))
            {
                this.CategoryForPrincipal = myMoney.Categories.GetOrCreateCategory(categoryForPrincipalName, CategoryType.Expense);
                categoryForPrincipalName = null;
            }
            if (!string.IsNullOrEmpty(categoryForInterestName))
            {
                this.categoryForInterest = myMoney.Categories.GetOrCreateCategory(categoryForInterestName, CategoryType.Expense);
                categoryForInterestName = null;
            }
        }

        #endregion

    }

    class AccountComparer : IComparer<Account>
    {
        public int Compare(Account x, Account y)
        {
            Account a = (Account)x;
            Account b = (Account)y;
            if (a == null && b != null) return -1;
            if (a != null && b == null) return 1;
            if (a == null && b == null) return 0;
            string n = a.Name;
            string m = b.Name;
            if (n == null && m != null) return -1;
            if (n != null && m == null) return 1;
            if (n == null && m == null) return 0;
            return n.CompareTo(m);
        }
    }

    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class OnlineAccounts : PersistentContainer, ICollection<OnlineAccount>
    {
        int NextOnlineAccount;
        Hashtable<int, OnlineAccount> onlineAccounts = new Hashtable<int, OnlineAccount>();
        Hashtable<string, OnlineAccount> instIndex = new Hashtable<string, OnlineAccount>();

        public OnlineAccounts()
        { // for serialization
        }

        public OnlineAccounts(PersistentObject parent)
            : base(parent)
        {
        }

        [XmlElement("OnlineAccount", typeof(OnlineAccount))]
        public OnlineAccount[] Items
        {
            get
            {
                IList<OnlineAccount> list = GetOnlineAccounts();
                return ((List<OnlineAccount>)list).ToArray();
            }
            set
            {
                // only used during serialization.
                foreach (OnlineAccount oa in this.Items)
                {
                    this.RemoveOnlineAccount(oa);
                }
                this.NextOnlineAccount = 0;
                foreach (OnlineAccount oa in value)
                {
                    oa.Id = this.NextOnlineAccount++;
                    this.AddOnlineAccount(oa);
                }
            }
        }

        public IList<OnlineAccount> GetOnlineAccounts()
        {
            List<OnlineAccount> list = new List<OnlineAccount>(this.onlineAccounts.Count);
            foreach (OnlineAccount i in this.onlineAccounts.Values)
            {
                if (!i.IsDeleted)
                {
                    list.Add(i);
                }
            }
            list.Sort(new OnlineAccountComparer());
            return list;
        }

        public void Clear()
        {
            if (NextOnlineAccount != 0 || this.onlineAccounts.Count != 0 || this.instIndex.Count != 0)
            {
                NextOnlineAccount = 0;
                this.onlineAccounts = new Hashtable<int, OnlineAccount>();
                this.instIndex = new Hashtable<string, OnlineAccount>();
                this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }

        public override void OnNameChanged(object o, string oldName, string newName)
        {
            if (oldName != null && instIndex.ContainsKey(oldName))
                instIndex.Remove(oldName);
            instIndex[newName] = (OnlineAccount)o;
        }

        // onlineAccounts
        public OnlineAccount AddOnlineAccount(int id)
        {
            OnlineAccount result = new OnlineAccount(this);
            result.Id = id;
            if (NextOnlineAccount <= id) NextOnlineAccount = id + 1;
            this.onlineAccounts.Add(id, result);
            this.FireChangeEvent(this, result, null, ChangeType.Inserted);
            return result;
        }

        public void AddOnlineAccount(OnlineAccount oa)
        {
            if (GetOnlineAccounts().Contains(oa))
            {
                return;
            }
            if (oa.Id == -1)
            {
                oa.Id = NextOnlineAccount++;
                oa.OnInserted();
            }
            else if (NextOnlineAccount <= oa.Id)
            {
                NextOnlineAccount = oa.Id + 1;
            }
            this.onlineAccounts[oa.Id] = oa;
            if (!string.IsNullOrEmpty(oa.Name))
            {
                this.instIndex[oa.Name] = oa;
            }
            this.FireChangeEvent(this, oa, null, ChangeType.Inserted);
        }

        public OnlineAccount AddOnlineAccount(string name)
        {
            OnlineAccount result = AddOnlineAccount(this.NextOnlineAccount++);
            result.Name = name;
            this.instIndex[name] = result;
            this.FireChangeEvent(this, result, null, ChangeType.Inserted);
            return result;
        }

        public OnlineAccount FindOnlineAccount(string name)
        {
            if (name == null) return null;
            // find or add account of givien name
            OnlineAccount result = (OnlineAccount)this.instIndex[name];
            return result;
        }

        public OnlineAccount FindOnlineAccountAt(int id)
        {
            return (OnlineAccount)this.onlineAccounts[id];
        }

        public bool RemoveOnlineAccount(OnlineAccount i)
        {
            if (this.onlineAccounts.ContainsKey(i.Id))
                this.onlineAccounts.Remove(i.Id);
            if (this.instIndex.ContainsKey(i.Name))
                this.instIndex.Remove(i.Name);
            i.OnDelete();
            return true;
        }

        #region ICollection

        public void Add(OnlineAccount item)
        {
            this.AddOnlineAccount(item);
        }

        public bool Contains(OnlineAccount item)
        {
            return onlineAccounts.ContainsKey(item.Id);
        }

        public void CopyTo(OnlineAccount[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { return onlineAccounts.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public override void Add(object child)
        {
            Add((OnlineAccount)child);
        }

        public override void RemoveChild(PersistentObject pe)
        {
            Remove((OnlineAccount)pe);
        }

        public bool Remove(OnlineAccount item)
        {
            return this.RemoveOnlineAccount(item);
        }

        public new IEnumerator<OnlineAccount> GetEnumerator()
        {
            foreach (OnlineAccount a in this.onlineAccounts.Values)
            {
                yield return a;
            }
        }

        protected override IEnumerator<PersistentObject> InternalGetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }

    //================================================================================
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    [TableMapping(TableName = "OnlineAccounts")]
    public class OnlineAccount : PersistentObject
    {
        int id = -1;
        string name;
        string institution;
        string ofx;
        string fid;
        string userid;
        string password;
        string userCred1;
        string userCred2;
        string authToken;
        string bankId;
        string brokerId;
        string branchId;
        string ofxVersion;
        string logoUrl;
        string appId = "QWIN";
        string appVersion = "1700";
        string sessionCookie;
        string clientUid;
        string accessKey;
        string userKey;
        DateTime? userKeyExpireDate;

        public OnlineAccount()
        { // for serializer
        }

        public OnlineAccount(OnlineAccounts parent)
            : base(parent)
        {
        }

        public OnlineAccount ShallowCopy()
        {
            // do NOT want to copy Parent or Id fields because this generates confusing change events.
            return new OnlineAccount()
            {
                name = this.name,
                institution = this.institution,
                ofx = this.ofx,
                fid = this.fid,
                userid = this.userid,
                password = this.password,
                bankId = this.bankId,
                brokerId = this.brokerId,
                branchId = this.branchId,
                ofxVersion = this.ofxVersion,
                logoUrl = this.logoUrl,
                appId = this.appId,
                appVersion = this.appVersion,
                sessionCookie = this.sessionCookie,
                clientUid = this.clientUid
            };
        }

        [XmlAttribute]
        [DataMember]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true)]
        public int Id
        {
            get { return this.id; }
            set { if (this.id != value) { this.id = value; OnChanged("Id"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Name", MaxLength = 80)]
        public string Name
        {
            get { return this.name; }
            set
            {
                if (this.name != value)
                {
                    string old = this.name;
                    this.name = Truncate(value, 80); OnChanged("Name");
                    OnNameChanged(old, this.name);
                }
            }
        }
        [XmlIgnore]
        public string ValidFileName
        {
            get
            {
                string fileName = this.Name;
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    if (fileName.Contains(c))
                    {
                        fileName = fileName.Replace(c, '_');
                    }
                }
                return fileName;
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Institution", MaxLength = 80, AllowNulls = true)]
        public string Institution
        {
            get { return this.institution; }
            set
            {
                if (this.institution != value)
                {
                    this.institution = Truncate(value, 80); OnChanged("Institution");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "OFX", MaxLength = 255, AllowNulls = true)]
        public string Ofx
        {
            get { return this.ofx; }
            set
            {
                if (this.ofx != value)
                {
                    this.ofx = Truncate(value, 255); OnChanged("Ofx");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "OfxVersion", MaxLength = 10, AllowNulls = true)]
        public string OfxVersion
        {
            get { return this.ofxVersion; }
            set
            {
                if (this.ofxVersion != value)
                {
                    this.ofxVersion = value;
                    OnChanged("OfxVersion");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "FID", MaxLength = 50, AllowNulls = true)]
        public string FID
        {
            get { return this.fid; }
            set
            {
                if (this.fid != value)
                {
                    string old = this.fid;
                    this.fid = Truncate(value, 50);
                    OnChanged("FID");
                }
            }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "UserId", MaxLength = 20, AllowNulls = true)]
        public string UserId
        {
            get { return this.userid; }
            set
            {
                if (this.userid != value)
                {
                    string old = this.userid;
                    this.userid = Truncate(value, 20);
                    OnChanged("UserId");
                }
            }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "Password", MaxLength = 50, AllowNulls = true)]
        public string Password
        {
            get { return this.password; }
            set
            {
                if (this.password != value)
                {
                    this.password = Truncate(value, 50);
                    OnChanged("Password");
                }
            }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "UserCred1", MaxLength = 200, AllowNulls = true)]
        public string UserCred1
        {
            get { return this.userCred1; }
            set
            {
                if (this.userCred1 != value)
                {
                    this.userCred1 = Truncate(value, 200);
                    OnChanged("UserCred1");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "UserCred2", MaxLength = 200, AllowNulls = true)]
        public string UserCred2
        {
            get { return this.userCred2; }
            set
            {
                if (this.userCred2 != value)
                {
                    this.userCred2 = Truncate(value, 200);
                    OnChanged("UserCred2");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "AuthToken", MaxLength = 200, AllowNulls = true)]
        public string AuthToken
        {
            get { return this.authToken; }
            set
            {
                if (this.authToken != value)
                {
                    this.authToken = Truncate(value, 200);
                    OnChanged("AuthToken");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "BankId", MaxLength = 50, AllowNulls = true)]
        public string BankId
        {
            get { return this.bankId; }
            set { if (this.bankId != value) { this.bankId = Truncate(value, 50); OnChanged("BankId"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "BranchId", MaxLength = 50, AllowNulls = true)]
        public string BranchId
        {
            get { return this.branchId; }
            set { if (this.branchId != value) { this.branchId = Truncate(value, 50); OnChanged("BranchId"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "BrokerId", MaxLength = 50, AllowNulls = true)]
        public string BrokerId
        {
            get { return this.brokerId; }
            set { if (this.brokerId != value) { this.brokerId = Truncate(value, 50); OnChanged("BrokerId"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "LogoUrl", MaxLength = 1000, AllowNulls = true)]
        public string LogoUrl
        {
            get { return this.logoUrl; }
            set { if (this.logoUrl != value) { this.logoUrl = Truncate(value, 1000); OnChanged("LogoUrl"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "AppId", MaxLength = 10, AllowNulls = true)]
        public string AppId
        {
            get { return this.appId; }
            set
            {
                if (this.appId != value)
                {
                    this.appId = Truncate(value, 10);
                    OnChanged("AppId");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "AppVersion", MaxLength = 10, AllowNulls = true)]
        public string AppVersion
        {
            get { return this.appVersion; }
            set
            {
                if (this.appVersion != value)
                {
                    this.appVersion = Truncate(value, 10);
                    OnChanged("AppVersion");
                }
            }
        }

        [XmlIgnore]
        public string SessionCookie
        {
            get { return this.sessionCookie; }
            set { this.sessionCookie = value; }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "ClientUid", MaxLength = 36, AllowNulls = true)]
        public string ClientUid
        {
            get { return this.clientUid; }
            set
            {
                if (this.clientUid != value)
                {
                    this.clientUid = Truncate(value, 36);
                    OnChanged("ClientUid");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "AccessKey", MaxLength = 36, AllowNulls = true)]
        public string AccessKey
        {
            get { return this.accessKey; }
            set
            {
                if (this.accessKey != value)
                {
                    this.accessKey = Truncate(value, 36);
                    OnChanged("AccessKey");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "UserKey", MaxLength = 64, AllowNulls = true)]
        public string UserKey
        {
            get { return this.userKey; }
            set
            {
                if (this.userKey != value)
                {
                    this.userKey = Truncate(value, 64);
                    OnChanged("UserKey");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "UserKeyExpireDate", AllowNulls = true)]
        public DateTime? UserKeyExpireDate
        {
            get { return this.userKeyExpireDate; }
            set
            {
                if (this.userKeyExpireDate != value)
                {
                    this.userKeyExpireDate = value;
                    OnChanged("UserKeyExpireDate");
                }
            }
        }


        /// <summary>
        /// We do not serialize these answers, the server only asks for them rarely.
        /// </summary>
        [XmlIgnore]
        public List<MfaChallengeAnswer> MfaChallengeAnswers { get; set; }


        // Needed by FormAccountDetails.
        public override string ToString()
        {
            return this.Name;
        }

    }

    public class MfaChallengeAnswer
    {
        public string Id { get; set; }
        public string Answer { get; set; }
    }

    class OnlineAccountComparer : IComparer<OnlineAccount>
    {
        public int Compare(OnlineAccount x, OnlineAccount y)
        {
            OnlineAccount a = (OnlineAccount)x;
            OnlineAccount b = (OnlineAccount)y;
            if (a == null && b != null) return -1;
            if (a != null && b == null) return 1;
            string n = a.Name;
            string m = b.Name;
            if (n == null && m != null) return -1;
            if (n != null && m == null) return 1;
            return n.CompareTo(m);
        }
    }



    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Aliases : PersistentContainer, ICollection<Alias>
    {
        int nextAlias;
        Hashtable<int, Alias> aliases = new Hashtable<int, Alias>();

        public Aliases()
        {
            // for serialization
        }

        public Aliases(PersistentObject parent)
            : base(parent)
        {
        }

        public override void Add(object child)
        {
            AddAlias((Alias)child);
        }

        public override void RemoveChild(PersistentObject pe)
        {
            RemoveAlias((Alias)pe);
        }

        // Aliases
        public Alias AddAlias(int id)
        {
            Alias result = new Alias(this);
            lock (this.aliases)
            {
                result.Id = id;
                if (this.nextAlias <= id) this.nextAlias = id + 1;
                this.aliases[id] = result;
            }
            this.FireChangeEvent(this, result, null, ChangeType.Inserted);

            return result;
        }

        public void AddAlias(Alias a)
        {
            lock (this.aliases)
            {
                if (a.Id == -1)
                {
                    a.Id = this.nextAlias++;
                    a.OnInserted();
                }
                else if (this.nextAlias <= a.Id)
                {
                    this.nextAlias = a.Id + 1;
                }
                a.Parent = this;
                a.OnInserted();
                this.aliases[a.Id] = a;
            }

            this.FireChangeEvent(this, a, null, ChangeType.Inserted);
        }

        public Alias FindAlias(string pattern)
        {
            if (pattern == null) return null;
            lock (this.aliases)
            {
                foreach (Alias a in this.aliases.Values)
                {
                    if (a.Pattern == pattern)
                    {
                        return a;
                    }
                }
            }
            return null;
        }

        public Alias FindMatchingAlias(string payee)
        {
            if (payee == null) return null;
            lock (this.aliases)
            {
                foreach (Alias a in this.aliases.Values)
                {
                    if (a.Matches(payee))
                    {
                        return a;
                    }
                }
            }
            return null;
        }

        public bool RemoveAlias(Alias a)
        {
            lock (this.aliases)
            {
                if (a.IsInserted)
                {
                    // then we can remove it immediately.
                    if (this.aliases.ContainsKey(a.Id))
                        this.aliases.Remove(a.Id);
                }
            }
            // mark it for deletion on next save
            a.OnDelete();
            return true;
        }

        public void RemoveAliasesOf(Payee p)
        {
            foreach (Alias a in this.GetAliases())
            {
                if (a.Payee == p)
                {
                    RemoveAlias(a);
                }

            }
        }

        public IList<Alias> GetAliases()
        {
            List<Alias> list = new List<Alias>(this.aliases.Count);
            lock (this.aliases)
            {
                foreach (Alias a in this.aliases.Values)
                {
                    if (!a.IsDeleted)
                    {
                        list.Add(a);
                    }
                }
            }
            return list;
        }

        public void RemoveDeletedAliases()
        {
            // Cleanup deleted objects
            List<Alias> list = new List<Alias>();
            foreach (Alias a in this.aliases.Values)
            {
                if (a.IsDeleted)
                    list.Add(a);
            }
            foreach (Alias a in list)
            {
                this.aliases.Remove(a.Id);
            }
        }

        #region ICollection

        public void Add(Alias item)
        {
            AddAlias(item);
        }

        public void Clear()
        {
            if (this.nextAlias != 0 || this.aliases.Count != 0)
            {
                this.nextAlias = 0;
                this.aliases = new Hashtable<int, Alias>();
                this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }

        public bool Contains(Alias item)
        {
            return this.aliases.ContainsKey(item.Id);
        }

        public void CopyTo(Alias[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { return this.aliases.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(Alias item)
        {
            if (this.aliases.ContainsKey(item.Id))
            {
                this.aliases.Remove(item.Id);
                return true;
            }
            return false;
        }

        public new IEnumerator<Alias> GetEnumerator()
        {
            foreach (Alias a in this.aliases.Values)
            {
                yield return a;
            }
        }
        #endregion

        protected override IEnumerator<PersistentObject> InternalGetEnumerator()
        {
            return GetEnumerator();
        }
    }

    //================================================================================
    [TableMapping(TableName = "Currencies")]
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Currency : PersistentObject
    {
        int id = -1;
        string name;
        string symbol;
        decimal ratio;
        decimal lastRatio;

        public Currency()
        { // for serialization
        }

        public Currency(Currencies container) : base(container) { }

        [XmlAttribute]
        [DataMember]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true)]
        public int Id
        {
            get { return this.id; }
            set
            {
                if (this.id != value)
                {
                    this.id = value; OnChanged("Id");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Symbol", MaxLength = 20)]
        public string Symbol
        {
            get { return this.symbol; }
            set
            {
                if (this.symbol != value)
                {
                    this.symbol = Truncate(value, 20);
                    Currencies currencies = this.Parent as Currencies;
                    if (currencies != null)
                    {
                        currencies.ResetCache();
                    }
                    OnChanged("Symbol");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Name", MaxLength = 80)]
        public string Name
        {
            get { return this.name; }
            set
            {
                if (this.name != value)
                {
                    this.name = Truncate(value, 80);
                    OnChanged("Name");
                }
            }
        }


        /// <summary>
        /// This is the current ratio of the given current to the US dollar.
        /// </summary>
        [DataMember]
        [ColumnMapping(ColumnName = "Ratio", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Ratio
        {
            get { return this.ratio; }
            set
            {
                //if (this.ratio != value)
                {
                    this.ratio = value;
                    OnChanged("Ratio");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "LastRatio", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal LastRatio
        {
            get { return this.lastRatio; }
            set { if (this.lastRatio != value) { this.lastRatio = value; OnChanged("LastRatio"); } }
        }

    }

    class CurrencyComparer : IComparer<Currency>
    {
        public int Compare(Currency x, Currency y)
        {
            Currency a = (Currency)x;
            Currency b = (Currency)y;
            if (a == null && b != null) return -1;
            if (a != null && b == null) return 1;
            if (a == null && b == null) return 0;
            string n = a.Symbol;
            string m = b.Symbol;
            if (n == null && m != null) return -1;
            if (n != null && m == null) return 1;
            if (n == null && m == null) return 0;
            return n.CompareTo(m);
        }
    }


    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Currencies : PersistentContainer, ICollection<Currency>
    {
        int nextCurrency;
        Hashtable<int, Currency> currencies = new Hashtable<int, Currency>();
        Hashtable<string, Currency> quickLookup = new Hashtable<string, Currency>();

        public Currencies()
        {
            // for serialization
        }

        public Currencies(PersistentObject parent)
            : base(parent)
        {
        }

        public void ResetCache()
        {
            lock (quickLookup)
            {
                quickLookup.Clear();
            }
        }

        public override void Add(object child)
        {
            AddCurrency((Currency)child);
        }

        public override void RemoveChild(PersistentObject pe)
        {
            RemoveCurrency((Currency)pe);
        }

        public Currency AddCurrency(int id)
        {
            Currency currency = new Currency(this);
            lock (this.currencies)
            {
                currency.Id = id;
                if (this.nextCurrency <= id) this.nextCurrency = id + 1;
                this.currencies[id] = currency;

                ResetCache();

            }
            this.FireChangeEvent(this, currency, null, ChangeType.Inserted);

            return currency;
        }

        public void AddCurrency(Currency a)
        {
            lock (this.currencies)
            {
                if (a.Id == -1)
                {
                    a.Id = this.nextCurrency++;
                    a.OnInserted();
                }
                else if (this.nextCurrency <= a.Id)
                {
                    this.nextCurrency = a.Id + 1;
                }
                a.Parent = this;
                a.OnInserted();
                this.currencies[a.Id] = a;


                ResetCache();
            }

            this.FireChangeEvent(this, a, null, ChangeType.Inserted);
        }

        public void CopyTo(Currency[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Convert the given amount from the source currency to the target currency.
        /// </summary>
        /// <param name="amount">The amount to be converted</param>
        /// <param name="source">The currency of the account containing the amount</param>
        /// <param name="target">The currency of the target account we are transfering to</param>
        /// <returns>The converted amount</returns>
        public decimal GetTransferAmount(decimal amount, string sourceCurrency, string targetCurrency)
        {
            if (sourceCurrency == targetCurrency)
            {
                // the easy case
                return amount;
            }

            Currency from = FindCurrency(sourceCurrency);
            Currency to = FindCurrency(targetCurrency);
            if (from == to)
            {
                return amount;
            }

            if (from == null)
            {
                // then we assume this is USD               
                if (to.Ratio == 0)
                {
                    return amount;
                }
                return amount / to.Ratio;
            }

            if (to == null)
            {
                // then we're converting to USD
                if (from.Ratio == 0)
                {
                    return amount;
                }
                return amount * from.Ratio;
            }

            if (from.Ratio == 0 || to.Ratio == 0)
            {
                // don't know yet.
                return amount;
            }

            return (amount * from.Ratio) / to.Ratio;
        }


        public Currency FindCurrency(string currencySymbol)
        {
            if (string.IsNullOrWhiteSpace(currencySymbol))
                return null;

            if (currencies.Count == 0)
                return null;

            lock (this.currencies)
            {
                Currency currencyFound;
                lock (quickLookup)
                {
                    if (quickLookup.Count == 0)
                    {
                        // First build a cache to speed up any other request
                        foreach (Currency c in this.currencies.Values)
                        {
                            if (!c.IsDeleted)
                            {
                                quickLookup[c.Symbol] = c;
                            }
                        }
                    }
                    if (quickLookup.TryGetValue(currencySymbol, out currencyFound))
                    {
                        return currencyFound;
                    }
                }

            }
            return null;
        }


        public bool RemoveCurrency(Currency a)
        {
            return Remove(a);
        }

        public IList<Currency> GetCurrencies()
        {
            List<Currency> list = new List<Currency>(this.currencies.Count);
            lock (this.currencies)
            {
                foreach (Currency a in this.currencies.Values)
                {
                    if (!a.IsDeleted)
                    {
                        list.Add(a);
                    }
                }
            }
            return list;
        }

        #region ICollection

        public void Add(Currency item)
        {
            AddCurrency(item);
        }

        public void Clear()
        {
            if (this.nextCurrency != 0 || this.currencies.Count != 0)
            {
                this.nextCurrency = 0;
                this.currencies = new Hashtable<int, Currency>();
                ResetCache();
                this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }

        public bool Contains(Currency item)
        {
            return this.currencies.ContainsKey(item.Id);
        }

        public int Count
        {
            get { return this.currencies.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(Currency item)
        {
            if (item.IsInserted)
            {
                lock (this.currencies)
                {
                    if (this.currencies.ContainsKey(item.Id))
                    {
                        this.currencies.Remove(item.Id);
                    }
                }
            }
            item.OnDelete();
            ResetCache();
            return true;
        }

        public new IEnumerator<Currency> GetEnumerator()
        {
            foreach (Currency a in this.currencies.Values)
            {
                yield return a;
            }
        }
        #endregion

        protected override IEnumerator<PersistentObject> InternalGetEnumerator()
        {
            return GetEnumerator();
        }
    }


    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Payees : PersistentContainer, ICollection<Payee>
    {
        int nextPayee;
        Hashtable<int, Payee> payees = new Hashtable<int, Payee>();
        Hashtable<string, Payee> payeeIndex = new Hashtable<string, Payee>();

        public Payees()
        {
            // for serialization
        }

        public Payees(PersistentObject parent)
            : base(parent)
        {
        }

        [XmlIgnore]
        public Payee Transfer
        {
            get { return this.FindPayee("Transfer", true); }
        }

        public void Clear()
        {
            if (this.nextPayee != 0 || this.payees.Count != 0 || this.payeeIndex.Count != 0)
            {
                this.nextPayee = 0;
                this.payees = new Hashtable<int, Payee>();
                this.payeeIndex = new Hashtable<string, Payee>();

                this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }
        public override void OnNameChanged(object o, string oldName, string newName)
        {
            if (o is Payee)
            {
                if (oldName != null && payeeIndex.ContainsKey(oldName))
                    payeeIndex.Remove(oldName);
                payeeIndex[newName] = (Payee)o;
            }
        }

        public int Count
        {
            get { return this.payees.Count; }
        }

        // Payees
        public Payee AddPayee(int id)
        {
            Payee result = new Payee(this);
            lock (payees)
            {
                result.Id = id;
                if (this.nextPayee <= id) this.nextPayee = id + 1;
                this.payees[id] = result;
            }
            return result;
        }

        public void AddPayee(Payee p)
        {
            lock (payees)
            {
                if (p.Id == -1)
                {
                    p.Id = this.nextPayee++;
                    p.OnInserted();
                }
                else if (this.nextPayee <= p.Id)
                {
                    this.nextPayee = p.Id + 1;
                }
                p.Parent = this;
                this.payees[p.Id] = p;
                OnNameChanged(p, null, p.Name);
            }
        }

        public Payee FindPayee(string name, bool add)
        {
            if (name == null) return null;
            // find or add account of givien name
            Payee result = (Payee)this.payeeIndex[name];
            if (result == null && add)
            {
                result = AddPayee(this.nextPayee++);
                result.Name = name;
                this.payeeIndex[name] = result;
                this.FireChangeEvent(this, result, null, ChangeType.Inserted);
            }
            return result;
        }

        public Payee FindPayeeAt(int id)
        {
            return (Payee)this.payees[id];
        }

        // todo: there should be no references left at this point...
        public bool RemovePayee(Payee p)
        {
            lock (payees)
            {
                if (p.IsInserted)
                {
                    // then we can remove it
                    if (this.payees.ContainsKey(p.Id))
                        this.payees.Remove(p.Id);
                }
                if (this.payeeIndex.ContainsKey(p.Name))
                    this.payeeIndex.Remove(p.Name);

                MyMoney money = this.Parent as MyMoney;
                if (money != null)
                {
                    money.Aliases.RemoveAliasesOf(p);
                }
            }
            // mark it for deletion on next save.
            p.OnDelete();
            return true;
        }

        public IList<Payee> GetPayees()
        {
            List<Payee> list = new List<Payee>(this.payees.Count);
            lock (payees)
            {
                foreach (Payee p in this.payees.Values)
                {
                    if (!p.IsDeleted)
                    {
                        list.Add(p);
                    }
                }
            }
            return list;
        }

        public IList<Payee> AllPayees
        {
            get
            {
                return GetPayees();
            }
        }



        public List<Payee> AllPayeesSorted
        {
            get
            {
                List<Payee> l = GetPayeesAsList();
                l.Sort(new PayeeComparer2());
                return l;
            }
        }

        public List<Payee> GetPayeesAsList()
        {
            List<Payee> list = new List<Payee>();
            lock (payees)
            {
                foreach (Payee p in this.payees.Values)
                {
                    if (!p.IsDeleted)
                    {
                        list.Add(p);
                    }
                }
            }
            return list;
        }

        public List<Payee> GetPayeesAsList(string filter)
        {
            string lower = StringHelpers.SafeLower(filter);
            List<Payee> list = new List<Payee>();
            lock (payees)
            {
                foreach (Payee p in this.payees.Values)
                {
                    if (!p.IsDeleted && (string.IsNullOrEmpty(lower) || StringHelpers.SafeLower(p.Name).Contains(lower)))
                    {
                        list.Add(p);
                    }
                }
            }
            return list;
        }

        #region ICollection

        public void Add(Payee item)
        {
            this.AddPayee(item);
        }

        public bool Contains(Payee item)
        {
            return this.payees.ContainsKey(item.Id);
        }

        public void CopyTo(Payee[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public override void Add(object child)
        {
            Add((Payee)child);
        }

        public override void RemoveChild(PersistentObject pe)
        {
            Remove((Payee)pe);
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(Payee item)
        {
            return this.RemovePayee(item);
        }

        public new IEnumerator<Payee> GetEnumerator()
        {
            foreach (Payee p in this.payees.Values)
            {
                yield return p;
            }
        }

        protected override IEnumerator<PersistentObject> InternalGetEnumerator()
        {
            return GetEnumerator();
        }

        internal Payee ImportPayee(Payee payee)
        {
            if (payee == null)
            {
                return null;
            }
            Payee p = FindPayee(payee.Name, false);
            if (p == null)
            {
                p = FindPayee(payee.Name, true);
            }
            return p;
        }
        #endregion
    }

    public enum StatusFlags
    {
        None,
        Unaccepted,
        Uncategorized
    }

    //================================================================================
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    [TableMapping(TableName = "Payees")]
    public class Payee : PersistentObject
    {
        int id = -1;
        int unaccepted;
        int uncategorized;
        string name;

        public Payee()
        { // for serialization only
        }

        public Payee(Payees container) : base(container) { }

        [XmlAttribute]
        [DataMember]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true)]
        public int Id
        {
            get { return this.id; }
            set { if (this.id != value) { this.id = value; OnChanged("Id"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Name", MaxLength = 255)]
        public string Name
        {
            get { return this.name; }
            set
            {
                if (this.name != value)
                {
                    string old = this.name;
                    this.name = Truncate(value, 255);
                    OnChanged("Name");
                    OnNameChanged(old, this.name);
                }
            }
        }

        public void Merge(Payee other)
        {
            // move all our unaccepted count to the other payee
            this.UnacceptedTransactions += other.UnacceptedTransactions;
            other.UnacceptedTransactions = 0;
            this.UncategorizedTransactions += other.UncategorizedTransactions;
            other.UncategorizedTransactions = 0;
        }

        public int UnacceptedTransactions
        {
            get { return this.unaccepted; }
            set
            {
                this.unaccepted = value;
                OnChanged("UnacceptedTransactions");
                OnChanged("Flags");
            }
        }

        public int UncategorizedTransactions
        {
            get { return this.uncategorized; }
            set
            {
                this.uncategorized = value;
                OnChanged("UncategorizedTransactions");
                OnChanged("Flags");
            }
        }

        public StatusFlags Flags
        {
            get
            {
                StatusFlags flags = StatusFlags.None;
                if (this.unaccepted > 0)
                {
                    flags |= StatusFlags.Unaccepted;
                }
                if (this.uncategorized > 0)
                {
                    flags |= StatusFlags.Uncategorized;
                }
                return flags;
            }
        }

        public override string ToString()
        {
            return this.Name;
        }

        public static Payee Deserialize(string xml)
        {
            Payee p = null;
            try
            {
                DataContractSerializer xs = new DataContractSerializer(typeof(Payee));
                using (StringReader sr = new StringReader(xml))
                {
                    XmlTextReader r = new XmlTextReader(sr);
                    p = (Payee)xs.ReadObject(r);
                    r.Close();
                }
            }
            catch
            {
            }
            return p;
        }

    }

    public enum AliasType { None, Regex };

    //================================================================================
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    [TableMapping(TableName = "Aliases")]
    public class Alias : PersistentObject
    {
        int id = -1;
        string pattern;
        AliasType type;
        int payeeId;
        Payee payee;
        Regex regex;

        public Alias()
        { // for serialization only
        }

        public Alias(PersistentContainer container) : base(container) { }

        [DataMember]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true)]
        public int Id { get { return id; } set { id = value; } }

        [DataMember]
        [ColumnMapping(ColumnName = "Pattern", MaxLength = 255)]
        public string Pattern
        {
            get { return this.pattern; }
            set
            {
                if (this.pattern != value)
                {
                    string old = this.pattern;
                    this.pattern = value;
                    OnChanged("Pattern");
                }
            }
        }

        public override void OnChanged(string name)
        {
            if (this.regex == null && this.AliasType == AliasType.Regex && this.pattern != null)
            {
                this.regex = new Regex(this.pattern);
            }
            base.OnChanged(name);
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Flags", SqlType = typeof(SqlInt32))]
        public AliasType AliasType
        {
            get { return this.type; }
            set
            {
                if (this.type != value)
                {
                    this.type = value; OnChanged("AliasType");
                }
            }
        }

        // for storage.
        [DataMember]
        int PayeeId
        {
            get { return payeeId; }
            set { payeeId = value; }
        }

        [XmlIgnore]
        [ColumnObjectMapping(ColumnName = "Payee", KeyProperty = "Id")]
        public Payee Payee
        {
            get { return this.payee; }
            set
            {
                if (this.payee != value)
                {
                    payeeId = value == null ? -1 : value.Id;
                    this.payee = value; OnChanged("Payee");
                }
            }
        }

        public bool Matches(string payee)
        {
            bool match = false;
            if (string.IsNullOrWhiteSpace(payee))
            {
                return false;
            }
            if (this.type == AliasType.Regex)
            {
                if (this.regex == null)
                {
                    this.regex = new Regex(this.pattern);
                }
                Match m = this.regex.Match(payee);
                match = (m != null && m.Success && m.Index == 0 && m.Length == payee.Length);
            }
            else
            {
                match = string.Compare(pattern, payee, true, CultureInfo.CurrentUICulture) == 0;
            }
            return match;
        }

        internal void PostDeserializeFixup(MyMoney myMoney)
        {
            this.Payee = myMoney.Payees.FindPayeeAt(this.PayeeId);
        }
    }

    public class PayeeComparer : IComparer<Payee>
    {
        public int Compare(Payee x, Payee y)
        {
            Payee a = (Payee)x;
            Payee b = (Payee)y;
            if (a == null && b != null) return -1;
            if (a != null && b == null) return 1;
            string n = a.Name;
            string m = b.Name;
            if (n == null && m != null) return -1;
            if (n != null && m == null) return 1;
            return n.CompareTo(m);
        }
    }

    public class PayeeComparer2 : IComparer<Payee>
    {
        public int Compare(Payee x, Payee y)
        {
            if (x == null && y != null) return -1;
            if (x != null && y == null) return 1;
            string n = x.Name;
            string m = y.Name;
            if (n == null && m != null) return -1;
            if (n != null && m == null) return 1;
            return n.CompareTo(m);
        }
    }


    public class RentExpenseTotal
    {
        public decimal TotalTaxes { get; set; }
        public decimal TotalMaintenance { get; set; }
        public decimal TotalRepairs { get; set; }
        public decimal TotalManagement { get; set; }
        public decimal TotalInterest { get; set; }

        public RentExpenseTotal()
        {
            TotalMaintenance = 0;
            TotalTaxes = 0;
            TotalRepairs = 0;
            TotalManagement = 0;
            TotalInterest = 0;
        }

        public decimal AllExpenses
        {
            get { return TotalTaxes + TotalRepairs + TotalMaintenance + TotalManagement + TotalInterest; }
        }
    }



    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class RentalBuildingSingleYearSingleDepartment
    {
        public Category DepartmentCategory;
        public string Name { get; set; }
        public RentBuilding Building { get; set; }
        public int Year { get; set; }
        public decimal Total { get; set; }

        public RentalBuildingSingleYearSingleDepartment()
        {
            Total = 0;
        }

        override public string ToString()
        {
            return string.Format("{0} {1}", Name, Total);
        }
    }



    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class RentalBuildingSingleYear
    {
        public List<RentalBuildingSingleYearSingleDepartment> Departments { get; set; }


        int yearStart;

        public int YearStart
        {
            get { return yearStart; }
        }

        int yearEnd;

        public int YearEnd
        {
            get { return yearEnd; }
        }

        public string Period
        {
            get
            {
                string period = RentBuilding.GetYearRangeString(YearStart, YearEnd);
                if (string.IsNullOrEmpty(period))
                {
                    period = Year.ToString();
                }
                return period;
            }
        }

        public RentBuilding Building { get; set; }

        public int Year { get; set; }



        public void RecalcYears()
        {
            this.yearStart = int.MaxValue;
            this.yearEnd = int.MinValue;


            // Incomes
            TotalIncome = this.Departments[0].Total;

            // Expenses
            this.TotalExpensesGroup = new RentExpenseTotal();

            TotalExpensesGroup.TotalTaxes = this.Departments[1].Total;
            TotalExpensesGroup.TotalRepairs = this.Departments[2].Total;
            TotalExpensesGroup.TotalMaintenance = this.Departments[3].Total;
            TotalExpensesGroup.TotalManagement = this.Departments[4].Total;
            TotalExpensesGroup.TotalInterest = this.Departments[5].Total;

        }


        public decimal TotalIncome { get; set; }

        public RentExpenseTotal TotalExpensesGroup { get; set; }

        public decimal TotalExpense
        {
            get
            {
                return TotalExpensesGroup.AllExpenses;
            }
        }


        public decimal TotalProfit
        {
            get
            {
                return TotalIncome + TotalExpense; // Expense is expressed in -negative form 
            }
        }


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="money"></param>
        /// <param name="building"></param>
        /// <param name="year"></param>
        public RentalBuildingSingleYear(
            MyMoney money,
            RentBuilding building,
            int year
            )
        {
            this.Building = building;
            this.Year = year;
            Departments = new List<RentalBuildingSingleYearSingleDepartment>();
        }

    }



    //================================================================================
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    [TableMapping(TableName = "RentBuildings")]
    public class RentBuilding : PersistentObject
    {
        #region PRIVATE PROPERTIES

        int id = -1;
        string name;

        //
        // Account and Category associated to this Rental
        //
        int categoryForIncome;
        int categoryForTaxes;
        int categoryForInterest;
        int categoryForRepairs;
        int categoryForMaintenance;
        int categoryForManagement;

        string address;
        DateTime purchasedDate = DateTime.Now;
        decimal purchasedPrice = 1;
        decimal estimatedValue = 2;
        decimal landValue = 1;

        string ownershipName1;
        string ownershipName2;
        decimal ownershipPercentage1;
        decimal ownershipPercentage2;

        string note;




        int yearStart = int.MaxValue;
        int yearEnd = int.MinValue;

        #endregion


        #region PERSISTED PROPERTIES

        [XmlAttribute]
        [DataMember]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true)]
        public int Id
        {
            get { return this.id; }
            set
            {
                if (this.id != value)
                {
                    this.id = value;
                    OnChanged("Id");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Name", MaxLength = 255)]
        public string Name
        {
            get { return this.name; }
            set
            {
                if (this.name != value)
                {
                    this.name = value;
                    OnChanged("Name");
                }
            }
        }



        [DataMember]
        [ColumnMapping(ColumnName = "Address", MaxLength = 255, AllowNulls = true)]
        public string Address
        {
            get { return this.address; }
            set
            {
                if (this.address != value)
                {
                    this.address = value;
                    OnChanged("Address");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "PurchasedDate", AllowNulls = true)]
        public DateTime PurchasedDate
        {
            get { return this.purchasedDate; }
            set
            {
                if (this.purchasedDate != value)
                {
                    this.purchasedDate = value;
                    OnChanged("PurchasedDate");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "PurchasedPrice", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal PurchasedPrice
        {
            get { return this.purchasedPrice; }
            set
            {
                if (this.purchasedPrice != value)
                {
                    this.purchasedPrice = value;
                    OnChanged("PurchasedPrice");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "LandValue", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal LandValue
        {
            get { return this.landValue; }
            set
            {
                if (this.landValue != value)
                {
                    this.landValue = value;
                    OnChanged("LandValue");
                }
            }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "EstimatedValue", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal EstimatedValue
        {
            get { return this.estimatedValue; }
            set
            {
                if (this.estimatedValue != value)
                {
                    this.estimatedValue = value;
                    OnChanged("EstimatedValue");
                }
            }
        }



        [DataMember]
        [ColumnMapping(ColumnName = "OwnershipName1", MaxLength = 255, AllowNulls = true)]
        public string OwnershipName1
        {
            get { return this.ownershipName1; }
            set
            {
                if (this.ownershipName1 != value)
                {
                    this.ownershipName1 = value;
                    OnChanged("OwnershipName1");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "OwnershipName2", MaxLength = 255, AllowNulls = true)]
        public string OwnershipName2
        {
            get { return this.ownershipName2; }
            set
            {
                if (this.ownershipName2 != value)
                {
                    this.ownershipName2 = value;
                    OnChanged("OwnershipName2");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "OwnershipPercentage1", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal OwnershipPercentage1
        {
            get { return this.ownershipPercentage1; }
            set
            {
                if (this.ownershipPercentage1 != value)
                {
                    this.ownershipPercentage1 = value;
                    OnChanged("OwnershipPercentage1");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "OwnershipPercentage2", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal OwnershipPercentage2
        {
            get { return this.ownershipPercentage2; }
            set
            {
                if (this.ownershipPercentage2 != value)
                {
                    this.ownershipPercentage2 = value;
                    OnChanged("OwnershipPercentage2");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Note", MaxLength = 255, AllowNulls = true)]
        public string Note
        {
            get { return this.note; }
            set
            {
                if (this.note != value)
                {
                    this.note = value;
                    OnChanged("Note");
                }
            }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "CategoryForTaxes", AllowNulls = true)]
        public int CategoryForTaxes
        {
            get { return this.categoryForTaxes; }
            set
            {
                if (this.categoryForTaxes != value)
                {
                    this.categoryForTaxes = value;
                    OnChanged("CategoryForTaxes");
                }
            }
        }

        [XmlAttribute]
        [DataMember]
        [ColumnMapping(ColumnName = "CategoryForIncome", AllowNulls = true)]
        public int CategoryForIncome
        {
            get { return this.categoryForIncome; }
            set
            {
                if (this.categoryForIncome != value)
                {
                    this.categoryForIncome = value;
                    OnChanged("CategoryForIncome");
                }
            }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "CategoryForInterest", AllowNulls = true)]
        public int CategoryForInterest
        {
            get { return this.categoryForInterest; }
            set
            {
                if (this.categoryForInterest != value)
                {
                    this.categoryForInterest = value;
                    OnChanged("CategoryForInterest");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "CategoryForRepairs", AllowNulls = true)]
        public int CategoryForRepairs
        {
            get { return this.categoryForRepairs; }
            set
            {
                if (this.categoryForRepairs != value)
                {
                    this.categoryForRepairs = value;
                    OnChanged("CategoryForRepairs");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "CategoryForMaintenance", AllowNulls = true)]
        public int CategoryForMaintenance
        {
            get { return this.categoryForMaintenance; }
            set
            {
                if (this.categoryForMaintenance != value)
                {
                    this.categoryForMaintenance = value;
                    OnChanged("CategoryForMaintenance");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "CategoryForManagement", AllowNulls = true)]
        public int CategoryForManagement
        {
            get { return this.categoryForManagement; }
            set
            {
                if (this.categoryForManagement != value)
                {
                    this.categoryForManagement = value;
                    OnChanged("CategoryForManagement");
                }
            }
        }


        #endregion


        #region PUBLIC PROPERTIES

        [XmlIgnore]
        public SortedDictionary<int, RentalBuildingSingleYear> Years { get; set; }

        [XmlIgnore]
        public decimal TotalProfit
        {
            get
            {
                decimal totalProfit = 0;
                foreach (var x in Years)
                {
                    totalProfit += x.Value.TotalProfit;
                }
                return totalProfit;
            }
        }
        [XmlIgnore]
        public decimal TotalIncome
        {
            get
            {
                decimal totalIncomes = 0;
                foreach (var x in Years)
                {
                    totalIncomes += x.Value.TotalIncome;
                }

                return totalIncomes;
            }
        }

        [XmlIgnore]
        public RentExpenseTotal TotalExpense
        {
            get
            {
                RentExpenseTotal totalExpenses = new RentExpenseTotal();

                foreach (var x in Years)
                {
                    totalExpenses.TotalTaxes += x.Value.TotalExpensesGroup.TotalTaxes;
                    totalExpenses.TotalRepairs += x.Value.TotalExpensesGroup.TotalRepairs;
                    totalExpenses.TotalMaintenance += x.Value.TotalExpensesGroup.TotalMaintenance;
                    totalExpenses.TotalManagement += x.Value.TotalExpensesGroup.TotalManagement;
                    totalExpenses.TotalInterest += x.Value.TotalExpensesGroup.TotalInterest;
                }
                return totalExpenses;
            }
        }


        [XmlIgnore]
        public string Period
        {
            get
            {
                RecalcYears();

                return GetYearRangeString(yearStart, yearEnd);
            }
        }

        public static string GetYearRangeString(int yearStart, int yearEnd)
        {
            if (yearStart == int.MaxValue || yearEnd == int.MinValue)
            {
                // This Rental does not yet contain any transactions
                // so there's no date range yet
                return string.Empty;
            }

            if (yearStart != yearEnd)
            {
                return string.Format("{0} - {1}", yearStart, yearEnd);
            }

            return string.Format("{0}", yearStart);
        }

        private void RecalcYears()
        {

            yearStart = int.MaxValue;
            yearEnd = int.MinValue;

            foreach (var x in Years)
            {
                yearStart = Math.Min(yearStart, x.Key);
                yearEnd = Math.Max(yearEnd, x.Key);
            }

        }


        #endregion

        public RentBuilding()
        {
            // for serialization only
        }

        public RentBuilding(RentBuildings container)
            : base(container)
        {
            Years = new SortedDictionary<int, RentalBuildingSingleYear>(new DescendingComparer<int>());
        }

        public RentBuilding ShallowCopy()
        {
            return (RentBuilding)this.MemberwiseClone();
        }

        public string GetUniqueKey()
        {
            return string.Format("{0}", id);
        }

        List<RentUnit> units = new List<RentUnit>();

        [XmlIgnore]
        public List<RentUnit> Units
        {
            get { return units; }
            set { units = value; }
        }


        public override string ToString()
        {
            return string.Format("{0}:{1}", this.id, this.Name);
        }

        public static RentBuilding Deserialize(string xml)
        {
            RentBuilding rentBuilding = null;
            try
            {
                DataContractSerializer xs = new DataContractSerializer(typeof(RentBuilding));
                using (StringReader sr = new StringReader(xml))
                {
                    XmlTextReader r = new XmlTextReader(sr);
                    rentBuilding = (RentBuilding)xs.ReadObject(r);
                    r.Close();
                }
            }
            catch
            {
            }
            return rentBuilding;
        }
    }


    //================================================================================
    /// <summary>
    /// The main collection for In-Memory-Model for rental properties (RentalBuildingAllYears)
    /// It will be populated by the storage module when it deserializes 
    /// or when the user Add,Delete a RentalBuildingAllYears
    /// </summary>
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class RentBuildings : PersistentContainer
    {

        private int nextRentBuilding;
        private Hashtable<string, RentBuilding> rentBuildings = new Hashtable<string, RentBuilding>();

        public RentUnits Units { get; set; }


        // for serialization only
        public RentBuildings()
        {
        }

        public RentBuildings(PersistentObject parent)
            : base(parent)
        {
            this.Units = new RentUnits(parent);
        }


        public void Clear()
        {
            if (this.nextRentBuilding != 0 || this.rentBuildings.Count != 0)
            {
                Units.Clear();

                this.nextRentBuilding = 0;
                this.rentBuildings = new Hashtable<string, RentBuilding>();
                this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }


        public int Count
        {
            get { return this.rentBuildings.Count; }
        }


        public void Add(RentBuilding item)
        {
            this.AddRentBuilding(item);
        }

        public void AddRentBuilding(RentBuilding r)
        {

            lock (rentBuildings)
            {
                if (r.Id == -1)
                {
                    r.Id = ++this.nextRentBuilding;
                }
                else if (this.nextRentBuilding <= r.Id)
                {
                    this.nextRentBuilding = r.Id;
                }

                r.Parent = this;
                this.rentBuildings[r.GetUniqueKey()] = r;
                FireChangeEvent(this, new ChangeEventArgs(r, null, ChangeType.Inserted));
            }
        }

        public RentBuilding Find(string key)
        {
            return (RentBuilding)this.rentBuildings[key];
        }

        public RentBuilding FindByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            foreach (RentBuilding r in rentBuildings.Values)
            {
                if (r.Name == name)
                {
                    return r;
                }
            }
            return null;
        }

        // todo: there should be no references left at this point...
        public bool RemoveBuilding(RentBuilding x)
        {
            lock (rentBuildings)
            {
                if (x.IsInserted)
                {
                    // then we can remove it immediately
                    if (this.rentBuildings.ContainsKey(x.GetUniqueKey()))
                    {
                        this.rentBuildings.Remove(x.GetUniqueKey());
                    }
                }
            }
            // mark it for deletion on next save
            x.OnDelete();
            return true;
        }

        ThreadSafeObservableCollection<RentBuilding> observableCollection = new ThreadSafeObservableCollection<RentBuilding>();

        public ObservableCollection<RentBuilding> GetList()
        {
            lock (rentBuildings)
            {
                observableCollection.Clear();
                AggregateBuildingInformation((MyMoney)this.Parent);

                foreach (RentBuilding r in this.rentBuildings.Values)
                {
                    if (!r.IsDeleted)
                    {
                        observableCollection.Add(r);
                    }
                }
            }
            return observableCollection;
        }



        public override bool FireChangeEvent(Object sender, object item, string name, ChangeType type)
        {
            if (sender == this && type == ChangeType.Reloaded)
            {
                AggregateBuildingInformation((MyMoney)this.Parent);
            }
            return FireChangeEvent(sender, new ChangeEventArgs(item, name, type));
        }


        public void AggregateBuildingInformation(MyMoney money)
        {

            // Bucket all the rent Incomes & Expenses into each building
            foreach (RentBuilding building in this.rentBuildings.Values)
            {

                //-------------------------------------------------------------
                // Find all Transactions for this building
                // We need to math the BUILDING and Any Categories (also look in the Splits)
                //
                var yearsOfTransactionForThisBuilding = from t in money.Transactions.Items
                                                        where MatchAnyCategories(building, t)
                                                        orderby t.Date.Year
                                                        group t by t.Date.Year into yearGroup
                                                        select new { Year = yearGroup.Key, TransactionsForThatYear = yearGroup };

                foreach (var g in yearsOfTransactionForThisBuilding)
                {
                    int year = g.Year;

                    RentalBuildingSingleYear transactionsForYear = new RentalBuildingSingleYear(money, building, year);

                    //    DepartmentIncome = 0,
                    //    DepartmentTaxes = 1,
                    //    DepartmentRepairs = 2,
                    //    DepartmentMaintenance = 3,
                    //    DepartmentManagement = 4,
                    //    DepartmentLoanInterest = 5


                    TotalForCategory(transactionsForYear, g.TransactionsForThatYear, "Income", money.Categories.FindCategoryById(building.CategoryForIncome));
                    TotalForCategory(transactionsForYear, g.TransactionsForThatYear, "Taxes", money.Categories.FindCategoryById(building.CategoryForTaxes));
                    TotalForCategory(transactionsForYear, g.TransactionsForThatYear, "Repairs", money.Categories.FindCategoryById(building.CategoryForRepairs));
                    TotalForCategory(transactionsForYear, g.TransactionsForThatYear, "Maintenance", money.Categories.FindCategoryById(building.CategoryForMaintenance));
                    TotalForCategory(transactionsForYear, g.TransactionsForThatYear, "Management", money.Categories.FindCategoryById(building.CategoryForManagement));
                    TotalForCategory(transactionsForYear, g.TransactionsForThatYear, "Interest", money.Categories.FindCategoryById(building.CategoryForInterest));

                    transactionsForYear.RecalcYears();

                    building.Years[year] = transactionsForYear;
                }
            }
        }

        private static void TotalForCategory(
            RentalBuildingSingleYear transactionsForYear,
            IGrouping<int, Transaction> transactionForSingleBuildingForSingleYear,
            string label,
            Category categoryToFind
            )
        {

            decimal total = 0;

            if (categoryToFind != null)
            {
                foreach (var t in transactionForSingleBuildingForSingleYear)
                {
                    total += GetTotalAmountMatchingThisCategoryId(t, categoryToFind.Id);
                }
            }

            transactionsForYear.Departments.Add(new RentalBuildingSingleYearSingleDepartment()
            {
                Building = transactionsForYear.Building,
                Year = transactionsForYear.Year,
                Name = label,
                DepartmentCategory = categoryToFind,
                Total = total
            });

        }



        private static decimal GetTotalAmountMatchingThisCategoryId(Transaction t, int c)
        {

            if (t.Category != null)
            {
                if (t.IsSplit)
                {
                    decimal totalMatchingInThisSplit = 0;
                    foreach (Split s in t.Splits)
                    {
                        if (s.Category != null && s.Category.IsDescedantOrMatching(c))
                        {
                            totalMatchingInThisSplit += s.Amount;
                        }
                    }
                    return totalMatchingInThisSplit;
                }
                else
                {
                    if (t.Category.IsDescedantOrMatching(c))
                    {
                        return t.Amount;
                    }
                }
            }

            return 0;
        }

        private static bool MatchAnyCategories(RentBuilding b, Transaction t)
        {

            if (t.Category != null && t.Amount != 0)
            {

                if (t.IsSplit)
                {
                    foreach (Split s in t.Splits)
                    {
                        if (s.Amount != 0)
                        {
                            if (IsCategoriesMatched(b, s.Category))
                            {
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    return IsCategoriesMatched(b, t.Category);
                }
            }

            return false;
        }

        private static bool IsCategoriesMatched(RentBuilding b, Category c)
        {
            List<int> toMatch = new List<int>();
            toMatch.Add(b.CategoryForIncome);
            toMatch.Add(b.CategoryForTaxes);
            toMatch.Add(b.CategoryForInterest);
            toMatch.Add(b.CategoryForManagement);
            toMatch.Add(b.CategoryForMaintenance);
            toMatch.Add(b.CategoryForRepairs);

            return c != null && c.IsDescedantOrMatching(toMatch);
        }
        #region ICollection



        public bool Contains(RentBuilding item)
        {
            return this.rentBuildings.ContainsKey(item.GetUniqueKey());
        }

        public void CopyTo(RentBuilding[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public override void Add(object child)
        {
            Add((RentBuilding)child);
        }

        public override void RemoveChild(PersistentObject pe)
        {
            Remove((RentBuilding)pe);
        }

        public bool Remove(RentBuilding item)
        {
            return this.RemoveBuilding(item);
        }

        public new IEnumerator<RentBuilding> GetEnumerator()
        {
            foreach (RentBuilding r in this.rentBuildings.Values)
            {
                yield return r;
            }
        }

        protected override IEnumerator<PersistentObject> InternalGetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion
    }


    //================================================================================
    [TableMapping(TableName = "RentUnits")]
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class RentUnit : PersistentObject
    {
        #region PRIVATE PROPERTIES

        int id;
        int building;
        string name;
        string renter;
        string note;
        #endregion


        #region PERSISTED PROPERTIES

        [XmlAttribute]
        [DataMember]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true)]
        public int Id
        {
            get { return this.id; }
            set
            {
                if (this.id != value)
                {
                    this.id = value;
                    OnChanged("Id");
                }
            }
        }

        [XmlAttribute]
        [DataMember]
        [ColumnMapping(ColumnName = "Building")]
        public int Building
        {
            get { return this.building; }
            set
            {
                if (this.building != value)
                {
                    this.building = value;
                    OnChanged("Building");
                }
            }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "Name", MaxLength = 255)]
        public string Name
        {
            get { return this.name; }
            set
            {
                if (this.name != value)
                {
                    this.name = value;
                    OnChanged("Name");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Renter", MaxLength = 255, AllowNulls = true)]
        public string Renter
        {
            get { return this.renter; }
            set
            {
                if (this.renter != value)
                {
                    this.renter = value;
                    OnChanged("Renter");
                }
            }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "Note", MaxLength = 255, AllowNulls = true)]
        public string Note
        {
            get { return this.note; }
            set
            {
                if (this.note != value)
                {
                    this.note = value;
                    OnChanged("Note");
                }
            }
        }
        #endregion




        public RentUnit()
        {
            // for serialization only
        }

        public RentUnit(RentUnits container)
            : base(container)
        {
        }

        public override string ToString()
        {
            return string.Format("{0}:{1} {2} {3}", this.id, this.building, this.Name, this.renter);
        }

        public static RentUnit Deserialize(string xml)
        {
            RentUnit x = null;
            try
            {
                DataContractSerializer xs = new DataContractSerializer(typeof(RentBuilding));
                using (StringReader sr = new StringReader(xml))
                {
                    XmlTextReader r = new XmlTextReader(sr);
                    x = (RentUnit)xs.ReadObject(r);
                    r.Close();
                }
            }
            catch
            {
            }
            return x;
        }
    }


    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class RentUnits : PersistentContainer, ICollection<RentUnit>
    {
        int nextUnit;
        Hashtable<int, RentUnit> collection = new Hashtable<int, RentUnit>();


        // for serialization only
        public RentUnits()
        {
        }
        public RentUnits(PersistentObject parent)
            : base(parent)
        {
        }

        public void Clear()
        {
            if (this.nextUnit != 0 || this.collection.Count != 0)
            {
                this.nextUnit = 0;
                this.collection = new Hashtable<int, RentUnit>();
                this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }


        public int Count
        {
            get { return this.collection.Count; }
        }


        public void AddRentUnit(RentUnit x)
        {
            lock (collection)
            {
                x.Parent = this;
                this.collection[x.Id] = x;
            }
        }

        public bool Remove(RentUnit x)
        {
            lock (collection)
            {
                if (this.collection.ContainsKey(x.Id))
                {
                    this.collection.Remove(x.Id);
                }
            }
            x.OnDelete();
            return true;
        }

        public RentUnit Get(int id)
        {
            lock (collection)
            {
                if (this.collection.ContainsKey(id))
                {
                    return this.collection[id] as RentUnit;
                }
            }
            return null;
        }
        public List<RentUnit> GetList()
        {
            List<RentUnit> list = new List<RentUnit>();

            lock (collection)
            {
                foreach (RentUnit x in this.collection.Values)
                {
                    if (!x.IsDeleted)
                    {
                        list.Add(x);
                    }
                }
            }
            return list;
        }


        #region ICollection

        public void Add(RentUnit item)
        {
            this.AddRentUnit(item);
        }

        public override void Add(object child)
        {
            Add((RentUnit)child);
        }

        public override void RemoveChild(PersistentObject pe)
        {
            this.Remove((RentUnit)pe);
        }

        public bool Contains(RentUnit item)
        {
            return this.collection.ContainsKey(item.Id);
        }

        public void CopyTo(RentUnit[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public new IEnumerator<RentUnit> GetEnumerator()
        {
            foreach (RentUnit ru in this.collection.Values)
            {
                yield return ru;
            }
        }

        protected override IEnumerator<PersistentObject> InternalGetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion
    }


    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Categories : PersistentContainer, ICollection<Category>
    {
        int nextCategory;
        Hashtable<int, Category> categories = new Hashtable<int, Category>();
        Hashtable<string, Category> categoryIndex = new Hashtable<string, Category>();

        // for serialization only
        public Categories()
        {
        }

        public Categories(PersistentObject parent)
            : base(parent)
        {
        }

        [XmlIgnore]
        public Category Split
        {
            get
            {
                return GetOrCreateCategory("Split", CategoryType.None);
            }
        }

        [XmlIgnore]
        public Category SalesTax
        {
            get
            {
                return GetOrCreateCategory("Taxes:Sales Tax", CategoryType.Expense);
            }
        }

        [XmlIgnore]
        public Category InterestEarned
        {
            get { return this.GetOrCreateCategory("Savings:Interest", CategoryType.Income); }
        }

        [XmlIgnore]
        public Category Savings
        {
            get { return this.GetOrCreateCategory("Savings", CategoryType.Income); }
        }

        [XmlIgnore]
        public Category InvestmentCredit
        {
            get { return this.GetOrCreateCategory("Investments:Credit", CategoryType.Income); }
        }

        [XmlIgnore]
        public Category InvestmentDebit
        {
            get { return this.GetOrCreateCategory("Investments:Debit", CategoryType.Expense); }
        }

        [XmlIgnore]
        public Category InvestmentInterest
        {
            get { return this.GetOrCreateCategory("Investments:Interest", CategoryType.Income); }
        }

        [XmlIgnore]
        public Category InvestmentDividends
        {
            get { return this.GetOrCreateCategory("Investments:Dividends", CategoryType.Income); }
        }

        [XmlIgnore]
        public Category InvestmentFees
        {
            get { return this.GetOrCreateCategory("Investments:Fees", CategoryType.Expense); }
        }

        [XmlIgnore]
        public Category InvestmentMutualFunds
        {
            get { return this.GetOrCreateCategory("Investments:Mutual Funds", CategoryType.Expense); }
        }

        [XmlIgnore]
        public Category InvestmentStocks
        {
            get { return this.GetOrCreateCategory("Investments:Stocks", CategoryType.Expense); }
        }

        [XmlIgnore]
        public Category InvestmentOther
        {
            get { return this.GetOrCreateCategory("Investments:Other", CategoryType.Expense); }
        }

        [XmlIgnore]
        public Category InvestmentBonds
        {
            get { return this.GetOrCreateCategory("Investments:Bonds", CategoryType.Expense); }
        }
        [XmlIgnore]
        public Category InvestmentOptions
        {
            get { return this.GetOrCreateCategory("Investments:Options", CategoryType.Expense); }
        }
        [XmlIgnore]
        public Category InvestmentTransfer
        {
            get { return this.GetOrCreateCategory("Investments:Transfer", CategoryType.None); }
        }
        [XmlIgnore]
        public Category InvestmentReinvest
        {
            get { return this.GetOrCreateCategory("Investments:Reinvest", CategoryType.None); }
        }


        [XmlIgnore]
        public Category InvestmentLongTermCapitalGainsDistribution
        {
            get { return this.GetOrCreateCategory("Investments:Long Term Capital Gains Distribution", CategoryType.Income); }
        }

        [XmlIgnore]
        public Category InvestmentShortTermCapitalGainsDistribution
        {
            get { return this.GetOrCreateCategory("Investments:Short Term Capital Gains Distribution", CategoryType.Income); }
        }

        [XmlIgnore]
        public Category InvestmentMiscellaneous
        {
            get { return this.GetOrCreateCategory("Investments:Miscellaneous", CategoryType.Expense); }
        }

        [XmlIgnore]
        public Category TransferToDeletedAccount
        {
            get { return this.GetOrCreateCategory("Xfer to Deleted Account", CategoryType.None); }
        }

        [XmlIgnore]
        public Category TransferFromDeletedAccount
        {
            get { return this.GetOrCreateCategory("Xfer from Deleted Account", CategoryType.None); }
        }

        [XmlIgnore]
        public Category Transfer
        {
            get { return this.GetOrCreateCategory("Transfer", CategoryType.None); }
        }

        [XmlIgnore]
        public Category Unknown
        {
            get { return this.GetOrCreateCategory("Unknown", CategoryType.None); }
        }

        public void Clear()
        {
            if (this.nextCategory != 0 || this.categories.Count != 0 || this.categoryIndex.Count != 0)
            {
                this.nextCategory = 0;
                this.categories = new Hashtable<int, Category>();
                this.categoryIndex = new Hashtable<string, Category>();
                this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }

        // Categories
        public void AddCategory(Category result)
        {
            if (result.Id == -1)
            {
                result.Id = this.nextCategory++;
                result.OnInserted();
            }
            else if (this.nextCategory <= result.Id)
            {
                this.nextCategory = result.Id + 1;
            }
            this.categories[result.Id] = result;
            if (!string.IsNullOrEmpty(result.Name))
            {
                OnNameChanged(result, null, result.Name);
            }
        }

        public override void OnNameChanged(object o, string oldName, string newName)
        {
            if (oldName != null && categoryIndex.ContainsKey(oldName))
                categoryIndex.Remove(oldName);
            categoryIndex[newName] = (Category)o;
        }

        public Category FindCategory(string name)
        {
            if (name == null || name.Length == 0) return null;
            return (Category)this.categoryIndex[name];
        }

        public Category GetOrCreateCategory(string name, CategoryType type)
        {
            if (name == null || name.Length == 0) return null;

            Category result = null;
            this.categoryIndex.TryGetValue(name, out result);
            if (result == null)
            {
                result = new Category(this);
                result.Type = type;
                AddCategory(result);
                result.Name = name;
                AddParents(result);
                this.categoryIndex[name] = result;
                this.FireChangeEvent(this, result, null, ChangeType.Inserted);
            }
            return result;
        }

        public Category FindCategoryById(int id)
        {
            return (Category)this.categories[id];
        }

        // todo: there should be no references left at this point...
        public bool RemoveCategory(Category c)
        {
            if (c.IsInserted)
            {
                // then we can remove it
                if (this.categories.ContainsKey(c.Id))
                    this.categories.Remove(c.Id);
            }

            if (this.categoryIndex.ContainsKey(c.Name))
                this.categoryIndex.Remove(c.Name);

            // mark it for deletion on next save
            c.OnDelete();
            return true;
        }

        public IList<Category> GetCategories()
        {
            List<Category> list = new List<Category>(this.categories.Count);
            foreach (Category c in this.categories.Values)
            {
                if (!c.IsDeleted)
                {
                    list.Add(c);
                }
            }
            list.Sort(new Comparison<Category>(CategoryComparer));
            return list;
        }

        public List<Category> GetRootCategories()
        {
            HashSet<Category> visited = new HashSet<Category>();
            List<Category> list = new List<Category>();
            foreach (Category c in this.categories.Values)
            {
                Category r = c.Root;
                if (!c.IsDeleted && !visited.Contains(r))
                {
                    list.Add(r);
                    visited.Add(r);
                }
            }
            list.Sort(new Comparison<Category>(CategoryComparer));
            return list;
        }

        /// <summary>
        /// Replace the parent of the given category with a new parent.
        /// </summary>
        /// <param name="category">The category to change</param>
        /// <param name="oldParent">The old parent we are removing</param>
        /// <param name="newParent">The new parent we are inserting</param>
        /// <returns>The new updated category</returns>
        public Category ReParent(Category category, Category oldParent, Category newParent)
        {
            Debug.Assert(oldParent.Contains(category));

            if (category == oldParent)
            {
                return newParent;
            }

            string name = category.Name;
            string oldname = oldParent.Name;
            Debug.Assert(name.Length > oldname.Length);

            string tail = name.Substring(oldname.Length);
            string newname = newParent.Name + tail;

            Category c = this.FindCategory(newname);
            if (c == null)
            {
                c = this.GetOrCreateCategory(newname, category.Type);
                c.Color = category.Color;
                c.Budget = category.Budget;
                c.BudgetRange = category.BudgetRange;
                c.Balance = category.Balance;
                c.Description = category.Description;
                c.TaxRefNum = category.TaxRefNum;
            }
            return c;
        }

        private int CategoryComparer(Category a, Category b)
        {
            if (a == null && b != null) return -1;
            if (a != null && b == null) return 1;
            string n = a.Name;
            string m = b.Name;
            if (n == null && m != null) return -1;
            if (n != null && m == null) return 1;
            return n.CompareTo(m);
        }

        public IList<Category> AllCategories
        {
            get
            {
                return GetCategories();
            }
        }

        void AddParents(Category c)
        {
            string name = c.Name;
            int i = name.LastIndexOf(":");
            bool foundParent = false;
            while (i > 0 && !foundParent)
            {
                string pname = name.Substring(0, i);
                Category parent = FindCategory(pname);
                foundParent = parent != null;
                if (!foundParent)
                {
                    parent = GetOrCreateCategory(pname, c.Type);
                }
                parent.AddSubcategory(c);
                c = parent;
                name = pname;
                i = name.LastIndexOf(":");
            }
        }

        public override bool FireChangeEvent(Object sender, object item, string name, ChangeType type)
        {
            if (sender == this && type == ChangeType.Reloaded)
            {
                FixParents();
            }
            return base.FireChangeEvent(sender, item, name, type);
        }

        internal void FixParents()
        {
            foreach (Category c in GetCategories())
            {
                int parentId = c.ParentId;
                if (parentId != -1)
                {
                    Category m = this.FindCategoryById(parentId);
                    if (m == null)
                    {
                        AddParents(c);
                    }
                    else if (m != c)
                    {
                        m.PrivateAddSubcategory(c);
                    }
                }
            }
        }


        #region ICollection

        public void Add(Category item)
        {
            AddCategory(item);
        }

        public int Count
        {
            get { return this.categories.Count; }
        }

        public bool Contains(Category item)
        {
            return this.categories.ContainsKey(item.Id);
        }

        public void CopyTo(Category[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public override void Add(object child)
        {
            Add((Category)child);
        }

        public override void RemoveChild(PersistentObject pe)
        {
            RemoveCategory((Category)pe);
        }

        public bool Remove(Category item)
        {
            return RemoveCategory(item);
        }

        public new IEnumerator<Category> GetEnumerator()
        {
            foreach (Category c in this.categories.Values)
            {
                yield return c;
            }
        }
        #endregion

        protected override IEnumerator<PersistentObject> InternalGetEnumerator()
        {
            return GetEnumerator();
        }

        internal void ComputeCategoryBalance()
        {
            MyMoney money = (MyMoney)this.Parent;
            money.BeginUpdate(this);

            foreach (Category c in this.GetRootCategories())
            {
                c.ClearBalance();
            }

            foreach (Transaction t in money.Transactions.GetAllTransactionsByDate())
            {
                if (t.Status == TransactionStatus.Void || t.Account == null || !t.BudgetBalanceDate.HasValue || !t.Account.IsBudgeted)
                {
                    continue;
                }

                if (t.Account != null && t.Account.IsCategoryFund)
                {
                    Category c = t.Account.GetFundCategory();
                    c.Root.Balance += t.Amount;
                }
                else if (t.IsSplit)
                {
                    foreach (Split s in t.Splits)
                    {
                        Category c = s.Category;
                        if (c != null)
                        {
                            c.Root.Balance += t.Amount;
                        }
                    }
                }
                else
                {
                    Category c = t.Category;
                    if (c != null)
                    {
                        c.Root.Balance += t.Amount;
                    }
                }
            }

            money.EndUpdate();
        }

        // import a category from another Money database.
        internal Category ImportCategory(Category category)
        {
            if (category == null || string.IsNullOrEmpty(category.Name))
            {
                return null;
            }
            Category ic = FindCategory(category.Name);
            if (ic == null)
            {
                ic = GetOrCreateCategory(category.Name, category.Type);
                ic.Color = category.Color;
                ic.Description = category.Description;
                ic.TaxRefNum = category.TaxRefNum;
            }
            return ic;
        }
    }

    public enum CategoryType
    {
        None,
        Income,
        Expense,
        Savings,
        Reserved, // this is not used (but hard to delete because of database).
        Transfer, // special category only used by pie charts
        Investments // so you can separate out investment income and expenditures.
    }

    public enum CalendarRange
    {
        None,
        Daily,
        Weekly,
        BiWeekly,
        Monthly,
        BiMonthly,
        TriMonthly,
        Quarterly,
        SemiAnnually,
        Annually
    }

    //================================================================================
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    [TableMapping(TableName = "Categories")]
    public class Category : PersistentObject
    {
        int id = -1;
        string label;
        string name; // full name
        string description;
        CategoryType type;
        decimal budget;
        CalendarRange range;
        decimal balance;
        bool isEditing;
        string colorString;
        int taxRefNum;

        int parentid = -1;
        Category parent; // parent category
        ThreadSafeObservableCollection<Category> subcategories;

        public Category()
        {
            // for serialization
        }

        public Category(Categories container) : base(container) { }

        [XmlAttribute]
        [DataMember]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true)]
        public int Id
        {
            get { return this.id; }
            set
            {
                if (this.id != value)
                {
                    this.id = value; OnChanged("Id");
                }
            }
        }

        public override void OnDelete()
        {
            if (this.subcategories != null)
            {
                for (int i = this.subcategories.Count - 1; i >= 0; i--)
                {
                    Category c = this.subcategories[i] as Category;
                    c.OnDelete();
                }
            }
            base.OnDelete();
            if (this.parent != null)
            {
                this.parent.RemoveSubcategory(this);
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "ParentId", AllowNulls = true)]
        public int ParentId
        {
            get { return (this.parent != null) ? this.parent.Id : this.parentid; }
            set { this.parentid = value; }
        }

        [XmlIgnore]
        public Category ParentCategory
        {
            get { return this.parent; }
            set
            {
                if (this.parent != value)
                {
                    if (this.parent != null)
                    {
                        this.parent.RemoveSubcategory(this);
                    }
                    this.parent = value;

                    if (value != null)
                    {
                        value.AddSubcategory(this);
                    }
                    OnChanged("ParentCategory");
                    OnChanged("InheritedColor");
                }
            }
        }

        [XmlIgnore]
        public Category Root
        {
            get
            {
                if (this.parent == null) return this;
                return this.parent.Root;
            }
        }

        public static string Combine(string name, string suffix)
        {
            if (name == null) return suffix;
            return name.Trim() + ":" + suffix.Trim();
        }

        public string GetFullName()
        {
            if (this.parent == null)
            {
                return this.label;
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(this.label);
                Category p = this.parent;
                while (p != null)
                {
                    sb.Insert(0, ':');
                    sb.Insert(0, p.label);
                    p = p.parent;
                }
                return sb.ToString();
            }
        }

        public string GetFullNameOfParent()
        {
            if (this.parent == null)
            {
                return string.Empty;
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                Category p = this.parent;
                while (p != null)
                {
                    sb.Insert(0, ':');
                    sb.Insert(0, p.label);
                    p = p.parent;
                }
                return sb.ToString();
            }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "Name", MaxLength = 80)]
        public string Name
        {
            get
            {
                if (this.name == null)
                {
                    this.name = GetFullName();
                }
                return this.name;
            }
            set
            {
                if (this.name != value)
                {
                    if (value[0] == '[')
                    {
                        throw new MoneyException("Invalid category name");
                    }

                    string old = this.name;
                    this.name = Truncate(value, 80);
                    this.label = null;
                    this.label = this.Label;
                    if (this.Parent != null)
                    {
                        this.Parent.BeginUpdate(true);
                        try
                        {
                            OnNameChanged(old);
                        }
                        finally
                        {
                            this.Parent.EndUpdate();
                        }
                    }
                    else
                    {
                        OnNameChanged(old);
                    }
                }
            }
        }

        void OnNameChanged(string old)
        {
            RenameSubcategories();
            OnNameChanged(old, this.name);
            OnChanged("Name");
        }

        [XmlIgnore]
        public string Label
        {
            get
            {
                if (this.label == null)
                {
                    if (this.name != null)
                    {
                        int i = this.name.LastIndexOf(':');
                        if (i >= 0)
                        {
                            this.label = this.name.Substring(i + 1);
                        }
                        else
                        {
                            this.label = this.name;
                        }
                    }
                }
                return this.label;
            }
            set
            {
                if (this.label != value)
                {
                    // Set the new name
                    this.label = value;
                    string fullNameNew = this.GetFullName();

                    this.Name = fullNameNew;

                    OnChanged("Label");

                }
            }
        }

        [XmlIgnore]
        public string Prefix
        {
            get
            {
                int indexFromRight = Name.LastIndexOf(':');
                if (indexFromRight == -1)
                {
                    return string.Empty;
                }
                return Name.Substring(0, indexFromRight + 1);
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Description", MaxLength = 255, AllowNulls = true)]
        public string Description
        {
            get { return this.description; }
            set
            {
                if (this.description != value)
                {
                    this.description = Truncate(value, 255); OnChanged("Description");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Type", SqlType = typeof(SqlInt32))]
        public CategoryType Type
        {
            get { return this.type; }
            set
            {
                if (this.type != value)
                {
                    this.type = value; OnChanged("Type");
                }
            }
        }

        /// <summary>
        /// User settable color for this category (defaults to parent color if not defined).
        /// </summary>
        [DataMember]
        [ColumnMapping(ColumnName = "Color", MaxLength = 10, AllowNulls = true)]
        public string Color
        {
            get
            {
                return this.colorString;
            }

            set
            {
                string s = value;
                if (string.IsNullOrWhiteSpace(s)) // database sometimes loads the string "        ".
                {
                    s = null;
                }
                this.colorString = s;
                OnChanged("Color");
                NotifySubcategoriesChanged("InheritedColor");
            }
        }

        private void NotifySubcategoriesChanged(string name)
        {
            RaisePropertyChanged(name);
            if (this.subcategories != null)
            {
                foreach (Category c in this.subcategories)
                {
                    c.NotifySubcategoriesChanged(name);
                }
            }
        }

        [XmlIgnore]
        public string InheritedColor
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(this.colorString))
                {
                    return this.colorString;
                }
                else if (this.parent != null)
                {
                    return this.parent.InheritedColor;
                }
                else if (this.Name == "Unknown" || this.Name == "Xfer to Deleted Account")
                {
                    return "#000000"; // black
                }
                // return the name of the category so that the CategoryToBrush converter can generate
                // a named color for this category.
                return this.Name;
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Budget", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Budget
        {
            get { return this.budget; }
            set
            {
                if (this.budget != value)
                {
                    this.budget = value;
                    OnChanged("Budget");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Balance", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Balance
        {
            get { return this.balance; }
            set
            {
                if (this.balance != value)
                {
                    decimal difference = value - this.balance;
                    this.balance = value;
                    OnChanged("Balance");
                    // propagate balance up the tree.
                    if (this.parent != null)
                    {
                        this.parent.Balance += difference;
                    }
                }
            }
        }

        public void ClearBalance()
        {
            this.balance = 0;
            OnChanged("Balance");
            if (HasSubcategories)
            {
                foreach (Category c in this.subcategories)
                {
                    c.ClearBalance();
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Frequency", SqlType = typeof(SqlInt32), AllowNulls = true)]
        public CalendarRange BudgetRange
        {
            get { return this.range; }
            set
            {
                if (this.range != value)
                {
                    this.range = value;
                    OnChanged("Frequency");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "TaxRefNum", AllowNulls = true)]
        public int TaxRefNum
        {
            get { return this.taxRefNum; }
            set
            {
                if (this.taxRefNum != value)
                {
                    this.taxRefNum = value;
                    OnChanged("TaxRefNum");
                }
            }
        }

        public override string ToString()
        {
            return this.Name;
        }

        public void PrivateAddSubcategory(Category c)
        {
            if (c == this || this.IsDescedantOrMatching(c.Id))
            {
                return;
            }

            c.parent = this;
            if (subcategories == null) subcategories = new ThreadSafeObservableCollection<Category>();
            subcategories.Add(c);
        }

        public void AddSubcategory(Category c)
        {
            if (c.parent != null)
            {
                c.parent.RemoveSubcategory(c);
            }
            c.parent = this;
            if (subcategories == null) subcategories = new ThreadSafeObservableCollection<Category>();
            subcategories.Add(c);
            c.Name = c.GetFullName();
            this.FireChangeEvent(this, null, ChangeType.ChildChanged);
        }

        public void RemoveSubcategory(Category c)
        {
            Debug.Assert(subcategories != null);
            if (subcategories == null)
            {
                return;
            }
            this.subcategories.Remove(c);
            c.parent = null;
            c.Name = c.GetFullName();
            this.FireChangeEvent(this, null, ChangeType.ChildChanged);
        }

        public IList<Category> GetSubcategories()
        {
            return this.subcategories;
        }

        [XmlIgnore]
        [IgnoreDataMemberAttribute]
        public IList<Category> Subcategories
        {
            get { return this.subcategories; }
        }

        [XmlIgnore]
        [IgnoreDataMemberAttribute]
        public bool HasSubcategories { get { return this.subcategories != null; } }

        public bool IsDescedantOrMatching(int categoryId)
        {
            if (id == categoryId)
            {
                return true;
            }

            if (this.parent != null && this.parent.IsDescedantOrMatching(categoryId))
            {
                return true;
            }
            return false;
        }

        public bool IsDescedantOrMatching(List<int> categoriesIdToMatch)
        {

            if (categoriesIdToMatch.Contains(id))
            {
                return true;
            }

            if (this.parent != null && this.parent.IsDescedantOrMatching(categoriesIdToMatch))
            {
                return true;
            }
            return false;
        }


        /// <summary>
        /// Returns true if this category is equal to or is a parent of the given child category.
        /// For example: "Auto:Insurance".Contains("Auto") returns false, but "Auto".Contains("Auto:Insurance")
        /// returns true.
        /// </summary>
        /// <param name="child"></param>
        /// <returns></returns>
        public bool Contains(Category child)
        {
            if (child == null) return false;
            if (child == this) return true;
            do
            {
                child = child.ParentCategory;
                if (child == this) return true;
            } while (child != null);
            return false;
        }

        /// <summary>
        /// Returns true if this category contains the given string in any of it's name parts.
        /// For example: "Auto:Insurance:Volvo".Contains("Insurance") returns true as does 
        /// "Auto:Insurance:Volvo".Contains("Auto") and "Auto:Insurance:Volvo".Contains("Volvo").
        /// </summary>
        /// <param name="substring"></param>
        /// <returns></returns>
        public bool Contains(string substring)
        {
            if (substring == null) return false;
            Category c = this;
            do
            {
                if (substring == c.Label) return true;
                c = c.ParentCategory;
            } while (c != null);
            return false;
        }


        void RenameSubcategories()
        {
            if (this.subcategories != null)
            {
                foreach (Category c in this.subcategories)
                {
                    c.Name = c.GetFullName();
                    c.RenameSubcategories();
                }
            }
        }

        static public decimal RangeToDaily(decimal budget, CalendarRange r, int years)
        {
            switch (r)
            {
                case CalendarRange.Annually:
                    return budget / (years * 365);
                case CalendarRange.BiMonthly:
                    return (budget * 6) / 365;
                case CalendarRange.Daily:
                    return budget;
                case CalendarRange.Monthly:
                    return (budget * 12) / 365;
                case CalendarRange.Quarterly:
                    return (budget * 3) / 365;
                case CalendarRange.SemiAnnually:
                    return (budget * 2) / 365;
                case CalendarRange.TriMonthly:
                    return (budget * 4) / 365;
                case CalendarRange.Weekly:
                    return (budget * 52) / 365;
                case CalendarRange.BiWeekly:
                    return (budget * 26) / 365;
            }
            return 0;
        }

        static public decimal DailyToRange(decimal budget, CalendarRange r, int years)
        {
            switch (r)
            {
                case CalendarRange.Annually:
                    return budget * years * 365;
                case CalendarRange.BiMonthly:
                    return (budget * 365) / 6;
                case CalendarRange.Daily:
                    return budget;
                case CalendarRange.Monthly:
                    return (budget * 365) / 12;
                case CalendarRange.Quarterly:
                    return (budget * 365) / 3;
                case CalendarRange.SemiAnnually:
                    return (budget * 365) / 2;
                case CalendarRange.TriMonthly:
                    return (budget * 365) / 4;
                case CalendarRange.Weekly:
                    return (budget * 365) / 52;
                case CalendarRange.BiWeekly:
                    return (budget * 365) / 26;
            }
            return 0;
        }

        public bool Matches(object o, Operation op)
        {
            if (o == null) return false;
            string s = o.ToString();
            switch (op)
            {
                case Operation.None:
                    return false;
                case Operation.Contains:
                    return this.Contains(s);
                case Operation.Equals:
                    return this.Name == s;
                case Operation.GreaterThan:
                    return s.CompareTo(this.Name) < 0;
                case Operation.GreaterThanEquals:
                    return s.CompareTo(this.Name) <= 0;
                case Operation.LessThan:
                    return s.CompareTo(this.Name) > 0;
                case Operation.LessThanEquals:
                    return s.CompareTo(this.Name) >= 0;
                case Operation.NotContains:
                    return !this.Contains(s);
                case Operation.NotEquals:
                    return s != this.Name;
            }
            return false;
        }

        /// <summary>
        /// Whether category name is being edited.
        /// </summary>
        [XmlIgnore]
        public bool IsEditing
        {
            get { return isEditing; }
            set
            {
                if (isEditing != value)
                {
                    isEditing = value;
                    OnChanged("IsEditing");
                }
            }
        }

    }

    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Securities : PersistentContainer, ICollection<Security>
    {
        int nextSecurity;
        Hashtable<int, Security> securities = new Hashtable<int, Security>();
        Hashtable<string, Security> securityIndex = new Hashtable<string, Security>();


        // for serialization only
        public Securities()
        {
        }
        public Securities(PersistentObject parent)
            : base(parent)
        {
        }
        public void Clear()
        {
            if (this.nextSecurity != 0 || this.securities.Count != 0 || this.securityIndex.Count != 0)
            {
                this.nextSecurity = 0;
                this.securities = new Hashtable<int, Security>();
                this.securityIndex = new Hashtable<string, Security>();
                this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }

        public Security NewSecurity()
        {
            Security s = new Security(this);
            s.Id = this.nextSecurity++;
            this.securities[s.Id] = s;
            this.FireChangeEvent(this, s, null, ChangeType.Inserted);
            return s;
        }

        // Securities
        public Security AddSecurity(int id)
        {
            Security s = new Security(this);
            s.Id = id;
            if (this.nextSecurity <= id) this.nextSecurity = id + 1;
            this.securities[id] = s;
            this.FireChangeEvent(this, s, null, ChangeType.Inserted);
            return s;
        }
        public Security AddSecurity(Security s)
        {
            s.Parent = this;
            if (s.Id == -1)
            {
                s.Id = nextSecurity++;
                s.OnInserted();
            }
            else if (nextSecurity <= s.Id)
            {
                nextSecurity = s.Id + 1;
            }
            this.securities[s.Id] = s;
            OnNameChanged(s, null, s.Name);
            this.FireChangeEvent(this, s, null, ChangeType.Inserted);
            return s;
        }

        public override void OnNameChanged(object o, string oldName, string newName)
        {
            if (oldName != null && securityIndex.ContainsKey(oldName))
            {
                securityIndex.Remove(oldName);
            }
            if (!string.IsNullOrEmpty(newName))
            {
                securityIndex[newName] = (Security)o;
            }
        }

        public Security FindSecurity(string name, bool add)
        {
            if (name == null || name.Length == 0) return null;
            Security result = null;
            result = (Security)this.securityIndex[name];
            if (result == null && add)
            {
                result = AddSecurity(this.nextSecurity);
                result.Name = name;
                this.securityIndex[name] = result;
                this.FireChangeEvent(this, result, null, ChangeType.Inserted);
            }
            return result;
        }

        public Security FindSecurityById(string name)
        {
            name = name.Trim();
            if (name == null || name.Length == 0) return null;
            foreach (Security s in this.securities.Values)
            {
                if (s.CuspId != null && string.Compare(s.CuspId, name, StringComparison.InvariantCultureIgnoreCase) == 0)
                    return s;
            }
            return null;
        }

        public Security FindSymbol(string name, bool add)
        {
            name = name.Trim();
            if (name == null || name.Length == 0) return null;
            foreach (Security s in this.securities.Values)
            {
                if (string.Compare(s.Symbol, name, StringComparison.InvariantCultureIgnoreCase) == 0)
                    return s;
            }
            Security result = null;
            if (add)
            {
                result = AddSecurity(this.nextSecurity);
                result.Symbol = name;
                result.Name = name;
                this.FireChangeEvent(this, result, null, ChangeType.Inserted);
            }
            return result;
        }

        public Security FindSecurityAt(int id)
        {
            return (Security)this.securities[id];
        }

        // todo: there should be no references left at this point...
        public bool RemoveSecurity(Security s)
        {
            if (s.IsInserted)
            {
                // then we can remove it immediately.
                if (this.securities.ContainsKey(s.Id))
                    this.securities.Remove(s.Id);
            }

            string name = s.Name;
            if (!string.IsNullOrEmpty(name) && this.securityIndex.ContainsKey(name))
                this.securityIndex.Remove(name);

            // mark it for removal on next save
            s.OnDelete();

            // remove any stock split information.
            MyMoney money = this.Parent as MyMoney;
            money.StockSplits.OnSecurityRemoved(s);

            return true;
        }

        public List<Security> GetSecurities()
        {
            List<Security> list = new List<Security>(this.securities.Count);
            foreach (Security s in this.securities.Values)
            {
                if (!s.IsDeleted && !string.IsNullOrWhiteSpace(s.Name))
                {
                    list.Add(s);
                }
            }
            return list;
        }

        public List<Security> GetSortedSecurities()
        {
            List<Security> list = GetSecurities();
            list.Sort(Security.Compare);
            return list;
        }

        public List<Security> AllSecurities
        {
            get
            {
                return GetSecurities();
            }
        }

        public List<Security> GetSecuritiesAsList()
        {
            List<Security> list = new List<Security>();
            lock (securities)
            {
                foreach (Security x in this.securities.Values)
                {
                    if (!x.IsDeleted)
                    {
                        list.Add(x);
                    }
                }
            }
            return list;
        }

        public List<Security> GetSecuritiesAsList(string filter)
        {
            string lower = StringHelpers.SafeLower(filter);
            List<Security> list = new List<Security>();
            lock (securities)
            {
                foreach (Security x in this.securities.Values)
                {
                    if (!x.IsDeleted && (string.IsNullOrEmpty(lower) || StringHelpers.SafeLower(x.Name).Contains(lower) || StringHelpers.SafeLower(x.Symbol).Contains(lower)))
                    {
                        list.Add(x);
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Return a sorted ascending list of unique Symbols found in the Securities collection
        /// </summary>
        public IList<string> AllSymbols
        {
            get
            {
                List<string> list = new List<string>();
                foreach (Security s in this.securities.Values)
                {
                    if (!s.IsDeleted && string.IsNullOrWhiteSpace(s.Symbol) == false)
                    {
                        if (list.Contains(s.Symbol))
                        {
                            // This symbol is already in the list
                        }
                        else
                        {
                            list.Add(s.Symbol);
                        }
                    }
                }
                list.Sort(new Comparison<string>((p1, p2) => { return string.Compare(p1, p2); }));
                return list;
            }
        }


        #region ICollection

        public void Add(Security item)
        {
            this.AddSecurity(item);
        }

        public bool Contains(Security item)
        {
            return securities.ContainsKey(item.Id);
        }

        public void CopyTo(Security[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { return securities.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public override void Add(object child)
        {
            Add((Security)child);
        }

        public override void RemoveChild(PersistentObject pe)
        {
            Remove((Security)pe);
        }

        public bool Remove(Security item)
        {
            return this.RemoveSecurity(item);
        }

        public new IEnumerator<Security> GetEnumerator()
        {
            foreach (Security s in this.securities.Values)
            {
                yield return s;
            }
        }

        protected override IEnumerator<PersistentObject> InternalGetEnumerator()
        {
            return GetEnumerator();
        }

        internal Security ImportSecurity(Security security)
        {
            if (security == null || string.IsNullOrEmpty(security.Name))
            {
                return null;
            }
            Security s = FindSecurity(security.Name, true);
            s.Symbol = security.Symbol;
            s.CuspId = security.CuspId;
            s.Price = security.Price;
            s.LastPrice = security.LastPrice;
            s.SecurityType = security.SecurityType;
            s.Taxable = security.Taxable;
            s.PriceDate = security.PriceDate;
            // todo: merge stock splits...
            return s;
        }
        #endregion
    }

    public enum SecurityType
    {
        None,
        Bond, // Bonds
        MutualFund,
        Equity, // stocks
        MoneyMarket, // cash
        ETF, // electronically traded fund
        Reit, // Real estate investment trust
        Futures, // Futures (a type of commodity investment)
    }

    public enum YesNo
    {
        Yes, // default is yes
        No
    }

    //================================================================================
    [TableMapping(TableName = "Securities")]
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Security : PersistentObject
    {
        int id = -1;
        string name;
        string symbol;
        decimal price;
        DateTime priceDate;
        decimal lastPrice;
        string cuspid;
        bool expanded;
        SecurityType type;
        YesNo taxable;

        static Security()
        {
            None = new Security() { Name = "<None>" };
        }

        public Security()
        { // for serialization
        }

        public Security(Securities container) : base(container) { }

        public readonly static Security None;

        [XmlAttribute]
        [DataMember]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true)]
        public int Id
        {
            get { return this.id; }
            set
            {
                if (this.id != value)
                {
                    this.id = value; OnChanged("Id");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Name", MaxLength = 80)]
        public string Name
        {
            get { return this.name; }
            set
            {
                if (this.name != value)
                {
                    string old = this.name;
                    this.name = Truncate(value, 80); OnChanged("Name");
                    OnNameChanged(old, this.name);
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Symbol", MaxLength = 20)]
        public string Symbol
        {
            get { return this.symbol; }
            set
            {
                if (this.symbol != value)
                {
                    this.symbol = Truncate(value, 20);
                    OnChanged("Symbol");
                }
            }
        }

        [XmlIgnore]
        public bool HasSymbol { get { return !string.IsNullOrEmpty(this.symbol); } }

        [DataMember]
        [ColumnMapping(ColumnName = "Price", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Price
        {
            get { return this.price; }
            set { if (this.price != value) { this.price = value; OnChanged("Price"); OnChanged("PercentChange"); OnChanged("IsDown"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "LastPrice", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal LastPrice
        {
            get { return this.lastPrice; }
            set { if (this.lastPrice != value) { this.lastPrice = value; OnChanged("LastPrice"); OnChanged("PercentChange"); OnChanged("IsDown"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "CUSPID", MaxLength = 20, AllowNulls = true)]
        public string CuspId
        {
            get { return this.cuspid; }
            set { if (this.cuspid != value) { this.cuspid = Truncate(value, 20); OnChanged("CuspId"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "SECURITYTYPE", AllowNulls = true)]
        public SecurityType SecurityType
        {
            get { return this.type; }
            set { if (this.type != value) { this.type = value; OnChanged("SecurityType"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "TAXABLE", SqlType = typeof(SqlByte), AllowNulls = true)]
        public YesNo Taxable
        {
            get { return this.taxable; }
            set { if (this.taxable != value) { this.taxable = value; OnChanged("Taxable"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "PriceDate", SqlType = typeof(SqlDateTime), AllowNulls = true)]
        public DateTime PriceDate
        {
            get { return this.priceDate; }
            set { if (this.priceDate != value) { this.priceDate = value; OnChanged("PriceDate"); } }
        }

        [XmlIgnore]
        public decimal PercentChange
        {
            get { return this.lastPrice == 0 ? 0 : (this.price - this.lastPrice) * 100 / this.lastPrice; }
        }

        [XmlIgnore]
        public bool IsDown
        {
            get { return this.price < this.lastPrice; }
        }

        [XmlIgnore]
        public bool IsReadOnly
        {
            get { return false; }
        }


        public override string ToString()
        {
            // bugbug: this is only so that we can show a short symbol in the investment grid, if we need to
            // change this to the Name then we could use a ValueConverter instead.
            if (!string.IsNullOrEmpty(this.Symbol)) return this.Symbol;
            if (!string.IsNullOrEmpty(this.CuspId)) return this.CuspId;
            if (!string.IsNullOrEmpty(this.Name)) return this.Name;
            return "";
        }

        ObservableStockSplits splits;

        /// <summary>
        /// Get a non-null list of stock splits related to this Security
        /// </summary>
        public ObservableStockSplits StockSplits
        {
            get
            {
                if (splits == null)
                {
                    Securities parent = this.Parent as Securities;
                    MyMoney money = parent.Parent as MyMoney;
                    splits = new ObservableStockSplits(this, money.StockSplits);
                }
                return this.splits;
            }
        }

        /// <summary>
        /// Used only for UI binding.
        /// </summary>
        [XmlIgnore]
        public bool IsExpanded
        {
            get { return expanded; }
            set
            {
                if (value != expanded)
                {
                    expanded = value;
                    OnChanged("IsExpanded");
                }
            }
        }

        public static int Compare(Security a, Security b)
        {
            if (a == null && b != null) return -1;
            if (a != null && b == null) return 1;
            if (a == null && b == null) return 0;
            string n = a.Name;
            string m = b.Name;
            if (n == null && m != null) return -1;
            if (n != null && m == null) return 1;
            if (n == null && m == null) return 0;

            return n.CompareTo(m);
        }

        public static string GetSecurityTypeCaption(SecurityType st)
        {
            string caption = string.Empty;
            switch (st)
            {
                case SecurityType.Bond:
                    caption = "Bonds";
                    break;
                case SecurityType.MutualFund:
                    caption = "Mutual Funds";
                    break;
                case SecurityType.Equity:
                    caption = "Equities";
                    break;
                case SecurityType.MoneyMarket:
                    caption = "Money Market";
                    break;
                case SecurityType.ETF:
                    caption = "ETFs";
                    break;
                case SecurityType.Reit:
                    caption = "Reits";
                    break;
                case SecurityType.Futures:
                    caption = "Futures";
                    break;
                default:
                    caption = "Other";
                    break;
            }
            return caption;
        }

        internal bool Merge(Security s2)
        {
            if (!string.IsNullOrEmpty(this.symbol) && !string.IsNullOrEmpty(s2.symbol) && this.symbol != s2.symbol)
            {
                // these are not really duplicates then...
                // the Name might be wrong on one of them, but they have unique ticker symbols.
                return false;
            }
            if (string.IsNullOrEmpty(this.symbol))
            {
                this.Symbol = s2.Symbol;
            }


            if (!string.IsNullOrEmpty(this.cuspid) && !string.IsNullOrEmpty(s2.cuspid) && this.cuspid != s2.cuspid)
            {
                // these are not really duplicates then...
                // the Name might be wrong on one of them, but they have unique cuspids.
                return false;
            }

            if (string.IsNullOrEmpty(this.cuspid))
            {
                this.CuspId = s2.CuspId;
            }

            if (this.splits == null || this.splits.Count == 0)
            {
                if (s2.splits != null)
                {
                    foreach (StockSplit s in s2.splits)
                    {
                        StockSplit copy = new StockSplit();
                        copy.Security = s.Security;
                        copy.Date = s.Date;
                        copy.Numerator = s.Numerator;
                        copy.Denominator = s.Denominator;
                        this.StockSplits.Add(s);
                        s.OnDelete();
                    }
                }
            }

            return true;
        }
    }

    class SecurityComparer : IComparer<Security>
    {
        public int Compare(Security x, Security y)
        {
            return Security.Compare(x, y);
        }
    }

    class SecuritySymbolComparer : IComparer<Security>
    {
        public int Compare(Security a, Security b)
        {
            if (a == null && b != null) return -1;
            if (a != null && b == null) return 1;
            if (a == null && b == null) return 0;
            string n = a.Symbol;
            string m = b.Symbol;
            if (n == null && m != null) return -1;
            if (n != null && m == null) return 1;
            if (n == null && m == null) return 0;
            return n.CompareTo(m);
        }
    }

    //================================================================================
    public enum TransactionStatus
    {
        None,
        Electronic,
        Cleared,
        Reconciled,
        Void,
    }

    //================================================================================
    public class TransactionComparerByDate : IComparer<Transaction>
    {
        public int Compare(Transaction x, Transaction y)
        {
            if (x == null)
            {
                return -1;
            }
            else if (y == null)
            {
                return 1;
            }

            return x.CompareByDate(y);
        }

    }

    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Transactions : PersistentContainer, ICollection<Transaction>
    {
        long nextTransaction;
        Hashtable<long, Transaction> transactions = new Hashtable<long, Transaction>();

        public Transactions()
        { // for serialization
        }

        public Transactions(PersistentObject parent)
            : base(parent)
        {
        }
        public void Clear()
        {
            if (this.nextTransaction != 0 || this.transactions.Count != 0)
            {
                this.nextTransaction = 0;
                this.transactions = new Hashtable<long, Transaction>();
                this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }

        [XmlIgnore]
        public int Count
        {
            get { return this.transactions.Count; }
        }

        // for serialization.
        public Transaction[] Items
        {
            get
            {
                List<Transaction> list = new List<Transaction>();
                foreach (Transaction t in transactions.Values)
                {
                    if (t.IsDeleted)
                        continue;
                    list.Add(t);
                }
                return list.ToArray();
            }
            set
            {
                if (value != null)
                {
                    lock (transactions)
                    {
                        foreach (Transaction t in value)
                        {
                            t.Id = this.nextTransaction++;
                            this.AddTransaction(t);
                        }
                    }
                }
            }
        }

        public Transaction NewTransaction(Account a)
        {
            Transaction t = new Transaction(this);
            t.Account = a;
            t.Date = DateTime.Now;
            return t;
        }

        public Transaction AddTransaction(long id)
        {
            Transaction t = new Transaction(this);
            t.Id = id;

            if (this.transactions.ContainsKey(id))
            {
                throw new Exception("Failed to add transaction with duplicate Id=" + id);
            }

            lock (transactions)
            {
                if (this.nextTransaction <= id) this.nextTransaction = id + 1;
                this.transactions[t.Id] = t;
            }
            this.FireChangeEvent(this, t, null, ChangeType.Inserted);
            return t;
        }

        public void AddTransaction(Transaction t)
        {
            lock (transactions)
            {
                long id = t.Id;

                if (id == -1)
                {
                    t.Id = this.nextTransaction++;
                }
                else
                {
                    // this is the XmlStore.Load scenario where we have to honor the ids found in the XML file.
                    if (this.nextTransaction <= id)
                    {
                        this.nextTransaction = id + 1;
                    }

                    if (this.transactions.ContainsKey(t.Id))
                    {
                        // in this case we have a big problem.
                        throw new Exception("InternalError: Failed to add transaction with duplicate Id=" + t.Id);
                    }
                }

                // since this transaction has a brand new id, it cannot have any attachments.
                t.HasAttachment = false;

                if (this.transactions.ContainsKey(t.Id))
                {
                    // weird, this means our "nextTransaction" got wacked some how, so let's fix it
                    // by finding the real high water mark.
                    ResetNextTransactionId();

                    t.Id = this.nextTransaction++;

                    if (this.transactions.ContainsKey(t.Id))
                    {
                        throw new Exception("InternalError: Failed to add transaction with duplicate Id=" + t.Id);
                    }
                }

                this.transactions[t.Id] = t;
                t.OnInserted();

                if (t.Investment != null)
                {
                    t.Investment.OnInserted();
                }
            }
            this.FireChangeEvent(this, t, null, ChangeType.Inserted);
        }

        private void ResetNextTransactionId()
        {
            this.nextTransaction = 0;

            foreach (Transaction u in this.transactions.Values)
            {
                long id = u.Id;
                if (this.nextTransaction <= id)
                {
                    this.nextTransaction = id + 1;
                }
            }
        }

        public bool RemoveTransaction(Transaction t)
        {
            if (t.IsInserted)
            {
                lock (transactions)
                {
                    // then we can remove it immediately.
                    this.transactions.Remove(t.Id);
                }
            }
            if (t.Investment != null)
            {
                t.Investment.OnDelete();
            }
            if (t.IsSplit)
            {
                t.Splits.RemoveAll();
            }
            // mark it for removal on next save.
            t.OnDelete();
            return true;
        }

        public Transaction FindTransactionById(long id)
        {
            return (Transaction)transactions[id];
        }

        public Transaction FindFITID(string fitid, Account a)
        {
            foreach (Transaction t in transactions.Values)
            {
                if (t.FITID == fitid && t.Account == a)
                    return t;
            }
            return null;
        }

        public Transaction Merge(Aliases aliases, Transaction t, Dictionary<long, Transaction> excluded)
        {
            lock (this.transactions)
            {
                Alias alias = aliases.FindMatchingAlias(t.PayeeName);
                if (alias != null)
                {
                    if (string.IsNullOrEmpty(t.Memo)) t.Memo = t.PayeeName;
                    t.OriginalPayee = t.PayeeName;
                    t.Payee = alias.Payee;
                }

                Transaction u = this.FindMatching(t, excluded);
                if (u != null)
                {

                    // Merge new details with matching transaction.
                    u.FITID = t.FITID;
                    if (string.IsNullOrEmpty(u.Number) && t.Number != null)
                        u.Number = t.Number;
                    if (string.IsNullOrEmpty(u.Memo) && t.Memo != null)
                        u.Memo = t.Memo;
                    if (u.Status == TransactionStatus.None)
                        u.Status = TransactionStatus.Electronic;

                    Investment i = t.Investment;
                    Investment j = u.Investment;
                    if (i != null && j != null)
                    {
                        j.Merge(i);
                    }

                    return u;
                }
            }
            return null;
        }

        // the critical details must match, other details can be filled in by electronic download.
        static bool InvestmentDetailsMatch(Transaction t, Transaction u)
        {
            Investment i = t.Investment;
            Investment j = t.Investment;
            if (i == null) return j == null;
            if (j == null) return false;

            return (i.Security == j.Security && i.Units == j.Units && i.UnitPrice == j.UnitPrice &&
                    i.Type == j.Type && i.TradeType == j.TradeType);
        }

        private bool DatesMatch(Transaction u, Transaction t)
        {
            DateTime udt = u.Date;
            DateTime dt = t.Date;
            // 'u' is a real transaction
            // 't' is a potential merge transaction.

            TimeSpan span = (dt - udt).Duration();
            if (span.TotalHours < 24)
            {
                return true;
            }
            if (u.MergeDate.HasValue)
            {
                // was merged once before, let's see if that date matches t
                udt = u.MergeDate.Value;
                if (udt.Year == dt.Year && udt.Month == dt.Month && udt.Day == dt.Day)
                {
                    return true;
                }
            }
            if (t.MergeDate.HasValue)
            {
                // was merged once before, let's see if that date matches t
                udt = u.Date;
                dt = t.MergeDate.Value;
                if (udt.Year == dt.Year && udt.Month == dt.Month && udt.Day == dt.Day)
                {
                    return true;
                }
            }
            return false;
        }

        public Transaction FindMatching(Transaction t, Dictionary<long, Transaction> excluded)
        {
            Account account = t.Account;
            string number = t.Number;
            DateTime dt = t.Date;
            string payee = t.PayeeName;
            decimal amount = t.Amount;
            string fitid = t.FITID;


            Transaction best = null;
            foreach (Transaction u in transactions.Values)
            {
                if (!u.IsDeleted && u.Account == account && u.amount == amount && InvestmentDetailsMatch(t, u) && !excluded.ContainsKey(u.Id))
                {
                    TimeSpan diff = dt - u.Date;
                    if (Math.Abs(diff.Days) < 30)
                    {
                        if (fitid != null && u.FITID == fitid && DatesMatch(u, t))
                        {
                            // Let a matching FITID or check number take precedence over
                            // matching the rest of the details.
                            return u;
                        }
                        else if (!string.IsNullOrEmpty(number) && StringHelpers.Matches(u.Number, number))
                        {
                            // same check number - must be the same then
                            return u;
                        }
                        else if (DatesMatch(u, t) && PayeesMatch(t, u))
                        {
                            if (best == null) best = u;
                        }
                    }
                }
            }
            return best;
        }

        public bool PayeesMatch(Transaction x, Transaction y)
        {
            string xName = x.PayeeName;
            string yName = y.PayeeName;

            string xOriginal = x.OriginalPayee;
            string yOriginal = y.OriginalPayee;

            if (xName != null && yName != null && string.Compare(xName, yName, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }

            if (xOriginal != null && yName != null && string.Compare(xOriginal, yName, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }

            if (xName != null && yOriginal != null && string.Compare(xName, yOriginal, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }

            if (xOriginal != null && yOriginal != null && string.Compare(xOriginal, yOriginal, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }

            return false;
        }

        // NOTE: This is not sorted by date, so be careful using this low level method.
        public ICollection<Transaction> GetAllTransactions()
        {
            return this;
        }


        public ICollection<Transaction> GetAllTransactionsByDate()
        {
            List<Transaction> view = new List<Transaction>();
            foreach (Transaction t in transactions.Values)
            {
                if (t.IsDeleted)
                    continue;
                view.Add(t);
            }
            view.Sort(new TransactionComparerByDate());
            return view;
        }

        public IList<Transaction> GetTransactionsFrom(Account a)
        {
            List<Transaction> view = new List<Transaction>();
            lock (transactions)
            {
                foreach (Transaction t in transactions.Values)
                {
                    if (t.IsDeleted || t.Account != a)
                        continue;

                    view.Add(t);
                }
            }
            view.Sort(SortByDate);
            return view;
        }

        public Transaction GetLatestTransactionFrom(Account a)
        {
            DateTime date = DateTime.MinValue;
            Transaction latest = null;
            lock (transactions)
            {
                foreach (Transaction t in transactions.Values)
                {
                    if (t.IsDeleted || t.Account != a)
                        continue;

                    if (t.Date > date)
                    {
                        date = t.Date;
                        latest = t;
                    }
                }
            }
            return latest;
        }

        public IList<Transaction> GetTransactionsFrom(Account a, Predicate<Transaction> include)
        {
            List<Transaction> view = new List<Transaction>();
            lock (transactions)
            {
                foreach (Transaction t in transactions.Values)
                {
                    if (t.IsDeleted || t.Account != a || (include != null && !include(t)))
                        continue;

                    view.Add(t);
                }
            }
            view.Sort(SortByDate);
            return view;
        }


        public DateTime? GetMostRecentBudgetDate()
        {
            DateTime? result = null;
            lock (transactions)
            {
                foreach (Transaction t in transactions.Values)
                {
                    if (t.BudgetBalanceDate.HasValue && (result == null || t.BudgetBalanceDate.Value > result))
                    {
                        result = t.BudgetBalanceDate;
                    }
                }
            }
            return result;
        }

        public DateTime? GetFirstBudgetDate()
        {
            DateTime? result = null;
            lock (transactions)
            {
                foreach (Transaction t in transactions.Values)
                {
                    if (t.BudgetBalanceDate.HasValue && (result == null || t.BudgetBalanceDate.Value < result))
                    {
                        result = t.BudgetBalanceDate;
                    }
                }
            }
            return result;
        }


        public bool Rebalance(CostBasisCalculator calculator, Account a)
        {
            if (a == null)
            {
                return false;
            }

            bool changed = false;
            this.BeginUpdate(true);
            try
            {
                lock (this.transactions)
                {

                    decimal balance = a.OpeningBalance;

                    int unaccepted = 0;
                    foreach (Transaction t in GetTransactionsFrom(a))
                    {
                        if (t.Unaccepted)
                            unaccepted++;

                        if (t.IsDeleted || t.Status == TransactionStatus.Void)
                            continue;

                        // current account balance 
                        balance += t.Amount;

                        // snapshot the current running balance value
                        t.Balance = balance;
                    }

                    if (a.Type == AccountType.Brokerage || a.Type == AccountType.Retirement)
                    {
                        foreach (SecurityPurchase sp in calculator.GetHolding(a).GetHoldings())
                        {
                            Security s = sp.Security;
                            if (Math.Floor(sp.UnitsRemaining) > 0)
                            {
                                balance += sp.MarketValue;
                            }
                        }
                    }

                    // Refresh the Account balance value
                    a.Balance = balance;
                    a.Unaccepted = unaccepted;

                }
            }
            finally
            {
                this.EndUpdate();
            }
            return changed;
        }


        static decimal GetInvestmentValue(System.Collections.IEnumerable data, out decimal taxes)
        {
            decimal balance = 0;
            decimal tax = 0;
            foreach (object row in data)
            {
                Investment it = null;
                Transaction t = row as Transaction;
                if (t == null)
                {
                    it = row as Investment;
                }
                else
                {
                    it = t.Investment;
                }
                if (it != null)
                {
                    tax += it.Taxes;
                    decimal qty = it.Units;
                    decimal value = qty == 0 ? 0 : (it.Security == null ? 0 : (it.Security.Price * qty));
                    if (it.Type == InvestmentType.Add || it.Type == InvestmentType.Buy)
                    {
                        balance += value;
                    }
                    else
                    {
                        balance -= value;
                    }
                }
                else if (t != null)
                {
                    balance += t.Amount;
                }
            }
            taxes = tax;
            return balance;
        }

        public static decimal GetBalance(MyMoney money, System.Collections.IEnumerable data, Account account, bool normalize, bool withoutTax, out int count, out decimal salestax, out decimal investmentValue)
        {
            count = 0;
            salestax = 0;
            investmentValue = 0;

            decimal balance = account != null ? account.OpeningBalance : 0;
            DateTime lastDate = DateTime.Now;
            bool hasInvestments = false;

            foreach (object row in data)
            {
                count++;
                Transaction t = row as Transaction;
                if (t != null && !t.IsDeleted && t.Status != TransactionStatus.Void)
                {
                    lastDate = t.Date;

                    decimal iTax = 0;
                    if (t.Investment != null)
                    {
                        hasInvestments = true;
                        iTax = t.Investment.Taxes;
                    }

                    if (normalize)
                    {
                        salestax += t.CurrencyNormalizedAmount(t.NetSalesTax) + t.CurrencyNormalizedAmount(iTax);

                        if (withoutTax)
                        {
                            balance += t.CurrencyNormalizedAmount(t.AmountMinusTax);
                        }
                        else
                        {
                            balance += t.CurrencyNormalizedAmount(t.Amount);
                        }                        
                    }
                    else
                    {
                        // We don't currently have a scenario where we want unnormalized, without tax
                        Debug.Assert(withoutTax == false);

                        balance += t.Amount;
                        salestax += t.NetSalesTax + iTax;
                    }
                }
            }

            if (hasInvestments && account != null)
            {
                // get the investment value as of the date of the last transaction
                CostBasisCalculator calculator = new CostBasisCalculator(money, lastDate);
                foreach (SecurityPurchase sp in calculator.GetHolding(account).GetHoldings())
                {
                    Security s = sp.Security;
                    if (Math.Floor(sp.UnitsRemaining) > 0)
                    {
                        investmentValue += sp.MarketValue;
                    }
                }
            }
            return balance;
        }


        public decimal ReconciledBalance(Account a, DateTime statementDate)
        {
            DateTime std = new DateTime(statementDate.Year, statementDate.Month, statementDate.Day);
            DateTime reconciledMonth = new DateTime(statementDate.Year, statementDate.Month, 1);

            decimal balance = a.OpeningBalance;
            foreach (Transaction t in GetTransactionsFrom(a))
            {
                DateTime td = new DateTime(t.Date.Year, t.Date.Month, t.Date.Day);

                if (t.Status == TransactionStatus.Reconciled && t.ReconciledDate.HasValue)
                {
                    DateTime rd = new DateTime(t.ReconciledDate.Value.Year, t.ReconciledDate.Value.Month, 1);
                    if (rd >= reconciledMonth)
                    {
                        // this belongs to the next statement then.
                        continue;
                    }
                }

                if (t.IsDeleted || t.Status != TransactionStatus.Reconciled || td > std)
                    continue;

                balance += t.Amount;
            }
            return balance;
        }


        public decimal EstimatedBalance(Account a, DateTime est)
        {
            DateTime et = new DateTime(est.Year, est.Month, est.Day);
            decimal balance = a.OpeningBalance;
            foreach (Transaction t in GetTransactionsFrom(a))
            {
                DateTime td = new DateTime(t.Date.Year, t.Date.Month, t.Date.Day);
                if (t.IsDeleted || t.Status == TransactionStatus.Void || td > et)
                    continue;
                balance += t.Amount;
            }
            return balance;
        }

        public List<Transaction> GetTransactionsByStatus(Account a, TransactionStatus status)
        {
            List<Transaction> view = new List<Transaction>();
            foreach (Transaction t in transactions.Values)
            {
                if (t.IsDeleted || t.Status != status)
                    continue;

                if (t.Account == a)
                {
                    view.Add(t);
                }
            }
            view.Sort(new TransactionComparerByDate());
            return view;
        }

        public IList<Transaction> FindTransfersToAccount(Account a)
        {
            List<Transaction> view = new List<Transaction>();
            foreach (Transaction t in this.transactions.Values)
            {
                if (t.IsDeleted)
                    continue;

                if (t.ContainsTransferTo(a))
                {
                    view.Add(t);
                }
            }
            view.Sort(SortByDate);
            return view;
        }

        public static int SortByDate(Transaction x, Transaction y)
        {
            if (x == null)
            {
                return -1;
            }
            else if (y == null)
            {
                return 1;
            }
            return x.CompareByDate(y);
        }

        public IList<Transaction> GetTransactionsByPayee(Payee p, Predicate<Transaction> include)
        {
            List<Transaction> view = new List<Transaction>();
            foreach (Transaction t in transactions.Values)
            {
                if (t.IsDeleted)
                    continue;

                if (include != null && !include(t))
                    continue;

                bool splitItUp = false;
                bool splitHasPayee = false;
                bool payeeMatches = t.Payee == p;

                if (t.IsSplit)
                {
                    foreach (Split s in t.Splits.Items)
                    {
                        if (s.Amount != 0)
                        {
                            if (s.Payee != null && s.Payee != p)
                            {
                                // found a split for another payee, so this transaction probably needs to be subdivided.
                                splitItUp = true;
                            }
                            else if (s.Payee == p)
                            {
                                splitHasPayee = true;
                            }
                        }
                    }
                }
                if (payeeMatches || splitHasPayee)
                {
                    if (splitItUp)
                    {
                        foreach (Split s in t.Splits.Items)
                        {
                            if (s.Amount != 0 && (s.Payee == p || (s.Payee == null && payeeMatches)))
                            {
                                view.Add(new Transaction(t, s)); // add fake transaction to promote the split out so we can see it.
                            }
                        }
                    }
                    else
                    {
                        view.Add(t);
                    }
                }
            }
            view.Sort(SortByDate);
            return view;
        }



        public IList<Transaction> GetTransactionsBySecurity(Security s, Predicate<Transaction> include)
        {
            List<Transaction> view = new List<Transaction>();

            MyMoney money = this.Parent as MyMoney;

            var splits = money.StockSplits.GetStockSplitsForSecurity(s);

            foreach (Transaction t in this.GetAllTransactionsByDate())
            {
                if (t.IsDeleted || t.Investment == null)
                    continue;

                if (include != null && !include(t))
                    continue;

                if (t.Investment.Security == s)
                {
                    t.Investment.CurrentUnits = t.Investment.Units;
                    t.Investment.CurrentUnitPrice = t.Investment.UnitPrice;
                    view.Add(t);
                }
            }

            view.Sort(SortByDate);

            decimal sortedRunningUnits = 0;
            decimal runningUnitPrice = 0;

            foreach (Transaction t in view)
            {
                foreach (StockSplit split in splits)
                {
                    t.Investment.ApplySplit(split);
                }

                if (t.InvestmentType == InvestmentType.Buy || t.InvestmentType == InvestmentType.Add)
                {
                    if (t.Investment.CurrentUnits == 0)
                    {
                        if (sortedRunningUnits == 0)
                        {
                            // leave empty
                        }
                        else
                        {
                            t.RoutingPath = "|";
                        }
                    }
                    else
                    {
                        if (sortedRunningUnits == 0)
                        {
                            t.RoutingPath = "B"; // new purchase lot
                        }
                        else
                        {
                            t.RoutingPath = "A";  // Add to existing lot
                        }
                        sortedRunningUnits += t.Investment.CurrentUnits;
                    }
                }
                else if (t.InvestmentType == InvestmentType.Sell || t.InvestmentType == InvestmentType.Remove)
                {
                    sortedRunningUnits -= t.Investment.CurrentUnits;

                    if (sortedRunningUnits == 0)
                    {
                        t.RoutingPath = "C";  // Actual Close with a positive balance
                    }
                    else
                    {
                        t.RoutingPath = "S"; // Sell in place sell
                    }
                }
                else
                {
                    t.RoutingPath = "|";
                }

                // The UnitPrice is not a mandatory field.  Add rarely have it and dividend never have it.
                // Use the last non-zero value as the closest value
                if (t.Investment.CurrentUnitPrice != 0)
                {
                    runningUnitPrice = t.Investment.CurrentUnitPrice;
                }

                t.RunningUnits = sortedRunningUnits;

                decimal factor = 1;
                if (t.Investment.Security.SecurityType == SecurityType.Futures)
                {
                    factor = 100;
                }                                                                               
                t.RunningBalance = factor * t.RunningUnits * runningUnitPrice;
            }

            return view;
        }

        private static bool IsLastChar(string currentColumnsStack, char charToCheck)
        {
            if (string.IsNullOrEmpty(currentColumnsStack))
            {
                return false;
            }
            return currentColumnsStack.Last() == charToCheck;
        }

        private static string PopLastChar(string currentColumnsStack)
        {
            if (string.IsNullOrEmpty(currentColumnsStack) == false)
            {
                return currentColumnsStack.Remove(currentColumnsStack.Count() - 1);
            }
            return string.Empty;
        }

        public IList<Transaction> GetTransactionsByCategory(Category c, Predicate<Transaction> include)
        {
            return GetTransactionsByCategory(c, include, (cat) => { return c.Contains(cat); });
        }

        public IList<Transaction> GetTransactionsByCategory(Category c, Predicate<Transaction> include, Predicate<Category> matches)
        {
            MyMoney money = this.Parent as MyMoney;
            List<Transaction> view = new List<Transaction>();
            foreach (Transaction t in transactions.Values)
            {
                if (t.IsDeleted)
                    continue;

                if (include != null && !include(t))
                    continue;

                if (t.IsSplit)
                {
                    bool all = true;
                    foreach (Split s in t.Splits.Items)
                    {
                        if (!matches(s.Category))
                        {
                            all = false;
                            break;
                        }
                    }
                    if (all)
                    {
                        // all splits match the category so the whole transaction can be listed.
                        view.Add(t);
                    }
                    else
                    {
                        // add fake transactions to promote the specific split out so we can see it.
                        foreach (Split s in t.Splits.Items)
                        {
                            if (matches(s.Category))
                            {
                                view.Add(new Transaction(t, s));
                            }
                        }
                    }
                }
                else if (matches(t.Category) || (t.Account != null && t.Account.IsCategoryFund && t.Account.GetFundCategory() == c) ||
                    (money != null && c == money.Categories.Unknown && t.Category == null && t.Transfer == null))
                {
                    view.Add(t);
                }
            }
            view.Sort(SortByDate);
            return view;
        }


        public IList<Transaction> GetTransactionsByCategory(Category c, int filterYear, bool ignoreAmountZero)
        {
            if (c == null)
            {
                return null;
            }

            MyMoney money = this.Parent as MyMoney;
            List<Transaction> view = new List<Transaction>();
            foreach (Transaction t in transactions.Values)
            {
                if (t.IsDeleted)
                    continue;

                if (t.Date.Year == filterYear)
                {
                    if (t.IsSplit)
                    {
                        foreach (Split s in t.Splits.Items)
                        {
                            if (ignoreAmountZero && s.Amount == 0)
                            {
                                // Skip this one
                            }
                            else
                            {
                                if (c.Contains(s.Category))
                                {
                                    view.Add(new Transaction(t, s)); // add fake transaction to promote the split out so we can see it.
                                }
                            }
                        }
                    }
                    else if (c.Contains(t.Category) || (t.Account != null && t.Account.IsCategoryFund && t.Account.GetFundCategory() == c) ||
                        (money != null && c == money.Categories.Unknown && t.Category == null && t.Transfer == null))
                    {
                        if (ignoreAmountZero && t.Amount == 0)
                        {
                            // Skip this one
                        }
                        else
                        {
                            view.Add(t);
                        }
                    }
                }
            }
            view.Sort(SortByDate);
            return view;
        }


        /// <summary>
        /// Execute a full query across all transactions of all accounts
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public IList<Transaction> ExecuteQuery(QueryRow[] query)
        {
            List<Transaction> view = new List<Transaction>();
            foreach (Transaction t in transactions.Values)
            {
                if (t.IsDeleted)
                    continue;

                // todo: what about splits?
                bool matches = true;
                bool first = true;
                foreach (QueryRow r in query)
                {
                    bool result = t.Matches(r);
                    Conjunction con = r.Conjunction;
                    if (first)
                    {
                        matches = result;
                        first = false;
                    }
                    else if (con == Conjunction.Or)
                    {
                        matches |= result;
                    }
                    else
                    {
                        matches &= result;
                    }
                }

                if (matches)
                {
                    view.Add(t);
                }
            }
            view.Sort(SortByDate);
            return view;
        }

        #region FILTER TRANSACTION HELPERS

        static public bool IsAnyFieldsMatching(Transaction t, FilterLiteral filter, bool includeAccountField)
        {
            //
            // Optionally the Account field name can be included in the Filter
            // we have this special treatment of AccountName because some transaction views don't include 
            // this field in the view.
            if (includeAccountField && filter.MatchSubstring(t.Account.Name))
            {
                return true;
            }

            return filter.MatchSubstring(t.PayeeOrTransferCaption) ||
                   filter.MatchSubstring(t.CategoryFullName) ||
                   filter.MatchSubstring(t.Memo) ||
                   filter.MatchDate(t.Date) ||
                   filter.MatchDecimal(t.amount);
        }

        static public bool IsSplitsMatching(Splits splits, FilterLiteral filter)
        {
            if (splits != null)
            {
                foreach (Split s in splits)
                {
                    if (filter.MatchSubstring(s.PayeeOrTransferCaption) ||
                        filter.MatchSubstring(s.CategoryName) ||
                        filter.MatchSubstring(s.Memo) ||
                        filter.MatchDecimal(s.Amount))
                    {
                        return true;
                    }
                }
            }
            return false;
        }


        #endregion

        public void CheckTransfers(MyMoney money, List<Transaction> dangling, List<Account> deletedaccounts)
        {
            foreach (Transaction t in transactions.Values)
            {
                t.CheckTransfers(money, dangling, deletedaccounts);
            }
        }

        #region ICollection

        public void Add(Transaction item)
        {
            this.AddTransaction(item);
        }

        public bool Contains(Transaction item)
        {
            return this.transactions.ContainsKey(item.Id);
        }

        public void CopyTo(Transaction[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public override void Add(object child)
        {
            Add((Transaction)child);
        }

        public override void RemoveChild(PersistentObject pe)
        {
            Remove((Transaction)pe);
        }

        public bool Remove(Transaction item)
        {
            return this.RemoveTransaction(item);
        }

        public new IEnumerator<Transaction> GetEnumerator()
        {
            // there are so many things that can have a side effect of adding a transaction
            // that it is better to just return a snapshot here.
            List<Transaction> list = new List<Transaction>();
            foreach (Transaction t in this.transactions.Values)
            {
                list.Add(t);
            }
            return list.GetEnumerator();
        }

        protected override IEnumerator<PersistentObject> InternalGetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion


        internal override void MarkAllNew()
        {
            foreach (Transaction t in this)
            {
                if (!t.IsDeleted)
                {
                    t.OnInserted();
                    if (t.IsSplit)
                    {
                        foreach (Split s in t.Splits)
                        {
                            if (!s.IsDeleted)
                            {
                                s.OnInserted();
                            }
                        }
                    }
                    if (t.Investment != null)
                    {
                        t.Investment.OnInserted();
                    }
                }
            }
        }
    }

    //================================================================================
    public class TransferChangedEventArgs : EventArgs
    {
        Transaction transaction;
        Transfer transfer;

        public TransferChangedEventArgs(Transaction t, Transfer newValue)
        {
            transaction = t;
            transfer = newValue;
        }

        public Transaction Transaction { get { return this.transaction; } }

        public Transfer NewValue { get { return this.transfer; } }
    }

    //================================================================================
    public class SplitTransferChangedEventArgs : EventArgs
    {
        Split split;
        Transfer transfer;

        public SplitTransferChangedEventArgs(Split s, Transfer newValue)
        {
            split = s;
            transfer = newValue;
        }

        public Split Split { get { return this.split; } }

        public Transfer NewValue { get { return this.transfer; } }
    }

    //================================================================================
    public class Transfer
    {
        public long Id;
        public Transaction Owner; // the source of the transfer.
        public Split OwnerSplit; // the source split, if it is a transfer in a split.
        public Transaction Transaction; // the related transaction
        public Split Split; // the related split, if it is a transfer in a split.

        public Transfer(long id, Transaction owner, Transaction t)
        {
            this.Owner = owner;
            this.Id = id;
            this.Transaction = t;
        }

        public Transfer(long id, Transaction owner, Transaction t, Split s)
        {
            this.Owner = owner;
            this.Id = id;
            this.Transaction = t;
            this.Split = s;
        }

        public Transfer(long id, Transaction owner, Split owningSplit, Transaction t)
        {
            this.Owner = owner;
            this.OwnerSplit = owningSplit;
            this.Id = id;
            this.Transaction = t;
        }

        // NOTE: we do not support a transfer from one split to another split, this is a pretty unlikely scenario,
        // although it would be possible, if you withdraw 500 cash from one account, then combine $100 of that with 
        // a check for $200 in a single deposit, then the $100 is split on the source as a "transfer" to the 
        // deposited account, and the $300 deposit is split between the cash and the check.  Like I said, pretty unlikely.

        public long TransactionId { get { return Transaction.Id; } }
    }

    public enum TransactionFlags
    {
        None,
        Unaccepted = 1,
        Budgeted = 2,
        HasAttachment = 4,
        NotDuplicate = 8
    }

    //================================================================================
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    [TableMapping(TableName = "Transactions")]
    public class Transaction : PersistentObject
    {
        long id = -1;
        Account account; // that this transaction originated.
        DateTime date;
        internal decimal amount;
        decimal salesTax;
        TransactionStatus status;
        string memo;
        Payee payee;
        Category category;
        string number; // requires value.Length < 10
        Investment investment;
        Transfer transfer;
        string fitid;
        internal Account to; // for debugging only.        
        decimal balance;
        decimal runningUnits;
        decimal runningBalance;
        string routingPath;
        TransactionFlags flags;
        DateTime? reconciledDate;
        Splits splits;
        string pendingTransfer;
        DateTime? budgetBalanceDate;
        Transaction related;
        Split relatedSplit;
        DateTime? mergeDate;
        string originalPayee; // before auto-aliasing, helps with future merging.
        TransactionViewFlags viewState; // ui transient state only, not persisted.

        enum TransactionViewFlags : byte
        {
            None,
            TransactionDropTarget = 1,
            AttachmentDropTarget = 2,
            Reconciling = 4
        }

        public Transaction()
        { // for serialization.
        }

        /// <summary>
        /// Create fake transaction from the details in the given split.
        /// </summary>
        public Transaction(Transaction t, Split s)
        {
            this.id = t.id;
            this.related = t;
            this.Account = t.Account;
            this.Date = t.Date;
            this.IsReadOnly = true;

            Payee toFake = s.Payee;
            if (s.Payee == null)
            {
                toFake = t.Payee;
            }
            // we want a disconnected Payee so we don't corrupt the real Payee book keeping.
            if (toFake != null) { 
                this.payee = new Data.Payee() { Name = toFake.Name, Id = toFake.Id };
            }
            this.Memo = s.Memo;
            this.Amount = s.Amount;
            this.transfer = s.Transfer; // hmmm
            this.relatedSplit = s;

            // Category must be set last in order to ensure
            // that the proxy split[0] was set already
            // not also that we are using the private property "category" and not the "Category"
            // we need to do this in order avoid changing the original transaction split
            this.category = s.Category;
        }

        public Transaction(Transactions container) : base(container) { }

        [XmlAttribute]
        [DataMember]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true)]
        public long Id
        {
            get { return this.id; }
            set
            {
                if (this.id != value)
                {
                    this.id = value; OnChanged("Id");

                    if (this.Investment != null)
                    {
                        this.Investment.Id = value; // mirror Id in the investment.
                    }
                }
            }
        }

        private void SetViewState(TransactionViewFlags flag, bool set)
        {
            if (set)
            {
                this.viewState |= flag;
            }
            else
            {
                this.viewState &= ~flag;
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public bool TransactionDropTarget
        {
            get { return (this.viewState & TransactionViewFlags.TransactionDropTarget) != 0; }
            set
            {
                if (this.TransactionDropTarget != value)
                {
                    SetViewState(TransactionViewFlags.TransactionDropTarget, value);
                    // don't use OnChanged, we don't want this to make the database dirty.
                    FireChangeEvent(this, new ChangeEventArgs(this, "TransactionDropTarget", ChangeType.None));
                }
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public bool AttachmentDropTarget
        {
            get { return (this.viewState & TransactionViewFlags.AttachmentDropTarget) != 0; }
            set
            {
                if (this.AttachmentDropTarget != value)
                {
                    SetViewState(TransactionViewFlags.AttachmentDropTarget, value);
                    // don't use OnChanged, we don't want this to make the database dirty.
                    FireChangeEvent(this, new ChangeEventArgs(this, "AttachmentDropTarget", ChangeType.None));
                }
            }
        }

        [XmlIgnore]
        [ColumnObjectMapping(ColumnName = "Account", KeyProperty = "Id")]
        public Account Account
        {
            get { return this.account; }
            set
            {
                if (this.account != value)
                {
                    this.account = value;
                    OnChanged("Account");
                }
            }
        }

        string accountName;

        // for serialization
        [DataMember]
        public string AccountName
        {
            get
            {
                return (this.account != null) ? this.account.Name : this.accountName;
            }
            set
            {
                this.accountName = value;
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Date")]
        public DateTime Date
        {
            get { return date.Date; }
            set
            {
                if (this.date != value)
                {
                    date = value;
                    if (Transfer != null)
                        Transfer.Transaction.date = value;
                    OnChanged("Date");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Status", AllowNulls = true)]
        public TransactionStatus Status
        {
            get { return this.status; }
            set
            {
                if (this.status != value)
                {
                    this.status = value;
                    OnChanged("Status");
                    OnChanged("StatusString");
                }
            }
        }

        [XmlIgnore]
        [ColumnObjectMapping(ColumnName = "Payee", KeyProperty = "Id", AllowNulls = true)]
        public Payee Payee
        {
            get { return payee; }
            set
            {
                if (this.payee != value)
                {
                    if (this.payee != null && value != null && !this.BatchMode)
                    {
                        // transfer the counts from the old payee to the new payee
                        if (this.Unaccepted)
                        {
                            this.payee.UnacceptedTransactions--;
                            value.UnacceptedTransactions++;
                        }
                        if (this.category == null && this.transfer == null && !this.IsSplit)
                        {
                            this.payee.UncategorizedTransactions--;
                            value.UncategorizedTransactions++;
                        }
                    }
                    payee = value;
                    OnChanged("Payee");
                    OnChanged("PayeeOrTransferCaption");
                }
            }
        }

        string payeeName;
        // for serialization
        [DataMember]
        public string PayeeName
        {
            get { return payee != null ? payee.Name : this.payeeName; }
            set { this.payeeName = value; }
        }

        public string PayeeNameNotNull
        {
            get { return PayeeName == null ? string.Empty : PayeeName; }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "OriginalPayee", AllowNulls = true, MaxLength = 255)]
        public string OriginalPayee
        {
            get { return originalPayee; }
            set
            {
                if (this.originalPayee != value)
                {
                    this.originalPayee = Truncate(value, 255);
                    OnChanged("OriginalPayee");
                }
            }
        }

        [XmlIgnore]
        [ColumnObjectMapping(ColumnName = "Category", KeyProperty = "Id", AllowNulls = true)]
        public Category Category
        {
            get { return category; }
            set
            {
                if (IsFake)
                {
                    this.ChangeFakeCategory = value;
                    return;
                }

                if (this.category != value)
                {
                    Category old = this.category;
                    if (!this.BatchMode && this.payee != null)
                    {
                        if (old == null && value != null)
                        {
                            this.payee.UncategorizedTransactions--;
                        }
                        else if (old != null && value == null)
                        {
                            this.payee.UncategorizedTransactions++;
                        }
                    }
                    if (this.IsBudgeted)
                    {
                        if (old != null)
                        {
                            old.Balance -= this.amount;
                        }
                        if (value != null)
                        {
                            value.Balance += this.amount;
                        }
                    }
                    this.category = value;
                    OnChanged("Category");
                    OnChanged("CategoryName");
                    OnChanged("CategoryNonNull");
                    if (Transfer != null && Transfer.Transaction.category == old)
                    {
                        Transfer.Transaction.Category = value;
                    }

                    // See if user is overriding a split category, then delete the splits
                    if (this.IsSplit && value != this.MyMoney.Categories.Split)
                    {
                        this.Splits.Clear();
                    }
                }
            }
        }

        /// <summary>
        /// A fake transaction is a proxy for a Split entry into a real transaction
        /// so changing the category of a fake transaction would amount to nothing since the fake transaction are discarded.
        /// However fake transaction has a pointers to the real location of the Split they originally came from
        /// the proxy pointer to the real split is located in the item [0] of the Splits collection
        /// </summary>
        [XmlIgnore]
        private Category ChangeFakeCategory
        {
            set
            {
                if (IsFake && this.Splits != null && this.Splits.Count > 0)
                {
                    // Splits[0] is a special proxy entry that points to the real split located in the real transaction
                    // that this fake traction is based on
                    this.Splits.Items[0].Category = value;
                }
            }
        }



        string categoryName;
        // for serialization;
        [DataMember]
        public string CategoryName
        {
            get { return category != null ? category.Name : this.categoryName; }
            set { this.categoryName = value; }
        }

        // for serialization;
        public string CategoryFullName
        {
            get { return category != null ? category.GetFullName() : string.Empty; }
        }

        /// <summary>
        /// For data binding where we need a non-null category, including a default category for transfers.
        /// </summary>
        [XmlIgnore]
        public Category CategoryNonNull
        {
            get
            {
                if (category != null)
                {
                    return category;
                }
                if (transfer != null)
                {
                    return this.MyMoney.Categories.Transfer;
                }
                if (IsSplit)
                {
                    return this.MyMoney.Categories.Split;
                }
                return this.MyMoney.Categories.Unknown;
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Memo", MaxLength = 255, AllowNulls = true)]
        public string Memo
        {
            get { return memo; }
            set
            {
                if (this.memo != value)
                {
                    memo = Truncate(value, 255);
                    if (Transfer != null)
                        Transfer.Transaction.memo = memo;
                    OnChanged("Memo");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Number", MaxLength = 10, AllowNulls = true)]
        public string Number
        {
            get { return this.number; }
            set
            {
                if (this.number != value)
                {
                    this.number = Truncate(value, 10);
                    OnChanged("Number");
                }
            }
        }

        /// <summary>
        /// Get or set a transient boolean value indicating that this transaction is being included in current statement reconciliation.
        /// </summary>
        [XmlIgnore]
        [IgnoreDataMember]
        public bool IsReconciling
        {
            get { return (this.viewState & TransactionViewFlags.Reconciling) != 0; }
            set
            {
                if (this.IsReconciling != value)
                {
                    SetViewState(TransactionViewFlags.Reconciling, value);
                    FireChangeEvent(this, new ChangeEventArgs(this, "IsReconciling", ChangeType.None));
                }
            }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "ReconciledDate", AllowNulls = true)]
        public DateTime? ReconciledDate
        {
            get { return this.reconciledDate; }
            set
            {
                if (this.reconciledDate != value)
                {
                    this.reconciledDate = value;
                    OnChanged("ReconciledDate");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "BudgetBalanceDate", AllowNulls = true)]
        public DateTime? BudgetBalanceDate
        {
            get { return this.budgetBalanceDate; }
            set
            {
                if (this.budgetBalanceDate != value)
                {
                    this.budgetBalanceDate = value;
                    OnChanged("BudgetBalanceDate");
                }
            }
        }

        public Investment GetOrCreateInvestment()
        {
            if (this.investment == null)
            {
                this.investment = new Investment(this.Parent);
                this.investment.Id = this.Id; // link them by Id.
                this.investment.Transaction = this;
                OnChanged("Id");
            }
            return this.investment;
        }

        [DataMember]
        public Investment Investment
        {
            get { return this.investment; }
            set
            {
                if (this.investment != value)
                {
                    this.investment = value;
                    OnChanged("Investment");
                }
            }
        }

        [IgnoreDataMember]
        public string TransferTo
        {
            get
            {
                if (this.pendingTransfer != null)
                    return pendingTransfer;

                if (this.transfer != null)
                {
                    return this.transfer.Transaction.Account.Name;
                }
                return this.TransferName;
            }
            set
            {
                this.pendingTransfer = value;
                this.TransferName = null;
            }
        }

        private string GetPayeeOrTransferCaption()
        {
            Transfer transfer = null;
            Investment investment = null;
            decimal amount = 0;

            investment = this.Investment;
            transfer = this.Transfer;
            amount = this.Amount;

            bool isFrom = false;
            if (transfer != null)
            {
                if (investment != null)
                {
                    if (investment != null && investment.Type == InvestmentType.Add)
                    {
                        isFrom = true;
                    }
                }
                else if (amount > 0)
                {
                    isFrom = true;
                }
                return GetTransferCaption(transfer.Transaction.Account, isFrom);
            }

            if (this.payee == null) return string.Empty;

            return PayeeNameNotNull;
        }

        public static bool IsTransferCaption(string value)
        {
            return (value.StartsWith(Walkabout.Properties.Resources.TransferFromPrefix) ||
                    value.StartsWith(Walkabout.Properties.Resources.TransferToPrefix) ||
                    value.StartsWith(Walkabout.Properties.Resources.TransferToClosedAccountPrefix) ||
                    value.StartsWith(Walkabout.Properties.Resources.TransferFromClosedAccountPrefix));
        }

        /// <summary>
        /// This helper methods creates the payee name for a transfer, like "Transfer to: Discover Card"
        /// </summary>
        /// <param name="account"></param>
        /// <param name="isFrom"></param>
        /// <returns></returns>
        public static string GetTransferCaption(Account account, bool isFrom)
        {
            string caption = null;
            if (account.IsClosed)
            {
                caption = (isFrom) ? Walkabout.Properties.Resources.TransferFromClosedAccountPrefix : Walkabout.Properties.Resources.TransferToClosedAccountPrefix;
            }
            else
            {
                caption = (isFrom) ? Walkabout.Properties.Resources.TransferFromPrefix : Walkabout.Properties.Resources.TransferToPrefix;
            }
            caption += account.Name;
            return caption;
        }

        /// <summary>
        /// Parses something like "Transfer to: Discover Card" and returns "Discover Card".
        /// </summary>
        /// <param name="value">The string to disect</param>
        /// <returns>The account name</returns>
        public static string ExtractTransferAccountName(string value)
        {
            string accountName = null;

            if (value.StartsWith(Walkabout.Properties.Resources.TransferFromPrefix))
            {
                accountName = value.Substring(Walkabout.Properties.Resources.TransferFromPrefix.Length);
            }
            else if (value.StartsWith(Walkabout.Properties.Resources.TransferToPrefix))
            {
                accountName = value.Substring(Walkabout.Properties.Resources.TransferToPrefix.Length);
            }
            else if (value.StartsWith(Walkabout.Properties.Resources.TransferToClosedAccountPrefix))
            {
                accountName = value.Substring(Walkabout.Properties.Resources.TransferToPrefix.Length);
            }
            else if (value.StartsWith(Walkabout.Properties.Resources.TransferFromClosedAccountPrefix))
            {
                accountName = value.Substring(Walkabout.Properties.Resources.TransferToPrefix.Length);
            }
            return accountName;
        }

        public string PayeeOrTransferCaption
        {
            get { return GetPayeeOrTransferCaption(); }
            set
            {
                if (this.PayeeOrTransferCaption != value)
                {
                    MyMoney money = MyMoney;

                    if (string.IsNullOrEmpty(value))
                    {
                        this.Payee = null;
                    }
                    else if (IsTransferCaption(value))
                    {
                        if (money != null)
                        {
                            string accountName = ExtractTransferAccountName(value);
                            Account a = money.Accounts.FindAccount(accountName);
                            if (a != null)
                            {
                                money.Transfer(this, a);
                                money.Rebalance(a);
                            }
                        }
                    }
                    else
                    {
                        // find MyMoney container
                        if (money != null)
                        {
                            this.Payee = money.Payees.FindPayee(value, true);
                        }
                    }
                }
            }
        }

        internal MyMoney MyMoney
        {
            get
            {
                if (Parent == null)
                {
                    if (related != null)
                    {
                        return related.MyMoney;
                    }
                }
                else
                {
                    Transactions parent = this.Parent as Transactions;
                    if (parent != null)
                    {
                        return parent.Parent as MyMoney;
                    }
                }
                return null;
            }
        }

        [XmlIgnore] // break circular reference for serialization
        [ColumnObjectMapping(ColumnName = "Transfer", KeyProperty = "TransactionId", AllowNulls = true)]
        public Transfer Transfer
        {
            get { return this.transfer; }
            set
            {
                if (this.transfer != value)
                {
                    MyMoney parent = this.MyMoney;
                    if (parent != null)
                    {
                        parent.OnTransferChanged(this, value);
                        if (value != null)
                        {
                            this.Payee = parent.Payees.Transfer;
                        }
                    }
                    transferId = value == null ? -1 : value.TransactionId;
                    transferSplitId = (value == null) ? -1 : ((value.Split == null) ? -1 : value.Split.Id);
                    this.transfer = value;
                    OnChanged("Transfer");
                    OnChanged("PayeeOrTransferCaption");
                }
            }
        }

        string transferName; // for serialization only.

        [DataMember]
        public string TransferName
        {
            get { if (this.transfer != null) return this.transfer.Transaction.Account.Name; return transferName; }
            set { this.transferName = value; }
        }

        long transferId = -1;

        // serialization
        [DataMember]
        long TransferId
        {
            get { return transferId; }
            set { transferId = value; }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "FITID", MaxLength = 40, AllowNulls = true)]
        public string FITID
        {
            get { return fitid; }
            set
            {
                if (fitid != value)
                {
                    fitid = Truncate(value, 40);
                    OnChanged("FITID");
                }
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public bool Unaccepted
        {
            get { return (this.flags & TransactionFlags.Unaccepted) != 0; }
            set
            {
                if (this.Unaccepted != value)
                {
                    if (value)
                    {
                        SetFlag(TransactionFlags.Unaccepted);
                    }
                    else
                    {
                        ClearFlag(TransactionFlags.Unaccepted);
                    }
                    OnChanged("Unaccepted");

                    if (this.IsSplit)
                    {
                        // make sure split bold font is updated.
                        foreach (Split s in this.NonNullSplits)
                        {
                            s.OnChanged("Unaccepted");
                        }
                    }
                }
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public bool HasAttachment
        {
            get { return (this.flags & TransactionFlags.HasAttachment) != 0; }
            set
            {
                if (this.HasAttachment != value)
                {
                    if (value)
                    {
                        SetFlag(TransactionFlags.HasAttachment);
                    }
                    else
                    {
                        ClearFlag(TransactionFlags.HasAttachment);
                    }
                    OnChanged("HasAttachment");
                }
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public bool NotDuplicate
        {
            get { return (this.flags & TransactionFlags.NotDuplicate) != 0; }
            set
            {
                if (this.NotDuplicate != value)
                {
                    if (value)
                    {
                        SetFlag(TransactionFlags.NotDuplicate);
                    }
                    else
                    {
                        ClearFlag(TransactionFlags.NotDuplicate);
                    }
                    OnChanged("NotDuplicate");
                }
            }
        }

        void SetFlag(TransactionFlags flag)
        {
            this.Flags |= flag;
        }

        void ClearFlag(TransactionFlags flag)
        {
            this.Flags ^= flag;
        }

        [ColumnMapping(ColumnName = "Flags", SqlType = typeof(SqlInt32), AllowNulls = false)]
        [XmlIgnore] // the silly XmlSerializer can't serialize this as 'TransactionFlags'
        public TransactionFlags Flags
        {
            get { return this.flags; }
            set
            {
                if (this.flags != value)
                {
                    // must call setter on Unaccepted so account get updated.
                    if (!this.BatchMode)
                    {
                        if ((this.flags & TransactionFlags.Unaccepted) == 0 && (value & TransactionFlags.Unaccepted) != 0)
                        {
                            // It was not unaccepted but now it is, so increase the count
                            if (this.payee != null)
                            {
                                this.payee.UnacceptedTransactions++;
                            }
                            if (this.account != null)
                            {
                                this.Account.Unaccepted++;
                            }
                        }
                        else if ((this.flags & TransactionFlags.Unaccepted) != 0 && (value & TransactionFlags.Unaccepted) == 0)
                        {
                            // It was unaccepted but now it is not, so decrease the count
                            if (this.payee != null)
                            {
                                this.payee.UnacceptedTransactions--;
                            }
                            if (this.account != null)
                            {
                                this.Account.Unaccepted--;
                            }
                        }
                    }
                    this.flags = value;
                    OnChanged("Flags");
                }
            }
        }

        // This one is for serialization.
        [DataMember]
        public int SerializedFlags
        {
            get { return (int)this.flags; }
            set { this.flags = (TransactionFlags)value; }
        }

        [IgnoreDataMember]
        public bool IsBudgeted
        {
            get
            {
                if (IsFake)
                {
                    if (relatedSplit != null)
                    {
                        return relatedSplit.IsBudgeted;
                    }
                    else if (this.related != null)
                    {
                        return this.related.IsBudgeted;
                    }
                }
                return (this.flags & TransactionFlags.Budgeted) != 0;
            }
            set { SetBudgeted(value, null); }
        }

        public void SetBudgeted(bool value, List<TransactionException> errors)
        {
            if (this.IsFake)
            {
                if (relatedSplit != null)
                {
                    // fake transaction is an unrolled split for by Cateogry view, so budgeting this
                    // is adding the split to the given category.
                    relatedSplit.SetBudgeted(value, errors);
                }
                else if (this.related != null)
                {
                    this.related.IsBudgeted = value;
                }
                return;
            }

            if (this.IsBudgeted != value)
            {
                if (value)
                {
                    this.UpdateBudget(true, errors);
                    SetFlag(TransactionFlags.Budgeted);
                }
                else
                {
                    this.UpdateBudget(false, errors);
                    ClearFlag(TransactionFlags.Budgeted);
                    this.BudgetBalanceDate = null;
                }
                OnChanged("IsBudgeted");
            }
        }


        [XmlIgnore]
        [IgnoreDataMember]
        public string StatusString
        {
            get
            {
                switch (Status)
                {
                    case TransactionStatus.None:
                        return "";
                    case TransactionStatus.Cleared:
                        return "C";
                    case TransactionStatus.Reconciled:
                        return "R";
                    case TransactionStatus.Electronic:
                        return "E";
                    case TransactionStatus.Void:
                        return "V";
                }
                return "";
            }
            set
            {
                if (value == null) return;
                switch (value.Trim().ToLowerInvariant())
                {
                    case "":
                        Status = TransactionStatus.None;
                        break;
                    case "c":
                        Status = TransactionStatus.Cleared;
                        break;
                    case "r":
                        Status = TransactionStatus.Reconciled;
                        break;
                    case "e":
                        Status = TransactionStatus.Electronic;
                        break;
                    case "v":
                        Status = TransactionStatus.Void;
                        break;
                }
                OnChanged("StatusString");
            }
        }

        [XmlIgnore]
        public bool AmountError { get; set; }

        [DataMember]
        [ColumnMapping(ColumnName = "Amount", SqlType = typeof(SqlMoney))]
        public decimal Amount
        {
            get { return amount; }
            set
            {
                if (amount != value)
                {
                    if (this.Status == TransactionStatus.Reconciled && !this.IsReconciling)
                    {
                        // raise the events so that the grid rebinds the value that was not changed.
                        AmountError = true;
                        throw new MoneyException("Cannot change the value of a reconciled transaction unless you are balancing the account");
                    }

                    if (this.transfer != null)
                    {
                        bool sameCurrency = (this.transfer.Transaction.Account.NonNullCurrency == this.Account.NonNullCurrency);

                        if ((this.amount == 0 && this.IsInserted) || sameCurrency)
                        {
                            decimal other = MyMoney.Currencies.GetTransferAmount(-value, this.Account.Currency, this.transfer.Transaction.Account.Currency);

                            if (this.transfer.Split != null)
                            {
                                this.transfer.Split.InternalSetAmount(other);
                            }
                            else
                            {
                                if (sameCurrency && this.transfer.Transaction.Status == TransactionStatus.Reconciled &&
                                    !this.Transfer.Transaction.IsDeleted)
                                {
                                    throw new MoneyException("Other side of transfer is reconciled");
                                }
                                this.transfer.Transaction.InternalSetAmount(other);
                            }
                        }
                        else
                        {
                            // Allow these to be different since we don't know how to translate across currencies.
                            // todo: remind the user to fix the other side...   

                            // But if this is a newly inserted transaction then compute the other side as a convenience
                            // to the user.
                        }
                    }
                    InternalSetAmount(value);
                }
            }
        }

        // Set the amount, without checking for transfers.
        internal void InternalSetAmount(decimal value)
        {
            OnAmountChanged(this.amount, value);
            this.amount = value;
            AmountError = false;
            OnChanged("Credit");
            OnChanged("Debit");
            OnChanged("Amount");
        }

        void OnAmountChanged(decimal oldValue, decimal newValue)
        {
            decimal diff = newValue - oldValue;
            if (this.IsBudgeted && !this.IsFake)
            {
                if (this.account != null && this.account.IsCategoryFund && this.Transfer != null)
                {
                    Category c = this.account.GetFundCategory();
                    c.Balance += diff;
                }
                else if (this.category != null)
                {
                    this.category.Balance += diff;
                }
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public decimal AmountMinusTax
        {
            get
            {
                return this.amount + this.NetSalesTax;
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public decimal NetSalesTax
        {
            get
            {
                // Returns the amount of sales tax to be added based on whether this is an 
                // income or expense transaction. 
                decimal tax = this.SalesTax;
                if (this.Amount < 0)
                {
                    return tax;
                }
                else
                {
                    return -tax; // then it was a refund, so sales tax was refunded also!
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "SalesTax", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal SalesTax
        {
            get
            {
                return this.salesTax;
            }
            set
            {
                if (this.salesTax != value)
                {
                    this.salesTax = value;
                    OnChanged("SalesTax");
                }
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public decimal Balance
        {
            get { return this.balance; }
            set
            {
                if (this.balance != value)
                {
                    this.balance = value;
                    OnTransientChanged("Balance");
                }
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public decimal RunningBalance
        {
            get { return this.runningBalance; }
            set
            {
                if (this.runningBalance != value)
                {
                    this.runningBalance = value;
                    //OnChangedNoImpactToStorage("RunningBalance");
                }
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public decimal RunningUnits
        {
            get { return this.runningUnits; }
            set
            {
                if (this.runningUnits != value)
                {
                    this.runningUnits = value;
                    //OnChangedNoImpactToStorage("RunningUnits");
                }
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public string RoutingPath
        {
            get { return this.routingPath; }
            set
            {
                if (this.routingPath != value)
                {
                    this.routingPath = value;
                    //OnChangedNoImpactToStorage("routingPath");
                }
            }
        }

        [XmlIgnore]
        public SqlDecimal Tax
        {
            get { return (this.salesTax > 0) ? new SqlDecimal(this.salesTax) : SqlDecimal.Null; }
            set { if (!value.IsNull) this.SalesTax = (decimal)value; OnChanged("Tax"); }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public SqlDecimal Credit
        {
            get { return (Amount > 0) ? new SqlDecimal(Amount) : SqlDecimal.Null; }
            set { if (!value.IsNull) SetDebitCredit(value); }
        }


        [XmlIgnore]
        [IgnoreDataMember]
        public SqlDecimal Debit
        {
            get { return (Amount <= 0) ? new SqlDecimal(-Amount) : SqlDecimal.Null; }
            set { if (!value.IsNull) SetDebitCredit(-value); }
        }

        int lastSet;

        void SetDebitCredit(SqlDecimal value)
        {
            if (!value.IsNull)
            {
                decimal amount = value.Value;
                int tick = Environment.TickCount;
                if (amount == 0 && lastSet / 100 == Environment.TickCount / 100)
                {
                    // weirdness with how row is committed, it will commit 0 to Credit field after
                    // it commits a real value to Debit field and/or vice versa, so we check for 0
                    // happening right after a real value and ignore it.  Should happen within 1/10th
                    // of a second.
                    return;
                }
                lastSet = tick;
                Amount = amount;
                OnChanged("Credit");
                OnChanged("Debit");
                OnChanged("HasCreditAndIsSplit");
            }
        }


        [XmlIgnore]
        [IgnoreDataMember]
        public bool HasCreditAndIsSplit
        {
            get { return Amount > 0 && IsSplit; }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public bool HasDebitAndIsSplit
        {
            get { return Amount <= 0 && IsSplit; }
        }

        /// <summary>
        /// This property is here just to avoid BindingExpression errors in our XAML bindings.
        /// </summary>
        [XmlIgnore]
        public bool IsDown
        {
            get { return false; }
        }


        /// <summary>
        /// A transaction is considered "fake" if it the parent of the transaction is not the Money container and that there's a "real" related transaction.
        /// 
        /// Fake transaction are create in the scenario where the user selects a Category and we aggregate all "real" Transactions
        /// and also parts of real transactions found their Splits 
        /// </summary>
        [XmlIgnore]
        [IgnoreDataMember]
        public bool IsFake
        {
            get { return this.Parent == null && this.related != null; }
        }

        /// <summary>
        /// This transaction is fake and is probably for the purpose of ViewByCategory and represents a split.
        /// </summary>
        [XmlIgnore]
        [IgnoreDataMember]
        public bool IsFakeSplit
        {
            get { return this.Parent == null && this.relatedSplit != null; }
        }


        [XmlIgnore]
        [IgnoreDataMember]
        public bool IsReadOnly
        {
            get;
            set;
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public Transaction Related
        {
            get { return related; }
        }

        int transferSplitId = -1;

        /// <summary>
        /// This is a hack purely here to force this column to be created in the database, it is not used in memory.
        /// It has to do with how transfers in a split transaction are stored.
        /// </summary>
        [DataMember]
        [ColumnMapping(ColumnName = "TransferSplit", AllowNulls = true)]
        int TransferSplit
        {
            get { return transferSplitId; }
            set { transferSplitId = value; }
        }

        [DataMember]
        public Splits Splits
        {
            get
            {
                return this.splits;
            }
            set
            {
                if (this.splits != null) this.splits.RemoveAll();
                this.splits = value;
                if (value != null)
                {
                    this.splits.Transaction = this;
                }
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public Splits NonNullSplits
        {
            get
            {
                if (this.splits == null)
                    this.splits = new Splits(this, this);
                return this.splits;
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public bool IsSplit
        {
            get
            {
                if (IsFake)
                {
                    return false;
                }
                return this.splits != null && this.splits.Count > 0;
            }
        }

        public Split FindSplit(int id)
        {
            if (this.splits == null) return null;
            return this.splits.FindSplit(id);
        }


        public static bool IsDeletedAccount(Account to, MyMoney money, List<Account> deletedaccounts)
        {
            foreach (Account a in deletedaccounts)
            {
                if (a == to)
                {
                    return true;
                }
            }
            if (money.Transactions.GetTransactionsFrom(to).Count == 0)
            {
                deletedaccounts.Add(to);
                return true;
            }
            return false;
        }

        public bool ContainsTransferTo(Account a)
        {
            if (this.IsSplit)
            {
                foreach (Split s in this.Splits.GetSplits())
                {
                    if (s.Transfer != null && s.Transfer.Transaction.Account == a)
                    {
                        return true;
                    }
                }
            }
            if (this.Transfer != null && this.Transfer.Transaction.Account == a)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Move this transaction and/or any Splits from the old category to the new category
        /// and any subcategory to a matching subcategory under the new category.  For example:
        /// Move "Auto:Insurance" to "Insurance" will also move "Auto:Insurance:Volvo" to
        /// "Insurance:Volvo".  So all subcategories go along for the ride.
        /// </summary>
        /// <param name="oldCategory"></param>
        /// <param name="newCategory"></param>
        public void ReCategorize(Category oldCategory, Category newCategory)
        {
            if (this.IsFakeSplit)
            {
                Split s = this.relatedSplit;
                if (oldCategory.Contains(s.Category))
                {
                    s.Category = this.MyMoney.Categories.ReParent(s.Category, oldCategory, newCategory);
                }
            }
            else if (this.IsFake)
            {
                this.related.ReCategorize(oldCategory, newCategory);
            }
            else if (this.IsSplit)
            {
                foreach (Split s in this.Splits.Items)
                {
                    if (oldCategory.Contains(s.Category))
                    {
                        s.Category = this.MyMoney.Categories.ReParent(s.Category, oldCategory, newCategory);
                    }
                }
            }
            else if (oldCategory.Contains(this.Category))
            {
                this.Category = this.MyMoney.Categories.ReParent(this.Category, oldCategory, newCategory);
            }
        }

        public void CheckTransfers(MyMoney money, List<Transaction> dangling, List<Account> deletedaccounts)
        {
            bool added = false;
            if (this.to != null && this.Transfer == null)
            {
                if (IsDeletedAccount(this.to, money, deletedaccounts))
                {
                    this.Category = this.Amount < 0 ? money.Categories.TransferToDeletedAccount :
                                        money.Categories.TransferFromDeletedAccount;
                    this.to = null;
                }
                else
                {
                    added = true;
                    dangling.Add(this);
                }
            }

            if (this.Transfer != null)
            {
                Transaction other = this.transfer.Transaction;
                if (other.IsSplit)
                {
                    int count = 0;
                    Split splitXfer = null;

                    foreach (Split s in other.splits.GetSplits())
                    {
                        if (s.Transfer != null)
                        {
                            if (splitXfer == null)
                            {
                                splitXfer = s;
                            }
                            if (s.Transfer.Transaction == this)
                            {
                                count++;
                            }
                        }
                    }
                    if (count == 0)
                    {
                        if (other.transfer != null && other.transfer.Transaction == this)
                        {
                            // Ok, well it could be that the transfer is the whole transaction, but then 
                            // one side was itemized. For example, you transfer 500 from one account to
                            // another, then on the deposit side you want to record what that was for 
                            // by itemizing the $500 in a split.  If this is the case then it is not dangling.
                        }
                        else if (!AutoFixDandlingTransfer(splitXfer))
                        {
                            added = true;
                            dangling.Add(this);
                        }
                    }
                }
                else if ((other.Transfer == null || other.Transfer.Transaction != this) && !AutoFixDandlingTransfer(null))
                {
                    added = true;
                    dangling.Add(this);
                }
            }

            if (this.splits != null)
            {
                if (this.splits.CheckTransfers(money, dangling, deletedaccounts) && !added)
                {
                    dangling.Add(this); // only add transaction once.
                }
            }

        }

        private bool AutoFixDandlingTransfer(Split splitTransfer)
        {
            Transaction other = this.transfer.Transaction;
            if (splitTransfer != null && other.Transfer == null)
            {
                Transaction alternate = splitTransfer.Transfer.Transaction;
                if (this.status == TransactionStatus.Reconciled && alternate.status != TransactionStatus.Reconciled &&
                    this.payee == alternate.payee && this.category == alternate.category && this.amount == alternate.amount)
                {
                    // then we can swap out the unreconciled transaction for the reconciled one and delete the unreconciled one and we'll be good.
                    splitTransfer.Transfer = new Transfer(this.id, other, splitTransfer, this);
                    return true;
                }
            }
            else if (other.Transfer != null && other.Transfer.Transaction != this)
            {
            }
            return false;
        }


        [DataMember]
        [ColumnMapping(ColumnName = "MergeDate", AllowNulls = true)]
        public DateTime? MergeDate
        {
            get { return this.mergeDate; }
            set
            {
                if (this.mergeDate != value)
                {
                    this.mergeDate = value;
                    OnChanged("MergeDate");
                }
            }
        }

        public bool Merge(Transaction t)
        {
            MyMoney money = (MyMoney)this.MyMoney;
            bool rc = false;

            if (t.CategoryName == money.Categories.TransferToDeletedAccount.Name ||
                t.CategoryName == money.Categories.TransferFromDeletedAccount.Name)
            {
                // hmmm, the imported database has deleted account!
                // so best we can do is skip this one.               
                return false;
            }

            if (t.IsSplit)
            {
                if (this.NonNullSplits.Merge(money, t.Splits))
                {
                    rc = true;
                }
            }
            else if (this.IsSplit)
            {
                // imported transaction deleted the splits?  
                // Hmmm.
                //throw new ApplicationException("Cannot merge deleted splits.");
            }

            if (this.Transfer != null && t.Transfer != null)
            {
                if (this.Transfer.Transaction.Account.AccountId == t.Transfer.Transaction.account.AccountId &&
                    this.transfer.Transaction.account.Name == t.transfer.Transaction.account.Name)
                {
                    // they are both transfering to the same account, so we're good.
                }
                else
                {
                    throw new ApplicationException("Cannot merge when both transactions are transfered to a different place");
                }
            }
            
            if (string.IsNullOrEmpty(this.fitid) && !string.IsNullOrEmpty(t.fitid))
            {
                this.FITID = t.fitid;
                rc = true;
            }

            if (this.date == null && t.date != null && this.date != t.date)
            {
                this.date = t.date;
                rc = true;
            }

            if (string.IsNullOrEmpty(this.number) && !string.IsNullOrEmpty(t.number))
            { 
                this.Number = t.number;
                rc = true;
            }

            if (this.Payee == null && t.Payee != null)
            {
                this.Payee = money.Payees.ImportPayee(t.Payee); 
                rc = true;
            }

            if (this.category == null && t.category != null)
            {
                this.Category = money.Categories.ImportCategory(t.category); 
                rc = true;
            }

            if (string.IsNullOrEmpty(this.memo))
            {
                if (!string.IsNullOrEmpty(t.memo))
                {
                    this.Memo = t.memo;
                    rc = true;
                }
                else if (this.Payee != t.Payee)
                {
                    this.Memo = t.Payee.Name;
                    rc = true;
                }
            }

            if (t.Status != this.Status && this.Status != TransactionStatus.Reconciled && t.status != TransactionStatus.None)
            {
                this.Status = t.status;
                rc = true;
            }

            if (t.salesTax != this.salesTax)
            {
                this.SalesTax = t.salesTax;
                rc = true;
            }

            if (this.Unaccepted && !t.Unaccepted)
            {
                this.Unaccepted = t.Unaccepted;
                rc = true;
            }

            if (this.transfer == null && t.transfer != null)
            {
                // Transfer has an owner Transaction, so it can't simply be re-used, it has to be recreated.
                Account a = money.Accounts.FindAccount(t.transfer.Transaction.Account.Name);
                if (a == null)
                {
                    throw new Exception("Cannot merge transction from account that doesn't exist in the target");
                }
                money.Transfer(this, a);
                rc = true;
            }

            // Remember the date of the merged transaction so we can auto-merge next time the user does a download.
            this.MergeDate = t.Date;
            this.OriginalPayee = t.PayeeName;

            Investment i = t.Investment;
            Investment j = this.Investment;
            if (i != null && j != null)
            {
                if (j.Security == null && i.Security != null)
                {
                    j.Security = money.Securities.ImportSecurity(i.Security);
                    rc = true;
                }
                if (j.Type == InvestmentType.None && i.Type != InvestmentType.None)
                {
                    j.Type = i.Type;
                    rc = true;
                }
                if (j.TradeType == InvestmentTradeType.None && i.TradeType != InvestmentTradeType.None)
                {
                    j.TradeType = i.TradeType;
                    rc = true;
                }
                if (j.UnitPrice == 0 && i.UnitPrice != 0)
                {
                    j.UnitPrice = i.UnitPrice;
                    rc = true;
                }
                if (j.Units == 0 && i.Units != 0)
                {
                    j.Units = i.Units;
                    rc = true;
                }
                if (j.Commission == 0 && i.Commission != 0)
                {
                    j.Commission = i.Commission;
                    rc = true;
                }
                if (j.Fees == 0 && i.Fees != 0)
                {
                    j.Fees = i.Fees;
                    rc = true;
                }
                if (j.Load == 0 && i.Load != 0)
                {
                    j.Load = i.Load;
                    rc = true;
                }
                if (j.Taxes == 0 && i.Taxes != 0)
                {
                    j.Taxes = i.Taxes;
                    rc = true;
                }
                if (!j.TaxExempt && i.TaxExempt)
                {
                    j.TaxExempt = i.TaxExempt;
                    rc = true;
                }
                if (j.Withholding == 0 && i.Withholding != 0)
                {
                    j.Withholding = i.Withholding;
                    rc = true;
                }
            }
            return rc;
        }

        public bool Matches(QueryRow q)
        {
            switch (q.Field)
            {
                case Field.None:
                    return false; // or should this allow matching of any field?
                case Field.Accepted:
                    return q.Matches(!this.Unaccepted);
                case Field.Budgeted:
                    return q.Matches(!this.IsBudgeted && this.Account.IsBudgeted);
                case Field.Account:
                    return q.Matches(this.Account.Name);
                case Field.Payment:
                    return this.Amount <= 0 ? q.Matches(-this.Amount) : false;
                case Field.Deposit:
                    return this.Amount >= 0 ? q.Matches(this.Amount) : false;
                case Field.Category:
                    return (this.Category == null) ? false : q.Matches(this.Category.GetFullName());
                case Field.Date:
                    return q.Matches(this.Date);
                case Field.Memo:
                    return q.Matches(this.Memo == null ? "" : this.Memo);
                case Field.Number:
                    return q.Matches(this.Number == null ? "" : this.Number);
                case Field.Payee:
                    return q.Matches(this.Payee == null ? "" : this.Payee.Name);
                case Field.SalesTax:
                    return q.Matches(this.SalesTax);
                case Field.Status:
                    return q.Matches(this.Status.ToString());
            }
            return false;
        }


        public decimal CurrencyNormalizedAmount(decimal Amount)
        {
            // Convert the value to USD 
            // ToDo: Convert to default currency.

            // Use cached value for performance
            if (this.Account.AccountCurrencyRatio != 0)
            {
                return Amount * this.Account.AccountCurrencyRatio;
            }

            MyMoney money = this.Account.Parent.Parent as MyMoney;
            if (money != null)
            {
                Currency c = money.Currencies.FindCurrency(this.account.NonNullCurrency);
                if (c != null)
                {
                    //-----------------------------------------------------
                    // Apply ratio of conversion
                    // for example USD 2,000 * CAN .95 = 1,900 (in USD currency)
                    Amount *= c.Ratio;

                    this.Account.AccountCurrencyRatio = c.Ratio;
                }
                else
                {
                    // We must be using the default currency, so the ratio is 1

                    this.Account.AccountCurrencyRatio = 1;
                }
            }
            return Amount;
        }



        private void UpdateBudget(bool budgeting, List<TransactionException> errors)
        {
            Category c = this.Category;

            if (this.Account.IsCategoryFund)
            {
                c = this.Account.GetFundCategory();
            }

            if (this.salesTax != 0)
            {
                Category salesTax = this.MyMoney.Categories.SalesTax;
                // reverse the sign on the salesTax because it is an expense.
                salesTax.Balance += budgeting ? -this.salesTax : this.salesTax;
            }

            TransactionException ex = null;
            if (this.IsSplit)
            {

                if (this.Splits.Unassigned != 0 && budgeting)
                {
                    ex = new TransactionException(this, "This transaction has an unassigned split amount");
                }
                foreach (Split s in this.Splits.GetSplits())
                {
                    if (s.Category == null)
                    {
                        if (budgeting)
                        {
                            if (s.Transfer != null && !s.Transfer.Transaction.Account.IsBudgeted)
                            {
                                ex = new TransactionException(this, "This transaction has an uncategorized split transfer to a non-budgeted account");
                            }
                            else if (s.Transfer == null)
                            {
                                ex = new TransactionException(this, "This transaction has an uncategorized split");
                            }
                        }
                    }
                    else
                    {
                        s.IsBudgeted = budgeting;
                    }
                }
            }
            else
            {
                if (c == null)
                {
                    if (budgeting)
                    {
                        if (this.Transfer != null && !this.Transfer.Transaction.Account.IsBudgeted)
                        {
                            ex = new TransactionException(this, "This transaction has an uncategorized transfer to a non-budgeted account");
                        }
                        else if (this.Transfer == null)
                        {
                            ex = new TransactionException(this, "This transaction has an no category");
                        }
                    }
                }
                else
                {
                    c.Balance += budgeting ? this.AmountMinusTax : -this.AmountMinusTax;
                }
            }
            if (ex != null)
            {
                if (errors != null)
                {
                    errors.Add(ex);
                }
                else
                {
                    throw ex;
                }
            }
        }

        public override void OnDelete()
        {
            if (this.IsBudgeted && this.account != null && this.account.IsCategoryFund)
            {
                Category c = this.account.GetFundCategory();
                c.Balance -= this.amount;
            }
            base.OnDelete();
        }

        /// <summary>
        /// Find all the objects referenced by this Transaction and wire them back up
        /// </summary>
        /// <param name="money">The owner</param>
        /// <param name="parent">The container</param>
        /// <param name="from">The account this transaction belongs to</param>
        /// <param name="duplicateTransfers">How to handle transfers.  In a cut/paste situation you want
        /// to create new transfer transactions (true), but in a XmlStore.Load situation we do not (false)</param>
        public void PostDeserializeFixup(MyMoney money, Transactions parent, Account from, bool duplicateTransfers)
        {
            this.Parent = parent;

            if (this.CategoryName != null)
            {
                this.Category = money.Categories.GetOrCreateCategory(this.CategoryName, CategoryType.None);
                this.CategoryName = null;
            }
            if (from != null)
            {
                this.Account = from;
            }
            else if (this.AccountName != null)
            {
                this.Account = money.Accounts.FindAccount(this.AccountName);
            }
            this.AccountName = null;
            if (this.PayeeName != null)
            {
                this.Payee = money.Payees.FindPayee(this.PayeeName, true);
                this.PayeeName = null;
            }

            // do not copy budgetting information outside of balancing the budget.
            // (Note: setting IsBudgetted to false will screw up the budget balance).
            this.flags = this.flags & ~TransactionFlags.Budgeted;

            if (duplicateTransfers)
            {
                if (this.TransferName != null)
                {
                    Account to = money.Accounts.FindAccount(this.TransferName);
                    if (to == null)
                    {
                        to = money.Accounts.AddAccount(this.TransferName);
                    }
                    if (to != from)
                    {
                        money.Transfer(this, to);
                        this.TransferName = null;
                    }
                }
            }
            else if (this.TransferId != -1 && this.Transfer == null)
            {
                Transaction other = money.Transactions.FindTransactionById(this.transferId);
                if (this.TransferSplit != -1)
                {
                    // then the other side of this is a split.
                    Split s = other.NonNullSplits.FindSplit(this.TransferSplit);
                    if (s != null)
                    {
                        s.Transaction = other;
                        this.Transfer = new Transfer(0, this, other, s);
                        s.Transfer = new Transfer(0, other, s, this);
                    }
                }
                else if (other != null)
                {
                    this.Transfer = new Transfer(0, this, other);
                    other.Transfer = new Transfer(0, other, this);
                }
            }

            if (this.Investment != null)
            {
                this.Investment.Parent = parent;
                this.Investment.Transaction = this;
                if (this.Investment.SecurityName != null)
                {
                    this.Investment.Security = money.Securities.FindSecurity(this.Investment.SecurityName, true);
                }
            }
            if (this.IsSplit)
            {
                this.Splits.Transaction = this;
                this.Splits.Parent = this;
                foreach (Split s in this.Splits.Items)
                {
                    s.PostDeserializeFixup(money, this, duplicateTransfers);
                }
            }
        }

        #region Investment properties

        [XmlIgnore]
        public InvestmentType InvestmentType
        {
            get
            {
                if (this.Investment != null)
                {
                    return Investment.Type;
                }
                return Data.InvestmentType.None;
            }
            set
            {
                GetOrCreateInvestment().Type = value;
            }
        }

        [XmlIgnore]
        public Security InvestmentSecurity
        {
            get
            {
                if (this.Investment != null)
                {
                    return Investment.Security;
                }
                return null;
            }
            set
            {
                if (value == null || value == Security.None)
                {
                    if (this.Investment != null)
                    {
                        Investment.Security = null;
                    }
                }
                else
                {
                    GetOrCreateInvestment().Security = value;
                }
                OnChanged("InvestmentSecurity");
            }
        }

        [XmlIgnore]
        public string InvestmentSecuritySymbol
        {
            get
            {
                if (this.Investment != null && Investment.Security != null)
                {
                    return Investment.Security.Symbol;
                }
                return null;
            }
            set
            {
                var i = GetOrCreateInvestment();
                if (i.Security == null)
                {
                    this.MyMoney.Securities.AddSecurity(new Security() { Symbol = value });
                }
                else if (i.Security != null && i.Security.Symbol != value)
                {
                    // todo: is the user trying to change the symbol for this security, or are they trying to add a new security?
                }
            }
        }

        [XmlIgnore]
        public decimal InvestmentUnits
        {
            get
            {
                if (this.Investment != null)
                {
                    return Investment.Units;
                }
                return 0;
            }
            set
            {
                GetOrCreateInvestment().Units = value;
            }
        }

        [XmlIgnore]
        public decimal InvestmentUnitPrice
        {
            get
            {
                if (this.Investment != null)
                {
                    return Investment.UnitPrice;
                }
                return 0;
            }
            set
            {
                GetOrCreateInvestment().UnitPrice = value;
            }
        }

        [XmlIgnore]
        public decimal InvestmentCommission
        {
            get
            {
                if (this.Investment != null)
                {
                    return Investment.Commission;
                }
                return 0;
            }
            set
            {
                GetOrCreateInvestment().Commission = value;
            }
        }
        #endregion


        /// <summary>
        /// NOTE: This method has to match what the TransactionView does with it's sort member (date),
        /// plus the SecondarySortOrder="Date,NegativeAmount,Id"
        /// </summary>
        /// <param name="compareTo"></param>
        /// <returns></returns>
        internal int CompareByDate(Transaction compareTo)
        {
            if (this == compareTo)
            {
                return 0;
            }

            if (this.date.Date == compareTo.date.Date)
            {
                // sort deposits first, then withdrawals
                if (compareTo.amount == this.amount)
                {
                    // if amounts are the same, then sort by transaction Id to
                    // ensure a stable sort order.
                    return (int)(this.Id - compareTo.Id);
                }
                else if (compareTo.amount > this.amount)
                {
                    return 1;
                }
                return -1;
            }

            return DateTime.Compare(this.date, compareTo.date);
        }

        // used in TransactionView.xaml as the SecondarySortOrder
        // so that deposits appear before payments when they occur on the same date.
        public decimal NegativeAmount
        {
            get
            {
                return -this.amount;
            }
        }

        #region DataBinding Hack to work around some new weird WPF behavior for Category editing
        [XmlIgnore]
        [IgnoreDataMember]
        public IList<Category> Categories
        {
            get
            {
                return this.MyMoney.Categories.AllCategories;
            }
        }

        public void UpdateCategoriesView()
        {
            RaisePropertyChanged("Categories");
        }

        #endregion
    }

    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Splits : PersistentContainer, ICollection<Split>
    {
        Transaction transaction;
        int nextSplit;
        Hashtable<int, Split> splits;
        decimal unassigned;
        decimal? amountMinusSalesTax;

        public Splits()
        { // for serialization.
        }

        public Splits(Transaction t, PersistentObject parent)
            : base(parent)
        {
            this.transaction = t;
        }

        public override bool FireChangeEvent(Object sender, object item, string name, ChangeType type)
        {
            if (!base.FireChangeEvent(sender, item, name, type))
            {
                if (sender is Split)
                    Rebalance();
                if (this.transaction != null && this.transaction.Parent != null)
                    this.transaction.Parent.FireChangeEvent(sender, item, name, type);
                return false;
            }
            return true;
        }

        public override void EndUpdate()
        {
            base.EndUpdate();
            if (!IsUpdating)
            {
                Rebalance();
            }
        }

        [XmlIgnore]
        public Transaction Transaction
        {
            get { return this.transaction; }
            set {  this.transaction = value; }
        }

        [XmlIgnore]
        public decimal? AmountMinusSalesTax
        {
            get { return this.amountMinusSalesTax; }
            set { this.amountMinusSalesTax = value; }
        }

        [XmlIgnore]
        public int Count
        {
            get
            {
                int count = 0;
                if (splits != null)
                {
                    foreach (var de in this.splits)
                    {
                        Split s = (Split)de.Value;
                        if (!s.IsDeleted)
                        {
                            count++;
                        }
                    }
                }
                return count;

                // [Chris] This method is performance critical and the above code is about twice as fast as the
                // following LINQ implementation.
                //return (splits == null) ? 0 : (from Split s in this.splits.Values where !s.IsDeleted select s).Count(); 
            }
        }

        bool balancing;

        public decimal Rebalance()
        {
            if (!balancing)
            {
                try
                {
                    balancing = true;
                    // calculate unassigned balance and display in status bar.
                    decimal total = 0;
                    if (this.splits != null)
                    {
                        foreach (Split s in this.splits.Values)
                        {
                            if (s.IsDeleted)
                                continue;
                            total += s.Amount;
                        }
                    }
                    if (this.amountMinusSalesTax != null)
                    {
                        this.Unassigned = this.AmountMinusSalesTax.Value - total;
                    } 
                    else
                    {
                        this.Unassigned = this.Transaction.AmountMinusTax - total;
                    }

                    if (this.unassigned != 0)
                    {
                        this.SplitsBalanceMessage = "Unassigned amount " + unassigned.ToString();
                    }
                    else
                    {
                        this.SplitsBalanceMessage = "";
                    }
                }
                finally
                {
                    balancing = false;
                }
            }
            return this.Unassigned;
        }


        public decimal TotalSplitAmount
        {
            get
            {
                if (this.transaction.IsSplit == true)
                {
                    return Rebalance();
                }
                return this.transaction.Amount;
            }
        }

        public decimal Unassigned
        {
            get
            {
                return this.unassigned;
            }
            set
            {
                if (value != this.unassigned)
                {
                    this.unassigned = value;
                    this.FireChangeEvent(this, this, "Unassigned", ChangeType.Changed);
                }
            }
        }

        // for serialization 
        public Split[] Items
        {
            get
            {
                return ((List<Split>)GetSplits()).ToArray();
            }
            set
            {
                if (value != null)
                {
                    foreach (Split s in value)
                    {
                        s.Id = nextSplit++;
                        AddSplit(s);
                    }
                }

                this.FireChangeEvent(this, this, "TotalSplitAmount", ChangeType.Changed);
                this.FireChangeEvent(this, this, "Count", ChangeType.Changed);
            }
        }

        public int IndexOf(Split s)
        {
            if (splits != null)
            {
                int i = 0;
                foreach (var es in this)
                {
                    if (s == es)
                    {
                        return i;
                    }
                    i++;
                }
            }
            return -1;
        }


        string message;

        public string SplitsBalanceMessage
        {
            get { return message; }
            set
            {
                if (this.message != value)
                {
                    this.message = value;
                    this.FireChangeEvent(this, this, "SplitsBalanceMessage", ChangeType.Changed);
                }
            }
        }

        SplitsObservableCollection theSplits;

        public ObservableCollection<Split> ObservableCollection
        {
            get
            {
                if (this.Transaction.IsSplit == false)
                {
                    // there is no need for an observable Collection since this is not a Split transaction
                    return null;
                }

                if (theSplits == null && this.Parent != null)
                {
                    theSplits = new SplitsObservableCollection(this);
                }

                return theSplits;
            }
        }

        class SplitsObservableCollection : ThreadSafeObservableCollection<Split>
        {
            Splits parent;

            public SplitsObservableCollection(Splits splits)
            {
                this.parent = splits;

                foreach (Split s in splits.GetSplits())
                {
                    this.Add(s);
                }
                this.parent.Changed += new EventHandler<ChangeEventArgs>(OnParentChanged);
                Rebalance();
            }

            void OnParentChanged(object sender, ChangeEventArgs args)
            {
                Rebalance();
            }

            MyMoney MyMoney
            {
                get
                {
                    Transaction t = parent.Parent as Transaction;
                    Transactions ts = t.Parent as Transactions;
                    return ts.Parent as MyMoney;
                }
            }

            protected override void InsertItem(int index, Split s)
            {
                base.InsertItem(index, s);
                if (s.Parent == null)
                {
                    s.Parent = this.parent;

                    // Set some expected default value for a new Split
                    s.Id = -1;
                    s.Transaction = parent.Transaction;

                    // Append the collection of Split for this transaction with the new split created by the dataGrid
                    MyMoney money = this.MyMoney;
                    money.BeginUpdate(this);
                    s.Transaction.Splits.AddSplit(s);
                    Rebalance();
                    money.EndUpdate();

                    parent.FireChangeEvent(this, this, "Count", ChangeType.Changed);
                }
            }

            protected override void RemoveItem(int index)
            {
                Split s = this[index];
                MyMoney money = this.MyMoney;
                money.BeginUpdate(this);
                base.RemoveItem(index);
                ((Splits)s.Parent).RemoveSplit(s);
                Rebalance();
                money.EndUpdate();
                parent.FireChangeEvent(this, this, "Count", ChangeType.Changed);
            }

            void Rebalance()
            {
                if (this.parent != null)
                {
                    this.parent.Rebalance();
                }
            }

        }

        public void RemoveAll()
        {
            foreach (Split s in this.GetSplits())
            {
                this.RemoveSplit(s);
            }
            if (theSplits != null)
            {
                theSplits.Clear();
            }
            FireChangeEvent(this, this, "Count", ChangeType.Changed);
            if (this.transaction != null)
            {
                this.transaction.OnChanged("HasCreditAndIsSplit");
                this.transaction.OnChanged("HasDebitAndIsSplit");
            }
            this.nextSplit = 0;
        }

        public Split NewSplit()
        {
            Split s = new Split(this);
            return s;
        }

        public void Insert(int index, Split s)
        {
            if (this.splits == null)
                this.splits = new Hashtable<int, Split>();

            if (s.Id == -1)
            {
                s.Id = nextSplit++;
                s.OnInserted();
            }
            else if (s.Id >= nextSplit)
            {
                nextSplit = s.Id + 1;
            }
            splits[s.Id] = s;
            if (theSplits != null && !theSplits.Contains(s))
            {
                if (index > 0 && index < theSplits.Count)
                {
                    theSplits.Insert(index, s);
                }
                else
                {
                    theSplits.Add(s);
                }
            }
            this.FireChangeEvent(this, s, null, ChangeType.Inserted);
            this.FireChangeEvent(this, this, "Count", ChangeType.Changed);
            if (this.transaction != null)
            {
                this.transaction.OnChanged("HasCreditAndIsSplit");
                this.transaction.OnChanged("HasDebitAndIsSplit");
                this.transaction.OnChanged("NonNullSplits");
            }
        }

        public void AddSplit(Split s)
        {
            int len = (theSplits != null ? theSplits.Count : 0);
            Insert(len, s);
        }

        public Split AddSplit()
        {
            Split s = NewSplit();
            AddSplit(s);
            return s;
        }
        public Split AddSplit(int id)
        {
            Split s = new Split(this);
            s.Id = id;
            if (id >= nextSplit) nextSplit = id + 1;
            AddSplit(s);
            return s;
        }

        public Split FindSplit(int id)
        {
            if (this.splits == null) return null;
            return (Split)this.splits[id];
        }


        public Split FindSplitContainingCategory(Category c)
        {
            if (this.splits == null) return null;
            foreach (Split s in this.GetSplits())
            {
                if (c.Contains(s.Category))
                    return s;
            }
            return null;
        }

        public bool RemoveSplit(Split s)
        {
            if (this.splits == null) return false;
            s.Transfer = null; // remove the transfer.
            if (s.IsInserted)
            {
                // then we can remove it immedately.
                if (this.splits.ContainsKey(s.Id))
                    this.splits.Remove(s.Id);
            }

            // mark it for removal on next save.
            s.OnDelete();
            if (theSplits != null && theSplits.Contains(s))
            {
                theSplits.Remove(s);
            }
            this.FireChangeEvent(this, s, null, ChangeType.Deleted);
            this.FireChangeEvent(this, this, "Count", ChangeType.Changed);
            this.transaction.OnChanged("HasCreditAndIsSplit");
            this.transaction.OnChanged("HasDebitAndIsSplit");
            return true;
        }

        public IList<Split> GetSplits()
        {
            List<Split> list = new List<Split>();
            if (this.splits == null) return list;
            foreach (Split s in this.splits.Values)
            {
                if (!s.IsDeleted)
                {
                    list.Add(s);
                }
            }
            list.Sort(new SplitIdComparer());
            return list;
        }

        public bool CheckTransfers(MyMoney money, List<Transaction> dangling, List<Account> deletedaccounts)
        {
            bool add = false;
            if (this.splits != null)
            {
                Transaction t = this.Transaction;
                foreach (Split s in this.splits.Values)
                {
                    if (s.to != null && s.Transfer == null)
                    {
                        if (Transaction.IsDeletedAccount(s.to, money, deletedaccounts))
                        {
                            s.Category = s.Amount < 0 ? money.Categories.TransferToDeletedAccount :
                                money.Categories.TransferFromDeletedAccount;
                            s.to = null;
                        }
                        else
                        {
                            add = true;
                        }
                    }
                    else if (s.Transfer != null)
                    {
                        Transfer other = s.Transfer.Transaction.Transfer;
                        if (other == null || other.Transaction != t)
                        {
                            add = true;
                        }
                    }
                }
            }
            return add;
        }

        // deserialize the given splits and add to this set
        public void DeserializeInto(MyMoney money, string xml)
        {
            DataContractSerializer xs = new DataContractSerializer(typeof(Splits));
            using (StringReader sr = new StringReader(xml))
            {
                Splits result = null;
                using (XmlReader r = XmlReader.Create(sr))
                {
                    result = (Splits)xs.ReadObject(r);
                }
                foreach (Split s in result.Items)
                {
                    // Now fix up the splits with the given transaction and 
                    // fix the category and transfer.            
                    if (!s.IsDeleted)
                    {
                        s.PostDeserializeFixup(money, this.transaction, true);
                        s.Id = -1; // re-assign id's.
                        this.AddSplit(s);
                    }
                }
            }
            this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
        }

        #region ICollection

        public void Add(Split item)
        {
            this.AddSplit(item);
        }

        public void Clear()
        {
            RemoveAll();
        }

        public bool Contains(Split item)
        {
            return this.splits == null ? false : this.splits.ContainsKey(item.Id);
        }

        public void CopyTo(Split[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public override void Add(object child)
        {
            Add((Split)child);
        }

        public override void RemoveChild(PersistentObject pe)
        {
            Remove((Split)pe);
        }

        public bool Remove(Split item)
        {
            return this.RemoveSplit(item);
        }

        public new IEnumerator<Split> GetEnumerator()
        {
            if (this.splits != null)
            {
                foreach (Split s in this.splits.Values)
                {
                    yield return s;
                }
            }
        }

        protected override IEnumerator<PersistentObject> InternalGetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        /// <summary>
        /// Cleanup any splits that looks really empty
        /// </summary>
        internal void RemoveEmptySplits()
        {
            foreach (Split s in new List<Split>(this.splits.Values))
            {
                //
                // only do this if the Split looks totally empty
                //
                if (s.Amount == 0 && string.IsNullOrWhiteSpace(s.CategoryName) && string.IsNullOrWhiteSpace(s.PayeeName) && string.IsNullOrWhiteSpace(s.Memo))
                {
                    this.Remove(s);
                }
            }
        }

        internal bool Merge(MyMoney money, Splits other)
        {
            bool changed = false; 
            foreach (Split o in other)
            {
                if (o.CategoryName == money.Categories.TransferToDeletedAccount.Name ||
                    o.CategoryName == money.Categories.TransferFromDeletedAccount.Name)
                {
                    // hmmm, the imported database has deleted account!
                    // so best we can do is skip this one.
                    continue;    
                }

                Split s = this.FindSplit(o.Id);
                if (s == null)
                {
                    // add split
                    s = this.NewSplit();
                    changed = true;
                }


                if (s.Transfer != null)
                {
                    if (o.Transfer == null)
                    {
                        // no longer a transfer?
                        throw new Exception("Cannot merge deleted split");
                    }
                    else if (o.Transfer.Transaction.AccountName == s.Transfer.Transaction.AccountName)
                    {
                        // good
                    }
                    else
                    {
                        throw new Exception("Cannot merge split that are transfered to different places");
                    }
                }
                else
                {
                    if (o.Transfer != null)
                    {
                        string acct = o.Transfer.Transaction.AccountName;
                        Account a = money.Accounts.FindAccount(acct);
                        if (a == null)
                        {
                            throw new Exception("Cannot merge split transfer to non-existing account: " + acct);
                        }
                        money.Transfer(s, a);
                    }
                }
                if (s.amount != o.amount)
                {
                    changed = true;
                    s.Amount = o.amount;
                }
                if (s.CategoryName != o.CategoryName)
                {
                    s.Category = money.Categories.ImportCategory(o.Category);
                    changed = true;
                }
                if (s.Flags != o.Flags)
                {
                    changed = true;
                    s.Flags = o.Flags;
                }
                if (s.Memo != o.Memo)
                {
                    changed = true;
                    s.Memo = o.Memo;
                }
                if (s.PayeeName != o.PayeeName)
                {
                    s.Payee = money.Payees.ImportPayee(o.Payee);
                    changed = true;
                }
            }

            // delete splits not found in 'other'
            foreach (Split s in this)
            {
                Split o = other.FindSplit(s.Id);
                if (o == null)
                {
                    this.RemoveSplit(s);
                }
            }
            return changed;
        }
    }

    public enum SplitFlags
    {
        None,
        Budgeted = 1,
    }

    //================================================================================
    [TableMapping(TableName = "Splits")]
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Split : PersistentObject
    {
        Transaction transaction;
        int id = -1;
        Category category;
        Payee payee;
        internal decimal amount; // so we can keep transfer's in sync
        Transfer transfer;
        string memo;
        internal Account to; // for debugging only
        string pendingTransfer;
        SplitFlags flags;
        DateTime? budgetBalanceDate;

        public Split()
            : base(null)
        { // for serializer
        }

        public Split(Splits parent)
            : base(parent)
        {
            this.transaction = parent.Transaction;
        }

        [XmlIgnore] // this is a parent pointer..
        [ColumnObjectMapping(ColumnName = "Transaction", KeyProperty = "Id")]
        public Transaction Transaction
        {
            get { return this.transaction; }
            set { if (this.transaction != value) { this.transaction = value; OnChanged("Transaction"); } }
        }

        [XmlAttribute]
        [DataMember]
        [ColumnMapping(ColumnName = "Id")]
        public int Id
        {
            get { return this.id; }
            set { if (this.id != value) { this.id = value; OnChanged("Id"); } }
        }

        [XmlIgnore]
        [ColumnObjectMapping(ColumnName = "Category", KeyProperty = "Id", AllowNulls = true)]
        public Category Category
        {
            get { return this.category; }
            set
            {
                if (this.category != value)
                {
                    if (!this.BatchMode && this.transaction.IsBudgeted)
                    {
                        Category old = this.category;
                        if (old != null)
                        {
                            old.Balance -= this.amount;
                        }
                        if (value != null)
                        {
                            value.Balance += this.amount;
                        }
                    }
                    this.category = value;
                    OnChanged("Category");
                }
            }
        }

        string categoryName;
        // for serialization;
        [DataMember]
        public string CategoryName
        {
            get { return category != null ? category.Name : this.categoryName; }
            set { this.categoryName = value; }
        }


        [XmlIgnore]
        [ColumnObjectMapping(ColumnName = "Payee", KeyProperty = "Id", AllowNulls = true)]
        public Payee Payee
        {
            get { return this.payee; }
            set
            {
                if (this.payee != value)
                {
                    this.payee = value; OnChanged("Payee");
                    OnChanged("PayeeOrTransferCaption");
                }
            }
        }

        string payeeName;
        // for serialization
        [DataMember]
        public string PayeeName
        {
            get { return payee != null ? payee.Name : this.payeeName; }
            set { this.payeeName = value; }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Amount", SqlType = typeof(SqlMoney))]
        public decimal Amount
        {
            get { return this.amount; }
            set
            {
                if (this.amount != value)
                {
                    if (this.transfer != null)
                    {
                        if (this.transfer.Transaction.Account.Currency == this.Transaction.Account.Currency)
                        {

                            // Keep the other side of transfer in sync.
                            if (this.transfer.Transaction.Status == TransactionStatus.Reconciled)
                            {
                                throw new MoneyException("Other side of transfer is reconciled");
                            }
                            if (this.transfer.Split != null)
                            {
                                this.transfer.Split.InternalSetAmount(-value); // negated
                            }
                            else
                            {
                                this.transfer.Transaction.InternalSetAmount(-value); // negated
                            }
                        }
                        else
                        {
                            // Allow these to be different since we don't know how to translate across currencies.
                            // todo: remind the user to fix the other side...
                        }
                    }
                    InternalSetAmount(value);
                }
            }
        }

        // Set the amount without checking for transfers.
        internal void InternalSetAmount(decimal value)
        {
            OnAmountChanged(this.amount, value);
            this.amount = value;
            OnChanged("Amount");
        }

        long transferId = -1;

        [DataMember]
        public long TransferId
        {
            get { return transferId; }
            set { transferId = value; }
        }

        [DataMember]
        public string TransferTo
        {
            get
            {
                if (this.pendingTransfer != null)
                    return pendingTransfer;

                if (this.transfer != null)
                {
                    Debug.Assert(this.transfer.Split == null);
                    return this.transfer.Transaction.Account.Name;
                }
                return null;
            }
            set
            {
                this.pendingTransfer = value;
            }
        }

        [XmlIgnore] /* not serializable */
        [ColumnObjectMapping(ColumnName = "Transfer", KeyProperty = "TransactionId", SqlType = typeof(SqlInt64), AllowNulls = true)]
        public Transfer Transfer
        {
            get { return this.transfer; }
            set
            {
                if (this.transfer != value)
                {
                    if (value == null && this.transfer != null)
                    {
                        if (this.transfer.Transaction.Status == TransactionStatus.Reconciled &&
                            !this.transfer.Transaction.IsDeleted)
                        {
                            throw new MoneyException("Other side of transfer is reconciled");
                        }
                    }
                    MyMoney money = this.transaction.MyMoney;
                    if (money != null)
                    {
                        money.OnSplitTransferChanged(this, value);
                    }
                    this.transfer = value;
                    this.transferId = (value == null) ? -1 : transfer.Transaction.Id;
                    OnChanged("Transfer");
                }
            }
        }

        public void ClearTransfer()
        {
            this.transfer = null;
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Memo", MaxLength = 255, AllowNulls = true)]
        public string Memo
        {
            get { return this.memo; }
            set { if (this.memo != value) { this.memo = Truncate(value, 255); OnChanged("Memo"); } }
        }

        [XmlIgnore]
        public SqlDecimal Credit
        {
            get { return (Amount > 0) ? new SqlDecimal(Amount) : SqlDecimal.Null; }
            set { if (!value.IsNull) Amount = (decimal)value; OnChanged("Debit"); OnChanged("Credit"); }
        }

        [XmlIgnore]
        public SqlDecimal Debit
        {
            get { return (Amount <= 0) ? new SqlDecimal(-Amount) : SqlDecimal.Null; }
            set { if (!value.IsNull) Amount = -value.Value; OnChanged("Debit"); OnChanged("Credit"); }
        }

        [XmlIgnore]
        public bool Unaccepted
        {
            get { return transaction.Unaccepted; }
        }


        [IgnoreDataMember]
        public bool IsBudgeted
        {
            get { return (this.flags & SplitFlags.Budgeted) != 0; }
            set { SetBudgeted(value, null); }
        }

        public void SetBudgeted(bool value, List<TransactionException> errors)
        {
            if (this.IsBudgeted != value)
            {
                if (value)
                {
                    this.UpdateBudget(true, errors);
                    SetFlag(SplitFlags.Budgeted);
                }
                else
                {
                    this.UpdateBudget(false, errors);
                    ClearFlag(SplitFlags.Budgeted);
                    this.BudgetBalanceDate = null;
                }
                OnChanged("IsBudgeted");
            }
        }


        void SetFlag(SplitFlags flag)
        {
            this.Flags |= flag;
        }

        void ClearFlag(SplitFlags flag)
        {
            this.Flags ^= flag;
        }

        [ColumnMapping(ColumnName = "Flags", SqlType = typeof(SqlInt32), AllowNulls = true)]
        [XmlIgnore] // the silly XmlSerializer can serialize this as 'TransactionFlags'
        public SplitFlags Flags
        {
            get { return this.flags; }
            set
            {
                if (this.flags != value)
                {
                    this.flags = value;
                    OnChanged("Flags");
                }
            }
        }

        // This one is for serialization.
        [DataMember]
        public int SerializedFlags
        {
            get { return (int)this.flags; }
            set { this.flags = (SplitFlags)value; }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "BudgetBalanceDate", AllowNulls = true)]
        public DateTime? BudgetBalanceDate
        {
            get { return this.budgetBalanceDate; }
            set
            {
                if (this.budgetBalanceDate != value)
                {
                    this.budgetBalanceDate = value;
                    OnChanged("BudgetBalanceDate");
                }
            }
        }

        private void UpdateBudget(bool budgeting, List<TransactionException> errors)
        {
            Category c = this.Category;
            TransactionException ex = null;
            if (c == null)
            {
                ex = new TransactionException(this.transaction, "This transaction has an no category, so it cannot be budgeted");
            }
            else
            {
                c.Balance += budgeting ? this.Amount : -this.Amount;
            }

            if (ex != null)
            {
                if (errors != null)
                {
                    errors.Add(ex);
                }
                else
                {
                    throw ex;
                }
            }
        }

        public void PostDeserializeFixup(MyMoney money, Transaction owner, bool duplicateTransfers)
        {
            PersistentContainer parent = owner.NonNullSplits;
            this.OnInserted();
            this.Transaction = owner;
            this.Parent = parent;


            // do not copy budgetting information outside of balancing the budget.
            // (Note: setting IsBudgetted to false will screw up the budget balance).
            this.flags = this.flags & ~SplitFlags.Budgeted;

            if (this.payeeName != null)
            {
                this.Payee = money.Payees.FindPayee(this.payeeName, true);
                this.payeeName = null;
            }
            if (this.CategoryName != null)
            {
                this.Category = money.Categories.GetOrCreateCategory(this.CategoryName, CategoryType.None);
                CategoryName = null;
            }
            if (duplicateTransfers)
            {
                if (this.TransferTo != null)
                {
                    Account to = money.Accounts.FindAccount(this.TransferTo);
                    if (to != null)
                    {
                        if (owner.Account == to)
                        {
                            // cannot transfer to the same account
                            // so we have to leave it dangling...
                            this.memo = "Could not transfer to " + this.TransferTo;
                        }
                        else
                        {
                            money.Transfer(this, to);
                        }
                    }
                    this.TransferTo = null;
                }
            }
            else if (TransferId != -1 && this.Transfer == null)
            {
                Transaction other = money.Transactions.FindTransactionById(this.transferId);
                if (other != null)
                {
                    this.Transfer = new Transfer(0, this.transaction, this, other);
                    other.Transfer = new Transfer(0, other, this.transaction, this);
                }
            }
        }

        private string GetPayeeOrTransferCaption()
        {
            Transfer transfer = null;
            decimal amount = 0;

            transfer = this.Transfer;
            amount = this.Amount;

            bool isFrom = false;
            if (transfer != null)
            {
                if (amount > 0)
                {
                    isFrom = true;
                }
                return Walkabout.Data.Transaction.GetTransferCaption(transfer.Transaction.Account, isFrom);
            }

            if (this.payee == null) return string.Empty;

            return this.Payee != null ? this.payee.Name : "";
        }

        [XmlIgnore]
        public string PayeeOrTransferCaption
        {
            get { return GetPayeeOrTransferCaption(); }
            set
            {
                if (this.PayeeOrTransferCaption != value)
                {
                    MyMoney money = MyMoney;

                    if (string.IsNullOrEmpty(value))
                    {
                        this.Payee = null;
                    }
                    else if (Walkabout.Data.Transaction.IsTransferCaption(value))
                    {
                        if (money != null)
                        {
                            string accountName = Walkabout.Data.Transaction.ExtractTransferAccountName(value);
                            Account a = money.Accounts.FindAccount(accountName);
                            if (a != null)
                            {
                                // the 'to' or 'from' ness of this transfer is simply controlled by the 'sign' of the amount being transfered.
                                money.Transfer(this, a);
                            }
                        }
                    }
                    else
                    {
                        // find MyMoney container
                        if (money != null)
                        {
                            this.Payee = money.Payees.FindPayee(value, true);
                        }
                    }
                }
            }
        }

        [XmlIgnore]
        MyMoney MyMoney
        {
            get
            {
                Splits parent = this.Parent as Splits;
                if (parent != null)
                {
                    Transactions tran = this.Transaction.Parent as Transactions;
                    if (tran != null)
                    {
                        return tran.Parent as MyMoney;
                    }
                }
                return null;
            }
        }

        void OnAmountChanged(decimal oldValue, decimal newValue)
        {
            if (!this.BatchMode && this.transaction != null && this.transaction.IsBudgeted && !this.transaction.IsFake)
            {
                decimal diff = newValue - oldValue;
                Account account = this.transaction.Account;
                if (account != null && account.IsCategoryFund && this.Transfer != null)
                {
                    Category c = account.GetFundCategory();
                    c.Balance += diff;
                }
                else if (this.category != null)
                {
                    this.category.Balance += diff;
                }
            }
        }

        #region DataBinding Hack to work around some new weird WPF behavior for Category editing

        [XmlIgnore]
        [IgnoreDataMember]
        public IList<Category> Categories
        {
            get
            {
                return new List<Category>(this.MyMoney.Categories.AllCategories);
            }
        }

        public void UpdateCategoriesView()
        {
            RaisePropertyChanged("Categories");
        }

        #endregion

    }

    class SplitIdComparer : IComparer<Split>
    {
        public int Compare(Split a, Split b)
        {
            if (a == null && b != null) return -1;
            if (a != null && b == null) return 1;
            return a.Id - b.Id;
        }
    }

    //================================================================================
    public enum InvestmentType
    {
        Add,
        Remove,
        Buy,
        Sell,
        None,
        Dividend
    }

    //================================================================================
    // This class is an extension of Transaction, used for investment transactions only
    // It is 1:1 with Transaction, so it doesn't need it's own change tracking.
    [TableMapping(TableName = "Investments")]
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Investment : PersistentObject
    {
        long id;
        Security security;
        decimal unitprice;
        decimal units;
        decimal commission;
        InvestmentType type = InvestmentType.None;
        InvestmentTradeType tradeType = InvestmentTradeType.None;
        bool taxExempt;
        decimal withholding;
        decimal markupdown;
        decimal taxes;
        decimal fees;
        decimal load;
        // post stock split
        decimal currentUnits;
        decimal currentUnitPrice;
        Transaction transaction;

        public Investment()
        { // for serialization 
        }

        public Investment(PersistentContainer parent)
            : base(parent)
        {
        }

        public Transaction Transaction
        {
            get { return this.transaction; }
            set { this.transaction = value; }
        }

        [DataMember]
        [XmlAttribute]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true, SqlType = typeof(SqlInt64))]
        public long Id
        {
            get { return id; }
            set { id = value; }
        }

        // The actual transaction date.
        [IgnoreDataMember]
        public DateTime Date { get { return this.transaction.Date; } }

        // for summary portfolio transactions.
        [IgnoreDataMember]
        public DateTime DateAquired { get; set; }

        [IgnoreDataMember]
        [ColumnObjectMapping(ColumnName = "Security", KeyProperty = "Id")]
        public Security Security
        {
            get { return this.security; }
            set
            {
                if (this.security != value)
                {
                    this.security = value; OnChanged("Security");
                }
            }
        }

        string securityName; // used for serialization only.

        [DataMember]
        public string SecurityName
        {
            get { return this.security != null ? this.security.Name : securityName; }
            set { securityName = value; }
        }

        /// <summary>
        /// This is a computed field (it is the current market unit price for the security).
        /// </summary>
        public decimal Price
        {
            get { return this.security != null ? this.security.Price : 0; }
            set
            {
                if (this.security != null)
                {
                    this.security.Price = value; OnChanged("Price");
                }
            }
        }

        /// <summary>
        /// What you paid for the security (used in computing your cost basis)
        /// </summary>
        [DataMember]
        [ColumnMapping(ColumnName = "UnitPrice", SqlType = typeof(SqlMoney), OldColumnName = "Cost")]
        public decimal UnitPrice
        {
            get { return this.unitprice; }
            set { if (this.unitprice != value) { this.unitprice = value; OnChanged("UnitPrice"); } }
        }

        /// <summary>
        /// For example, how many shares did you buy
        /// </summary>
        [DataMember]
        [ColumnMapping(ColumnName = "Units", OldColumnName = "Quantity", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Units
        {
            get { return this.units; }
            set { if (this.units != value) { this.units = value; OnChanged("Units"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Commission", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Commission
        {
            get { return this.commission; }
            set { if (this.commission != value) { this.commission = value; OnChanged("Commission"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "MarkUpDown", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal MarkUpDown
        {
            get { return this.markupdown; }
            set { if (this.markupdown != value) { this.markupdown = value; OnChanged("MarkUpDown"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Taxes", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Taxes
        {
            get { return this.taxes; }
            set { if (this.taxes != value) { this.taxes = value; OnChanged("Taxes"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Fees", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Fees
        {
            get { return this.fees; }
            set { if (this.fees != value) { this.fees = value; OnChanged("Fees"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Load", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Load
        {
            get { return this.load; }
            set { if (this.load != value) { this.load = value; OnChanged("Load"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "InvestmentType", SqlType = typeof(SqlInt32))]
        public InvestmentType Type
        {
            get { return this.security == null ? InvestmentType.None : this.type; }
            set { if (this.type != value) { this.type = value; OnChanged("InvestmentType"); } }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "TradeType", SqlType = typeof(SqlInt32), AllowNulls = true)]
        public InvestmentTradeType TradeType
        {
            get { return this.tradeType; }
            set { if (this.tradeType != value) { this.tradeType = value; OnChanged("TradeType"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "TaxExempt", SqlType = typeof(SqlBoolean), AllowNulls = true)]
        public bool TaxExempt
        {
            get { return this.taxExempt; }
            set { if (this.taxExempt != value) { this.taxExempt = value; OnChanged("TaxExempt"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Withholding", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Withholding
        {
            get { return this.withholding; }
            set { if (this.withholding != value) { this.withholding = value; OnChanged("Withholding"); } }
        }

        [XmlIgnore]
        public decimal TotalCost
        {
            get { return this.unitprice * this.units; }
        }

        [XmlIgnore]
        public bool IsReadOnly
        {
            get { return this.Units <= 0; }
        }


        /// <summary>
        /// TODO : This entry is only here to please the DataTemplate Grid View
        /// </summary>
        [XmlIgnore]
        public bool Unaccepted
        {
            get { return false; }
        }


        /// <summary>
        /// This is here just so we can XAML bind to Investment and Transaction without lots of debug output warnings about this property.
        /// </summary>
        [XmlIgnore]
        public bool IsReconciling
        {
            get { return false; }
            set { }
        }


        // The stock split adjusted Units, minus any Sell transactions that have happened along the way.
        public decimal CurrentUnits
        {
            get { return this.currentUnits; }
            set { this.currentUnits = value; }
        }

        // The stock split adjusted unit price.  If you bought 10 shares at $1 each, and there was a 2 for 1 split
        // then you have 20 shares, and the current unit price then is 50 cents each and this is the price used
        // in calculating the Cost Basis below.
        public decimal CurrentUnitPrice
        {
            get { return currentUnitPrice; }
            set { currentUnitPrice = value; }
        }

        public decimal OriginalCostBasis
        {
            get
            {
                // looking for the original unsplit cost basis at the date of this transaction.                
                decimal proceeds = this.UnitPrice * this.Units;

                if (this.transaction.amount != 0)
                {
                    // We may have paid more for the stock than "price" in a buy transaction because of brokerage fees and
                    // this can be included in the cost basis.  We may have also received less than "price" in a sale
                    // transaction, and that can also reduce our capital gain, so we use the transaction amount if we 
                    // have one.
                    return Math.Abs(this.transaction.amount);
                }

                // But if the sale proceeds were not recorded for some reason, then we fall back on the proceeds.
                return proceeds;
            }
        }

        public decimal CostBasis
        {
            get
            {
                // get the current "split-aware" cost basis 
                return CurrentUnitPrice * CurrentUnits;
            }
        }

        public decimal MarketValue 
        { 
            get 
            { 
                decimal factor = 1;

                // futures prices are always listed by the instance.  But wen you buy 1 contract, you always get 100 futures in that contract
                if (this.Security.SecurityType == SecurityType.Futures)
                {
                    factor = 100;
                }
                return factor * CurrentUnits * this.Security.Price; 
            }
        }

        [XmlIgnore]
        public decimal GainLoss { get { return MarketValue - CostBasis; } }

        [XmlIgnore]
        public decimal PercentGainLoss { get { return CostBasis == 0 ? 0 : (GainLoss * 100) / CostBasis; } }

        [XmlIgnore]
        public bool IsDown { get { return this.GainLoss < 0; } }

        public void ApplySplit(StockSplit s)
        {
            if (s.Date > this.Date && s.Denominator != 0 && s.Numerator != 0)
            {
                currentUnits = (currentUnits * s.Numerator) / s.Denominator;
                currentUnitPrice = (currentUnitPrice * s.Denominator) / s.Numerator;
            }
        }

        internal void ResetCostBasis()
        {
            this.currentUnits = this.Units;
            this.currentUnitPrice = this.UnitPrice;
        }

        internal void Merge(Investment i)
        {
            this.security = i.security;
            this.Price = i.Price;
            this.unitprice = i.unitprice;
            this.units = i.units;
            this.commission = i.commission;
            this.markupdown = i.markupdown;
            this.taxes = i.taxes;
            this.fees = i.fees;
            this.load = i.load;
            this.type = i.type;
            this.tradeType = i.tradeType;
            this.taxExempt = i.taxExempt;
            this.withholding = i.withholding;
        }

    }

    public enum InvestmentTradeType
    {
        None = 0,
        Buy = 1, BuyToOpen = 2, BuyToCover = 3, BuyToClose = 4,
        Sell = 5, SellShort = 6
    }

    /// <summary>
    /// This helper class is for maintaining a list of StockSplits associated with a given security.
    /// </summary>
    public class ObservableStockSplits : ThreadSafeObservableCollection<StockSplit>
    {
        Security security;
        StockSplits splits;
        bool initializing;

        public ObservableStockSplits(Security security, StockSplits splits)
        {
            initializing = true;
            this.security = security;
            this.splits = splits;
            int index = 0;
            foreach (StockSplit s in splits.GetStockSplitsForSecurity(security))
            {
                this.Insert(index++, s);
            }
            initializing = false;
            // todo: listen to MyMoney.StocksSplits changes and sync this collection...
        }

        public Security Security { get { return this.security; } }

        protected override void InsertItem(int index, StockSplit item)
        {
            base.InsertItem(index, item);
            if (!initializing)
            {
                splits.AddStockSplit(item);
                item.Security = this.security;
            }
        }

        protected override void RemoveItem(int index)
        {
            StockSplit s = this[index];
            base.RemoveItem(index);
            splits.RemoveStockSplit(s);
        }

        protected override void ClearItems()
        {
            foreach (StockSplit s in this)
            {
                s.OnDelete();
            }
            base.ClearItems();
        }

        /// <summary>
        /// Cleanup any splits that looks really empty
        /// </summary>
        internal void RemoveEmptySplits()
        {
            for (int i = this.Count - 1; i >= 0; )
            {
                StockSplit s = this[i];
                // only do this if the Split looks totally empty
                if (s.Date == new DateTime() && s.Numerator == 0 && s.Denominator == 0)
                {
                    splits.RemoveStockSplit(s);
                    RemoveItem(i);
                    if (i == this.Count)
                    {
                        i--;
                    }
                }
                else
                {
                    i--;
                }
            }
        }
    }

    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class StockSplits : PersistentContainer, ICollection<StockSplit>
    {
        long nextStockSplit;
        Hashtable<long, StockSplit> stockSplits = new Hashtable<long, StockSplit>();

        // for serialization only
        public StockSplits()
        {
        }
        public StockSplits(PersistentObject parent)
            : base(parent)
        {
        }
        public void Clear()
        {
            if (this.nextStockSplit != 0 || this.stockSplits.Count != 0)
            {
                this.nextStockSplit = 0;
                this.stockSplits = new Hashtable<long, StockSplit>();
                this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }

        public StockSplit NewStockSplit()
        {
            StockSplit s = new StockSplit(this);
            s.Id = this.nextStockSplit++;
            this.stockSplits[s.Id] = s;
            this.FireChangeEvent(this, s, null, ChangeType.Inserted);
            return s;
        }

        // StockSplits
        public StockSplit AddStockSplit(long id)
        {
            StockSplit s = new StockSplit(this);
            s.Id = id;
            if (this.nextStockSplit <= id) this.nextStockSplit = id + 1;
            this.stockSplits[id] = s;
            this.FireChangeEvent(this, s, null, ChangeType.Inserted);
            return s;
        }

        public StockSplit AddStockSplit(StockSplit s)
        {
            s.Parent = this;
            if (s.Id == -1)
            {
                s.Id = nextStockSplit++;
                s.OnInserted();
            }
            else if (nextStockSplit <= s.Id)
            {
                nextStockSplit = s.Id + 1;
            }
            this.stockSplits[s.Id] = s;
            this.FireChangeEvent(this, s, null, ChangeType.Inserted);
            return s;
        }

        public StockSplit FindStockSplitById(long id)
        {
            return (StockSplit)this.stockSplits[id];
        }

        // todo: there should be no references left at this point...
        public bool RemoveStockSplit(StockSplit s)
        {
            if (s.IsInserted)
            {
                // then we can remove it immediately.
                if (this.stockSplits.ContainsKey(s.Id))
                    this.stockSplits.Remove(s.Id);
            }
            // mark it for removal on next save
            s.OnDelete();
            return true;
        }

        public IList<StockSplit> GetStockSplitsForSecurity(Security s)
        {
            List<StockSplit> list = new List<StockSplit>();
            foreach (StockSplit split in this.stockSplits.Values)
            {
                if (!s.IsDeleted && split.Security == s)
                {
                    list.Add(split);
                }
            }
            return list;
        }

        internal void OnSecurityRemoved(Security s)
        {
            foreach (StockSplit split in this.stockSplits.Values)
            {
                if (split.Security == s)
                {
                    split.OnDelete();
                }
            }
        }

        #region ICollection

        public void Add(StockSplit item)
        {
            this.AddStockSplit(item);
        }

        public bool Contains(StockSplit item)
        {
            return stockSplits.ContainsKey(item.Id);
        }

        public void CopyTo(StockSplit[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { return stockSplits.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public override void Add(object child)
        {
            Add((StockSplit)child);
        }

        public override void RemoveChild(PersistentObject pe)
        {
            Remove((StockSplit)pe);
        }

        public bool Remove(StockSplit item)
        {
            return this.RemoveStockSplit(item);
        }

        public new IEnumerator<StockSplit> GetEnumerator()
        {
            foreach (StockSplit s in this.stockSplits.Values)
            {
                yield return s;
            }
        }

        protected override IEnumerator<PersistentObject> InternalGetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion


    }

    [TableMapping(TableName = "StockSplits")]
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class StockSplit : PersistentObject
    {
        long id = -1;
        Security security;
        DateTime date;

        // If numerator is less than denominator then it is a stock split, otherwise
        // it is a reverse stock split.
        decimal numerator;
        decimal denominator;

        public StockSplit()
        { // for serialization
        }

        public StockSplit(StockSplits container) : base(container) { }

        [XmlAttribute]
        [DataMember]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true)]
        public long Id
        {
            get { return this.id; }
            set
            {
                if (this.id != value)
                {
                    this.id = value; OnChanged("Id");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Date")]
        public DateTime Date
        {
            get { return date; }
            set
            {
                if (this.date != value)
                {
                    date = value;
                    OnChanged("Date");
                }
            }
        }

        [DataMember]
        [ColumnObjectMapping(ColumnName = "Security", KeyProperty = "Id")]
        public Security Security
        {
            get { return this.security; }
            set
            {
                if (this.security != value)
                {
                    this.security = value; OnChanged("Security");
                }
            }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "Numerator", SqlType = typeof(SqlMoney))]
        public decimal Numerator
        {
            get { return numerator; }
            set
            {
                if (this.numerator != value)
                {
                    numerator = value;
                    OnChanged("Numerator");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Denominator", SqlType = typeof(SqlMoney))]
        public decimal Denominator
        {
            get { return denominator; }
            set
            {
                if (this.denominator != value)
                {
                    if (value == 0)
                    {
                        throw new ArgumentOutOfRangeException("Cannot set a zero denominator");
                    }
                    denominator = value;
                    OnChanged("Denominator");
                }
            }
        }
    }


    public class TransactionException : Exception
    {
        Transaction t;

        public TransactionException(Transaction t, string message)
            : base(message)
        {
            this.t = t;
        }

        public Transaction Transaction { get { return t; } }
    }

    class MoneyException : Exception
    {
        public MoneyException(string message)
            : base(message)
        {
        }

    }

}


