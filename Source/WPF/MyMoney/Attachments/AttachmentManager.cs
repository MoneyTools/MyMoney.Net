using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Windows;
using Walkabout.Data;
using Walkabout.Dialogs;
using Walkabout.Configuration;
using System.Diagnostics;
using System.IO;
using Walkabout.Utilities;
using System.Threading;

#if PerformanceBlocks
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
#endif

namespace Walkabout.Attachments
{
    /// <summary>
    /// This class manages the storage of attachments images on disk organized in directories matching the 
    /// bank account names and with file names whose name is the transaction number + attachment index + file extension.
    /// It also manages the deletion of attachments when transaction is deleted, and the movement of attachments
    /// if a transfer transaction is moved to a different account.
    /// </summary>
    public class AttachmentManager : IDisposable
    {
        private MyMoney myMoney;
        private AttachmentWatcher watcher;
        
        public AttachmentManager(MyMoney myMoney)
        {
            this.myMoney = myMoney;
            this.watcher = new AttachmentWatcher(myMoney);
        }

        public string AttachmentDirectory
        {
            get { return this.watcher.AttachmentDirectory; }
            set { this.watcher.AttachmentDirectory = value; }
        }

        public MyMoney MyMoney
        {
            get { return myMoney; }
            set { myMoney = value; }
        }

        public void Stop()
        {
            if (this.myMoney != null)
            {
                myMoney.Accounts.Changed -= new EventHandler<ChangeEventArgs>(OnAccountsChanged);
                myMoney.Transactions.Changed -= new EventHandler<ChangeEventArgs>(OnTransactionsChanged);
                myMoney.Changed -= new EventHandler<ChangeEventArgs>(OnTransactionsChanged);
                myMoney.BeforeTransferChanged -= new EventHandler<TransferChangedEventArgs>(OnBeforeTransferChanged);
                myMoney.BeforeSplitTransferChanged -= new EventHandler<SplitTransferChangedEventArgs>(OnBeforeSplitTransferChanged);
            }
            this.watcher.Stop();
        }

        public void Start()
        {
            this.watcher.Stop();
            if (myMoney != null)
            {
                // listen to transaction changed events so that we can cleanup attachments when transactions
                // are deleted.
                myMoney.Accounts.Changed += new EventHandler<ChangeEventArgs>(OnAccountsChanged);
                myMoney.Transactions.Changed += new EventHandler<ChangeEventArgs>(OnTransactionsChanged);
                myMoney.Changed += new EventHandler<ChangeEventArgs>(OnMoneyChanged);
                myMoney.BeforeTransferChanged += new EventHandler<TransferChangedEventArgs>(OnBeforeTransferChanged);
                myMoney.BeforeSplitTransferChanged += new EventHandler<SplitTransferChangedEventArgs>(OnBeforeSplitTransferChanged);
                this.watcher.ScanAllAccounts();
            }
        }

        /// <summary>
        /// Search attachments directory and update the "HasAttachments" flag on this transaction 
        /// if any attachments are found.
        /// </summary>
        /// <param name="t"></param>
        public void FindAttachments(Transaction t)
        {
            if (this.watcher != null)
            {
                bool hasAttachments = this.watcher.HasAttachments(this.AttachmentDirectory, t);
                t.HasAttachment = hasAttachments;
            }
        }

        void OnBeforeSplitTransferChanged(object sender, SplitTransferChangedEventArgs e)
        {
            Split split = e.Split;
            Transfer transfer = e.NewValue;

            if (split.Transfer != null)
            {
                if (transfer == null )
                {
                    // transfer is being deleted, try and consolidate attachments on this side.
                    MoveAttachments(split.Transfer.Transaction, split.Transaction);
                }
                else
                {
                    // transfer is being moved. So move attachments with it.
                    MoveAttachments(split.Transfer.Transaction, transfer.Transaction);
                }
            }
        }

