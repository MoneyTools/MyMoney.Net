using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Utilities;
#if PerformanceBlocks
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
#endif

namespace Walkabout.Data
{
    public enum ChangeType { None, Changed, Inserted, Deleted, Reloaded, Rebalanced, ChildChanged, TransientChanged };

    public delegate void ErrorHandler(string errorMessage);

    public delegate bool AccountFilterPredicate(Account a);


    public class ChangeEventArgs : EventArgs
    {
        private readonly object item;
        private readonly string name;
        private readonly ChangeType type;
        private ChangeEventArgs next;
        private object source;

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

    internal class BatchSync
    {
        private readonly object syncObject = new object();
        private int refCount;

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
        private BatchSync batched;
        private bool changePending;
        private bool saveChangeHistory;
        private ChangeEventArgs head;
        private ChangeEventArgs tail;
        private PersistentObject parent;
        private EventHandlerCollection<ChangeEventArgs> handlers;

        private BatchSync GetBatched()
        {
            if (this.batched == null)
            {
                this.batched = new BatchSync();
            }
            return this.batched;
        }


        protected PersistentContainer()
        {
        }

        protected PersistentContainer(PersistentObject parent)
        {
            this.parent = parent;
        }

        public PersistentObject Parent { get { return this.parent; } set { this.parent = value; } }

        public event EventHandler<ChangeEventArgs> Changed
        {
            add
            {
                if (this.handlers == null)
                {
                    this.handlers = new EventHandlerCollection<ChangeEventArgs>();
                }
                this.handlers.AddHandler(value);
            }
            remove
            {
                if (this.handlers != null)
                {
                    this.handlers.RemoveHandler(value);
                }
            }
        }

        public int ChangeListenerCount => (this.handlers == null) ? 0 : this.handlers.ListenerCount;

        /// <summary>
        /// Fire a change event 
        /// </summary>
        /// <param name="sender">Object that owns the changed item</param>
        /// <param name="item">The item that was changed</param>
        /// <param name="name">The property that was changed </param>
        /// <param name="type">The type of change being made</param>
        /// <returns>Returns true if the parent container or this container are in batching mode</returns>
        public virtual bool FireChangeEvent(object sender, object item, string name, ChangeType type)
        {
            return this.FireChangeEvent(sender, new ChangeEventArgs(item, name, type));
        }

        /// <summary>
        /// Fire a change event 
        /// </summary>
        /// <param name="sender">Object that owns the changed item</param>
        /// <param name="item">The item that was changed</param>
        /// <param name="type">The type of change being made</param>
        /// <returns>Returns true if the parent container or this container are in batching mode</returns>
        public virtual bool FireChangeEvent(object sender, ChangeEventArgs args)
        {
            if (this.GetBatched().Read() == 0)
            {
                if (this.parent != null)
                {
                    if (this.parent.FireChangeEvent(sender, args))
                    {
                        return true;// parent is batching.
                    }
                }

                this.SendEvent(sender, args);
                return false;
            }
            else
            {
                this.changePending = true;
                if (this.saveChangeHistory)
                {
                    if (this.tail != null)
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

        private void SendEvent(object sender, ChangeEventArgs args)
        {
            if (this.handlers != null && this.handlers.HasListeners)
            {
                this.handlers.RaiseEvent(sender, args);
            }

            PersistentObject.RaisePropertyChangeEvents(args);
        }

        public bool IsUpdating
        {
            get { return this.GetBatched().Read() > 0; }
        }

        public virtual void BeginUpdate(bool saveChangeHistory)
        {
            // batched updates
            if (this.IsUpdating && !this.saveChangeHistory)
            {
                saveChangeHistory = this.saveChangeHistory;
            }

            this.GetBatched().Increment();
            this.saveChangeHistory = saveChangeHistory;
        }

        public virtual void EndUpdate()
        {
            if (this.GetBatched().Decrement() == 0 && this.handlers != null && this.handlers.HasListeners && this.changePending)
            {
                this.changePending = false;
                if (this.head != null)
                {
                    this.SendEvent(this, this.head);
                }
                else
                {
                    this.SendEvent(this, new ChangeEventArgs(this, null, ChangeType.Changed));
                }
                this.head = this.tail = null;
            }
        }

        public virtual void OnNameChanged(object o, string oldName, string newName)
        {
        }

        public virtual string Serialize()
        {
            DataContractSerializer xs = new DataContractSerializer(this.GetType(), MyMoney.GetKnownTypes());
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
                {
                    list.Add(pe);
                }
            }
            foreach (PersistentObject pe in list)
            {
                this.RemoveChild(pe, true);
            }
        }

        public abstract void Add(object child);

        public abstract void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false);

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
            return this.InternalGetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.InternalGetEnumerator();
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
        private EventHandlerCollection<ChangeEventArgs> handlers;
        private EventHandlerCollection<PropertyChangedEventHandler, PropertyChangedEventArgs> propertyChangeHandlers;

        [XmlIgnore]
        public bool BatchMode;


        public event PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                if (this.propertyChangeHandlers == null)
                {
                    this.propertyChangeHandlers = new EventHandlerCollection<PropertyChangedEventHandler, PropertyChangedEventArgs>();
                }
                this.propertyChangeHandlers.AddHandler(value);
                if (this.propertyChangeHandlers.ListenerCount > 500)
                {
                    Debug.WriteLine(string.Format("PropertyChanged handler leak detected on {0}", this.GetType().Name));
                }
            }
            remove
            {
                if (this.propertyChangeHandlers != null)
                {
                    this.propertyChangeHandlers.RemoveHandler(value);
                }
            }
        }


