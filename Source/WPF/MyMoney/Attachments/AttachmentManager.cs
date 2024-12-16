﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Walkabout.Data;
using Walkabout.Utilities;

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
        private readonly MyMoney myMoney;
        private readonly AttachmentWatcher watcher;
        private readonly Dictionary<Account, string> nameMap;

        public AttachmentManager(MyMoney myMoney)
        {
            this.myMoney = myMoney;
            this.watcher = new AttachmentWatcher(myMoney);
            // save the original account names, in case the account is renamed.
            this.nameMap = new Dictionary<Account, string>();
            foreach (var item in myMoney.Accounts.GetAccounts())
            {
                this.nameMap[item] = item.Name;
            }
        }

        public string AttachmentDirectory
        {
            get { return this.watcher.AttachmentDirectory; }
            set { this.watcher.AttachmentDirectory = value; }
        }

        public string SetupAttachmentDirectory(string databasePath)
        {
            string localName = Path.GetFileNameWithoutExtension(databasePath) + ".Attachments";
            string dir = Path.GetDirectoryName(databasePath);
            string attachmentpath = Path.Combine(dir, localName);
            Directory.CreateDirectory(attachmentpath);
            this.AttachmentDirectory = attachmentpath;
            return attachmentpath;
        }

        public void Stop()
        {
            if (this.myMoney != null)
            {
                this.myMoney.Accounts.Changed -= new EventHandler<ChangeEventArgs>(this.OnAccountsChanged);
                this.myMoney.Transactions.Changed -= new EventHandler<ChangeEventArgs>(this.OnTransactionsChanged);
                this.myMoney.Changed -= new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);
                this.myMoney.BeforeTransferChanged -= new EventHandler<TransferChangedEventArgs>(this.OnBeforeTransferChanged);
                this.myMoney.BeforeSplitTransferChanged -= new EventHandler<SplitTransferChangedEventArgs>(this.OnBeforeSplitTransferChanged);
            }
            this.watcher.Stop();
        }

        public void Start()
        {
            this.Stop();
            if (this.myMoney != null)
            {
                // listen to transaction changed events so that we can cleanup attachments when transactions
                // are deleted.
                this.myMoney.Accounts.Changed += new EventHandler<ChangeEventArgs>(this.OnAccountsChanged);
                this.myMoney.Transactions.Changed += new EventHandler<ChangeEventArgs>(this.OnTransactionsChanged);
                this.myMoney.Changed += new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);
                this.myMoney.BeforeTransferChanged += new EventHandler<TransferChangedEventArgs>(this.OnBeforeTransferChanged);
                this.myMoney.BeforeSplitTransferChanged += new EventHandler<SplitTransferChangedEventArgs>(this.OnBeforeSplitTransferChanged);
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

        private void OnBeforeSplitTransferChanged(object sender, SplitTransferChangedEventArgs e)
        {
            Split split = e.Split;
            Transfer transfer = e.NewValue;

            if (split.Transfer != null)
            {
                if (transfer == null)
                {
                    // transfer is being deleted, try and consolidate attachments on this side.
                    this.MoveAttachments(split.Transfer.Transaction, split.Transaction);
                }
                else
                {
                    // transfer is being moved. So move attachments with it.
                    this.MoveAttachments(split.Transfer.Transaction, transfer.Transaction);
                }
            }
        }

        private void OnBeforeTransferChanged(object sender, TransferChangedEventArgs e)
        {
            Transaction t = e.Transaction;
            Transfer tran = e.NewValue;
            if (t.Transfer != null)
            {
                if (tran == null)
                {
                    // transfer is being deleted, try and consolidate attachments on this side.
                    this.MoveAttachments(t.Transfer.Transaction, t);
                }
                else
                {
                    // transfer is being moved. So move attachments with it.
                    this.MoveAttachments(t.Transfer.Transaction, tran.Transaction);
                }
            }
        }

        private void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
            while (args != null)
            {
                if (args.Item is Transaction t)
                {
                    if (args.ChangeType == ChangeType.Deleted)
                    {
                        // transaction is being deleted, so delete attachments with it.
                        // todo: would be nice to warn the user they are losing them...
                        this.DeleteAttachments(t);
                    }
                    else if (args.ChangeType == ChangeType.Inserted)
                    {
                        if (this.watcher != null)
                        {
                            this.watcher.QueueTransaction(t);
                        }
                    }
                }
                else if (args.Item is Account a)
                {
                    if (args.ChangeType == ChangeType.Inserted)
                    {
                        if (this.watcher != null)
                        {
                            this.watcher.QueueAccount(a);
                        }
                    }
                    else if (args.ChangeType == ChangeType.Changed && args.Name == "Name")
                    {
                        this.OnAccountRenamed(a);
                    }
                }
                args = args.Next;
            }
            if (this.watcher != null)
            {
                this.watcher.StartQueued();
            }
        }

        private void OnTransactionsChanged(object sender, ChangeEventArgs args)
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
                        this.DeleteAttachments(t);
                    }
                    else if (args.ChangeType == ChangeType.Inserted)
                    {
                        this.watcher.QueueTransaction(t);
                    }
                }
                args = args.Next;
            }
        }

        private void OnAccountsChanged(object sender, ChangeEventArgs args)
        {
            while (args != null)
            {
                Account a = args.Item as Account;
                if (a != null)
                {
                    if (args.ChangeType == ChangeType.Inserted)
                    {
                        this.watcher.QueueAccount(a);
                    }
                    else if (args.ChangeType == ChangeType.Changed && args.Name == "Name")
                    {
                        this.OnAccountRenamed(a);
                    }
                }
                args = args.Next;
            }
        }

        private void OnAccountRenamed(Account a)
        {
            if (this.nameMap.TryGetValue(a, out string oldName))
            {
                if (oldName != a.Name)
                {
                    this.watcher.OnRenameAccount(this.AttachmentDirectory, a, oldName);
                    this.nameMap[a] = a.Name;
                }
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
                foreach (string file in this.GetAttachments(t))
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
                    string newFileName = this.GetUniqueFileName(t, Path.GetExtension(fileName));
                    File.Copy(fileName, newFileName);
                }
            }
            t.HasAttachment = true;
        }

        public void MoveAttachments(Transaction fromTransaction, Transaction toTransaction)
        {
            foreach (string fileName in this.GetAttachments(fromTransaction))
            {
                toTransaction.HasAttachment = true;
                string newFileName = this.GetUniqueFileName(toTransaction, Path.GetExtension(fileName));
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
            }
            fromTransaction.HasAttachment = false;
        }

        public void MoveAttachments(Transaction transaction, Account newAccount)
        {
            foreach (string fileName in this.GetAttachments(transaction))
            {
                string newFileName = this.GetUniqueFileName(transaction, newAccount, Path.GetExtension(fileName));
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
            }
        }

        private void DeleteAttachments(Transaction t)
        {
            foreach (string fileName in this.GetAttachments(t))
            {
                TempFilesManager.DeleteFile(fileName);
            }
            t.HasAttachment = false;
        }

        public List<string> GetAttachments(Transaction t)
        {
            return this.watcher.GetAttachments(this.AttachmentDirectory, t);
        }

        public string GetUniqueFileName(Transaction t, string extension)
        {
            return this.GetUniqueFileName(t, t.Account, extension);
        }

        public string GetUniqueFileName(Transaction t, Account newAccount, string extension)
        {
            string path = this.AttachmentDirectory;
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException("The field 'AttachmentDirectory' is not initialized");
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string dir = Path.Combine(path, NativeMethods.GetValidFileName(newAccount.Name));
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

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
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Stop();
            }
        }

        internal bool MoveAttachments(string path)
        {
            bool exists = false;
            string existing = this.AttachmentDirectory;
            bool hasValue = !string.IsNullOrEmpty(path);
            if (hasValue)
            {
                try
                {
                    if (!string.IsNullOrEmpty(existing) && Directory.Exists(existing) &&
                        string.Compare(path, existing, StringComparison.OrdinalIgnoreCase) != 0 &&
                        !Directory.Exists(path))
                    {
                        if (MessageBoxEx.Show("Would you like to move the existing attachments directory to this new path?", "Rename Directory", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.OK)
                        {
                            Directory.Move(existing, path);
                        }
                        else
                        {
                            return false;
                        }
                    }

                    exists = Directory.Exists(path);
                    if (!exists)
                    {
                        if (MessageBoxEx.Show("The storage location does not exist, would you like to create it?", "Create Directory", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.OK)
                        {
                            Directory.CreateDirectory(path);
                        }
                        else
                        {
                            return false;
                        }
                    }
                    this.AttachmentDirectory = path;
                }
                catch (Exception ex)
                {
                    exists = false;
                    MessageBoxEx.Show("Unexpected error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            return true;
        }
    }

    internal class AttachmentWatcher
    {
        private readonly ConcurrentQueue<Account> accountQueue;
        private readonly ConcurrentQueue<Transaction> transactionQueue;
        private bool threadRunning;
        private readonly MyMoney money;
        private readonly AutoResetEvent threadStopEvent = new AutoResetEvent(false);

        internal string AttachmentDirectory { get; set; }

        public AttachmentWatcher(MyMoney money)
        {
            this.accountQueue = new ConcurrentQueue<Account>();
            this.transactionQueue = new ConcurrentQueue<Transaction>();
            this.money = money;
        }

        internal void Stop()
        {
            if (this.threadRunning)
            {
                this.threadRunning = false;
                this.threadStopEvent.WaitOne(5000);
            }
        }

        internal void QueueTransaction(Transaction t)
        {
            if (t != null && !this.transactionQueue.Contains(t))
            {
                this.transactionQueue.Enqueue(t);
            }
        }

        internal void QueueAccount(Account a)
        {
            if (a != null && !this.accountQueue.Contains(a))
            {
                this.accountQueue.Enqueue(a);
            }
        }

        public void ScanAllAccounts()
        {
            foreach (Account a in this.money.Accounts)
            {
                this.QueueAccount(a);
            }
            this.StartQueued();
        }

        public void StartQueued()
        {
            if (this.accountQueue.Count > 0 || this.transactionQueue.Count > 0)
            {
                this.StartThread();
            }
        }

        private void StartThread()
        {
            if (!this.threadRunning)
            {
                this.threadRunning = true;
                this.threadStopEvent.Reset();
                Task.Run(this.ScanDirectory);
            }
        }

        /// <summary>
        /// This runs on a background thread and finds all attachments and updates the HasAttachment
        /// flag on all transactions.
        /// </summary>
        /// <param name="state"></param>
        public void ScanDirectory()
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
                    while (this.accountQueue.TryDequeue(out a) && this.threadRunning)
                    {
                        this.FindAttachments(path, a);
                    }

                    // process pending individual transaction checks.
                    List<Tuple<Transaction, bool>> toUpdate = new List<Tuple<Transaction, bool>>();
                    Transaction t;
                    while (this.transactionQueue.TryDequeue(out t) && this.threadRunning)
                    {
                        bool yes = this.HasAttachments(path, t);
                        if (t.HasAttachment != yes)
                        {
                            toUpdate.Add(new Tuple<Transaction, bool>(t, yes));
                        }
                    }

                    // Updating Money transactions has to happen on the UI thread.
                    UiDispatcher.BeginInvoke(new Action(() =>
                    {
                        this.BatchUpdate(toUpdate);
                    }));
                }
            }
            catch
            {
            }
#if PerformanceBlocks
            }
#endif
            this.threadRunning = false;
            this.threadStopEvent.Set();
        }

        private void BatchUpdate(List<Tuple<Transaction, bool>> toUpdate)
        {
            this.money.BeginUpdate(this);
            try
            {
                foreach (var pair in toUpdate)
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
            if (string.IsNullOrEmpty(a.Name))
            {
                return;
            }
            HashSet<Transaction> set = new HashSet<Transaction>();

            string accountDirectory = Path.Combine(path, NativeMethods.GetValidFileName(a.Name));
            if (!string.IsNullOrEmpty(accountDirectory) && Directory.Exists(accountDirectory))
            {
                foreach (string fileName in Directory.GetFiles(accountDirectory, "*.*"))
                {
                    if (!this.threadRunning)
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
                    var files = Directory.GetFiles(accountDir, t.Id + ".*.*");
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
                    files.AddRange(Directory.GetFiles(accountDirectory, t.Id + ".*.*"));
                }
            }
            return files;
        }

        public void OnRenameAccount(string path, Account a, string oldName)
        {
            if (Directory.Exists(path) && !string.IsNullOrEmpty(a.Name) && !string.IsNullOrEmpty(oldName))
            {
                string oldAccountDirectory = Path.Combine(path, NativeMethods.GetValidFileName(oldName));
                string newAccountDirectory = Path.Combine(path, NativeMethods.GetValidFileName(a.Name));
                if (Directory.Exists(oldAccountDirectory) && !Directory.Exists(newAccountDirectory))
                {
                    Debug.WriteLine($"Account renamed from {oldName} to {a.Name}");
                    Directory.Move(oldAccountDirectory, newAccountDirectory);
                }
            }
        }

    }

}