        void OnBeforeTransferChanged(object sender, TransferChangedEventArgs e)
        {
            Transaction t = e.Transaction;
            Transfer tran = e.NewValue;
            if (t.Transfer != null)
            {
                if (tran == null)
                {
                    // transfer is being deleted, try and consolidate attachments on this side.
                    MoveAttachments(t.Transfer.Transaction, t);
                }
                else
                {
                    // transfer is being moved. So move attachments with it.
                    MoveAttachments(t.Transfer.Transaction, tran.Transaction);
                }
            }
        }

        void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
            while (args != null)
            {
                Transaction t = args.Item as Transaction;
                if (t != null)
                {
                    if (args.ChangeType == ChangeType.Deleted)
                    {
                        // transaction is being deleted, so delete attachments with it.
                        // todo: would be nice to warn the user they are losing them...
                        DeleteAttachments(t);
                    }
                    else if (args.ChangeType == ChangeType.Inserted)
                    {
                        if (watcher != null)
                        {
                            watcher.QueueTransaction(t);
                        }
                    }
                }
                else
                {
                    Account a = args.Item as Account;
                    if (args.ChangeType == ChangeType.Inserted)
                    {
                        if (watcher != null)
                        {
                            watcher.QueueAccount(a);
                        }
                    }
                }
                args = args.Next;
            }
            if (watcher != null)
            {
                watcher.StartQueued();
            }
        }

        void OnTransactionsChanged(object sender, ChangeEventArgs args)
        {
            while (args != null)
            {
                Transaction t = args.Item as Transaction;
                if (t != null)
                {
                    if (args.ChangeType == ChangeType.Deleted)
                    {
                        // transaction is being deleted, so delete attachments with it.
                        // todo: would be nice to warn the user they are losing them...
                        DeleteAttachments(t);
                    }
                    else if (args.ChangeType == ChangeType.Inserted)
                    {
                        watcher.QueueTransaction(t);
                    }
                }
                args = args.Next;
            }
        }

        void OnAccountsChanged(object sender, ChangeEventArgs args)
        {
            while (args != null)
            {
                Account a = args.Item as Account;
                if (a != null && args.ChangeType == ChangeType.Inserted)
                {
                    watcher.QueueAccount(a);
                }
                args = args.Next;
            }
        }