        public void RaisePropertyChanged(string propertyName)
        {
            if (this.propertyChangeHandlers != null && this.propertyChangeHandlers.HasListeners)
            {
                this.propertyChangeHandlers.RaiseEvent(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public int ChangeListenerCount => (this.handlers == null) ? 0 : this.handlers.ListenerCount;

        public event EventHandler<ChangeEventArgs> Changed
        {
            add
            {
                if (this.handlers == null)
                {
                    this.handlers = new EventHandlerCollection<ChangeEventArgs>();
                }
                this.handlers.AddHandler(value);
                if (this.handlers.ListenerCount > 500)
                {
                    Debug.WriteLine(string.Format("Changed handler leak detected on {0}", this.GetType().Name));
                }
            }
            remove
            {
                if (this.handlers != null)
                {
                    this.handlers.RemoveHandler(value);
                }
            }
        }

        private ChangeType change = ChangeType.Inserted;

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
            {
                this.change = ChangeType.None;
            }
        }

        public virtual void OnInserted()
        {
            if (this.change != ChangeType.Inserted)
            {
                this.change = ChangeType.Inserted;
                this.FireChangeEvent(this, "IsInserted", ChangeType.Inserted);
            }
        }

        public virtual void OnDelete()
        {
            if (this.change != ChangeType.Deleted)
            {
                this.change = ChangeType.Deleted;
                this.FireChangeEvent(this, "IsDeleted", ChangeType.Deleted);
            }
        }

        public virtual void OnChanged(string name)
        {
            if (this.BatchMode == false)
            {
                // Insert or Delete take precedence over Changed.
                if (this.change == ChangeType.None)
                {
                    this.change = ChangeType.Changed;
                }
                this.FireChangeEvent(this, name, ChangeType.Changed);
            }
        }

        /// <summary>
        /// This method is called when a computed property changes.
        /// This type of change is not persisted.
        /// </summary>
        /// <param name="name"></param>
        public virtual void OnTransientChanged(string name)
        {
            this.FireChangeEvent(this, name, ChangeType.TransientChanged);
        }

        public virtual void OnNameChanged(string oldName, string newName)
        {
            if (this.Parent != null)
            {
                this.Parent.OnNameChanged(this, oldName, newName);
            }
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

        private BatchSync batched;
        private bool changePending;
        private ChangeEventArgs head;
        private ChangeEventArgs tail;
        private object changeSource;

        private BatchSync GetBatched()
        {
            if (this.batched == null)
            {
                this.batched = new BatchSync();
            }
            return this.batched;
        }

        public bool IsUpdating
        {
            get { return this.GetBatched().Read() > 0; }
        }

        public virtual void BeginUpdate(object source)
        {
            // batched updates
            this.changeSource = source;
            this.GetBatched().Increment();
        }

        public virtual void EndUpdate()
        {
            if (this.GetBatched().Decrement() == 0 && this.changePending)
            {
                this.changeSource = null;
                this.changePending = false;
                if (this.head != null)
                {
                    this.FireChangeEvent(this, this.head);
                }
                else
                {
                    this.FireChangeEvent(this, new ChangeEventArgs(this, null, ChangeType.Changed));
                }

                this.head = this.tail = null;
            }
        }


        internal void FlushUpdates()
        {
            this.changePending = false;
            this.changeSource = null;
            this.head = this.tail = null;
        }

        protected void FireChangeEvent(object item, string name, ChangeType type)
        {
            this.FireChangeEvent(this, new ChangeEventArgs(item, name, type));
        }

        public virtual bool FireChangeEvent(object sender, ChangeEventArgs args)
        {
            if (this.GetBatched().Read() > 0)
            {
                args.ChangeSource = this.changeSource;
                this.changePending = true;
                if (this.head == null)
                {
                    this.head = this.tail = args;
                }
                else
                {
                    this.tail.Next = args;
                    this.tail = args;
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
                this.SendEvent(sender, args);
            }
            return false;
        }

        private void SendEvent(object sender, ChangeEventArgs args)
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
                this.Serialize(w);
                w.Close();
                return sw.ToString();
            }
        }

        public virtual void Serialize(XmlWriter w)
        {
            DataContractSerializer xs = new DataContractSerializer(this.GetType(), MyMoney.GetKnownTypes());
            xs.WriteObject(w, this);
        }


        protected static string Truncate(string s, int length)
        {
            if (s == null)
            {
                return s;
            }

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
        private AccountAliases accountAliases;
        private Currencies currencies;
        private Categories categories;
        private Transactions transactions;
        private Securities securities;
        private StockSplits stockSplits;
        private RentBuildings buildings;
        private TransactionExtras extras;
        private CultureInfo cultureInfo = new CultureInfo("en-US");
        internal PayeeIndex payeeAccountIndex;
        private bool watching;

        public static Type[] GetKnownTypes()
        {
            return new Type[]
            {
                typeof(Account),
                typeof(Payee),
                typeof(Transaction),
                typeof(Currency),
                typeof(Category),
                typeof(Transaction),
                typeof(Security),
                typeof(RentBuilding),
                typeof(Payee),
                typeof(Split),
                typeof(StockSplit),
                typeof(TransactionExtra)
            };
        }

        public MyMoney()
        {
            this.LazyInitialize();
            this.WatchChanges();
        }

        private void LazyInitialize()
        {
            // In the XmlSerialization case the MyMoney default constructor is never called!
            // So, we may have to lazily initialize these fields in that case.
            if (this.Accounts == null)
            {
                this.Accounts = new Accounts(this);
            }
            if (this.OnlineAccounts == null)
            {
                this.OnlineAccounts = new OnlineAccounts(this);
            }
            if (this.Payees == null)
            {
                this.Payees = new Payees(this);
            }
            if (this.Aliases == null)
            {
                this.Aliases = new Aliases(this);
            }
            if (this.AccountAliases == null)
            {
                this.AccountAliases = new AccountAliases(this);
            }
            if (this.Categories == null)
            {
                this.Categories = new Categories(this);
            }
            if (this.Currencies == null)
            {
                this.Currencies = new Currencies(this);
            }
            if (this.Transactions == null)
            {
                this.Transactions = new Transactions(this);
            }
            if (this.Securities == null)
            {
                this.Securities = new Securities(this);
            }
            if (this.StockSplits == null)
            {
                this.StockSplits = new StockSplits(this);
            }
            if (this.Buildings == null)
            {
                this.Buildings = new RentBuildings(this);
            }
            if (this.LoanPayments == null)
            {
                this.LoanPayments = new LoanPayments(this);
            }
            if (this.payeeAccountIndex == null)
            {
                this.payeeAccountIndex = new PayeeIndex(this);
            }
            if (this.extras == null)
            {
                this.extras = new TransactionExtras(this);
            }
        }

        private void WatchChanges()
        {
            if (!this.watching)
            {
                this.watching = true;
                EventHandler<ChangeEventArgs> handler = new EventHandler<ChangeEventArgs>(this.OnChanged);
                this.Accounts.Changed += handler;
                this.OnlineAccounts.Changed += handler;
                this.Payees.Changed += handler;
                this.Aliases.Changed += handler;
                this.AccountAliases.Changed += handler;
                this.Categories.Changed += handler;
                this.Currencies.Changed += handler;
                this.Transactions.Changed += handler;
                this.Securities.Changed += handler;
                this.StockSplits.Changed += handler;
                this.Buildings.Changed += handler;
                this.LoanPayments.Changed += handler;
                this.TransactionExtras.Changed += handler;
            }
        }

        internal void OnLoaded()
        {
            this.LazyInitialize();
            this.payeeAccountIndex.Reload();
            this.Transactions.JoinExtras(this.TransactionExtras);
            this.WatchChanges();
        }


        [DataMember]
        public Accounts Accounts
        {
            get { return this.accounts; }
            set { this.accounts = value; this.accounts.Parent = this; }
        }

        [DataMember]
        public OnlineAccounts OnlineAccounts
        {
            get { return this.onlineAccounts; }
            set { this.onlineAccounts = value; this.onlineAccounts.Parent = this; }
        }

        [DataMember]
        public Payees Payees
        {
            get { return this.payees; }
            set { this.payees = value; this.payees.Parent = this; }
        }

        [DataMember]
        public Aliases Aliases
        {
            get { return this.aliases; }
            set { this.aliases = value; this.aliases.Parent = this; }
        }

        [DataMember]
        public AccountAliases AccountAliases
        {
            get { return this.accountAliases; }
            set { this.accountAliases = value; this.accountAliases.Parent = this; }
        }

        [DataMember]
        public TransactionExtras TransactionExtras
        {
            get { return this.extras; }
            set { this.extras = value; this.extras.Parent = this; }
        }

        [DataMember]
        public Categories Categories
        {
            get { return this.categories; }
            set { this.categories = value; this.categories.Parent = this; }
        }

        [DataMember]
        public Currencies Currencies
        {
            get { return this.currencies; }
            set { this.currencies = value; this.currencies.Parent = this; }
        }

        [DataMember]
        public Transactions Transactions
        {
            get { return this.transactions; }
            set { this.transactions = value; this.transactions.Parent = this; }
        }

        [DataMember]
        public Securities Securities
        {
            get { return this.securities; }
            set { this.securities = value; this.securities.Parent = this; }
        }


        [DataMember]
        public StockSplits StockSplits
        {
            get { return this.stockSplits; }
            set { this.stockSplits = value; this.stockSplits.Parent = this; }
        }

        [DataMember]
        public RentBuildings Buildings
        {
            get { return this.buildings; }
            set { this.buildings = value; this.buildings.Parent = this; }
        }

        private EventHandlerCollection<ChangeEventArgs> balanceHandlers;

        public event EventHandler<ChangeEventArgs> Rebalanced
        {
            add
            {
                if (this.balanceHandlers == null)
                {
                    this.balanceHandlers = new EventHandlerCollection<ChangeEventArgs>();
                }
                this.balanceHandlers.AddHandler(value);
            }
            remove
            {
                if (this.balanceHandlers != null)
                {
                    this.balanceHandlers.RemoveHandler(value);
                }
            }
        }

        internal void OnChanged(object sender, ChangeEventArgs e)
        {
            this.FireChangeEvent(sender, e);
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
                if (string.IsNullOrEmpty(s.Symbol))
                {
                    continue;
                }

                if (map.ContainsKey(s.Symbol))
                {
                    Security original = map[s.Symbol];
                    // found a duplicate!
                    // So remove it and fix up all transactions to point to the original
                    foreach (Transaction t in this.Transactions.GetAllTransactions())
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
            this.BeginUpdate(this);
            this.RemoveUnusedPayees();
            this.RemoveUnusedOnlineAccounts();

            database.Save(this);

            this.EndUpdate();
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
            foreach (OnlineAccount oa in this.OnlineAccounts.GetOnlineAccounts())
            {
                if (!used.Contains(oa))
                {
                    toRemove.Add(oa);
                }
            }
            foreach (OnlineAccount oa in toRemove)
            {
                this.OnlineAccounts.Remove(oa);
            }
        }

        /// <summary>
        /// Get list of all Payees that are actually referenced from a Transaction or a Split.
        /// </summary>
        /// <returns></returns>
        internal HashSet<Payee> GetUsedPayees()
        {
            HashSet<Payee> used = new HashSet<Payee>();
            foreach (Transaction t in this.Transactions.GetAllTransactions())
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
            HashSet<Payee> used = this.GetUsedPayees();

            // make sure payee is not being used by an Alias.
            Dictionary<string, Alias> payeesReferencedByAliases = new Dictionary<string, Alias>();
            foreach (Alias a in this.Aliases.GetAliases())
            {
                payeesReferencedByAliases[a.Payee.Name] = a;
            }

            List<Payee> toRemove = new List<Payee>();
            foreach (Payee p in this.Payees.GetPayees())
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
                this.Payees.RemovePayee(p);
            }
        }

        public List<Security> GetUnusedSecurities()
        {
            HashSet<Security> used = new HashSet<Security>();
            foreach (Transaction t in this.Transactions.GetAllTransactions())
            {
                if (t.Investment != null && t.Investment.Security != null)
                {
                    used.Add(t.Investment.Security);
                }
            }
            List<Security> unused = new List<Security>();
            foreach (Security s in this.Securities.GetSecurities())
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
            foreach (Transaction t in this.Transactions.GetAllTransactions())
            {
                if (t.Investment != null && t.Investment.Security != null && pred(t.Account))
                {
                    used.Add(t.Investment.Security);
                }
            }
            List<Security> result = new List<Security>();
            foreach (Security s in this.Securities.GetSecurities())
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
            foreach (Security s in this.GetUnusedSecurities())
            {
                this.Securities.RemoveSecurity(s);
            }
        }

        public static void EnsureInvestmentAccount(Account a, string line, int lineNumber)
        {
            if (a.Type != AccountType.Brokerage && a.Type != AccountType.Retirement)
            {
                throw new MoneyException(string.Format("Received information for investment account on line {0}: {1}, but account {2} is of type {3}",
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
            this.Rebalance(calculator, a);
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
                    this.Rebalance(calculator, a);
                }
            }
        }

        private readonly HashSet<Account> balancePending = new HashSet<Account>();

        public override void EndUpdate()
        {
            base.EndUpdate();

            if (!this.IsUpdating)
            {
                this.ApplyPendingBalance();
            }
        }

        private void ApplyPendingBalance()
        {
            bool changed = false;
            if (this.balancePending != null && this.balancePending.Count > 0)
            {
                CostBasisCalculator calculator = new CostBasisCalculator(this, DateTime.Now);
                this.Transactions.BeginUpdate(false);
                try
                {
                    foreach (Account account in new List<Account>(this.balancePending))
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
                this.balancePending.Clear();
            }

        }

        public bool Rebalance(Transaction t)
        {
            bool changed = false;
            if (this.IsUpdating)
            {
                this.balancePending.Add(t.Account);
                if (t.Transfer != null)
                {
                    Transaction u = t.Transfer.Transaction;
                    this.balancePending.Add(u.Account);
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
            this.RemoveTransfer(t);

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

            this.Rebalance(t);
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
                        this.ClearTransferToAccount(s.Transfer);
                        s.ClearTransfer();
                        s.Category = s.Amount < 0 ? this.Categories.TransferToDeletedAccount :
                                this.Categories.TransferFromDeletedAccount;
                        if (string.IsNullOrEmpty(s.Memo))
                        {
                            s.Memo = a.Name;
                        }
                    }
                }
            }

            if (t.Transfer != null && t.Transfer.Transaction.Account == a)
            {
                this.ClearTransferToAccount(t.Transfer);
                t.Transfer = null;
                if (!t.IsSplit)
                {
                    t.Category = t.Amount < 0 ? this.Categories.TransferToDeletedAccount :
                                this.Categories.TransferFromDeletedAccount;
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
        private HashSet<Transfer> removing = new HashSet<Transfer>();

        public bool RemoveTransfer(Transfer t)
        {
            if (t != null)
            {
                if (this.removing == null)
                {
                    // bugbug: is this a C# bug???
                    this.removing = new HashSet<Transfer>();
                }
                if (!this.removing.Contains(t))
                {
                    this.removing.Add(t);

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
                                    s.Category = s.Amount < 0 ? this.Categories.TransferToDeletedAccount :
                                        this.Categories.TransferFromDeletedAccount;
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
                            this.removing.Add(target.Transfer);
                            if (sourceIsBeingDeleted)
                            {
                                target.Transfer = null;
                                target.Payee = this.Payees.Transfer;
                                target.Category = target.Amount < 0 ? this.Categories.TransferToDeletedAccount :
                                        this.Categories.TransferFromDeletedAccount;
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
                        this.removing.Remove(t);
                        this.removing.Remove(t.Transaction.Transfer);
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
                if (this.RemoveTransfer(t.Transfer))
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
                if (this.RemoveTransfer(s.Transfer))
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

            this.RemoveTransfer(t.Transfer);

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
            this.RemoveTransfer(t.Transfer);
            t.Transfer = null;
            this.RemoveTransfer(s.Transfer);
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
            this.Rebalance(to);
        }

        public event EventHandler<TransferChangedEventArgs> BeforeTransferChanged;

        private bool ignoreOnTransferChanged;

        internal void OnTransferChanged(Transaction transaction, Transfer value)
        {
            if (this.ignoreOnTransferChanged)
            {
                return;
            }
            if (BeforeTransferChanged != null)
            {
                BeforeTransferChanged(this, new TransferChangedEventArgs(transaction, value));
            }
            if (transaction.Transfer != null && transaction.Transfer != value)
            {
                this.ignoreOnTransferChanged = true;
                try
                {
                    this.RemoveTransfer(transaction);
                }
                finally
                {
                    this.ignoreOnTransferChanged = false;
                }
            }
        }

        public event EventHandler<SplitTransferChangedEventArgs> BeforeSplitTransferChanged;

        private bool ignoreOnSplitTransferChanged;

        internal void OnSplitTransferChanged(Split split, Transfer value)
        {
            if (this.ignoreOnSplitTransferChanged)
            {
                return;
            }
            if (BeforeSplitTransferChanged != null)
            {
                BeforeSplitTransferChanged(this, new SplitTransferChangedEventArgs(split, value));
            }
            if (split.Transfer != null && split.Transfer != value)
            {
                this.ignoreOnSplitTransferChanged = true;
                try
                {
                    this.RemoveTransfer(split);
                }
                finally
                {
                    this.ignoreOnSplitTransferChanged = false;
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

        public static void Categorize(Split s, Category cat)
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

        public IEnumerable<Alias> FindSubsumedAliases(Alias alias)
        {
            List<Alias> existingAliases = new List<Alias>();
            foreach (var a in this.Aliases.GetAliases())
            {
                if (a != alias && alias.Matches(a.Pattern))
                {
                    existingAliases.Add(a);
                }
            }
            return existingAliases;
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
                foreach (PersistentObject po in this.FindAliasMatches(alias, transactions))
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
            foreach (Transaction t in this.Transactions.GetAllTransactionsByDate())
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
            this.MarkAllUpToDate();

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
            foreach (AccountAlias a in this.AccountAliases) { a.OnUpdated(); }
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
            this.AccountAliases.MarkAllNew();
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

            foreach (var group in calc.GetHoldingsBySecurityType(null))
            {
                group.TaxStatus = TaxStatus.Any;
                foreach (SecurityPurchase sp in group.Purchases)
                {
                    unique.Add(sp.Security);
                }
            }

            List<Security> sorted = new List<Security>(unique);
            sorted.Sort(Security.Compare);
            return sorted;
        }

        /// <summary>
        /// Get the cash balance of the filtered accounts up to the given date.
        /// </summary>
        /// <param name="filter">The account filter predicate to decide which accounts to include.</param>
        /// <returns>Cash balance</returns>
        public decimal GetCashBalanceNormalized(DateTime date, Predicate<Account> filter)
        {
            decimal cash = 0;
            foreach (Account a in this.Accounts.GetAccounts(false))
            {
                if (filter(a))
                {
                    if (a.Type == AccountType.Loan)
                    {
                        throw new Exception("Do not use this method for Loan accounts");
                    }
                    decimal balance = 0;
                    bool first = true;
                    foreach (Transaction t in this.Transactions.GetTransactionsFrom(a))
                    {
                        if (t.Date > date)
                        {
                            break;
                        }
                        if (first)
                        {
                            balance = a.OpeningBalance;
                            first = false;
                        }
                        if (!t.IsDeleted && t.Status != TransactionStatus.Void)
                        {
                            balance += t.Amount;
                        }
                    }
                    cash += a.GetNormalizedAmount(balance);
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

        public int TotalChangeListenerCount
        {
            get
            {
                var total = this.ChangeListenerCount;
                total += this.AccountAliases.ChangeListenerCount;
                total += this.Accounts.ChangeListenerCount;
                total += this.Categories.ChangeListenerCount;
                total += this.Transactions.ChangeListenerCount;
                total += this.Securities.ChangeListenerCount;
                total += this.Payees.ChangeListenerCount;
                total += this.Currencies.ChangeListenerCount;
                total += this.Aliases.ChangeListenerCount;
                total += this.StockSplits.ChangeListenerCount;
                return total;
            }
        }
    }

    public class ErrorEventArgs : EventArgs
    {
        private readonly Exception error;

        public Exception Exception { get { return this.error; } }

        public ErrorEventArgs(Exception error)
        {
            this.error = error;
        }
    }

    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Accounts : PersistentContainer, ICollection<Account>
    {
        private int NextAccount = 0;
        private Hashtable<int, Account> accounts = new Hashtable<int, Account>(); // id->Account
        private Hashtable<string, Account> accountIndex = new Hashtable<string, Account>(); // name->Account

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
            foreach (Account a in this.accounts.Values)
            {
                return a;
            }
            return null;
        }

        public override void OnNameChanged(object o, string oldName, string newName)
        {
            if (oldName != null && this.accountIndex.ContainsKey(oldName))
            {
                this.accountIndex.Remove(oldName);
            }

            this.accountIndex[newName] = (Account)o;
        }

        public void Clear()
        {
            if (this.NextAccount != 0 || this.accounts.Count != 0 || this.accountIndex.Count != 0)
            {
                this.NextAccount = 0;
                this.accounts = new Hashtable<int, Account>();
                this.accountIndex = new Hashtable<string, Account>();
                this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }

        public Account AddAccount(int id)
        {
            Account a = new Account(this);
            a.Id = id;
            if (this.NextAccount <= id)
            {
                this.NextAccount = id + 1;
            }

            this.accounts[id] = a;
            return a;
        }

        public Account AddAccount(string name)
        {
            Debug.Assert(name != null);
            Account a = this.FindAccount(name);
            if (a == null)
            {
                a = this.AddAccount(this.NextAccount);
                a.Name = name;
                this.FireChangeEvent(this, a, null, ChangeType.Inserted);
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
            this.FireChangeEvent(this, new ChangeEventArgs(a, null, ChangeType.Inserted));
        }

        public Account FindAccount(string name)
        {
            if (name == null || name.Length == 0)
            {
                return null;
            }

            return this.accountIndex[name];
        }

        public Account FindAccountAt(int id)
        {
            return this.accounts[id];
        }

        public Account FindAccountByAccountId(string accountId)
        {
            foreach (Account a in this.accounts.Values)
            {
                if (a.AccountId == accountId)
                {
                    return a;
                }
            }
            return null;
        }

        private const string CategoryAccountPrefix = "Category: ";

        public Account FindCategoryFund(Category c)
        {
            Debug.Assert(c != null);
            Account a = this.FindAccount(CategoryAccountPrefix + c.Name);
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
            Account a = this.FindAccount(name);
            if (a != null)
            {
                if (a.Type != AccountType.CategoryFund)
                {
                    throw new MoneyException("Cannot create category account because account with conflicting name already exists");
                }
                return a;
            }

            a = this.AddAccount(this.NextAccount);
            a.Type = AccountType.CategoryFund;
            a.Flags = AccountFlags.Budgeted;
            a.Name = name;
            this.accountIndex[name] = a;
            this.FireChangeEvent(this, a, null, ChangeType.Inserted);
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
            this.Add((Category)child);
        }

        public override void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false)
        {
            this.RemoveAccount((Account)pe, forceRemoveAfterSave);
        }

        public bool RemoveAccount(Account a, bool forceRemoveAfterSave = false)
        {
            if (a.IsInserted || forceRemoveAfterSave)
            {
                if (this.accounts.ContainsKey(a.Id))
                {
                    this.accounts.Remove(a.Id);
                }

                string name = a.Name;
                if (name != null && this.accountIndex.ContainsKey(name))
                {
                    this.accountIndex.Remove(name);
                }
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
            return this.GetAccounts(false);
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
                foreach (Account a in this.GetAccounts())
                {
                    l.Add(a);
                }
                return l;
            }
        }

        #region ICollection

        public void Add(Account item)
        {
            this.AddAccount(item);
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
            return this.RemoveAccount(item);
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
            return this.GetEnumerator();
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

    [Flags]
    public enum TaxStatus
    {
        Taxable = 0,
        TaxDeferred = 1,
        TaxFree = 2,
        Any = 3
    }

    [Flags]
    public enum AccountFlags
    {
        None = 0,
        Budgeted = 1,
        Closed = 2,
        TaxDeferred = 4,
        TaxFree = 8
    }

    //================================================================================
    [TableMapping(TableName = "Accounts")]
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Account : PersistentObject
    {
        private int id = -1;
        private string accountId;
        private string ofxAccountId;
        private string name;
        private string description;
        private AccountType type;
        private decimal openingBalance;
        private decimal balance;
        private string currency;
        private decimal accountCurrencyRatio;
        private int onlineAccountId;
        private OnlineAccount onlineAccount;
        private string webSite;
        private int reconcileWarning;
        private DateTime lastSync;
        private DateTime lastBalance;
        private int unaccepted;
        private SqlGuid syncGuid;
        private AccountFlags flags;
        private Category category; // for category funds.
        private Category categoryForPrincipal;  // For Loan accounts
        private Category categoryForInterest;   // For Loan accounts

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
            set { if (this.id != value) { this.id = value; this.OnChanged("Id"); } }
        }

        [DataMember]
        [XmlAttribute]
        [ColumnMapping(ColumnName = "AccountId", MaxLength = 20, AllowNulls = true)]
        public string AccountId
        {
            get { return this.accountId; }
            set { if (this.accountId != value) { this.accountId = Truncate(value, 20); this.OnChanged("AccountId"); } }
        }

        [DataMember]
        [XmlAttribute]
        [ColumnMapping(ColumnName = "OfxAccountId", MaxLength = 50, AllowNulls = true)]
        public string OfxAccountId
        {
            get { return this.ofxAccountId == null ? this.accountId : this.ofxAccountId; }
            set { if (this.ofxAccountId != value) { this.ofxAccountId = Truncate(value, 50); this.OnChanged("OfxAccountId"); } }
        }

        [XmlIgnore]
        public bool IsClosed
        {
            get { return (this.flags & AccountFlags.Closed) != 0; }
            set
            {
                if (this.IsClosed != value)
                {
                    if (value)
                    {
                        this.flags |= AccountFlags.Closed;
                    }
                    else
                    {
                        this.flags &= ~AccountFlags.Closed;
                    }

                    this.OnChanged("IsClosed");
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
                    if (value)
                    {
                        this.flags |= AccountFlags.Budgeted;
                    }
                    else
                    {
                        this.flags &= ~AccountFlags.Budgeted;
                    }

                    this.OnChanged("IsBudgeted");
                }
            }
        }

        [XmlIgnore]
        public bool IsTaxDeferred
        {
            get { return (this.flags & AccountFlags.TaxDeferred) != 0; }
        }

        [XmlIgnore]
        public bool IsTaxFree
        {
            get { return (this.flags & AccountFlags.TaxFree) != 0; }
        }

        [XmlIgnore]
        public TaxStatus TaxStatus
        {
            get
            {
                return this.IsTaxDeferred ? TaxStatus.TaxDeferred :
                    (this.IsTaxFree ? TaxStatus.TaxFree : TaxStatus.Taxable);
            }
            set
            {
                switch (value)
                {
                    case TaxStatus.Taxable:
                        // remove any TaxDeferred or TaxFree flag.
                        this.flags &= ~(AccountFlags.TaxDeferred | AccountFlags.TaxFree);
                        break;
                    case TaxStatus.TaxDeferred:
                        // remove mututally exclusive TaxFree flag and add TaxDeferred
                        this.flags = (this.flags & ~AccountFlags.TaxFree) | AccountFlags.TaxDeferred;
                        break;
                    case TaxStatus.TaxFree:
                        // remove mututally exclusive TaxDeferred flag and add TaxFree
                        this.flags = (this.flags & ~AccountFlags.TaxDeferred) | AccountFlags.TaxFree;
                        break;
                    default:
                        break;
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
                    this.OnChanged("Name");
                    this.OnNameChanged(old, value);
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
                    this.OnChanged("Description");
                }
            }
        }

        [XmlIgnore]
        [ColumnMapping(ColumnName = "Type")]
        public AccountType Type
        {
            get { return this.type; }
            set { if (this.type != value) { this.type = value; this.OnChanged("Type"); } }
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
            set { if (this.openingBalance != value) { this.openingBalance = value; this.OnChanged("OpeningBalance"); } }
        }

        [XmlIgnore]
        public decimal Balance
        {
            get
            {
                return this.balance;
            }
            set
            {
                if (this.balance != value)
                {
                    this.balance = value;
                    this.OnTransientChanged("Balance");
                }
            }
        }

        public decimal GetNormalizedAmount(decimal amount)
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
                    return amount * c.Ratio;
                }
            }
            return amount;
        }


        /// <summary>
        /// Return the Balance in USD currency
        /// </summary>
        [XmlIgnore]
        public decimal BalanceNormalized
        {
            get
            {
                return this.GetNormalizedAmount(this.Balance);
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
                    this.currency = value; this.OnChanged("Currency");
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
                    this.onlineAccountId = value == null ? -1 : value.Id;
                    this.onlineAccount = value; this.OnChanged("OnlineAccount");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "WebSite", MaxLength = 512, AllowNulls = true)]
        public string WebSite
        {
            get { return this.webSite; }
            set { if (this.webSite != value) { this.webSite = value; this.OnChanged("WebSite"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "ReconcileWarning", AllowNulls = true)]
        public int ReconcileWarning
        {
            get { return this.reconcileWarning; }
            set { if (this.reconcileWarning != value) { this.reconcileWarning = value; this.OnChanged("ReconcileWarning"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "LastSync", AllowNulls = true)]
        public DateTime LastSync
        {
            get { return this.lastSync; }
            set { if (this.lastSync != value) { this.lastSync = value; this.OnChanged("LastSync"); } }
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
                    this.syncGuid = value; this.OnChanged("SyncGuid");
                }
            }
        }

        [DataMember(Name = "SyncGuid")]
        public string SerializedSyncGuid
        {
            get { return this.syncGuid.IsNull ? "" : this.syncGuid.ToString(); }
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
                this.OnChanged("SyncGuid");
            }
        }

        [ColumnMapping(ColumnName = "Flags", SqlType = typeof(SqlInt32), AllowNulls = true)]
        [XmlIgnore]// for some stupid reason the XmlSerializer can't serialize this field.
        public AccountFlags Flags
        {
            get { return this.flags; }
            set { if (this.flags != value) { this.flags = value; this.OnChanged("Flags"); } }
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
            set { if (this.lastBalance != value) { this.lastBalance = value; this.OnChanged("LastBalance"); } }
        }

        [DataMember]
        [XmlAttribute]
        public int Unaccepted
        {
            get { return this.unaccepted; }
            set
            {
                if (this.unaccepted != value)
                {
                    bool notify = (value == 0 && this.unaccepted != 0) || (value != 0 && this.unaccepted == 0);
                    this.unaccepted = value;
                    if (notify)
                    {
                        this.OnChanged("Unaccepted");
                    }
                }
            }
        }


        // This is for serialization
        [DataMember]
        public int SerializedFlags
        {
            get { return (int)this.flags; }
            set { if ((int)this.flags != value) { this.flags = (AccountFlags)value; this.OnChanged("Flags"); } }
        }

        public override string ToString()
        {
            return this.Name;
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
                this.OnChanged("CategoryForPrincipal");
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
                this.OnChanged("CategoryForInterest");
            }
        }

        #region Serialization Hack

        private string categoryForPrincipalName;

        /// <summary>
        /// for serialization only
        /// </summary>
        [DataMember]
        public string CategoryForPrincipalName
        {
            get { return this.categoryForPrincipal == null ? null : this.categoryForPrincipal.Name; }
            set { this.categoryForPrincipalName = value; }
        }

        private string categoryForInterestName;

        /// <summary>
        /// for serialization only
        /// </summary>
        [DataMember]
        public string CategoryForInterestName
        {
            get { return this.categoryForInterest == null ? null : this.categoryForInterest.Name; }
            set { this.categoryForInterestName = value; }
        }

        internal void PostDeserializeFixup(MyMoney myMoney)
        {
            this.OnlineAccount = myMoney.OnlineAccounts.FindOnlineAccountAt(this.OnlineAccountId);

            if (!string.IsNullOrEmpty(this.categoryForPrincipalName))
            {
                this.CategoryForPrincipal = myMoney.Categories.GetOrCreateCategory(this.categoryForPrincipalName, CategoryType.Expense);
                this.categoryForPrincipalName = null;
            }
            if (!string.IsNullOrEmpty(this.categoryForInterestName))
            {
                this.categoryForInterest = myMoney.Categories.GetOrCreateCategory(this.categoryForInterestName, CategoryType.Expense);
                this.categoryForInterestName = null;
            }
        }

        #endregion

    }

    internal class AccountComparer : IComparer<Account>
    {
        public int Compare(Account x, Account y)
        {
            Account a = x;
            Account b = y;
            if (a == null && b != null)
            {
                return -1;
            }

            if (a != null && b == null)
            {
                return 1;
            }

            if (a == null && b == null)
            {
                return 0;
            }

            string n = a.Name;
            string m = b.Name;
            if (n == null && m != null)
            {
                return -1;
            }

            if (n != null && m == null)
            {
                return 1;
            }

            if (n == null && m == null)
            {
                return 0;
            }

            return n.CompareTo(m);
        }
    }

    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class OnlineAccounts : PersistentContainer, ICollection<OnlineAccount>
    {
        private int NextOnlineAccount;
        private Hashtable<int, OnlineAccount> onlineAccounts = new Hashtable<int, OnlineAccount>();
        private Hashtable<string, OnlineAccount> instIndex = new Hashtable<string, OnlineAccount>();

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
                IList<OnlineAccount> list = this.GetOnlineAccounts();
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
            if (this.NextOnlineAccount != 0 || this.onlineAccounts.Count != 0 || this.instIndex.Count != 0)
            {
                this.NextOnlineAccount = 0;
                this.onlineAccounts = new Hashtable<int, OnlineAccount>();
                this.instIndex = new Hashtable<string, OnlineAccount>();
                this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }

        public override void OnNameChanged(object o, string oldName, string newName)
        {
            if (oldName != null && this.instIndex.ContainsKey(oldName))
            {
                this.instIndex.Remove(oldName);
            }

            this.instIndex[newName] = (OnlineAccount)o;
        }

        // onlineAccounts
        public OnlineAccount AddOnlineAccount(int id)
        {
            OnlineAccount result = new OnlineAccount(this);
            result.Id = id;
            if (this.NextOnlineAccount <= id)
            {
                this.NextOnlineAccount = id + 1;
            }

            this.onlineAccounts.Add(id, result);
            this.FireChangeEvent(this, result, null, ChangeType.Inserted);
            return result;
        }

        public void AddOnlineAccount(OnlineAccount oa)
        {
            if (this.GetOnlineAccounts().Contains(oa))
            {
                return;
            }
            if (oa.Id == -1)
            {
                oa.Id = this.NextOnlineAccount++;
                oa.OnInserted();
            }
            else if (this.NextOnlineAccount <= oa.Id)
            {
                this.NextOnlineAccount = oa.Id + 1;
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
            OnlineAccount result = this.AddOnlineAccount(this.NextOnlineAccount++);
            result.Name = name;
            this.instIndex[name] = result;
            this.FireChangeEvent(this, result, null, ChangeType.Inserted);
            return result;
        }

        public OnlineAccount FindOnlineAccount(string name)
        {
            if (name == null)
            {
                return null;
            }
            // find or add account of givien name
            OnlineAccount result = this.instIndex[name];
            return result;
        }

        public OnlineAccount FindOnlineAccountAt(int id)
        {
            return this.onlineAccounts[id];
        }

        public bool RemoveOnlineAccount(OnlineAccount i, bool forceRemoveAfterSave = false)
        {
            if (i.IsInserted || forceRemoveAfterSave)
            {
                if (this.onlineAccounts.ContainsKey(i.Id))
                {
                    this.onlineAccounts.Remove(i.Id);
                }

                if (this.instIndex.ContainsKey(i.Name))
                {
                    this.instIndex.Remove(i.Name);
                }
            }
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
            return this.onlineAccounts.ContainsKey(item.Id);
        }

        public void CopyTo(OnlineAccount[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { return this.onlineAccounts.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public override void Add(object child)
        {
            this.Add((OnlineAccount)child);
        }

        public override void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false)
        {
            this.RemoveOnlineAccount((OnlineAccount)pe, forceRemoveAfterSave);
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
            return this.GetEnumerator();
        }

        #endregion
    }

    //================================================================================
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    [TableMapping(TableName = "OnlineAccounts")]
    public class OnlineAccount : PersistentObject
    {
        private int id = -1;
        private string name;
        private string institution;
        private string ofx;
        private string fid;
        private string userid;
        private string password;
        private string userCred1;
        private string userCred2;
        private string authToken;
        private string bankId;
        private string brokerId;
        private string branchId;
        private string ofxVersion;
        private string logoUrl;
        private string appId = "QWIN";
        private string appVersion = "1700";
        private string sessionCookie;
        private string clientUid;
        private string accessKey;
        private string userKey;
        private DateTime? userKeyExpireDate;

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
                name = name,
                institution = institution,
                ofx = ofx,
                fid = fid,
                userid = userid,
                password = password,
                bankId = bankId,
                brokerId = brokerId,
                branchId = branchId,
                ofxVersion = ofxVersion,
                logoUrl = logoUrl,
                appId = appId,
                appVersion = appVersion,
                sessionCookie = sessionCookie,
                clientUid = clientUid
            };
        }

        [XmlAttribute]
        [DataMember]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true)]
        public int Id
        {
            get { return this.id; }
            set { if (this.id != value) { this.id = value; this.OnChanged("Id"); } }
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
                    this.name = Truncate(value, 80); this.OnChanged("Name");
                    this.OnNameChanged(old, this.name);
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
                    this.institution = Truncate(value, 80); this.OnChanged("Institution");
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
                    this.ofx = Truncate(value, 255); this.OnChanged("Ofx");
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
                    this.OnChanged("OfxVersion");
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
                    this.OnChanged("FID");
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
                    this.OnChanged("UserId");
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
                    this.OnChanged("Password");
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
                    this.OnChanged("UserCred1");
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
                    this.OnChanged("UserCred2");
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
                    this.OnChanged("AuthToken");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "BankId", MaxLength = 50, AllowNulls = true)]
        public string BankId
        {
            get { return this.bankId; }
            set { if (this.bankId != value) { this.bankId = Truncate(value, 50); this.OnChanged("BankId"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "BranchId", MaxLength = 50, AllowNulls = true)]
        public string BranchId
        {
            get { return this.branchId; }
            set { if (this.branchId != value) { this.branchId = Truncate(value, 50); this.OnChanged("BranchId"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "BrokerId", MaxLength = 50, AllowNulls = true)]
        public string BrokerId
        {
            get { return this.brokerId; }
            set { if (this.brokerId != value) { this.brokerId = Truncate(value, 50); this.OnChanged("BrokerId"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "LogoUrl", MaxLength = 1000, AllowNulls = true)]
        public string LogoUrl
        {
            get { return this.logoUrl; }
            set { if (this.logoUrl != value) { this.logoUrl = Truncate(value, 1000); this.OnChanged("LogoUrl"); } }
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
                    this.OnChanged("AppId");
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
                    this.OnChanged("AppVersion");
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
                    this.OnChanged("ClientUid");
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
                    this.OnChanged("AccessKey");
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
                    this.OnChanged("UserKey");
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
                    this.OnChanged("UserKeyExpireDate");
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

    internal class OnlineAccountComparer : IComparer<OnlineAccount>
    {
        public int Compare(OnlineAccount x, OnlineAccount y)
        {
            OnlineAccount a = x;
            OnlineAccount b = y;
            if (a == null && b != null)
            {
                return -1;
            }

            if (a != null && b == null)
            {
                return 1;
            }

            string n = a.Name;
            string m = b.Name;
            if (n == null && m != null)
            {
                return -1;
            }

            if (n != null && m == null)
            {
                return 1;
            }

            return n.CompareTo(m);
        }
    }

    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Aliases : PersistentContainer, ICollection<Alias>
    {
        private int nextAlias;
        private Hashtable<int, Alias> aliases = new Hashtable<int, Alias>();

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
            this.AddAlias((Alias)child);
        }

        public override void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false)
        {
            this.RemoveAlias((Alias)pe, forceRemoveAfterSave);
        }

        // Aliases
        public Alias AddAlias(int id)
        {
            Alias result = new Alias(this);
            lock (this.aliases)
            {
                result.Id = id;
                if (this.nextAlias <= id)
                {
                    this.nextAlias = id + 1;
                }

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
            if (pattern == null)
            {
                return null;
            }

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
            if (payee == null)
            {
                return null;
            }

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

        public bool RemoveAlias(Alias a, bool forceRemoveAfterSave = false)
        {
            lock (this.aliases)
            {
                if (a.IsInserted || forceRemoveAfterSave)
                {
                    // then we can remove it immediately.
                    if (this.aliases.ContainsKey(a.Id))
                    {
                        this.aliases.Remove(a.Id);
                        this.FireChangeEvent(this, a, "IsDeleted", ChangeType.Deleted);
                    }
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
                    this.RemoveAlias(a);
                }

            }
        }

        public IList<Alias> GetAliases(bool includedDeleted = false)
        {
            List<Alias> list = new List<Alias>(this.aliases.Count);
            lock (this.aliases)
            {
                foreach (Alias a in this.aliases.Values)
                {
                    if (!a.IsDeleted || includedDeleted)
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
                {
                    list.Add(a);
                }
            }
            foreach (Alias a in list)
            {
                this.aliases.Remove(a.Id);
            }
        }

        #region ICollection

        public void Add(Alias item)
        {
            this.AddAlias(item);
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
                item.OnDelete();
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
            return this.GetEnumerator();
        }
    }

    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class AccountAliases : PersistentContainer, ICollection<AccountAlias>
    {
        private int nextAlias;
        private Hashtable<int, AccountAlias> aliases = new Hashtable<int, AccountAlias>();

        public AccountAliases()
        {
            // for serialization
        }

        public AccountAliases(PersistentObject parent)
            : base(parent)
        {
        }

        public override void Add(object child)
        {
            this.AddAlias((AccountAlias)child);
        }

        public override void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false)
        {
            this.RemoveAlias((AccountAlias)pe, forceRemoveAfterSave);
        }

        // Aliases
        public AccountAlias AddAlias(int id)
        {
            AccountAlias result = new AccountAlias(this);
            lock (this.aliases)
            {
                result.Id = id;
                if (this.nextAlias <= id)
                {
                    this.nextAlias = id + 1;
                }

                this.aliases[id] = result;
            }
            this.FireChangeEvent(this, result, null, ChangeType.Inserted);

            return result;
        }

        public void AddAlias(AccountAlias a)
        {
            foreach (var alias in this.aliases.Values)
            {
                if (alias.Pattern == a.Pattern)
                {
                    // duplicate! so perhaps user is trying to move
                    // this alias to a different account!
                    alias.AccountId = a.AccountId;
                    this.FireChangeEvent(this, alias, null, ChangeType.Changed);
                    return;
                }
            }

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

        public AccountAlias FindAlias(string pattern)
        {
            if (pattern == null)
            {
                return null;
            }

            lock (this.aliases)
            {
                foreach (var a in this.aliases.Values)
                {
                    if (a.Pattern == pattern)
                    {
                        return a;
                    }
                }
            }
            return null;
        }

        public AccountAlias FindMatchingAlias(string accoundId)
        {
            if (string.IsNullOrEmpty(accoundId))
            {
                return null;
            }

            lock (this.aliases)
            {
                foreach (var a in this.aliases.Values)
                {
                    if (a.AccountId == accoundId)
                    {
                        return a;
                    }
                }
            }
            return null;
        }

        public bool RemoveAlias(AccountAlias a, bool forceRemoveAfterSave = false)
        {
            lock (this.aliases)
            {
                if (a.IsInserted || forceRemoveAfterSave)
                {
                    // then we can remove it immediately.
                    if (this.aliases.ContainsKey(a.Id))
                    {
                        this.aliases.Remove(a.Id);
                        this.FireChangeEvent(this, a, "IsDeleted", ChangeType.Deleted);
                    }
                }
            }
            // mark it for deletion on next save
            a.OnDelete();
            return true;
        }

        public void RemoveAliasesOf(string accountId)
        {
            foreach (var a in this.GetAliases())
            {
                if (a.AccountId == accountId)
                {
                    this.RemoveAlias(a);
                }

            }
        }

        public IList<AccountAlias> GetAliases()
        {
            List<AccountAlias> list = new List<AccountAlias>(this.aliases.Count);
            lock (this.aliases)
            {
                foreach (var a in this.aliases.Values)
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
            List<AccountAlias> list = new List<AccountAlias>();
            foreach (AccountAlias a in this.aliases.Values)
            {
                if (a.IsDeleted)
                {
                    list.Add(a);
                }
            }
            foreach (var a in list)
            {
                this.aliases.Remove(a.Id);
            }
        }

        #region ICollection

        public void Add(AccountAlias item)
        {
            this.AddAlias(item);
        }

        public void Clear()
        {
            if (this.nextAlias != 0 || this.aliases.Count != 0)
            {
                this.nextAlias = 0;
                this.aliases = new Hashtable<int, AccountAlias>();
                this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }

        public bool Contains(AccountAlias item)
        {
            return this.aliases.ContainsKey(item.Id);
        }

        public void CopyTo(AccountAlias[] array, int arrayIndex)
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

        public bool Remove(AccountAlias item)
        {
            if (this.aliases.ContainsKey(item.Id))
            {
                this.aliases.Remove(item.Id);
                return true;
            }
            return false;
        }

        public new IEnumerator<AccountAlias> GetEnumerator()
        {
            foreach (var a in this.aliases.Values)
            {
                yield return a;
            }
        }
        #endregion

        protected override IEnumerator<PersistentObject> InternalGetEnumerator()
        {
            return this.GetEnumerator();
        }
    }

    //================================================================================
    [TableMapping(TableName = "Currencies")]
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Currency : PersistentObject
    {
        private int id = -1;
        private string name;
        private string symbol;
        private string cultureCode;
        private decimal ratio;
        private decimal lastRatio;

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
                    this.id = value; this.OnChanged("Id");
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
                    this.OnChanged("Symbol");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "CultureCode", MaxLength = 80, AllowNulls = true)]
        public string CultureCode
        {
            get
            {
                if (string.IsNullOrEmpty(this.cultureCode) && !string.IsNullOrEmpty(this.symbol))
                {
                    CultureInfo ci = GetCultureForCurrency(this.symbol);
                    return ci.Name;
                }
                return this.cultureCode;
            }
            set
            {
                if (this.cultureCode != value)
                {
                    this.cultureCode = Truncate(value, 80);

                    this.OnChanged("CultureCode");
                }
            }
        }

        public static CultureInfo GetCultureForCurrency(string symbol)
        {
            foreach (var ci in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                var ri = new RegionInfo(ci.Name);
                if (ri.ISOCurrencySymbol == symbol)
                {
                    return ci;
                }
            }

            return CultureInfo.CurrentCulture;
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Name", MaxLength = 80)]
        public string Name
        {
            get
            {
                if (string.IsNullOrEmpty(this.name) && !string.IsNullOrEmpty(this.symbol))
                {
                    CultureInfo ci = GetCultureForCurrency(this.symbol);
                    var ri = new RegionInfo(ci.Name);
                    return ri.CurrencyEnglishName;
                }
                return this.name;
            }
            set
            {
                if (this.name != value)
                {
                    this.name = Truncate(value, 80);
                    this.OnChanged("Name");
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
                    this.OnChanged("Ratio");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "LastRatio", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal LastRatio
        {
            get { return this.lastRatio; }
            set { if (this.lastRatio != value) { this.lastRatio = value; this.OnChanged("LastRatio"); } }
        }
    }

    internal class CurrencyComparer : IComparer<Currency>
    {
        public int Compare(Currency x, Currency y)
        {
            Currency a = x;
            Currency b = y;
            if (a == null && b != null)
            {
                return -1;
            }

            if (a != null && b == null)
            {
                return 1;
            }

            if (a == null && b == null)
            {
                return 0;
            }

            string n = a.Symbol;
            string m = b.Symbol;
            if (n == null && m != null)
            {
                return -1;
            }

            if (n != null && m == null)
            {
                return 1;
            }

            if (n == null && m == null)
            {
                return 0;
            }

            return n.CompareTo(m);
        }
    }


    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Currencies : PersistentContainer, ICollection<Currency>
    {
        private int nextCurrency;
        private Hashtable<int, Currency> currencies = new Hashtable<int, Currency>();
        private readonly Hashtable<string, Currency> quickLookup = new Hashtable<string, Currency>();

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
            lock (this.quickLookup)
            {
                this.quickLookup.Clear();
            }
        }

        public override void Add(object child)
        {
            this.AddCurrency((Currency)child);
        }

        public override void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false)
        {
            this.RemoveCurrency((Currency)pe, forceRemoveAfterSave);
        }

        public Currency AddCurrency(int id)
        {
            Currency currency = new Currency(this);
            lock (this.currencies)
            {
                currency.Id = id;
                if (this.nextCurrency <= id)
                {
                    this.nextCurrency = id + 1;
                }

                this.currencies[id] = currency;

                this.ResetCache();

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


                this.ResetCache();
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

            Currency from = this.FindCurrency(sourceCurrency);
            Currency to = this.FindCurrency(targetCurrency);
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

            return amount * from.Ratio / to.Ratio;
        }


        public Currency FindCurrency(string currencySymbol)
        {
            if (string.IsNullOrWhiteSpace(currencySymbol))
            {
                return null;
            }

            if (this.currencies.Count == 0)
            {
                return null;
            }

            lock (this.currencies)
            {
                Currency currencyFound;
                lock (this.quickLookup)
                {
                    if (this.quickLookup.Count == 0)
                    {
                        // First build a cache to speed up any other request
                        foreach (Currency c in this.currencies.Values)
                        {
                            if (!c.IsDeleted && !string.IsNullOrEmpty(c.Symbol))
                            {
                                this.quickLookup[c.Symbol] = c;
                            }
                        }
                    }
                    if (this.quickLookup.TryGetValue(currencySymbol, out currencyFound))
                    {
                        return currencyFound;
                    }
                }

            }
            return null;
        }


        public bool RemoveCurrency(Currency item, bool forceRemoveAfterSave = false)
        {
            if (item.IsInserted || forceRemoveAfterSave)
            {
                lock (this.currencies)
                {
                    if (this.currencies.ContainsKey(item.Id))
                    {
                        this.currencies.Remove(item.Id);
                        this.FireChangeEvent(this, item, "IsDeleted", ChangeType.Deleted);
                    }
                }
            }
            item.OnDelete();
            this.ResetCache();
            return true;
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
            this.AddCurrency(item);
        }

        public void Clear()
        {
            if (this.nextCurrency != 0 || this.currencies.Count != 0)
            {
                this.nextCurrency = 0;
                this.currencies = new Hashtable<int, Currency>();
                this.ResetCache();
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

        [XmlIgnore]
        public Currency DefaultCurrency { get; set; }

        public bool Remove(Currency item)
        {
            this.RemoveCurrency(item);
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
            return this.GetEnumerator();
        }

    }


    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Payees : PersistentContainer, ICollection<Payee>
    {
        private int nextPayee;
        private Hashtable<int, Payee> payees = new Hashtable<int, Payee>();
        private Hashtable<string, Payee> payeeIndex = new Hashtable<string, Payee>();

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
                if (oldName != null && this.payeeIndex.ContainsKey(oldName))
                {
                    this.payeeIndex.Remove(oldName);
                }

                this.payeeIndex[newName] = (Payee)o;
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
            lock (this.payees)
            {
                result.Id = id;
                if (this.nextPayee <= id)
                {
                    this.nextPayee = id + 1;
                }

                this.payees[id] = result;
            }
            return result;
        }

        public void AddPayee(Payee p)
        {
            lock (this.payees)
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
                this.OnNameChanged(p, null, p.Name);
            }
        }

        public Payee FindPayee(string name, bool add)
        {
            if (name == null)
            {
                return null;
            }
            // find or add account of givien name
            Payee result = this.payeeIndex[name];
            if (result == null && add)
            {
                result = this.AddPayee(this.nextPayee++);
                result.Name = name;
                this.payeeIndex[name] = result;
                this.FireChangeEvent(this, result, null, ChangeType.Inserted);
            }
            return result;
        }

        public Payee FindPayeeAt(int id)
        {
            return this.payees[id];
        }

        // todo: there should be no references left at this point...
        public bool RemovePayee(Payee p, bool forceRemoveAfterSave = false)
        {
            lock (this.payees)
            {
                if (p.IsInserted || forceRemoveAfterSave)
                {
                    // then we can remove it
                    if (this.payees.ContainsKey(p.Id))
                    {
                        this.payees.Remove(p.Id);
                        this.FireChangeEvent(this, p, "IsDeleted", ChangeType.Deleted);
                    }
                }
                if (this.payeeIndex.ContainsKey(p.Name))
                {
                    this.payeeIndex.Remove(p.Name);
                }

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
            lock (this.payees)
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
                return this.GetPayees();
            }
        }



        public List<Payee> AllPayeesSorted
        {
            get
            {
                List<Payee> l = this.GetPayeesAsList();
                l.Sort(new PayeeComparer2());
                return l;
            }
        }

        public List<Payee> GetPayeesAsList()
        {
            List<Payee> list = new List<Payee>();
            lock (this.payees)
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
            lock (this.payees)
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
            this.Add((Payee)child);
        }

        public override void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false)
        {
            this.RemovePayee((Payee)pe, forceRemoveAfterSave);
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
            return this.GetEnumerator();
        }

        internal Payee ImportPayee(Payee payee)
        {
            if (payee == null)
            {
                return null;
            }
            Payee p = this.FindPayee(payee.Name, false);
            if (p == null)
            {
                p = this.FindPayee(payee.Name, true);
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
        private int id = -1;
        private int unaccepted;
        private int uncategorized;
        private string name;

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
            set { if (this.id != value) { this.id = value; this.OnChanged("Id"); } }
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
                    this.OnChanged("Name");
                    this.OnNameChanged(old, this.name);
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
                this.OnChanged("UnacceptedTransactions");
                this.OnChanged("Flags");
            }
        }

        public int UncategorizedTransactions
        {
            get { return this.uncategorized; }
            set
            {
                this.uncategorized = value;
                this.OnChanged("UncategorizedTransactions");
                this.OnChanged("Flags");
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
                DataContractSerializer xs = new DataContractSerializer(typeof(Payee), MyMoney.GetKnownTypes());
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
        private int id = -1;
        private string pattern;
        private AliasType type;
        private int payeeId;
        private Payee payee;
        private Regex regex;

        public Alias()
        { // for serialization only
        }

        public Alias(PersistentContainer container) : base(container) { }

        [DataMember]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true)]
        public int Id { get { return this.id; } set { this.id = value; } }

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
                    this.OnChanged("Pattern");
                }
            }
        }

        public override void OnChanged(string name)
        {
            if (this.AliasType == AliasType.Regex && this.pattern != null)
            {
                this.regex = new Regex(this.pattern);
            }
            else
            {
                this.regex = null;
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
                    this.type = value;
                    this.OnChanged("AliasType");
                }
            }
        }

        // for storage.
        [DataMember]
        private int PayeeId
        {
            get { return this.payeeId; }
            set { this.payeeId = value; }
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
                    this.payeeId = value == null ? -1 : value.Id;
                    this.payee = value; this.OnChanged("Payee");
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
                match = m != null && m.Success && m.Index == 0 && m.Length == payee.Length;
            }
            else
            {
                match = string.Compare(this.pattern, payee, true, CultureInfo.CurrentUICulture) == 0;
            }
            return match;
        }

        internal void PostDeserializeFixup(MyMoney myMoney)
        {
            this.Payee = myMoney.Payees.FindPayeeAt(this.PayeeId);
        }
    }

    //================================================================================
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    [TableMapping(TableName = "AccountAliases")]
    public class AccountAlias : PersistentObject
    {
        private int id = -1;
        private string pattern;
        private AliasType type;
        private string accountId;
        private Regex regex;

        public AccountAlias()
        { // for serialization only
        }

        public AccountAlias(PersistentContainer container) : base(container) { }

        [DataMember]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true)]
        public int Id { get { return this.id; } set { this.id = value; } }

        [DataMember]
        [ColumnMapping(ColumnName = "Pattern", MaxLength = 255)]
        public string Pattern
        {
            get { return this.pattern; }
            set
            {
                if (this.pattern != value)
                {
                    this.pattern = value;
                    this.OnChanged("Pattern");
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
                    this.type = value; this.OnChanged("AliasType");
                }
            }
        }

        // for storage.
        [DataMember]
        [ColumnMapping(ColumnName = "AccountId", MaxLength = 20)]
        public string AccountId
        {
            get { return this.accountId; }
            set { this.accountId = value; }
        }

        internal void PostDeserializeFixup(MyMoney myMoney)
        {
        }

        public override string ToString()
        {
            return this.pattern;
        }
    }

    //================================================================================
    // Additional information we want to associatge with transactions on very rare occasions
    // that doesn't need to be in the Transaction table.
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    [TableMapping(TableName = "TransactionExtras")]
    public class TransactionExtra : PersistentObject
    {
        private int id = -1;
        private long transaction = -1;
        private int taxYear = -1;
        private DateTime? taxDate;

        public TransactionExtra()
        { // for serialization only
        }

        public TransactionExtra(PersistentContainer container) : base(container) { }

        [DataMember]
        [ColumnMapping(ColumnName = "Id", IsPrimaryKey = true)]
        public int Id { get { return this.id; } set { this.id = value; } }

        [DataMember]
        [ColumnMapping(ColumnName = "Transaction", SqlType = typeof(SqlInt64))]
        public long Transaction
        {
            get { return this.transaction; }
            set
            {
                if (this.transaction != value)
                {
                    if (this.Parent is TransactionExtras extras)
                    {
                        // fix up the index.
                        extras.OnTransactionIdChanged(this.transaction, value, this);
                    }
                    this.transaction = value;
                    this.OnChanged("Pattern");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "TaxYear", SqlType = typeof(SqlInt32))]
        public int TaxYear
        {
            get { return this.taxYear; }
            set
            {
                if (this.taxYear != value)
                {
                    this.taxYear = value;
                    this.OnChanged("TaxYear");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "TaxDate", AllowNulls = true)]
        public DateTime? TaxDate
        {
            get { return this.taxDate; }
            set
            {
                if (!this.taxDate.HasValue || this.taxDate.Value != value)
                {
                    this.taxDate = value;
                    this.OnChanged("TaxDate");
                }
            }
        }

        // Update this if we add any more extra properties to this object.
        public bool IsEmpty => this.taxDate == null;

        internal void PostDeserializeFixup(MyMoney myMoney)
        {
        }
    }

    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class TransactionExtras : PersistentContainer, ICollection<TransactionExtra>
    {
        private int nextId;
        private Hashtable<int, TransactionExtra> items = new Hashtable<int, TransactionExtra>();
        private Hashtable<long, TransactionExtra> byTransactionId = new Hashtable<long, TransactionExtra>();

        public TransactionExtras()
        {
            // for serialization
        }

        public TransactionExtras(PersistentObject parent)
            : base(parent)
        {
        }

        public override void Add(object child)
        {
            this.AddExtra((TransactionExtra)child);
        }

        public override void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false)
        {
            this.RemoveExtra((TransactionExtra)pe, forceRemoveAfterSave);
        }

        public bool RemoveExtra(TransactionExtra e, bool forceRemoveAfterSave = false)
        {
            lock (this.items)
            {
                if (e.IsInserted || forceRemoveAfterSave)
                {
                    lock (this.byTransactionId)
                    {
                        if (this.byTransactionId.Contains(e.Transaction))
                        {
                            this.byTransactionId.Remove(e.Transaction);
                        }
                    }
                    // then we can remove it immediately.
                    if (this.items.ContainsKey(e.Id))
                    {
                        this.items.Remove(e.Id);
                        this.FireChangeEvent(this, e, "IsDeleted", ChangeType.Deleted);
                    }
                }
            }
            // mark it for deletion on next save
            e.OnDelete();
            return true;
        }

        public TransactionExtra AddExtra(int id)
        {
            TransactionExtra result = new TransactionExtra(this);
            lock (this.items)
            {
                result.Id = id;
                if (this.nextId <= id)
                {
                    this.nextId = id + 1;
                }

                this.items[id] = result;
            }
            this.FireChangeEvent(this, result, null, ChangeType.Inserted);
            return result;
        }

        public void AddExtra(TransactionExtra extra)
        {
            lock (this.items)
            {
                if (extra.Id == -1)
                {
                    extra.Id = this.nextId++;
                    extra.OnInserted();
                }
                else if (this.nextId <= extra.Id)
                {
                    this.nextId = extra.Id + 1;
                }
                this.OnTransactionIdChanged(-1, extra.Transaction, extra);

                extra.Parent = this;
                extra.OnInserted();
                this.items[extra.Id] = extra;
            }

            this.FireChangeEvent(this, extra, null, ChangeType.Inserted);
        }

        public TransactionExtra FindByTransaction(long transactionId)
        {
            lock (this.byTransactionId)
            {
                if (this.byTransactionId.TryGetValue(transactionId, out TransactionExtra result))
                {
                    return result;
                }
            }
            return null;
        }

        public IList<TransactionExtra> GetExtras()
        {
            List<TransactionExtra> list = new List<TransactionExtra>(this.items.Count);
            lock (this.items)
            {
                foreach (var a in this.items.Values)
                {
                    if (!a.IsDeleted)
                    {
                        list.Add(a);
                    }
                }
            }

            return list;
        }

        protected override IEnumerator<PersistentObject> InternalGetEnumerator()
        {
            foreach (var i in this.ToArray())
            {
                yield return i;
            }
        }

        internal void OnTransactionIdChanged(long transaction, long value, TransactionExtra extra)
        {
            lock (this.byTransactionId)
            {
                if (this.byTransactionId.ContainsKey(transaction))
                {
                    var e = this.byTransactionId[transaction];
                    if (e != extra)
                    {
                        throw new Exception("Internal Error: Transaction doesn't match for TransactionExtra!");
                    }
                    this.byTransactionId.Remove(transaction);
                }

                if (value != -1)
                {
                    if (this.byTransactionId.TryGetValue(value, out TransactionExtra e) && e != extra)
                    {
                        throw new Exception("Cannot add a duplicate TransactionExtra for transaction " + extra.Transaction);
                    }
                    this.byTransactionId[value] = extra;
                }
            }
        }

        #region ICollection
        public int Count => this.items.Count;

        public bool IsReadOnly => false;

        public void Add(TransactionExtra item)
        {
            this.AddExtra(item);
        }

        public void Clear()
        {
            if (this.nextId != 0 || this.items.Count != 0)
            {
                this.nextId = 0;
                this.items = new Hashtable<int, TransactionExtra>();
                this.byTransactionId = new Hashtable<long, TransactionExtra>();
                this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }

        public bool Contains(TransactionExtra item)
        {
            lock (this.items)
            {
                return this.items.ContainsKey(item.Id);
            }
        }

        public void CopyTo(TransactionExtra[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(TransactionExtra item)
        {
            throw new NotImplementedException("Please use RemoveExtra() instead");
        }

        IEnumerator<TransactionExtra> IEnumerable<TransactionExtra>.GetEnumerator()
        {
            foreach (var i in this.ToArray())
            {
                yield return i;
            }
        }

        internal TransactionExtra[] ToArray()
        {
            TransactionExtra[] a = null;
            lock (this.items)
            {
                a = this.items.Values.ToArray();
            }
            return a;
        }

        internal void MigrateTaxYears(MyMoney parent, int fiscalYearStart)
        {
            List<TransactionExtra> dangling = new List<TransactionExtra>();
            foreach (var id in this.byTransactionId.Keys)
            {
                var e = this.byTransactionId[id];
                Transaction t = parent.Transactions.FindTransactionById(id);
                if (t != null)
                {
                    if (e.TaxYear != -1 && !e.TaxDate.HasValue)
                    {
                        // time to migrate it to a full date.
                        var d = new DateTime(e.TaxYear, fiscalYearStart + 1, 1);
                        e.TaxDate = d;
                    }
                }
                else
                {
                    // dangling extra!
                    dangling.Add(e);
                }
            }

            foreach (var toRemove in dangling)
            {
                this.RemoveExtra(toRemove);
            }
        }

        internal void OnRemoveTransaction(Transaction transaction)
        {
            var e = this.FindByTransaction(transaction.Id);
            if (e != null)
            {
                this.RemoveExtra(e);
            }
        }

        #endregion
    }

    public class PayeeComparer : IComparer<Payee>
    {
        public int Compare(Payee x, Payee y)
        {
            Payee a = x;
            Payee b = y;
            if (a == null && b != null)
            {
                return -1;
            }

            if (a != null && b == null)
            {
                return 1;
            }

            string n = a.Name;
            string m = b.Name;
            if (n == null && m != null)
            {
                return -1;
            }

            if (n != null && m == null)
            {
                return 1;
            }

            return n.CompareTo(m);
        }
    }

    public class PayeeComparer2 : IComparer<Payee>
    {
        public int Compare(Payee x, Payee y)
        {
            if (x == null && y != null)
            {
                return -1;
            }

            if (x != null && y == null)
            {
                return 1;
            }

            string n = x.Name;
            string m = y.Name;
            if (n == null && m != null)
            {
                return -1;
            }

            if (n != null && m == null)
            {
                return 1;
            }

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
            this.TotalMaintenance = 0;
            this.TotalTaxes = 0;
            this.TotalRepairs = 0;
            this.TotalManagement = 0;
            this.TotalInterest = 0;
        }

        public decimal AllExpenses
        {
            get { return this.TotalTaxes + this.TotalRepairs + this.TotalMaintenance + this.TotalManagement + this.TotalInterest; }
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
            this.Total = 0;
        }

        public override string ToString()
        {
            return string.Format("{0} {1}", this.Name, this.Total);
        }
    }



    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class RentalBuildingSingleYear
    {
        public List<RentalBuildingSingleYearSingleDepartment> Departments { get; set; }

        private int yearStart;

        public int YearStart
        {
            get { return this.yearStart; }
        }

        private int yearEnd;

        public int YearEnd
        {
            get { return this.yearEnd; }
        }

        public string Period
        {
            get
            {
                string period = RentBuilding.GetYearRangeString(this.YearStart, this.YearEnd);
                if (string.IsNullOrEmpty(period))
                {
                    period = this.Year.ToString();
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
            this.TotalIncome = this.Departments[0].Total;

            // Expenses
            this.TotalExpensesGroup = new RentExpenseTotal();

            this.TotalExpensesGroup.TotalTaxes = this.Departments[1].Total;
            this.TotalExpensesGroup.TotalRepairs = this.Departments[2].Total;
            this.TotalExpensesGroup.TotalMaintenance = this.Departments[3].Total;
            this.TotalExpensesGroup.TotalManagement = this.Departments[4].Total;
            this.TotalExpensesGroup.TotalInterest = this.Departments[5].Total;

        }


        public decimal TotalIncome { get; set; }

        public RentExpenseTotal TotalExpensesGroup { get; set; }

        public decimal TotalExpense
        {
            get
            {
                return this.TotalExpensesGroup.AllExpenses;
            }
        }


        public decimal TotalProfit
        {
            get
            {
                return this.TotalIncome + this.TotalExpense; // Expense is expressed in -negative form 
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
            this.Departments = new List<RentalBuildingSingleYearSingleDepartment>();
        }

    }



    //================================================================================
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    [TableMapping(TableName = "RentBuildings")]
    public class RentBuilding : PersistentObject
    {
        #region PRIVATE PROPERTIES

        private int id = -1;
        private string name;

        //
        // Account and Category associated to this Rental
        //
        private int categoryForIncome;
        private int categoryForTaxes;
        private int categoryForInterest;
        private int categoryForRepairs;
        private int categoryForMaintenance;
        private int categoryForManagement;
        private string address;
        private DateTime purchasedDate = DateTime.Now;
        private decimal purchasedPrice = 1;
        private decimal estimatedValue = 2;
        private decimal landValue = 1;
        private string ownershipName1;
        private string ownershipName2;
        private decimal ownershipPercentage1;
        private decimal ownershipPercentage2;
        private string note;
        private int yearStart = int.MaxValue;
        private int yearEnd = int.MinValue;

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
                    this.OnChanged("Id");
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
                    this.OnChanged("Name");
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
                    this.OnChanged("Address");
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
                    this.OnChanged("PurchasedDate");
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
                    this.OnChanged("PurchasedPrice");
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
                    this.OnChanged("LandValue");
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
                    this.OnChanged("EstimatedValue");
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
                    this.OnChanged("OwnershipName1");
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
                    this.OnChanged("OwnershipName2");
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
                    this.OnChanged("OwnershipPercentage1");
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
                    this.OnChanged("OwnershipPercentage2");
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
                    this.OnChanged("Note");
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
                    this.OnChanged("CategoryForTaxes");
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
                    this.OnChanged("CategoryForIncome");
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
                    this.OnChanged("CategoryForInterest");
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
                    this.OnChanged("CategoryForRepairs");
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
                    this.OnChanged("CategoryForMaintenance");
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
                    this.OnChanged("CategoryForManagement");
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
                foreach (var x in this.Years)
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
                foreach (var x in this.Years)
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

                foreach (var x in this.Years)
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
                this.RecalcYears();

                return GetYearRangeString(this.yearStart, this.yearEnd);
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

            this.yearStart = int.MaxValue;
            this.yearEnd = int.MinValue;

            foreach (var x in this.Years)
            {
                this.yearStart = Math.Min(this.yearStart, x.Key);
                this.yearEnd = Math.Max(this.yearEnd, x.Key);
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
            this.Years = new SortedDictionary<int, RentalBuildingSingleYear>(new DescendingComparer<int>());
        }

        public RentBuilding ShallowCopy()
        {
            return (RentBuilding)this.MemberwiseClone();
        }

        public string GetUniqueKey()
        {
            return string.Format("{0}", this.id);
        }

        private List<RentUnit> units = new List<RentUnit>();

        [XmlIgnore]
        public List<RentUnit> Units
        {
            get { return this.units; }
            set { this.units = value; }
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
                DataContractSerializer xs = new DataContractSerializer(typeof(RentBuilding), MyMoney.GetKnownTypes());
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
                this.Units.Clear();

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

            lock (this.rentBuildings)
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
                this.FireChangeEvent(this, new ChangeEventArgs(r, null, ChangeType.Inserted));
            }
        }

        public RentBuilding Find(string key)
        {
            return this.rentBuildings[key];
        }

        public RentBuilding FindByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (RentBuilding r in this.rentBuildings.Values)
            {
                if (r.Name == name)
                {
                    return r;
                }
            }
            return null;
        }

        // todo: there should be no references left at this point...
        public bool RemoveBuilding(RentBuilding x, bool forceRemoveAfterSave = false)
        {
            lock (this.rentBuildings)
            {
                if (x.IsInserted || forceRemoveAfterSave)
                {
                    // then we can remove it immediately
                    if (this.rentBuildings.ContainsKey(x.GetUniqueKey()))
                    {
                        this.rentBuildings.Remove(x.GetUniqueKey());
                        this.FireChangeEvent(this, x, "IsDeleted", ChangeType.Deleted);
                    }
                }
            }
            // mark it for deletion on next save
            x.OnDelete();
            return true;
        }

        private readonly ThreadSafeObservableCollection<RentBuilding> observableCollection = new ThreadSafeObservableCollection<RentBuilding>();

        public ObservableCollection<RentBuilding> GetList()
        {
            lock (this.rentBuildings)
            {
                this.observableCollection.Clear();
                this.AggregateBuildingInformation((MyMoney)this.Parent);

                // sorted by Rental Names
                foreach (RentBuilding r in this.rentBuildings.Values.OrderBy(item => { return item.Name; }))
                {
                    if (!r.IsDeleted)
                    {
                        this.observableCollection.Add(r);
                    }
                }
            }
            return this.observableCollection;
        }



        public override bool FireChangeEvent(object sender, object item, string name, ChangeType type)
        {
            if (sender == this && type == ChangeType.Reloaded)
            {
                this.AggregateBuildingInformation((MyMoney)this.Parent);
            }
            return this.FireChangeEvent(sender, new ChangeEventArgs(item, name, type));
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
            this.Add((RentBuilding)child);
        }

        public override void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false)
        {
            this.RemoveBuilding((RentBuilding)pe, forceRemoveAfterSave);
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
            return this.GetEnumerator();
        }
        #endregion
    }


    //================================================================================
    [TableMapping(TableName = "RentUnits")]
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class RentUnit : PersistentObject
    {
        #region PRIVATE PROPERTIES

        private int id;
        private int building;
        private string name;
        private string renter;
        private string note;
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
                    this.OnChanged("Id");
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
                    this.OnChanged("Building");
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
                    this.OnChanged("Name");
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
                    this.OnChanged("Renter");
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
                    this.OnChanged("Note");
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
                DataContractSerializer xs = new DataContractSerializer(typeof(RentBuilding), MyMoney.GetKnownTypes());
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
        private int nextUnit;
        private Hashtable<int, RentUnit> collection = new Hashtable<int, RentUnit>();


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
            lock (this.collection)
            {
                x.Parent = this;
                this.collection[x.Id] = x;
            }
        }

        public bool RemoveRentUnit(RentUnit x, bool forceRemoveAfterSave = false)
        {
            lock (this.collection)
            {
                if (x.IsInserted || forceRemoveAfterSave)
                {
                    if (this.collection.ContainsKey(x.Id))
                    {
                        this.collection.Remove(x.Id);
                        this.FireChangeEvent(this, x, "IsDeleted", ChangeType.Deleted);
                    }
                }
            }
            x.OnDelete();
            return true;
        }

        // ICollection.
        public bool Remove(RentUnit x)
        {
            return this.RemoveRentUnit(x);
        }

        public RentUnit Get(int id)
        {
            lock (this.collection)
            {
                if (this.collection.ContainsKey(id))
                {
                    return this.collection[id];
                }
            }
            return null;
        }
        public List<RentUnit> GetList()
        {
            List<RentUnit> list = new List<RentUnit>();

            lock (this.collection)
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
            this.Add((RentUnit)child);
        }

        public override void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false)
        {
            this.RemoveRentUnit((RentUnit)pe, forceRemoveAfterSave);
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
            return this.GetEnumerator();
        }
        #endregion
    }


    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Categories : PersistentContainer, ICollection<Category>
    {
        private int nextCategory;
        private Hashtable<int, Category> categories = new Hashtable<int, Category>();
        private Hashtable<string, Category> categoryIndex = new Hashtable<string, Category>();

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
                return this.GetOrCreateCategory("Split", CategoryType.None);
            }
        }

        [XmlIgnore]
        public Category SalesTax
        {
            get
            {
                return this.GetOrCreateCategory("Taxes:Sales Tax", CategoryType.Expense);
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


        [XmlIgnore]
        public Category UnassignedSplit
        {
            get { return this.GetOrCreateCategory("UnassignedSplit", CategoryType.None); }
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
                this.OnNameChanged(result, null, result.Name);
            }
        }

        public override void OnNameChanged(object o, string oldName, string newName)
        {
            if (oldName != null && this.categoryIndex.ContainsKey(oldName))
            {
                this.categoryIndex.Remove(oldName);
            }

            this.categoryIndex[newName] = (Category)o;
        }

        public Category FindCategory(string name)
        {
            if (name == null || name.Length == 0)
            {
                return null;
            }

            return this.categoryIndex[name];
        }

        public Category GetOrCreateCategory(string name, CategoryType type)
        {
            if (name == null || name.Length == 0)
            {
                return null;
            }

            Category result = null;
            this.categoryIndex.TryGetValue(name, out result);
            if (result == null)
            {
                result = new Category(this);
                result.Type = type;
                this.AddCategory(result);
                result.Name = name;
                this.AddParents(result);
                this.categoryIndex[name] = result;
                this.FireChangeEvent(this, result, null, ChangeType.Inserted);
            }
            else if (result.IsDeleted)
            {
                result.Undelete();
            }
            return result;
        }

        public Category FindCategoryById(int id)
        {
            return this.categories[id];
        }

        // todo: there should be no references left at this point...
        public bool RemoveCategory(Category c, bool forceRemoveAfterSave = false)
        {
            if (c.IsInserted || forceRemoveAfterSave)
            {
                // then we can remove it
                if (this.categories.ContainsKey(c.Id))
                {
                    this.categories.Remove(c.Id);
                    this.FireChangeEvent(this, c, "IsDeleted", ChangeType.Deleted);
                }
            }

            if (this.categoryIndex.ContainsKey(c.Name))
            {
                this.categoryIndex.Remove(c.Name);
            }

            // mark it for deletion on next save
            c.OnDelete();
            return true;
        }

        public IList<Category> SortedCategories => this.GetCategories();

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
            list.Sort(new Comparison<Category>(this.CategoryComparer));
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
            list.Sort(new Comparison<Category>(this.CategoryComparer));
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
            if (a == null && b != null)
            {
                return -1;
            }

            if (a != null && b == null)
            {
                return 1;
            }

            string n = a.Name;
            string m = b.Name;
            if (n == null && m != null)
            {
                return -1;
            }

            if (n != null && m == null)
            {
                return 1;
            }

            return n.CompareTo(m);
        }

        private void AddParents(Category c)
        {
            string name = c.Name;
            int i = name.LastIndexOf(":");
            bool foundParent = false;
            while (i > 0 && !foundParent)
            {
                string pname = name.Substring(0, i);
                Category parent = this.FindCategory(pname);
                foundParent = parent != null;
                if (!foundParent)
                {
                    parent = this.GetOrCreateCategory(pname, c.Type);
                }
                parent.AddSubcategory(c);
                c = parent;
                name = pname;
                i = name.LastIndexOf(":");
            }
        }

        public override bool FireChangeEvent(object sender, object item, string name, ChangeType type)
        {
            if (sender == this && type == ChangeType.Reloaded)
            {
                this.FixParents();
            }
            return base.FireChangeEvent(sender, item, name, type);
        }

        internal void FixParents()
        {
            foreach (Category c in this.GetCategories())
            {
                int parentId = c.ParentId;
                if (parentId != -1)
                {
                    Category m = this.FindCategoryById(parentId);
                    if (m == null)
                    {
                        this.AddParents(c);
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
            this.AddCategory(item);
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
            this.Add((Category)child);
        }

        public override void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false)
        {
            this.RemoveCategory((Category)pe, forceRemoveAfterSave);
        }

        public bool Remove(Category item)
        {
            return this.RemoveCategory(item);
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
            return this.GetEnumerator();
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
            Category ic = this.FindCategory(category.Name);
            if (ic == null)
            {
                ic = this.GetOrCreateCategory(category.Name, category.Type);
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
        private int id = -1;
        private string label;
        private string name; // full name
        private string description;
        private CategoryType type;
        private decimal budget;
        private CalendarRange range;
        private decimal balance;
        private bool isEditing;
        private string colorString;
        private int taxRefNum;
        private int parentid = -1;
        private Category parent; // parent category
        private ThreadSafeObservableCollection<Category> subcategories;

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
                    this.id = value; this.OnChanged("Id");
                }
            }
        }

        public override void OnDelete()
        {
            if (this.subcategories != null)
            {
                for (int i = this.subcategories.Count - 1; i >= 0; i--)
                {
                    Category c = this.subcategories[i];
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
                    this.OnChanged("ParentCategory");
                    this.OnChanged("InheritedColor");
                }
            }
        }

        [XmlIgnore]
        public Category Root
        {
            get
            {
                if (this.parent == null)
                {
                    return this;
                }

                return this.parent.Root;
            }
        }

        public static string Combine(string name, string suffix)
        {
            if (name == null)
            {
                return suffix;
            }

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
                    this.name = this.GetFullName();
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
                            this.OnNameChanged(old);
                        }
                        finally
                        {
                            this.Parent.EndUpdate();
                        }
                    }
                    else
                    {
                        this.OnNameChanged(old);
                    }
                }
            }
        }

        private void OnNameChanged(string old)
        {
            this.RenameSubcategories();
            this.OnNameChanged(old, this.name);
            this.OnChanged("Name");
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

                    this.OnChanged("Label");

                }
            }
        }

        [XmlIgnore]
        public string Prefix
        {
            get
            {
                int indexFromRight = this.Name.LastIndexOf(':');
                if (indexFromRight == -1)
                {
                    return string.Empty;
                }
                return this.Name.Substring(0, indexFromRight + 1);
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
                    this.description = Truncate(value, 255); this.OnChanged("Description");
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
                    this.type = value; this.OnChanged("Type");
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
                else
                {
                    s = s.Trim();
                }
                this.colorString = s;
                if (this.colorString == "#00FFFFFF")
                {
                    this.colorString = null;
                }
                this.OnChanged("Color");
                this.NotifySubcategoriesChanged("InheritedColor");
            }
        }

        private void NotifySubcategoriesChanged(string name)
        {
            this.RaisePropertyChanged(name);
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
                    this.OnChanged("Budget");
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
                    this.OnChanged("Balance");
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
            this.OnChanged("Balance");
            if (this.HasSubcategories)
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
                    this.OnChanged("Frequency");
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
                    this.OnChanged("TaxRefNum");
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
            if (this.subcategories == null)
            {
                this.subcategories = new ThreadSafeObservableCollection<Category>();
            }

            this.subcategories.Add(c);
        }

        public void AddSubcategory(Category c)
        {
            if (c.parent != null)
            {
                c.parent.RemoveSubcategory(c);
            }
            c.parent = this;
            if (this.subcategories == null)
            {
                this.subcategories = new ThreadSafeObservableCollection<Category>();
            }

            this.subcategories.Add(c);
            c.Name = c.GetFullName();
            this.FireChangeEvent(this, null, ChangeType.ChildChanged);
        }

        public void RemoveSubcategory(Category c)
        {
            Debug.Assert(this.subcategories != null);
            if (this.subcategories == null)
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
            if (this.id == categoryId)
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

            if (categoriesIdToMatch.Contains(this.id))
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
            if (child == null)
            {
                return false;
            }

            if (child == this)
            {
                return true;
            }

            do
            {
                child = child.ParentCategory;
                if (child == this)
                {
                    return true;
                }
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
            if (substring == null)
            {
                return false;
            }

            Category c = this;
            do
            {
                if (substring == c.Label)
                {
                    return true;
                }

                c = c.ParentCategory;
            } while (c != null);
            return false;
        }

        private void RenameSubcategories()
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

        public static decimal RangeToDaily(decimal budget, CalendarRange r, int years)
        {
            switch (r)
            {
                case CalendarRange.Annually:
                    return budget / (years * 365);
                case CalendarRange.BiMonthly:
                    return budget * 6 / 365;
                case CalendarRange.Daily:
                    return budget;
                case CalendarRange.Monthly:
                    return budget * 12 / 365;
                case CalendarRange.Quarterly:
                    return budget * 3 / 365;
                case CalendarRange.SemiAnnually:
                    return budget * 2 / 365;
                case CalendarRange.TriMonthly:
                    return budget * 4 / 365;
                case CalendarRange.Weekly:
                    return budget * 52 / 365;
                case CalendarRange.BiWeekly:
                    return budget * 26 / 365;
            }
            return 0;
        }

        public static decimal DailyToRange(decimal budget, CalendarRange r, int years)
        {
            switch (r)
            {
                case CalendarRange.Annually:
                    return budget * years * 365;
                case CalendarRange.BiMonthly:
                    return budget * 365 / 6;
                case CalendarRange.Daily:
                    return budget;
                case CalendarRange.Monthly:
                    return budget * 365 / 12;
                case CalendarRange.Quarterly:
                    return budget * 365 / 3;
                case CalendarRange.SemiAnnually:
                    return budget * 365 / 2;
                case CalendarRange.TriMonthly:
                    return budget * 365 / 4;
                case CalendarRange.Weekly:
                    return budget * 365 / 52;
                case CalendarRange.BiWeekly:
                    return budget * 365 / 26;
            }
            return 0;
        }

        public bool Matches(object o, Operation op)
        {
            if (o == null)
            {
                return false;
            }

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
            get { return this.isEditing; }
            set
            {
                if (this.isEditing != value)
                {
                    this.isEditing = value;
                    this.OnChanged("IsEditing");
                }
            }
        }

    }

    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Securities : PersistentContainer, ICollection<Security>
    {
        private int nextSecurity;
        private Hashtable<int, Security> securities = new Hashtable<int, Security>();
        private Hashtable<string, Security> securityIndex = new Hashtable<string, Security>();


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
            if (this.nextSecurity <= id)
            {
                this.nextSecurity = id + 1;
            }

            this.securities[id] = s;
            this.FireChangeEvent(this, s, null, ChangeType.Inserted);
            return s;
        }
        public Security AddSecurity(Security s)
        {
            s.Parent = this;
            if (s.Id == -1)
            {
                s.Id = this.nextSecurity++;
                s.OnInserted();
            }
            else if (this.nextSecurity <= s.Id)
            {
                this.nextSecurity = s.Id + 1;
            }
            this.securities[s.Id] = s;
            this.OnNameChanged(s, null, s.Name);
            this.FireChangeEvent(this, s, null, ChangeType.Inserted);
            return s;
        }

        public override void OnNameChanged(object o, string oldName, string newName)
        {
            if (oldName != null && this.securityIndex.ContainsKey(oldName))
            {
                this.securityIndex.Remove(oldName);
            }
            if (!string.IsNullOrEmpty(newName))
            {
                this.securityIndex[newName] = (Security)o;
            }
        }

        public Security FindSecurity(string name, bool add)
        {
            if (name == null || name.Length == 0)
            {
                return null;
            }

            Security result = null;
            result = this.securityIndex[name];
            if (result == null && add)
            {
                result = this.AddSecurity(this.nextSecurity);
                result.Name = name;
                this.securityIndex[name] = result;
                this.FireChangeEvent(this, result, null, ChangeType.Inserted);
            }
            return result;
        }

        public Security FindSecurityById(string name)
        {
            name = name.Trim();
            if (name == null || name.Length == 0)
            {
                return null;
            }

            foreach (Security s in this.securities.Values)
            {
                if (s.CuspId != null && string.Compare(s.CuspId, name, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    return s;
                }
            }
            return null;
        }

        public Security FindSymbol(string name, bool add)
        {
            name = name.Trim();
            if (name == null || name.Length == 0)
            {
                return null;
            }

            foreach (Security s in this.securities.Values)
            {
                if (string.Compare(s.Symbol, name, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    return s;
                }
            }
            Security result = null;
            if (add)
            {
                result = this.AddSecurity(this.nextSecurity);
                result.Symbol = name;
                result.Name = name;
                this.FireChangeEvent(this, result, null, ChangeType.Inserted);
            }
            return result;
        }

        public Security FindSecurityAt(int id)
        {
            return this.securities[id];
        }

        // todo: there should be no references left at this point...
        public bool RemoveSecurity(Security s, bool forceRemoveAfterSave = false)
        {
            if (s.IsInserted || forceRemoveAfterSave)
            {
                // then we can remove it immediately.
                if (this.securities.ContainsKey(s.Id))
                {
                    this.securities.Remove(s.Id);
                    this.FireChangeEvent(this, s, "IsDeleted", ChangeType.Deleted);
                }
            }

            string name = s.Name;
            if (!string.IsNullOrEmpty(name) && this.securityIndex.ContainsKey(name))
            {
                this.securityIndex.Remove(name);
            }

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
            List<Security> list = this.GetSecurities();
            list.Sort(Security.Compare);
            return list;
        }

        public List<Security> AllSecurities
        {
            get
            {
                return this.GetSecurities();
            }
        }

        public List<Security> GetSecuritiesAsList()
        {
            List<Security> list = new List<Security>();
            lock (this.securities)
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
            lock (this.securities)
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
            return this.securities.ContainsKey(item.Id);
        }

        public void CopyTo(Security[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { return this.securities.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public override void Add(object child)
        {
            this.Add((Security)child);
        }

        public override void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false)
        {
            this.RemoveSecurity((Security)pe, forceRemoveAfterSave);
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
            return this.GetEnumerator();
        }

        internal Security ImportSecurity(Security security)
        {
            if (security == null || string.IsNullOrEmpty(security.Name))
            {
                return null;
            }
            Security s = this.FindSecurity(security.Name, true);
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
        Private, // Investment in a private company.
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
        private int id = -1;
        private string name;
        private string symbol;
        private decimal price;
        private DateTime priceDate;
        private decimal lastPrice;
        private string cuspid;
        private bool expanded;
        private SecurityType type;
        private YesNo taxable;
        private ObservableStockSplits splits;


        static Security()
        {
            None = new Security() { Name = "<None>" };
        }

        public Security()
        { // for serialization
        }

        public Security(Securities container) : base(container) { }

        public static readonly Security None;

        public override void OnChanged(string name)
        {
            base.OnChanged(name);
            // Here we have to be able to sync the ObservableStockSplits without using an event handler
            // otherwise the event handlers create memory leaks.
            if (this.splits != null)
            {
                this.splits.OnSecurityChanged(this);
            }
        }

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
                    this.id = value; this.OnChanged("Id");
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
                    this.name = Truncate(value, 80); this.OnChanged("Name");
                    this.OnNameChanged(old, this.name);
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
                    this.OnChanged("Symbol");
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
            set { if (this.price != value) { this.price = value; this.OnChanged("Price"); this.OnChanged("PercentChange"); this.OnChanged("IsDown"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "LastPrice", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal LastPrice
        {
            get { return this.lastPrice; }
            set { if (this.lastPrice != value) { this.lastPrice = value; this.OnChanged("LastPrice"); this.OnChanged("PercentChange"); this.OnChanged("IsDown"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "CUSPID", MaxLength = 20, AllowNulls = true)]
        public string CuspId
        {
            get { return this.cuspid; }
            set { if (this.cuspid != value) { this.cuspid = Truncate(value, 20); this.OnChanged("CuspId"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "SECURITYTYPE", AllowNulls = true)]
        public SecurityType SecurityType
        {
            get { return this.type; }
            set { if (this.type != value) { this.type = value; this.OnChanged("SecurityType"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "TAXABLE", SqlType = typeof(SqlByte), AllowNulls = true)]
        public YesNo Taxable
        {
            get { return this.taxable; }
            set { if (this.taxable != value) { this.taxable = value; this.OnChanged("Taxable"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "PriceDate", SqlType = typeof(SqlDateTime), AllowNulls = true)]
        public DateTime PriceDate
        {
            get { return this.priceDate; }
            set { if (this.priceDate != value) { this.priceDate = value; this.OnChanged("PriceDate"); } }
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
            if (!string.IsNullOrEmpty(this.Symbol))
            {
                return this.Symbol;
            }

            if (!string.IsNullOrEmpty(this.CuspId))
            {
                return this.CuspId;
            }

            if (!string.IsNullOrEmpty(this.Name))
            {
                return this.Name;
            }

            return "";
        }

        /// <summary>
        /// Get a non-null list of stock splits related to this Security
        /// </summary>
        public ObservableStockSplits StockSplits
        {
            get
            {
                if (this.splits != null)
                {
                    return this.splits;
                }
                Securities parent = this.Parent as Securities;
                MyMoney money = parent.Parent as MyMoney;
                this.splits = new ObservableStockSplits(this, money);
                return this.splits;
            }
        }

        public bool HasObservableStockSplits
        {
            get => this.splits != null;
        }

        public List<StockSplit> StockSplitsSnapshot
        {
            get
            {
                // return a non-synchronizing snapshot of stocksplits related
                // to this security.
                List<StockSplit> result = new List<StockSplit>();
                Securities parent = this.Parent as Securities;
                MyMoney money = parent.Parent as MyMoney;
                foreach (StockSplit s in money.StockSplits.GetStockSplitsForSecurity(this))
                {
                    result.Add(s);
                }
                return result;
            }
        }

        /// <summary>
        /// Used only for UI binding.
        /// </summary>
        [XmlIgnore]
        public bool IsExpanded
        {
            get { return this.expanded; }
            set
            {
                if (value != this.expanded)
                {
                    this.expanded = value;
                    this.OnChanged("IsExpanded");
                }
            }
        }

        public override int GetHashCode()
        {
            int rc = !string.IsNullOrEmpty(this.Name) ? this.Name.GetHashCode() : 0;
            if (!string.IsNullOrEmpty(this.Symbol))
            {
                rc += this.symbol.GetHashCode();
            }
            return rc;
        }

        public static int Compare(Security a, Security b)
        {
            if (a == null && b != null)
            {
                return -1;
            }

            if (a != null && b == null)
            {
                return 1;
            }

            if (a == null && b == null)
            {
                return 0;
            }

            if (a == b)
            {
                return 0;
            }

            string n = a.Name;
            string m = b.Name;
            if (n == null && m != null)
            {
                return -1;
            }

            if (n != null && m == null)
            {
                return 1;
            }

            if (n == null && m == null)
            {
                return 0;
            }

            var rc = n.CompareTo(m);
            if (rc != 0)
            {
                return rc;
            }

            n = a.Symbol;
            m = b.Symbol;
            if (n == null && m != null)
            {
                return -1;
            }

            if (n != null && m == null)
            {
                return 1;
            }

            if (n == null && m == null)
            {
                return 0;
            }

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
                case SecurityType.Private:
                    caption = "Private";
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

            var splits = this.StockSplitsSnapshot;
            var s2Splits = s2.StockSplitsSnapshot;

            if (splits.Count == 0)
            {
                if (s2Splits.Count > 0)
                {
                    // then copy the splits across from the security we are merging.
                    foreach (StockSplit s in s2Splits)
                    {
                        StockSplit copy = new StockSplit();
                        copy.Security = s.Security;
                        copy.Date = s.Date;
                        copy.Numerator = s.Numerator;
                        copy.Denominator = s.Denominator;
                        splits.Add(s);
                        s.OnDelete();
                    }
                }
            }

            this.OnChanged("StockSplits");

            return true;
        }
    }

    internal class SecurityComparer : IComparer<Security>
    {
        public int Compare(Security x, Security y)
        {
            return Security.Compare(x, y);
        }
    }

    internal class SecuritySymbolComparer : IComparer<Security>
    {
        public int Compare(Security a, Security b)
        {
            if (a == null && b != null)
            {
                return -1;
            }

            if (a != null && b == null)
            {
                return 1;
            }

            if (a == null && b == null)
            {
                return 0;
            }

            string n = a.Symbol;
            string m = b.Symbol;
            if (n == null && m != null)
            {
                return -1;
            }

            if (n != null && m == null)
            {
                return 1;
            }

            if (n == null && m == null)
            {
                return 0;
            }

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
    public class TransactionComparerByTaxDate : IComparer<Transaction>
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

            return x.CompareByTaxDate(y);
        }

    }

    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Transactions : PersistentContainer, ICollection<Transaction>
    {
        private long nextTransaction;
        private Hashtable<long, Transaction> transactions = new Hashtable<long, Transaction>();

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
                foreach (Transaction t in this.transactions.Values)
                {
                    if (t.IsDeleted)
                    {
                        continue;
                    }

                    list.Add(t);
                }
                return list.ToArray();
            }
            set
            {
                if (value != null)
                {
                    lock (this.transactions)
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

            lock (this.transactions)
            {
                if (this.nextTransaction <= id)
                {
                    this.nextTransaction = id + 1;
                }

                this.transactions[t.Id] = t;
            }
            this.FireChangeEvent(this, t, null, ChangeType.Inserted);
            return t;
        }

        public void AddTransaction(Transaction t)
        {
            lock (this.transactions)
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
                    this.ResetNextTransactionId();

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

        public bool RemoveTransaction(Transaction t, bool forceRemoveAfterSave = false)
        {
            if (t.IsInserted || forceRemoveAfterSave)
            {
                lock (this.transactions)
                {
                    // then we can remove it immediately.
                    this.transactions.Remove(t.Id);
                    this.FireChangeEvent(this, t, "IsDeleted", ChangeType.Deleted);
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
            return this.transactions[id];
        }

        public Transaction FindFITID(string fitid, Account a)
        {
            foreach (Transaction t in this.transactions.Values)
            {
                if (t.FITID == fitid && t.Account == a)
                {
                    return t;
                }
            }
            return null;
        }

        /// <summary>
        /// Return true if the given pair of transactions represent a recurring event that
        /// has happened in the past looking back in the given collection of transactions.
        /// </summary>
        /// <param name="t">The first transaction of the pair</param>
        /// <param name="u">The second transaction of the pair</param>
        /// <param name="tc">The collection to search</param>
        /// <param name="amountDeltaPercent">The allowable % difference payment amounts</param>
        /// <param name="daysDeltaPercent">The allowable % difference in # days between payments</param>
        /// <param name="recurringCount">How many such transactions before we consider it a recurring type</param>
        /// <returns></returns>
        public static bool IsRecurring(Transaction t, Transaction u, IList<Transaction> tc,
            decimal amountDeltaPercent = 10,
            decimal daysDeltaPercent = 10,
            int recurringCount = 3)
        {
            if (t.Amount == 0)
            {
                return false;
            }

            // if we find prior transactions that match (or very closely match) that are on the 
            // same (or similar) TimeSpan then this might be a recurring transaction.
            TimeSpan span = t.Date - u.Date;
            decimal days = (decimal)Math.Abs(span.TotalDays);
            if (days == 0)
            {
                return false;
            }

            DateTime startDate = t.Date;
            int i = tc.IndexOf(t);
            int j = tc.IndexOf(u);
            Transaction start = t;
            if (j < i)
            {
                start = u;
                i = j;
            }

            // create a new list so we can sort by date, since 'tc' is not necessarily yet.
            List<Transaction> similarTransactions = new List<Transaction>();
            for (--i; i > 0; i--)
            {
                Transaction w = tc[i];
                if (w.amount == 0 || w.Status == TransactionStatus.Void)
                {
                    continue;
                }

                // if amount is within a given percentage of each other then it might be a recurring instance.
                if (w.PayeeName == t.PayeeName)
                {
                    var percentChange = Math.Abs((w.Amount - t.Amount) * 100 / t.Amount);
                    if (percentChange < amountDeltaPercent)
                    {
                        similarTransactions.Add(w);
                        if (similarTransactions.Count > 10)
                        {
                            break; // should be enough.
                        }
                    }
                }
            }

            if (similarTransactions.Count < recurringCount)
            {
                return false;
            }

            similarTransactions.Sort((x, y) => { return x.Date.CompareTo(y.Date); });

            List<double> daysBetween = new List<double>();
            Transaction previous = null;
            foreach (Transaction s in similarTransactions)
            {
                if (previous != null)
                {
                    TimeSpan diff = s.Date - previous.Date;
                    double diffDays = Math.Abs(diff.TotalDays);
                    if (diffDays > 0)
                    {
                        daysBetween.Add(diffDays);
                    }
                }
                previous = s;
            }

            var filtered = MathHelpers.RemoveOutliers(daysBetween, 1);
            decimal mean = (decimal)MathHelpers.Mean(filtered);

            if (Math.Abs((days - mean) * 100 / days) < daysDeltaPercent)
            {
                return true;
            }
            return false;
        }

        private static bool IsSameString(string a, string b)
        {
            if (string.IsNullOrEmpty(a))
            {
                return string.IsNullOrEmpty(b);
            }

            if (string.IsNullOrEmpty(b))
            {
                return false;
            }

            return a.Trim() == b.Trim();
        }

        private static bool IsPotentialDuplicate(Transaction t, Transaction u, int dayRange)
        {
            return !u.IsFake && !t.IsFake &&
                u != t && u.amount == t.amount && u.PayeeName == t.PayeeName &&
                // they must be in the same account (which they may not be if on the multi-account view).
                u.Account == t.Account &&
                IsSameString(u.Number, t.Number) &&
                // ignore transfers for now
                t.Transfer == null && u.Transfer == null &&
                // if user has already marked both as not duplicates, then skip it.
                (!t.NotDuplicate || !u.NotDuplicate) &&
                // and if they are investment transactions the stock type and unit quanities have to be the same
                IsPotentialDuplicate(t.Investment, u.Investment) &&
                // if they both have unique FITID fields, then the bank is telling us these are not duplicates.
                // they can't be both reconciled, because then we can't merge them!
                (u.Status != TransactionStatus.Reconciled || t.Status != TransactionStatus.Reconciled) &&
                // within specified date range
                Math.Abs((u.Date - t.Date).Days) < dayRange;
        }

        private static bool IsPotentialDuplicate(Investment u, Investment v)
        {
            if (u != null && v == null)
            {
                return false;
            }

            if (u == null && v != null)
            {
                return false;
            }

            if (u == null && v == null)
            {
                return true;
            }

            return u.TradeType == v.TradeType &&
                u.Units == v.Units &&
                u.UnitPrice == v.UnitPrice &&
                u.SecurityName == v.SecurityName;
        }

        /// <summary>
        /// Find a potential duplicate transaction in the given collection within the given
        /// date range.
        /// </summary>
        /// <param name="t">The starting transaction to search from</param>
        /// <param name="tc">The collection to search</param>
        /// <param name="searchRange">The range of dates to allow duplicate to be</param>
        /// <returns></returns>
        public static Transaction FindPotentialDuplicate(Transaction t, IList<Transaction> tc, TimeSpan searchRange)
        {
            int days = searchRange.Days;
            int[] indices = new int[2]; // one forward index, and one backward index.

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
                                Transaction u = tc[k];
                                if (u.PayeeName == t.PayeeName)
                                {
                                    if (IsPotentialDuplicate(t, u, days))
                                    {
                                        if (Transactions.IsRecurring(t, u, tc))
                                        {
                                            return null;
                                        }

                                        // ok, this is the closest viable duplicate...
                                        return u;
                                    }
                                }
                            }
                        }
                    }
                }
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
                    if (string.IsNullOrEmpty(t.Memo))
                    {
                        t.Memo = t.PayeeName;
                    }

                    t.OriginalPayee = t.PayeeName;
                    t.Payee = alias.Payee;
                }

                Transaction u = this.FindMatching(t, excluded);
                if (u != null)
                {

                    // Merge new details with matching transaction.
                    u.FITID = t.FITID;
                    if (string.IsNullOrEmpty(u.Number) && t.Number != null)
                    {
                        u.Number = t.Number;
                    }

                    if (string.IsNullOrEmpty(u.Memo) && t.Memo != null)
                    {
                        u.Memo = t.Memo;
                    }

                    if (u.Status == TransactionStatus.None)
                    {
                        u.Status = TransactionStatus.Electronic;
                    }

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
        private static bool InvestmentDetailsMatch(Transaction t, Transaction u)
        {
            Investment i = t.Investment;
            Investment j = t.Investment;
            if (i == null)
            {
                return j == null;
            }

            if (j == null)
            {
                return false;
            }

            return i.Security == j.Security && i.Units == j.Units && i.UnitPrice == j.UnitPrice &&
                    i.Type == j.Type && i.TradeType == j.TradeType;
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
            foreach (Transaction u in this.transactions.Values)
            {
                if (!u.IsDeleted && u.Account == account && u.amount == amount && InvestmentDetailsMatch(t, u) && !excluded.ContainsKey(u.Id))
                {
                    TimeSpan diff = dt - u.Date;
                    if (Math.Abs(diff.Days) < 30)
                    {
                        if (fitid != null && u.FITID == fitid && this.DatesMatch(u, t))
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
                        else if (this.DatesMatch(u, t) && this.PayeesMatch(t, u))
                        {
                            if (best == null)
                            {
                                best = u;
                            }
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
            foreach (Transaction t in this.transactions.Values)
            {
                if (t.IsDeleted)
                {
                    continue;
                }

                view.Add(t);
            }
            view.Sort(new TransactionComparerByDate());
            return view;
        }

        public ICollection<Transaction> GetAllTransactionsByTaxDate()
        {
            List<Transaction> view = new List<Transaction>();
            foreach (Transaction t in this.transactions.Values)
            {
                if (t.IsDeleted)
                {
                    continue;
                }

                view.Add(t);
            }
            view.Sort(new TransactionComparerByTaxDate());
            return view;
        }

        public IList<Transaction> GetTransactionsFrom(Account a)
        {
            List<Transaction> view = new List<Transaction>();
            lock (this.transactions)
            {
                foreach (Transaction t in this.transactions.Values)
                {
                    if (t.IsDeleted || t.Account != a)
                    {
                        continue;
                    }

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
            lock (this.transactions)
            {
                foreach (Transaction t in this.transactions.Values)
                {
                    if (t.IsDeleted || t.Account != a)
                    {
                        continue;
                    }

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
            lock (this.transactions)
            {
                foreach (Transaction t in this.transactions.Values)
                {
                    if (t.IsDeleted || t.Account != a || (include != null && !include(t)))
                    {
                        continue;
                    }

                    view.Add(t);
                }
            }
            view.Sort(SortByDate);
            return view;
        }


        public DateTime? GetMostRecentBudgetDate()
        {
            DateTime? result = null;
            lock (this.transactions)
            {
                foreach (Transaction t in this.transactions.Values)
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
            lock (this.transactions)
            {
                foreach (Transaction t in this.transactions.Values)
                {
                    if (t.BudgetBalanceDate.HasValue && (result == null || t.BudgetBalanceDate.Value < result))
                    {
                        result = t.BudgetBalanceDate;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Return the range of tax years that the transactions fall into.
        /// </summary>
        public Tuple<int, int> GetTaxYearRange(int fiscalYearStart)
        {
            int firstYear = DateTime.Now.Year;
            int lastYear = firstYear;

            ICollection<Transaction> transactions = this.GetAllTransactionsByTaxDate();
            Transaction first = transactions.FirstOrDefault();
            if (first != null)
            {
                var td = first.TaxDate;
                firstYear = td.Year;
                if (fiscalYearStart > 0 && td.Month >= fiscalYearStart + 1)
                {
                    firstYear++;
                }
            }
            Transaction last = transactions.LastOrDefault();
            if (last != null)
            {
                var td = last.TaxDate;
                lastYear = td.Year;
                if (fiscalYearStart > 0 && td.Month >= fiscalYearStart + 1)
                {
                    lastYear++;
                }
            }
            return new Tuple<int, int>(firstYear, lastYear);
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
                    foreach (Transaction t in this.GetTransactionsFrom(a))
                    {
                        if (t.Unaccepted)
                        {
                            unaccepted++;
                        }

                        if (!t.IsDeleted && t.Status != TransactionStatus.Void)
                        {
                            // current account balance 
                            balance += t.Amount;
                        }

                        // snapshot the current running balance value
                        t.Balance = balance;
                    }

                    if (a.Type == AccountType.Brokerage || a.Type == AccountType.Retirement)
                    {
                        foreach (SecurityPurchase sp in calculator.GetHolding(a).GetHoldings())
                        {
                            //if (Math.Floor(sp.UnitsRemaining) > 0)
                            {
                                balance += sp.LatestMarketValue;
                            }
                        }
                    }

                    // Refresh the Account balance value
                    if (a.Balance != balance)
                    {
                        changed = true;
                    }
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
                        investmentValue += sp.LatestMarketValue;
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
            foreach (Transaction t in this.GetTransactionsFrom(a))
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
                {
                    continue;
                }

                balance += t.Amount;
            }
            return balance;
        }


        public decimal EstimatedBalance(Account a, DateTime est)
        {
            DateTime et = new DateTime(est.Year, est.Month, est.Day);
            decimal balance = a.OpeningBalance;
            foreach (Transaction t in this.GetTransactionsFrom(a))
            {
                DateTime td = new DateTime(t.Date.Year, t.Date.Month, t.Date.Day);
                if (t.IsDeleted || t.Status == TransactionStatus.Void || td > et)
                {
                    continue;
                }

                balance += t.Amount;
            }
            return balance;
        }

        public IList<Transaction> FindTransfersToAccount(Account a)
        {
            List<Transaction> view = new List<Transaction>();
            foreach (Transaction t in this.transactions.Values)
            {
                if (t.IsDeleted)
                {
                    continue;
                }

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
            foreach (Transaction t in this.transactions.Values)
            {
                if (t.IsDeleted)
                {
                    continue;
                }

                if (include != null && !include(t))
                {
                    continue;
                }

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
                {
                    continue;
                }

                if (include != null && !include(t))
                {
                    continue;
                }

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

        public IList<Transaction> GetTransactionsByCategory(Category c, Predicate<Transaction> include)
        {
            return this.GetTransactionsByCategory(c, include, (cat) => { return c.Contains(cat); });
        }

        public IList<Transaction> GetTransactionsByCategory(Category c, Predicate<Transaction> include, Predicate<Category> matches)
        {
            MyMoney money = this.Parent as MyMoney;
            List<Transaction> view = new List<Transaction>();
            foreach (Transaction t in this.transactions.Values)
            {
                if (t.IsDeleted)
                {
                    continue;
                }

                if (include != null && !include(t))
                {
                    continue;
                }

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
            foreach (Transaction t in this.transactions.Values)
            {
                if (t.IsDeleted)
                {
                    continue;
                }

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
            foreach (Transaction t in this.transactions.Values)
            {
                if (t.IsDeleted)
                {
                    continue;
                }

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

        public static bool IsAnyFieldsMatching(Transaction t, FilterLiteral filter, bool includeAccountField)
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
                   filter.MatchString(t.Number) ||
                   filter.MatchDecimal(t.amount);
        }

        public static bool IsSplitsMatching(Splits splits, FilterLiteral filter)
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
            foreach (Transaction t in this.transactions.Values)
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
            this.Add((Transaction)child);
        }

        public override void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false)
        {
            this.RemoveTransaction((Transaction)pe, forceRemoveAfterSave);
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
            return this.GetEnumerator();
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

        internal void JoinExtras(TransactionExtras transactionExtras)
        {
            foreach (var e in transactionExtras.ToArray())
            {
                if (this.transactions.TryGetValue(e.Transaction, out Transaction t))
                {
                    t.Extra = e;
                }
                else
                {
                    Debug.WriteLine("Dangling TransactionExtras " + e.Id.ToString());
                }
            }
        }
    }

    //================================================================================
    public class TransferChangedEventArgs : EventArgs
    {
        private readonly Transaction transaction;
        private readonly Transfer transfer;

        public TransferChangedEventArgs(Transaction t, Transfer newValue)
        {
            this.transaction = t;
            this.transfer = newValue;
        }

        public Transaction Transaction { get { return this.transaction; } }

        public Transfer NewValue { get { return this.transfer; } }
    }

    //================================================================================
    public class SplitTransferChangedEventArgs : EventArgs
    {
        private readonly Split split;
        private readonly Transfer transfer;

        public SplitTransferChangedEventArgs(Split s, Transfer newValue)
        {
            this.split = s;
            this.transfer = newValue;
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

        public long TransactionId { get { return this.Transaction.Id; } }
    }

    [Flags]
    public enum TransactionFlags
    {
        None,
        Unaccepted = 1,
        Budgeted = 2,
        HasAttachment = 4,
        NotDuplicate = 8,
        HasStatement = 16
    }

    //================================================================================
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    [TableMapping(TableName = "Transactions")]
    public class Transaction : PersistentObject
    {
        private long id = -1;
        private Account account; // that this transaction originated.
        private DateTime date;
        internal decimal amount;
        private decimal salesTax;
        private TransactionStatus status;
        private string memo;
        private Payee payee;
        private Category category;
        private string number; // requires value.Length < 10
        private Investment investment;
        private Transfer transfer;
        private string fitid;
        internal Account to; // for debugging only.        
        private decimal balance;
        private decimal runningUnits;
        private decimal runningBalance;
        private string routingPath;
        private TransactionFlags flags;
        private DateTime? reconciledDate;
        private Splits splits;
        private string pendingTransfer;
        private DateTime? budgetBalanceDate;
        private readonly Transaction related;
        private readonly Split relatedSplit;
        private DateTime? mergeDate;
        private string originalPayee; // before auto-aliasing, helps with future merging.
        private TransactionViewFlags viewState; // ui transient state only, not persisted.

        private enum TransactionViewFlags : byte
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
            if (toFake != null)
            {
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
                    this.id = value; this.OnChanged("Id");

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
                    this.SetViewState(TransactionViewFlags.TransactionDropTarget, value);
                    // don't use OnChanged, we don't want this to make the database dirty.
                    this.FireChangeEvent(this, new ChangeEventArgs(this, "TransactionDropTarget", ChangeType.None));
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
                    this.SetViewState(TransactionViewFlags.AttachmentDropTarget, value);
                    // don't use OnChanged, we don't want this to make the database dirty.
                    this.FireChangeEvent(this, new ChangeEventArgs(this, "AttachmentDropTarget", ChangeType.None));
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
                    this.OnChanged("Account");
                }
            }
        }

        private string accountName;

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
            get { return this.date; }
            set
            {
                if (this.date.Date != value.Date)
                {
                    this.date = value;
                    if (this.Transfer != null)
                    {
                        this.Transfer.Transaction.date = value;
                    }

                    this.OnChanged("Date");
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
                    this.OnChanged("Status");
                    this.OnChanged("StatusString");
                }
            }
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
                    this.payee = value;
                    this.OnChanged("Payee");
                    this.OnChanged("PayeeOrTransferCaption");
                }
            }
        }

        private string payeeName;
        // for serialization
        [DataMember]
        public string PayeeName
        {
            get { return this.payee != null ? this.payee.Name : this.payeeName; }
            set { this.payeeName = value; }
        }

        public string PayeeNameNotNull
        {
            get { return this.PayeeName == null ? string.Empty : this.PayeeName; }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "OriginalPayee", AllowNulls = true, MaxLength = 255)]
        public string OriginalPayee
        {
            get { return this.originalPayee; }
            set
            {
                if (this.originalPayee != value)
                {
                    this.originalPayee = Truncate(value, 255);
                    this.OnChanged("OriginalPayee");
                }
            }
        }

        [XmlIgnore]
        [ColumnObjectMapping(ColumnName = "Category", KeyProperty = "Id", AllowNulls = true)]
        public Category Category
        {
            get { return this.category; }
            set
            {
                if (this.IsFake)
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
                    this.OnChanged("Category");
                    this.OnChanged("CategoryName");
                    this.OnChanged("CategoryNonNull");
                    if (this.Transfer != null && this.Transfer.Transaction.category == old)
                    {
                        this.Transfer.Transaction.Category = value;
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
                if (this.IsFake && this.Splits != null && this.Splits.Count > 0)
                {
                    // Splits[0] is a special proxy entry that points to the real split located in the real transaction
                    // that this fake traction is based on
                    this.Splits.Items[0].Category = value;
                }
            }
        }

        private string categoryName;
        // for serialization;
        [DataMember]
        public string CategoryName
        {
            get { return this.category != null ? this.category.Name : this.categoryName; }
            set { this.categoryName = value; }
        }

        // for serialization;
        public string CategoryFullName
        {
            get { return this.category != null ? this.category.GetFullName() : string.Empty; }
        }

        /// <summary>
        /// For data binding where we need a non-null category, including a default category for transfers.
        /// </summary>
        [XmlIgnore]
        public Category CategoryNonNull
        {
            get
            {
                if (this.category != null)
                {
                    return this.category;
                }
                if (this.transfer != null)
                {
                    return this.MyMoney.Categories.Transfer;
                }
                if (this.IsSplit)
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
            get { return this.memo; }
            set
            {
                if (this.memo != value)
                {
                    this.memo = Truncate(value, 255);
                    if (this.Transfer != null)
                    {
                        this.Transfer.Transaction.memo = this.memo;
                    }

                    this.OnChanged("Memo");
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
                    this.OnChanged("Number");
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
                    this.SetViewState(TransactionViewFlags.Reconciling, value);
                    this.FireChangeEvent(this, new ChangeEventArgs(this, "IsReconciling", ChangeType.None));
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
                    this.OnChanged("ReconciledDate");
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
                    this.OnChanged("BudgetBalanceDate");
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
                this.OnChanged("Id");
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
                    this.OnChanged("Investment");
                }
            }
        }

        [IgnoreDataMember]
        public string TransferTo
        {
            get
            {
                if (this.pendingTransfer != null)
                {
                    return this.pendingTransfer;
                }

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

            if (this.payee == null)
            {
                return string.Empty;
            }

            return this.PayeeNameNotNull;
        }

        public static bool IsTransferCaption(string value)
        {
            return value.StartsWith(Walkabout.Properties.Resources.TransferFromPrefix) ||
                    value.StartsWith(Walkabout.Properties.Resources.TransferToPrefix) ||
                    value.StartsWith(Walkabout.Properties.Resources.TransferToClosedAccountPrefix) ||
                    value.StartsWith(Walkabout.Properties.Resources.TransferFromClosedAccountPrefix);
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
                caption = isFrom ? Walkabout.Properties.Resources.TransferFromClosedAccountPrefix : Walkabout.Properties.Resources.TransferToClosedAccountPrefix;
            }
            else
            {
                caption = isFrom ? Walkabout.Properties.Resources.TransferFromPrefix : Walkabout.Properties.Resources.TransferToPrefix;
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
            get { return this.GetPayeeOrTransferCaption(); }
            set
            {
                if (this.PayeeOrTransferCaption != value)
                {
                    MyMoney money = this.MyMoney;

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
                if (this.Parent == null)
                {
                    if (this.related != null)
                    {
                        return this.related.MyMoney;
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
                    this.transferId = value == null ? -1 : value.TransactionId;
                    this.transferSplitId = (value == null) ? -1 : ((value.Split == null) ? -1 : value.Split.Id);
                    this.transfer = value;
                    this.OnChanged("Transfer");
                    this.OnChanged("PayeeOrTransferCaption");
                }
            }
        }

        private string transferName; // for serialization only.

        [DataMember]
        public string TransferName
        {
            get { if (this.transfer != null) { return this.transfer.Transaction.Account.Name; } return this.transferName; }
            set { this.transferName = value; }
        }

        private long transferId = -1;

        // serialization
        [DataMember]
        private long TransferId
        {
            get { return this.transferId; }
            set { this.transferId = value; }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "FITID", MaxLength = 40, AllowNulls = true)]
        public string FITID
        {
            get { return this.fitid; }
            set
            {
                if (this.fitid != value)
                {
                    this.fitid = Truncate(value, 40);
                    this.OnChanged("FITID");
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
                        this.SetFlag(TransactionFlags.Unaccepted);
                    }
                    else
                    {
                        this.ClearFlag(TransactionFlags.Unaccepted);
                    }
                    this.OnChanged("Unaccepted");

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
                        this.SetFlag(TransactionFlags.HasAttachment);
                    }
                    else
                    {
                        this.ClearFlag(TransactionFlags.HasAttachment);
                    }
                    this.OnChanged("HasAttachment");
                }
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public bool HasStatement
        {
            get { return (this.flags & TransactionFlags.HasStatement) != 0; }
            set
            {
                if (this.HasStatement != value)
                {
                    if (value)
                    {
                        this.SetFlag(TransactionFlags.HasStatement);
                    }
                    else
                    {
                        this.ClearFlag(TransactionFlags.HasStatement);
                    }
                    this.OnChanged("HasStatement");
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
                        this.SetFlag(TransactionFlags.NotDuplicate);
                    }
                    else
                    {
                        this.ClearFlag(TransactionFlags.NotDuplicate);
                    }
                    this.OnChanged("NotDuplicate");
                }
            }
        }

        private void SetFlag(TransactionFlags flag)
        {
            this.Flags |= flag;
        }

        private void ClearFlag(TransactionFlags flag)
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
                    this.OnChanged("Flags");
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
                if (this.IsFake)
                {
                    if (this.relatedSplit != null)
                    {
                        return this.relatedSplit.IsBudgeted;
                    }
                    else if (this.related != null)
                    {
                        return this.related.IsBudgeted;
                    }
                }
                return (this.flags & TransactionFlags.Budgeted) != 0;
            }
            set { this.SetBudgeted(value, null); }
        }

        public void SetBudgeted(bool value, List<TransactionException> errors)
        {
            if (this.IsFake)
            {
                if (this.relatedSplit != null)
                {
                    // fake transaction is an unrolled split for by Cateogry view, so budgeting this
                    // is adding the split to the given category.
                    this.relatedSplit.SetBudgeted(value, errors);
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
                    this.SetFlag(TransactionFlags.Budgeted);
                }
                else
                {
                    this.UpdateBudget(false, errors);
                    this.ClearFlag(TransactionFlags.Budgeted);
                    this.BudgetBalanceDate = null;
                }
                this.OnChanged("IsBudgeted");
            }
        }


        [XmlIgnore]
        [IgnoreDataMember]
        public string StatusString
        {
            get
            {
                switch (this.Status)
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
                if (value == null)
                {
                    return;
                }

                switch (value.Trim().ToLowerInvariant())
                {
                    case "":
                        this.Status = TransactionStatus.None;
                        break;
                    case "c":
                        this.Status = TransactionStatus.Cleared;
                        break;
                    case "r":
                        this.Status = TransactionStatus.Reconciled;
                        break;
                    case "e":
                        this.Status = TransactionStatus.Electronic;
                        break;
                    case "v":
                        this.Status = TransactionStatus.Void;
                        break;
                }
                this.OnChanged("StatusString");
            }
        }

        [XmlIgnore]
        public bool AmountError { get; set; }

        [DataMember]
        [ColumnMapping(ColumnName = "Amount", SqlType = typeof(SqlMoney))]
        public decimal Amount
        {
            get { return this.amount; }
            set
            {
                if (this.amount != value)
                {
                    if (this.Status == TransactionStatus.Reconciled && !this.IsReconciling)
                    {
                        // raise the events so that the grid rebinds the value that was not changed.
                        this.AmountError = true;
                        throw new MoneyException("Cannot change the value of a reconciled transaction unless you are balancing the account");
                    }

                    if (this.transfer != null)
                    {
                        bool sameCurrency = this.transfer.Transaction.Account.NonNullCurrency == this.Account.NonNullCurrency;

                        if ((this.amount == 0 && this.IsInserted) || sameCurrency)
                        {
                            decimal other = this.MyMoney.Currencies.GetTransferAmount(-value, this.Account.Currency, this.transfer.Transaction.Account.Currency);

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
                    this.InternalSetAmount(value);
                }
            }
        }

        // Set the amount, without checking for transfers.
        internal void InternalSetAmount(decimal value)
        {
            this.OnAmountChanged(this.amount, value);
            this.amount = value;
            this.AmountError = false;
            this.OnChanged("Credit");
            this.OnChanged("Debit");
            this.OnChanged("Amount");
        }

        private void OnAmountChanged(decimal oldValue, decimal newValue)
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
                    this.OnChanged("SalesTax");
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
                    this.OnTransientChanged("Balance");
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
            set { if (!value.IsNull) { this.SalesTax = (decimal)value; } this.OnChanged("Tax"); }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public SqlDecimal Credit
        {
            get { return (this.Amount > 0) ? new SqlDecimal(this.Amount) : SqlDecimal.Null; }
            set
            {
                if (!value.IsNull)
                {
                    this.SetDebitCredit(value);
                }
            }
        }


        [XmlIgnore]
        [IgnoreDataMember]
        public SqlDecimal Debit
        {
            get { return (this.Amount <= 0) ? new SqlDecimal(-this.Amount) : SqlDecimal.Null; }
            set
            {
                if (!value.IsNull)
                {
                    this.SetDebitCredit(-value);
                }
            }
        }

        private uint lastSet;

        private void SetDebitCredit(SqlDecimal value)
        {
            if (!value.IsNull)
            {
                decimal amount = value.Value;
                uint tick = NativeMethods.TickCount;
                if (amount == 0 && this.lastSet / 100 == tick / 100)
                {
                    // weirdness with how row is committed, it will commit 0 to Credit field after
                    // it commits a real value to Debit field and/or vice versa, so we check for 0
                    // happening right after a real value and ignore it.  Should happen within 1/10th
                    // of a second.
                    return;
                }
                this.lastSet = tick;
                this.Amount = amount;
                this.OnChanged("Credit");
                this.OnChanged("Debit");
                this.OnChanged("HasCreditAndIsSplit");
            }
        }


        [XmlIgnore]
        [IgnoreDataMember]
        public bool HasCreditAndIsSplit
        {
            get { return this.Amount > 0 && this.IsSplit; }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public bool HasDebitAndIsSplit
        {
            get { return this.Amount <= 0 && this.IsSplit; }
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
            get { return this.related; }
        }

        private int transferSplitId = -1;

        /// <summary>
        /// This is a hack purely here to force this column to be created in the database, it is not used in memory.
        /// It has to do with how transfers in a split transaction are stored.
        /// </summary>
        [DataMember]
        [ColumnMapping(ColumnName = "TransferSplit", AllowNulls = true)]
        private int TransferSplit
        {
            get { return this.transferSplitId; }
            set { this.transferSplitId = value; }
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
                if (this.splits != null)
                {
                    this.splits.RemoveAll();
                }

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
                {
                    this.splits = new Splits(this, this);
                }

                return this.splits;
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public bool IsSplit
        {
            get
            {
                if (this.IsFake)
                {
                    return false;
                }
                return this.splits != null && this.splits.Count > 0;
            }
        }

        public Split FindSplit(int id)
        {
            if (this.splits == null)
            {
                return null;
            }

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
                        else if (!this.AutoFixDandlingTransfer(splitXfer))
                        {
                            added = true;
                            dangling.Add(this);
                        }
                    }
                }
                else if ((other.Transfer == null || other.Transfer.Transaction != this) && !this.AutoFixDandlingTransfer(null))
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
                    this.OnChanged("MergeDate");
                }
            }
        }

        public bool Merge(Transaction t)
        {
            MyMoney money = this.MyMoney;
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

            if (this.date != t.date)
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

            if (t.salesTax != this.salesTax && t.salesTax != 0)
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
            if (this.Extra != null && this.Extra.Parent is TransactionExtras container)
            {
                container.OnRemoveTransaction(this);
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
            this.flags &= ~TransactionFlags.Budgeted;

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
                    return this.Investment.Type;
                }
                return Data.InvestmentType.None;
            }
            set
            {
                this.GetOrCreateInvestment().Type = value;
            }
        }

        [XmlIgnore]
        public Security InvestmentSecurity
        {
            get
            {
                if (this.Investment != null)
                {
                    return this.Investment.Security;
                }
                return null;
            }
            set
            {
                if (value == null || value == Security.None)
                {
                    if (this.Investment != null)
                    {
                        this.Investment.Security = null;
                    }
                }
                else
                {
                    this.GetOrCreateInvestment().Security = value;
                }
                this.OnChanged("InvestmentSecurity");
            }
        }

        [XmlIgnore]
        public string InvestmentSecuritySymbol
        {
            get
            {
                if (this.Investment != null && this.Investment.Security != null)
                {
                    return this.Investment.Security.Symbol;
                }
                return null;
            }
            set
            {
                var i = this.GetOrCreateInvestment();
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
                    return this.Investment.Units;
                }
                return 0;
            }
            set
            {
                this.GetOrCreateInvestment().Units = value;
            }
        }

        [XmlIgnore]
        public decimal InvestmentUnitPrice
        {
            get
            {
                if (this.Investment != null)
                {
                    return this.Investment.UnitPrice;
                }
                return 0;
            }
            set
            {
                this.GetOrCreateInvestment().UnitPrice = value;
            }
        }

        [XmlIgnore]
        public decimal InvestmentCommission
        {
            get
            {
                if (this.Investment != null)
                {
                    return this.Investment.Commission;
                }
                return 0;
            }
            set
            {
                this.GetOrCreateInvestment().Commission = value;
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

            var rc = DateTime.Compare(this.date, compareTo.date);
            if (rc == 0)
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

            return rc;
        }

        internal int CompareByTaxDate(Transaction compareTo)
        {
            if (this == compareTo)
            {
                return 0;
            }

            var rc = DateTime.Compare(this.TaxDate, compareTo.TaxDate);
            if (rc == 0)
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

            return rc;
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
                return this.MyMoney.Categories.SortedCategories;
            }
        }

        [XmlIgnore]
        [IgnoreDataMember]
        public TransactionExtra Extra { get; internal set; }

        [XmlIgnore]
        [IgnoreDataMember]
        public DateTime TaxDate => this.Extra != null && this.Extra.TaxDate.HasValue ? this.Extra.TaxDate.Value : this.Date;

        public void UpdateCategoriesView()
        {
            this.RaisePropertyChanged("Categories");
        }

        #endregion
    }

    /// <summary>
    /// This class represents a one time (load time only) map of which accounts 
    /// contain which payees.  This helps improve the performance
    /// of auto-categorization since we can limit which accounts need to be searched.
    /// </summary>
    public class PayeeIndex
    {
        private readonly MyMoney money;
        private readonly Dictionary<string, HashSet<Account>> map = new Dictionary<string, HashSet<Account>>();

        public PayeeIndex(MyMoney money)
        {
            this.money = money;
        }

        internal IEnumerable<Account> FindAccountsRelatedToPayee(string payeeOrTransferCaption)
        {
            if (this.map.TryGetValue(payeeOrTransferCaption, out var set))
            {
                foreach (var a in set)
                {
                    if (this.money.Accounts.Contains(a)) // make sure index is ok
                    {
                        yield return a;
                    }
                }
            }
        }

        internal void Reload()
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.Model, MeasurementId.Indexing))
            {
#endif
            // now we can build the payee index, only need to do non-closed accounts
            // since we don't care about old stale data in this index.
            foreach (var t in this.money.Transactions)
            {
                var account = t.Account;
                if (account != null && !account.IsClosed)
                {
                    if (t.IsSplit)
                    {
                        foreach (var s in t.Splits)
                        {
                            string cap = s.PayeeOrTransferCaption;
                            if (!string.IsNullOrEmpty(cap))
                            {
                                this.GetOrCreate(cap, account);
                                continue;
                            }
                        }
                    }

                    var caption = t.PayeeOrTransferCaption;
                    if (!string.IsNullOrEmpty(caption))
                    {
                        this.GetOrCreate(caption, account);
                    }
                }
            }
#if PerformanceBlocks
            }
#endif
        }

        public void GetOrCreate(string payeeOrTransferCaption, Account owner)
        {
            HashSet<Account> bucket = null;
            if (!this.map.TryGetValue(payeeOrTransferCaption, out bucket))
            {
                bucket = new HashSet<Account>();
                this.map[payeeOrTransferCaption] = bucket;
            }
            bucket.Add(owner);
        }
    }

    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class Splits : PersistentContainer, ICollection<Split>
    {
        private Transaction transaction;
        private int nextSplit;
        private Hashtable<int, Split> splits;
        private decimal unassigned;
        private decimal? amountMinusSalesTax;

        public Splits()
        { // for serialization.
        }

        public Splits(Transaction t, PersistentObject parent)
            : base(parent)
        {
            this.transaction = t;
        }

        public override bool FireChangeEvent(object sender, object item, string name, ChangeType type)
        {
            if (!base.FireChangeEvent(sender, item, name, type))
            {
                if (sender is Split)
                {
                    this.Rebalance();
                }

                if (this.transaction != null && this.transaction.Parent != null)
                {
                    this.transaction.Parent.FireChangeEvent(sender, item, name, type);
                }

                return false;
            }
            return true;
        }

        public override void EndUpdate()
        {
            base.EndUpdate();
            if (!this.IsUpdating)
            {
                this.Rebalance();
            }
        }

        [XmlIgnore]
        public Transaction Transaction
        {
            get { return this.transaction; }
            set { this.transaction = value; }
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
                if (this.splits != null)
                {
                    foreach (var de in this.splits)
                    {
                        Split s = de.Value;
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

        private bool balancing;

        public decimal Rebalance()
        {
            if (!this.balancing)
            {
                try
                {
                    this.balancing = true;
                    // calculate unassigned balance and display in status bar.
                    decimal total = 0;
                    if (this.splits != null)
                    {
                        foreach (Split s in this.splits.Values)
                        {
                            if (s.IsDeleted)
                            {
                                continue;
                            }

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
                        this.SplitsBalanceMessage = "Unassigned amount " + this.unassigned.ToString();
                    }
                    else
                    {
                        this.SplitsBalanceMessage = "";
                    }
                }
                finally
                {
                    this.balancing = false;
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
                    return this.Rebalance();
                }
                return this.transaction.Amount;
            }
        }

        public bool HasUnassigned
        {
            get { return this.unassigned != 0; }
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
                return ((List<Split>)this.GetSplits()).ToArray();
            }
            set
            {
                if (value != null)
                {
                    foreach (Split s in value)
                    {
                        s.Id = this.nextSplit++;
                        this.AddSplit(s);
                    }
                }

                this.FireChangeEvent(this, this, "TotalSplitAmount", ChangeType.Changed);
                this.FireChangeEvent(this, this, "Count", ChangeType.Changed);
            }
        }

        public int IndexOf(Split s)
        {
            if (this.splits != null)
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

        private string message;

        public string SplitsBalanceMessage
        {
            get { return this.message; }
            set
            {
                if (this.message != value)
                {
                    this.message = value;
                    this.FireChangeEvent(this, this, "SplitsBalanceMessage", ChangeType.Changed);
                }
            }
        }

        private SplitsObservableCollection theSplits;

        public ObservableCollection<Split> ObservableCollection
        {
            get
            {
                if (this.Transaction.IsSplit == false)
                {
                    // there is no need for an observable Collection since this is not a Split transaction
                    return null;
                }

                if (this.theSplits == null && this.Parent != null)
                {
                    this.theSplits = new SplitsObservableCollection(this);
                }

                return this.theSplits;
            }
        }

        private class SplitsObservableCollection : ThreadSafeObservableCollection<Split>
        {
            private readonly Splits parent;

            public SplitsObservableCollection(Splits splits)
            {
                this.parent = splits;

                foreach (Split s in splits.GetSplits())
                {
                    this.Add(s);
                }
                this.parent.Changed += new EventHandler<ChangeEventArgs>(this.OnParentChanged);
                this.Rebalance();
            }

            private void OnParentChanged(object sender, ChangeEventArgs args)
            {
                this.Rebalance();
            }

            private MyMoney MyMoney
            {
                get
                {
                    Transaction t = this.parent.Parent as Transaction;
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
                    s.Transaction = this.parent.Transaction;

                    // Append the collection of Split for this transaction with the new split created by the dataGrid
                    MyMoney money = this.MyMoney;
                    money.BeginUpdate(this);
                    s.Transaction.Splits.AddSplit(s);
                    this.Rebalance();
                    money.EndUpdate();

                    this.parent.FireChangeEvent(this, this, "Count", ChangeType.Changed);
                }
            }

            protected override void RemoveItem(int index)
            {
                Split s = this[index];
                MyMoney money = this.MyMoney;
                money.BeginUpdate(this);
                base.RemoveItem(index);
                ((Splits)s.Parent).RemoveSplit(s);
                this.Rebalance();
                money.EndUpdate();
                this.parent.FireChangeEvent(this, this, "Count", ChangeType.Changed);
            }

            private void Rebalance()
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
            if (this.theSplits != null)
            {
                this.theSplits.Clear();
            }
            this.FireChangeEvent(this, this, "Count", ChangeType.Changed);
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
            {
                this.splits = new Hashtable<int, Split>();
            }

            if (s.Id == -1)
            {
                s.Id = this.nextSplit++;
                s.OnInserted();
            }
            else if (s.Id >= this.nextSplit)
            {
                this.nextSplit = s.Id + 1;
            }
            this.splits[s.Id] = s;
            if (this.theSplits != null && !this.theSplits.Contains(s))
            {
                if (index > 0 && index < this.theSplits.Count)
                {
                    this.theSplits.Insert(index, s);
                }
                else
                {
                    this.theSplits.Add(s);
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
            int len = this.theSplits != null ? this.theSplits.Count : 0;
            this.Insert(len, s);
        }

        public Split AddSplit()
        {
            Split s = this.NewSplit();
            this.AddSplit(s);
            return s;
        }
        public Split AddSplit(int id)
        {
            Split s = new Split(this);
            s.Id = id;
            if (id >= this.nextSplit)
            {
                this.nextSplit = id + 1;
            }

            this.AddSplit(s);
            return s;
        }

        public Split FindSplit(int id)
        {
            if (this.splits == null)
            {
                return null;
            }

            return this.splits[id];
        }


        public Split FindSplitContainingCategory(Category c)
        {
            if (this.splits == null)
            {
                return null;
            }

            foreach (Split s in this.GetSplits())
            {
                if (c.Contains(s.Category))
                {
                    return s;
                }
            }
            return null;
        }

        public bool RemoveSplit(Split s, bool forceRemoveAfterSave = false)
        {
            if (this.splits == null)
            {
                return false;
            }

            s.Transfer = null; // remove the transfer.
            if (s.IsInserted || forceRemoveAfterSave)
            {
                // then we can remove it immedately.
                if (this.splits.ContainsKey(s.Id))
                {
                    this.splits.Remove(s.Id);
                    this.FireChangeEvent(this, s, "IsDeleted", ChangeType.Deleted);
                }
            }

            // mark it for removal on next save.
            s.OnDelete();
            if (this.theSplits != null && this.theSplits.Contains(s))
            {
                this.theSplits.Remove(s);
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
            if (this.splits == null)
            {
                return list;
            }

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
            DataContractSerializer xs = new DataContractSerializer(typeof(Splits), MyMoney.GetKnownTypes());
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
            this.RemoveAll();
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
            this.Add((Split)child);
        }

        public override void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false)
        {
            this.RemoveSplit((Split)pe, forceRemoveAfterSave);
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
            return this.GetEnumerator();
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
        private Transaction transaction;
        private int id = -1;
        private Category category;
        private Payee payee;
        internal decimal amount; // so we can keep transfer's in sync
        private Transfer transfer;
        private string memo;
        internal Account to; // for debugging only
        private string pendingTransfer;
        private SplitFlags flags;
        private DateTime? budgetBalanceDate;

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
            set { if (this.transaction != value) { this.transaction = value; this.OnChanged("Transaction"); } }
        }

        [XmlAttribute]
        [DataMember]
        [ColumnMapping(ColumnName = "Id")]
        public int Id
        {
            get { return this.id; }
            set { if (this.id != value) { this.id = value; this.OnChanged("Id"); } }
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
                    this.OnChanged("Category");
                }
            }
        }

        private string categoryName;
        // for serialization;
        [DataMember]
        public string CategoryName
        {
            get { return this.category != null ? this.category.Name : this.categoryName; }
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
                    this.payee = value; this.OnChanged("Payee");
                    this.OnChanged("PayeeOrTransferCaption");
                }
            }
        }

        private string payeeName;
        // for serialization
        [DataMember]
        public string PayeeName
        {
            get { return this.payee != null ? this.payee.Name : this.payeeName; }
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
                    this.InternalSetAmount(value);
                }
            }
        }

        // Set the amount without checking for transfers.
        internal void InternalSetAmount(decimal value)
        {
            this.OnAmountChanged(this.amount, value);
            this.amount = value;
            this.OnChanged("Amount");
        }

        private long transferId = -1;

        [DataMember]
        public long TransferId
        {
            get { return this.transferId; }
            set { this.transferId = value; }
        }

        [DataMember]
        public string TransferTo
        {
            get
            {
                if (this.pendingTransfer != null)
                {
                    return this.pendingTransfer;
                }

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
                    this.transferId = (value == null) ? -1 : this.transfer.Transaction.Id;
                    this.OnChanged("Transfer");
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
            set { if (this.memo != value) { this.memo = Truncate(value, 255); this.OnChanged("Memo"); } }
        }

        [XmlIgnore]
        public SqlDecimal Credit
        {
            get { return (this.Amount > 0) ? new SqlDecimal(this.Amount) : SqlDecimal.Null; }
            set { if (!value.IsNull) { this.Amount = (decimal)value; } this.OnChanged("Debit"); this.OnChanged("Credit"); }
        }

        [XmlIgnore]
        public SqlDecimal Debit
        {
            get { return (this.Amount <= 0) ? new SqlDecimal(-this.Amount) : SqlDecimal.Null; }
            set { if (!value.IsNull) { this.Amount = -value.Value; } this.OnChanged("Debit"); this.OnChanged("Credit"); }
        }

        [XmlIgnore]
        public bool Unaccepted
        {
            get { return this.transaction.Unaccepted; }
        }


        [IgnoreDataMember]
        public bool IsBudgeted
        {
            get { return (this.flags & SplitFlags.Budgeted) != 0; }
            set { this.SetBudgeted(value, null); }
        }

        public void SetBudgeted(bool value, List<TransactionException> errors)
        {
            if (this.IsBudgeted != value)
            {
                if (value)
                {
                    this.UpdateBudget(true, errors);
                    this.SetFlag(SplitFlags.Budgeted);
                }
                else
                {
                    this.UpdateBudget(false, errors);
                    this.ClearFlag(SplitFlags.Budgeted);
                    this.BudgetBalanceDate = null;
                }
                this.OnChanged("IsBudgeted");
            }
        }

        private void SetFlag(SplitFlags flag)
        {
            this.Flags |= flag;
        }

        private void ClearFlag(SplitFlags flag)
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
                    this.OnChanged("Flags");
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
                    this.OnChanged("BudgetBalanceDate");
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
            this.flags &= ~SplitFlags.Budgeted;

            if (this.payeeName != null)
            {
                this.Payee = money.Payees.FindPayee(this.payeeName, true);
                this.payeeName = null;
            }
            if (this.CategoryName != null)
            {
                this.Category = money.Categories.GetOrCreateCategory(this.CategoryName, CategoryType.None);
                this.CategoryName = null;
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
            else if (this.TransferId != -1 && this.Transfer == null)
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

            if (this.payee == null)
            {
                return string.Empty;
            }

            return this.Payee != null ? this.payee.Name : "";
        }

        [XmlIgnore]
        public string PayeeOrTransferCaption
        {
            get { return this.GetPayeeOrTransferCaption(); }
            set
            {
                if (this.PayeeOrTransferCaption != value)
                {
                    MyMoney money = this.MyMoney;

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
        private MyMoney MyMoney
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

        private void OnAmountChanged(decimal oldValue, decimal newValue)
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
                return new List<Category>(this.MyMoney.Categories.SortedCategories);
            }
        }

        public void UpdateCategoriesView()
        {
            this.RaisePropertyChanged("Categories");
        }

        #endregion

    }

    internal class SplitIdComparer : IComparer<Split>
    {
        public int Compare(Split a, Split b)
        {
            if (a == null && b != null)
            {
                return -1;
            }

            if (a != null && b == null)
            {
                return 1;
            }

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
        private long id;
        private Security security;
        private decimal unitprice;
        private decimal units;
        private decimal commission;
        private InvestmentType type = InvestmentType.None;
        private InvestmentTradeType tradeType = InvestmentTradeType.None;
        private bool taxExempt;
        private decimal withholding;
        private decimal markupdown;
        private decimal taxes;
        private decimal fees;
        private decimal load;

        // post stock split
        private decimal currentUnits;
        private decimal currentUnitPrice;
        private Transaction transaction;

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
            get { return this.id; }
            set { this.id = value; }
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
                    this.security = value; this.OnChanged("Security");
                }
            }
        }

        private string securityName; // used for serialization only.

        [DataMember]
        public string SecurityName
        {
            get { return this.security != null ? this.security.Name : this.securityName; }
            set { this.securityName = value; }
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
                    this.security.Price = value; this.OnChanged("Price");
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
            set { if (this.unitprice != value) { this.unitprice = value; this.OnChanged("UnitPrice"); } }
        }

        /// <summary>
        /// For example, how many shares did you buy
        /// </summary>
        [DataMember]
        [ColumnMapping(ColumnName = "Units", OldColumnName = "Quantity", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Units
        {
            get { return this.units; }
            set { if (this.units != value) { this.units = value; this.OnChanged("Units"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Commission", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Commission
        {
            get { return this.commission; }
            set { if (this.commission != value) { this.commission = value; this.OnChanged("Commission"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "MarkUpDown", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal MarkUpDown
        {
            get { return this.markupdown; }
            set { if (this.markupdown != value) { this.markupdown = value; this.OnChanged("MarkUpDown"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Taxes", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Taxes
        {
            get { return this.taxes; }
            set { if (this.taxes != value) { this.taxes = value; this.OnChanged("Taxes"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Fees", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Fees
        {
            get { return this.fees; }
            set { if (this.fees != value) { this.fees = value; this.OnChanged("Fees"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Load", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Load
        {
            get { return this.load; }
            set { if (this.load != value) { this.load = value; this.OnChanged("Load"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "InvestmentType", SqlType = typeof(SqlInt32))]
        public InvestmentType Type
        {
            get { return this.security == null ? InvestmentType.None : this.type; }
            set { if (this.type != value) { this.type = value; this.OnChanged("InvestmentType"); } }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "TradeType", SqlType = typeof(SqlInt32), AllowNulls = true)]
        public InvestmentTradeType TradeType
        {
            get { return this.tradeType; }
            set { if (this.tradeType != value) { this.tradeType = value; this.OnChanged("TradeType"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "TaxExempt", SqlType = typeof(SqlBoolean), AllowNulls = true)]
        public bool TaxExempt
        {
            get { return this.taxExempt; }
            set { if (this.taxExempt != value) { this.taxExempt = value; this.OnChanged("TaxExempt"); } }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Withholding", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Withholding
        {
            get { return this.withholding; }
            set { if (this.withholding != value) { this.withholding = value; this.OnChanged("Withholding"); } }
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
            get { return this.currentUnitPrice; }
            set { this.currentUnitPrice = value; }
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
                return this.CurrentUnitPrice * this.CurrentUnits;
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
                return factor * this.CurrentUnits * this.Security.Price;
            }
        }

        [XmlIgnore]
        public decimal GainLoss { get { return this.MarketValue - this.CostBasis; } }

        [XmlIgnore]
        public decimal PercentGainLoss { get { return this.CostBasis == 0 ? 0 : this.GainLoss * 100 / this.CostBasis; } }

        [XmlIgnore]
        public bool IsDown { get { return this.GainLoss < 0; } }

        public void ApplySplit(StockSplit s)
        {
            if (s.Date > this.Date && s.Denominator != 0 && s.Numerator != 0)
            {
                this.currentUnits = this.currentUnits * s.Numerator / s.Denominator;
                this.currentUnitPrice = this.currentUnitPrice * s.Denominator / s.Numerator;
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
    /// It is a cache of the subset of slits in Money.Splits that belong to a given Security and
    /// this cache is automatically updated so it remains up to date.
    /// </summary>
    public class ObservableStockSplits : ThreadSafeObservableCollection<StockSplit>
    {
        private readonly Security security;
        private readonly StockSplits splits;
        private bool initializing;
        private bool syncSync;

        public ObservableStockSplits(Security security, MyMoney money)
        {
            this.initializing = true;
            this.security = security;
            this.splits = money.StockSplits;
            int index = 0;
            foreach (StockSplit s in this.splits.GetStockSplitsForSecurity(security))
            {
                this.Insert(index++, s);
            }
            this.initializing = false;
        }

        internal void OnSecurityChanged(Security s)
        {
            if (!this.syncSync && s == this.security)
            {
                this.SyncSplits();
            }
        }

        internal void OnStockSplitChanged(StockSplit ss)
        {
            if (!this.syncSync && ss.Security == this.security)
            {
                this.SyncSplits();
            }
        }


        public Security Security { get { return this.security; } }

        private void SyncSplits()
        {
            this.initializing = true;
            HashSet<StockSplit> active = new HashSet<StockSplit>();
            foreach (StockSplit s in this.splits.GetStockSplitsForSecurity(this.security))
            {
                int index = 0;
                bool found = false;
                foreach (var existing in this)
                {
                    if (existing == s)
                    {
                        // we got it.
                        active.Add(existing);
                        found = true;
                        break;
                    }
                    else if (s.Date < existing.Date)
                    {
                        if (this.Contains(s))
                        {
                            // date order has changed.
                            this.RemoveItem(this.IndexOf(s));
                        }
                        this.InsertItem(index, s);
                        active.Add(s);
                        found = true;
                        break;
                    }
                    index++;
                }
                if (!found)
                {
                    this.InsertItem(this.Count, s);
                    active.Add(s);
                }
            }

            List<StockSplit> toRemove = new List<StockSplit>();

            foreach (var existing in this)
            {
                if (!active.Contains(existing))
                {
                    toRemove.Add(existing);
                }
            }

            foreach (var split in toRemove)
            {
                this.RemoveItem(this.IndexOf(split));
            }

            this.initializing = false;
        }

        protected override void InsertItem(int index, StockSplit item)
        {
            base.InsertItem(index, item);
            if (!this.initializing)
            {
                this.syncSync = true;
                try
                {
                    this.splits.AddStockSplit(item);
                    item.Security = this.security;
                }
                finally
                {
                    this.syncSync = false;
                }
            }
        }

        protected override void RemoveItem(int index)
        {
            StockSplit s = this[index];
            base.RemoveItem(index);
            if (!this.initializing)
            {
                this.syncSync = true;
                try
                {
                    this.splits.RemoveStockSplit(s);
                }
                finally
                {
                    this.syncSync = false;
                }
            }
        }

        protected override void ClearItems()
        {
            try
            {
                this.syncSync = true;
                foreach (StockSplit s in this)
                {
                    s.OnDelete();
                }
                base.ClearItems();
            }
            finally
            {
                this.syncSync = false;
            }
        }

        /// <summary>
        /// Cleanup any splits that looks really empty
        /// </summary>
        internal void RemoveEmptySplits()
        {
            try
            {
                this.syncSync = true;
                for (int i = this.Count - 1; i >= 0;)
                {
                    StockSplit s = this[i];
                    // only do this if the Split looks totally empty
                    if (s.Date == new DateTime() && s.Numerator == 0 && s.Denominator == 0)
                    {
                        this.splits.RemoveStockSplit(s);
                        this.RemoveItem(i);
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
            finally
            {
                this.syncSync = false;
            }
        }
    }

    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class StockSplits : PersistentContainer, ICollection<StockSplit>
    {
        private long nextStockSplit;
        private Hashtable<long, StockSplit> stockSplits = new Hashtable<long, StockSplit>();

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
            if (this.nextStockSplit <= id)
            {
                this.nextStockSplit = id + 1;
            }

            this.stockSplits[id] = s;
            this.FireChangeEvent(this, s, null, ChangeType.Inserted);
            return s;
        }

        public StockSplit AddStockSplit(StockSplit s)
        {
            s.Parent = this;
            if (s.Id == -1)
            {
                s.Id = this.nextStockSplit++;
                s.OnInserted();
            }
            else if (this.nextStockSplit <= s.Id)
            {
                this.nextStockSplit = s.Id + 1;
            }
            this.stockSplits[s.Id] = s;
            this.FireChangeEvent(this, s, null, ChangeType.Inserted);
            return s;
        }

        public StockSplit FindStockSplitById(long id)
        {
            return this.stockSplits[id];
        }

        public StockSplit FindStockSplitByDate(Security s, DateTime date)
        {
            return (from t in this.stockSplits.Values where t.Date == date && t.Security == s select t).FirstOrDefault();
        }

        // todo: there should be no references left at this point...
        public bool RemoveStockSplit(StockSplit s, bool forceRemoveAfterSave = false)
        {
            if (s.IsInserted || forceRemoveAfterSave)
            {
                // then we can remove it immediately.
                if (this.stockSplits.ContainsKey(s.Id))
                {
                    this.stockSplits.Remove(s.Id);
                    this.FireChangeEvent(this, s, "IsDeleted", ChangeType.Deleted);
                }
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
            list.Sort(new Comparison<StockSplit>((a, b) =>
            {
                return a.Date.CompareTo(b.Date);
            }));
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
            return this.stockSplits.ContainsKey(item.Id);
        }

        public void CopyTo(StockSplit[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { return this.stockSplits.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public override void Add(object child)
        {
            this.Add((StockSplit)child);
        }

        public override void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false)
        {
            this.RemoveStockSplit((StockSplit)pe, forceRemoveAfterSave);
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
            return this.GetEnumerator();
        }

        #endregion


    }

    [TableMapping(TableName = "StockSplits")]
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class StockSplit : PersistentObject
    {
        private long id = -1;
        private Security security;
        private DateTime date;

        // If numerator is less than denominator then it is a stock split, otherwise
        // it is a reverse stock split.
        private decimal numerator;
        private decimal denominator;

        public StockSplit()
        { // for serialization
        }

        public StockSplit(StockSplits container) : base(container) { }

        public override void OnChanged(string name)
        {
            base.OnChanged(name);
            // Here we have to be able to sync the ObservableStockSplits without using an event handler
            // otherwise the event handlers create memory leaks.
            if (this.security != null && this.security.HasObservableStockSplits)
            {
                this.security.StockSplits.OnStockSplitChanged(this);
            }
        }

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
                    this.id = value; this.OnChanged("Id");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Date")]
        public DateTime Date
        {
            get { return this.date; }
            set
            {
                if (this.date != value)
                {
                    this.date = value;
                    this.OnChanged("Date");
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
                    this.security = value; this.OnChanged("Security");
                }
            }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "Numerator", SqlType = typeof(SqlMoney))]
        public decimal Numerator
        {
            get { return this.numerator; }
            set
            {
                if (this.numerator != value)
                {
                    this.numerator = value;
                    this.OnChanged("Numerator");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Denominator", SqlType = typeof(SqlMoney))]
        public decimal Denominator
        {
            get { return this.denominator; }
            set
            {
                if (this.denominator != value)
                {
                    if (value == 0)
                    {
                        throw new ArgumentOutOfRangeException("Cannot set a zero denominator");
                    }
                    this.denominator = value;
                    this.OnChanged("Denominator");
                }
            }
        }
    }


    public class TransactionException : Exception
    {
        private readonly Transaction t;

        public TransactionException(Transaction t, string message)
            : base(message)
        {
            this.t = t;
        }

        public Transaction Transaction { get { return this.t; } }
    }

    internal class MoneyException : Exception
    {
        public MoneyException(string message)
            : base(message)
        {
        }

    }

}