        /// <summary>
        /// Merge attachments from external location, checking for and eliminating any duplicates.
        /// </summary>
        /// <param name="t">Transaction to associate attachments with</param>
        /// <param name="attachments">The external attachments we are importing.</param>
        internal void ImportAttachments(Transaction t, List<string> attachments)
        {
            if (attachments.Count == 0)
            {
                return;
            }
            List<HashedFile> existing = new List<HashedFile>();
            if (t.HasAttachment)
            {
                foreach (string file in GetAttachments(t))
                {
                    existing.Add(new HashedFile(file));
                }
            }

            // now check the new files.
            foreach (string fileName in attachments)
            {
                HashedFile newFile = new HashedFile(fileName);
                bool found = false;
                foreach (HashedFile f in existing)
                {
                    if (f.HashEquals(newFile) && f.DeepEquals(newFile))
                    {
                        // is identical, so we can skip this one, it is already on our target transaction.
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    string newFileName = GetUniqueFileName(t, Path.GetExtension(fileName));
                    File.Copy(fileName, newFileName);
                }
            }
            t.HasAttachment = true;
        }

        public void MoveAttachments(Transaction fromTransaction, Transaction toTransaction)
        {            
            foreach (string fileName in GetAttachments(fromTransaction))
            {
                toTransaction.HasAttachment = true;
                string newFileName = GetUniqueFileName(toTransaction, Path.GetExtension(fileName));
                try
                {
                    File.Move(fileName, newFileName);
                }
                catch
                {
                    // perhaps the old file is locked?
                    File.Copy(fileName, newFileName);
                    // delete it later.
                    TempFilesManager.AddTempFile(fileName);
                }
                TempFilesManager.RemoveTempFile(newFileName);
            }
            fromTransaction.HasAttachment = false;
        }

        private void DeleteAttachments(Transaction t)
        {
            if (t.Account == null || t.Account.IsCategoryFund)
            {
                return;
            }
            foreach (string fileName in GetAttachments(t))
            {
                TempFilesManager.DeleteFile(fileName);
            }
            t.HasAttachment = false;
        }        

        public List<string> GetAttachments(Transaction t)
        {
            return watcher.GetAttachments(this.AttachmentDirectory, t);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "AttachmentDirectory")]
        public string GetUniqueFileName(Transaction t, string extension)
        {            
            string path = this.AttachmentDirectory;   
            if (string.IsNullOrEmpty(path)) 
            {
                throw new InvalidOperationException("The field 'AttachmentDirectory' is not initialized");
            }
            string dir = Path.Combine(path, NativeMethods.GetValidFileName(t.Account.Name));
            Directory.CreateDirectory(dir);

            int index = 0;
            while (true)
            {
                string fileName = (index == 0) ?
                    Path.Combine(dir, t.Id + extension) :
                    Path.Combine(dir, t.Id + "." + index + extension);
                if (!File.Exists(fileName))
                {
                    return fileName;
                }
                index++;
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
                Stop();
            }
        }
    }

    class AttachmentWatcher 
    {
        private ConcurrentQueue<Account> accountQueue;
        private ConcurrentQueue<Transaction> transactionQueue;
        private bool threadRunning;
        private MyMoney money;
        private AutoResetEvent threadStopEvent = new AutoResetEvent(false);

        internal string AttachmentDirectory { get; set; }

        public AttachmentWatcher(MyMoney money)
        {
            this.accountQueue = new ConcurrentQueue<Account>();
            this.transactionQueue = new ConcurrentQueue<Transaction>();
            this.money = money;  
        }

        internal void Stop()
        {
            if (threadRunning)
            {
                threadRunning = false;
                threadStopEvent.WaitOne(5000);
            }
        }

        internal void QueueTransaction(Transaction t)
        {
            if (t != null && !transactionQueue.Contains(t))
            {
                transactionQueue.Enqueue(t);
            }
        }

        internal void QueueAccount(Account a)
        {
            if (a != null && !accountQueue.Contains(a))
            {
                accountQueue.Enqueue(a);
            }
        }

        public void ScanAllAccounts()
        {
            foreach (Account a in money.Accounts)
            {
                QueueAccount(a);
            }
            StartQueued();
        }

        public void StartQueued()
        {
            if (accountQueue.Count > 0 || transactionQueue.Count > 0)
            {
                StartThread();
            }
        }

        void StartThread()
        {
            if (!threadRunning)
            {
                threadRunning = true;
                threadStopEvent.Reset();
                ThreadPool.QueueUserWorkItem(new WaitCallback(ScanDirectory));
            }
        }

        /// <summary>
        /// This runs on a background thread and finds all attachments and updates the HasAttachment
        /// flag on all transactions.
        /// </summary>
        /// <param name="state"></param>
        public void ScanDirectory(object state)
        {
            // set of transactions that have attachments.
            Thread.Sleep(1000); // give app time to startup...
            
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.Model, MeasurementId.ScanAttachments))
            {
#endif
                try
                {
                    string path = this.AttachmentDirectory;
                    
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && this.money != null)
                    {
                        // process pending account checks
                        Account a;
                        while (accountQueue.TryDequeue(out a) && threadRunning)
                        {
                            FindAttachments(path, a);
                        }

                        // process pending individual transaction checks.
                        List<Tuple<Transaction, bool>> toUpdate = new List<Tuple<Transaction, bool>>();
                        Transaction t;
                        while (transactionQueue.TryDequeue(out t) && threadRunning)
                        {
                            bool yes = HasAttachments(path, t);
                            if (t.HasAttachment != yes)
                            {
                                toUpdate.Add(new Tuple<Transaction, bool>(t, yes));
                            }
                        }

                        // Updating Money transactions has to happen on the UI thread.
                        UiDispatcher.BeginInvoke(new Action(() =>
                        {
                            BatchUpdate(toUpdate);
                        }));
                    }
                }
                catch
                {
                }
#if PerformanceBlocks
            }
#endif
            threadRunning = false;
            threadStopEvent.Set();
        }

        private void BatchUpdate(List<Tuple<Transaction, bool>> toUpdate)
        {
            this.money.BeginUpdate(this);
            try
            {
                foreach(var pair in toUpdate)
                {
                    pair.Item1.HasAttachment = pair.Item2;
                }
            }
            finally
            {
                this.money.EndUpdate();
            }
        }

        private void FindAttachments(string path, Account a)
        {
            if (a.IsCategoryFund)
            {
                return;
            }
            HashSet<Transaction> set = new HashSet<Transaction>();

            string accountDirectory = Path.Combine(path, NativeMethods.GetValidFileName(a.Name));
            if (!string.IsNullOrEmpty(accountDirectory) && Directory.Exists(accountDirectory))
            {
                foreach (string fileName in Directory.GetFiles(accountDirectory, "*.*"))
                {
                    if (!threadRunning)
                    {
                        return;
                    }
                    string name = Path.GetFileName(fileName);
                    int i = name.IndexOf(".");
                    if (i > 0)
                    {
                        string s = name.Substring(0, i);
                        long id = 0;
                        if (long.TryParse(s, out id))
                        {
                            Transaction t = this.money.Transactions.FindTransactionById(id);
                            if (t != null)
                            {
                                set.Add(t);
                            }
                        }
                    }
                }
            }

            // Updating Money transactions has to happen on the UI thread.
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    this.money.BeginUpdate(this);
                    foreach (Transaction t in this.money.Transactions.GetTransactionsFrom(a))
                    {
                        if (!threadRunning)
                        {
                            return;
                        }
                        if (t.HasAttachment && !set.Contains(t))
                        {
                            t.HasAttachment = false;
                        }
                        if (!t.HasAttachment && set.Contains(t))
                        {
                            t.HasAttachment = true;
                        }
                    }
                }
                finally
                {
                    this.money.EndUpdate();
                }
            }));
        }

        /// <summary>
        /// Search attachments directory and update the "HasAttachments" flag on this transaction 
        /// if any attachments are found.
        /// </summary>
        /// <param name="path">The attachment directory</param>
        /// <param name="t">The transaction to check for</param>
        public bool HasAttachments(string path, Transaction t)
        {
            bool hasAttachments = false;
            if (t != null && !string.IsNullOrEmpty(path) && t.Account != null)
            {
                string accountDir = System.IO.Path.Combine(path, NativeMethods.GetValidFileName(t.Account.Name));
                if (System.IO.Directory.Exists(accountDir))
                {
                    var files = Directory.GetFiles(accountDir, t.Id + "*.*");
                    if (files.Length > 0)
                    {
                        hasAttachments = true;
                    }
                }
            }
            return hasAttachments;
        }

        /// <summary>
        /// Return list of attachments for given transaction
        /// </summary>
        /// <param name="path">The attachment directory</param>
        /// <param name="t">The transaction to check for</param>
        public List<string> GetAttachments(string path, Transaction t)
        {
            List<string> files = new List<string>();
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && t.Account != null && !string.IsNullOrEmpty(t.Account.Name))
            {
                string accountDirectory = Path.Combine(path, NativeMethods.GetValidFileName(t.Account.Name));
                if (Directory.Exists(accountDirectory))
                {
                    files.AddRange(Directory.GetFiles(accountDirectory, t.Id + "*.*"));                    
                }
            }
            return files;
        }

    }

}
