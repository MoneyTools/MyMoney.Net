using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Walkabout.Data;
using Walkabout.Utilities;

namespace Walkabout.Attachments
{
    public class StatementManager
    {
        private MyMoney myMoney;
        private DelayedActions actions = new DelayedActions();
        private Dictionary<Account, string> nameMap;
        private string statementsDir;
        private bool started;
        private bool loading;
        private bool loaded;
        // account name to index.
        private ConcurrentDictionary<string, StatementIndex> statements = new ConcurrentDictionary<string, StatementIndex>();

        public StatementManager(MyMoney myMoney)
        {
            this.myMoney = myMoney;
            this.UpdateNameMap();
        }

        void UpdateNameMap()
        {
            // save the original account names, in case the account is renamed.
            this.nameMap = new Dictionary<Account, string>();
            foreach (var item in this.myMoney.Accounts.GetAccounts())
            {
                this.nameMap[item] = item.Name;
            }
        }

        public string StatementsDirectory
        {
            get => this.statementsDir;
            set
            {
                if (this.statementsDir != value)
                {
                    this.statementsDir = value;
                    if (this.started && this.myMoney != null)
                    {
                        // start again at zero.
                        this.loaded = false;
                        this.actions.StartDelayedAction("ScanAccounts", () => this.LoadIndex(), TimeSpan.FromMilliseconds(10));
                    }
                }
            }
        }

        public bool IsLoaded => this.loaded;

        private StatementIndex GetOrCreateIndex(Account a)
        {
            string path = this.StatementsDirectory;
            if (string.IsNullOrEmpty(this.StatementsDirectory))
            {
                // we have no database loaded!
                return null;
            }

            string dir = Path.Combine(path, NativeMethods.GetValidFileName(a.Name));
            Directory.CreateDirectory(dir);
            string indexFile = Path.Combine(dir, "index.xml");

            if (!this.statements.TryGetValue(a.Name, out StatementIndex statementIndex))
            {
                statementIndex = new StatementIndex() { FileName = indexFile };
                this.statements[a.Name] = statementIndex;
            }
            return statementIndex;
        }

        public IEnumerable<StatementItem> GetStatements(Account a)
        {
            if (this.statements.TryGetValue(a.Name, out StatementIndex statementIndex))
            {
                foreach (var item in statementIndex.Items)
                {
                    yield return item;
                }
            }
        }

        public StatementItem GetStatement(Account a, DateTime date)
        {
            var statementIndex = this.GetOrCreateIndex(a);
            if (statementIndex == null) return null;
            return statementIndex.Items.Where(s => s.Date == date).FirstOrDefault();
        }

        public decimal GetStatementBalance(Account a, DateTime date)
        {
            var statementIndex = this.GetOrCreateIndex(a);
            if (statementIndex == null) return 0;
            var statement = this.GetStatement(a, date);
            if (statement != null)
            {
                return statement.StatementBalance;
            }
            return 0;
        }

        public string GetStatementFullPath(Account a, DateTime date)
        {
            var statementIndex = this.GetOrCreateIndex(a);
            if (statementIndex == null) return "";
            var statement = this.GetStatement(a, date);
            if (statement != null)
            {
                if (!string.IsNullOrEmpty(statement.Filename))
                {
                    var existingStatementFile = Path.Combine(Path.GetDirectoryName(statementIndex.FileName), statement.Filename);
                    return existingStatementFile;
                }
            }
            return "";
        }

        public bool UpdateStatement(Account a, StatementItem selected, DateTime date, string statementFile, decimal balance, bool flush = true)
        {
            var statementIndex = this.GetOrCreateIndex(a);
            if (statementIndex == null || !statementIndex.Items.Contains(selected))
            {
                return false;
            }

            // update the date, balance, statement file and hash
            selected.Date = date;
            selected.StatementBalance = balance;
            var statementDir = Path.GetDirectoryName(statementIndex.FileName);
            this.ComputeHash(statementIndex, selected, statementFile);

            if (flush)
            {
                this.SaveIndex(statementIndex);
            }

            return true;
        }

        private void SafeDeleteFile(StatementIndex index, StatementItem item)
        {
            var statementDir = Path.GetDirectoryName(index.FileName);
            var fullPath = Path.Combine(statementDir, item.Filename);
            if (File.Exists(fullPath))
            {
                if (this.IsBundledStatement(index, item))
                {
                    // it is being used elsewhere!
                    item.Filename = "";
                    item.Hash = "";
                }
                else
                {
                    // then it is safe to delete it!
                    File.Delete(fullPath);
                    item.Filename = "";
                    item.Hash = "";
                }
            }
            else
            {
                // file is already gone!
                item.Filename = "";
                item.Hash = "";
            }
        }

        private void ComputeHash(StatementIndex index, StatementItem item, string statementFile)
        {
            if (!string.IsNullOrEmpty(statementFile))
            {
                if (File.Exists(statementFile))
                {
                    var hash = Sha256Hash(statementFile);
                    var statementDir = Path.GetDirectoryName(index.FileName);

                    // if the statementFile is already in statementDir then use it.
                    if (string.Compare(Path.GetDirectoryName(statementFile), statementDir, StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        item.Hash = "";
                        // File is not already in our statementDir, so see if we can bundle it.
                        var bundled = this.FindBundledStatement(statementFile);
                        if (!string.IsNullOrEmpty(bundled))
                        {
                            // then the file is already referenced by another statement, perhaps in a different account,
                            // so we don't need another copy, instead just keep a reference to this one.
                            statementFile = bundled;
                        }
                        else
                        {
                            // Make sure we have a unique name for it
                            // copy the file to our directory (if it's not already there).
                            var newName = this.GetUniqueStatementName(statementDir, Path.GetFileName(statementFile));
                            File.Copy(statementFile, newName, true);
                            statementFile = newName;
                        }
                    }

                    var relative = FileHelpers.GetRelativePath(statementFile, index.FileName);
                    if (!string.IsNullOrEmpty(item.Filename) && item.Filename != relative)
                    {
                        // then the statement is being renamed, which means we might need to cleanup the old one
                        this.SafeDeleteFile(index, item);
                    }

                    item.Filename = relative;
                    item.Hash = hash;
                }
            }
            else
            {
                item.Filename = null;
                item.Hash = null;
            }
        }

        public bool AddStatement(Account a, DateTime date, string statementFile, decimal balance, bool flush = true)
        {
            var statementIndex = this.GetOrCreateIndex(a);
            if (statementIndex == null) return false;
            var statementDir = Path.GetDirectoryName(statementIndex.FileName);

            var statement = this.GetStatement(a, date);
            if (statement != null)
            {
                throw new InvalidOperationException("Internal Error: should have called UpdateStatement");
            }

            if (statement == null)
            {
                statement = new StatementItem()
                {
                    Date = date,
                    StatementBalance = balance
                };

                statementIndex.Items.Add(statement);
            }

            this.ComputeHash(statementIndex, statement, statementFile);

            if (flush)
            {
                this.SaveIndex(statementIndex);
            }

            return statement != null;
        }

        private string FindBundledStatement(string fileName)
        {
            if (File.Exists(fileName))
            {
                var hash = Sha256Hash(fileName);
                // sometimes banks bundle multiple accounts in the same statement.
                // if that happens we don't want to store duplicate statements across
                // all those accounts.  Instead we find the other index that already
                // has the statement and we point to that file instead.
                foreach (var index in this.statements.Values)
                {
                    foreach (var item in index.Items)
                    {
                        if (!string.IsNullOrEmpty(item.Filename))
                        {
                            var fullPath = Path.Combine(Path.GetDirectoryName(index.FileName), item.Filename);
                            if (item.Hash == hash && FileHelpers.FilesIdentical(fullPath, fileName))
                            {
                                return Path.Combine(Path.GetDirectoryName(index.FileName), item.Filename);
                            }
                        }
                    }
                }
            }
            return null;
        }

        private bool IsBundledStatement(StatementIndex parent, StatementItem toFind)
        {
            string fileName = Path.Combine(Path.GetDirectoryName(parent.FileName), toFind.Filename);
            // Return true if a different Statement directory is referencing this statement file.
            foreach (var index in this.statements.Values)
            {
                if (index != parent)
                {
                    foreach (var item in index.Items)
                    {
                        if (!string.IsNullOrEmpty(item.Filename))
                        {
                            var fullPath = Path.Combine(Path.GetDirectoryName(index.FileName), item.Filename);
                            if (FileHelpers.FilesIdentical(fullPath, fileName))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private void UpdateBundledPointers(string oldName, string newName)
        {
            string root = this.StatementsDirectory;
            if (string.IsNullOrEmpty(root)) return;

            foreach (var index in this.statements.Values)
            {
                var changed = false;
                foreach (var item in index.Items)
                {
                    var fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(index.FileName), item.Filename));
                    var parentDir = Path.GetDirectoryName(fullPath);
                    var dirName = Path.GetFileName(parentDir);
                    if (dirName == oldName)
                    {
                        // oh then we need to redirect this link to the newName.
                        var fileName = Path.GetFileName(item.Filename);
                        var newPath = Path.Combine(Path.GetDirectoryName(parentDir), newName, fileName);
                        var relative = FileHelpers.GetRelativePath(newPath, index.FileName);
                        item.Filename = relative;
                        changed = true;
                    }
                }
                if (changed)
                {
                    this.SaveIndex(index);
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "AttachmentDirectory")]
        private string GetUniqueStatementName(string path, string fileName)
        {
            string fullPath = Path.Combine(path, fileName);
            if (!File.Exists(fullPath))
            {
                return fullPath;
            }

            string baseName = Path.GetFileNameWithoutExtension(fileName);
            int index = 0;
            var extension = Path.GetExtension(fileName);
            while (true)
            {
                fullPath = Path.Combine(path, baseName) + index + extension;
                if (!File.Exists(fileName))
                {
                    return fullPath;
                }
                index++;
            }
        }

        private void SaveIndex(StatementIndex index)
        {
            // sort the items by date.
            index.Items.Sort((a, b) =>
            {
                return a.Date.CompareTo(b.Date);
            });
            var s = new XmlSerializer(typeof(StatementIndex));
            using (var fs = new FileStream(index.FileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                s.Serialize(fs, index);
            }
        }

        public string SetupStatementsDirectory(string databasePath)
        {
            string localName = Path.GetFileNameWithoutExtension(databasePath) + ".Statements";
            string dir = Path.GetDirectoryName(databasePath);
            string statementsPath = Path.Combine(dir, localName);
            Directory.CreateDirectory(statementsPath);
            this.StatementsDirectory = statementsPath;
            return statementsPath;
        }

        public void Stop()
        {
            this.started = false;
            this.actions.CancelAll();
            if (this.myMoney != null)
            {
                this.myMoney.Accounts.Changed -= new EventHandler<ChangeEventArgs>(this.OnAccountsChanged);
                this.myMoney.Changed -= new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);
            }
        }

        public void Start()
        {
            this.Stop();
            if (this.myMoney != null)
            {
                // listen to transaction changed events so that we can cleanup attachments when transactions
                // are deleted.
                this.myMoney.Accounts.Changed += new EventHandler<ChangeEventArgs>(this.OnAccountsChanged);
                this.myMoney.Changed += new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);
                this.started = true;
                this.loaded = false;
                this.actions.StartDelayedAction("ScanAccounts", () => this.LoadIndex(), TimeSpan.FromMilliseconds(100));
            }
        }

        void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
            this.HandleChanges(args);
        }

        void OnAccountsChanged(object sender, ChangeEventArgs args)
        {
            this.HandleChanges(args);
        }

        void HandleChanges(ChangeEventArgs args)
        {
            while (args != null)
            {
                if (args.Item is Account a)
                {
                    if (args.ChangeType == ChangeType.Inserted)
                    {
                        this.actions.StartDelayedAction("LoadAccount" + a.Name, () =>
                        {
                            this.UpdateNameMap();
                            this.LoadIndexFile(a.Name);
                        }, TimeSpan.FromMilliseconds(100));
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
                    this.OnRenameAccountFolder(a, oldName);
                    var index = this.GetOrLoadIndexFile(oldName);
                    this.nameMap[a] = a.Name;
                    if (index != null)
                    {
                        this.statements[a.Name] = index;
                        index.FileName = Path.Combine(this.StatementsDirectory, a.Name, "index.xml");
                        this.statements.TryRemove(oldName, out StatementIndex _);
                    }
                    // Now fix referential integrity for any bundled statements that were
                    // pointing to this folder.
                    this.UpdateBundledPointers(oldName, a.Name);
                }
            }
        }

        public void OnRenameAccountFolder(Account a, string oldName)
        {
            string path = this.StatementsDirectory;
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
        private void LoadIndex()
        {
            Task.Run(this.Load);
        }

        void CheckFileHashes(StatementIndex index)
        {
            if (index.Items == null)
            {
                index.Items = new List<StatementItem>();
            }
            bool updated = false;
            var dir = Path.GetDirectoryName(index.FileName);
            foreach (var item in index.Items)
            {
                if (!string.IsNullOrEmpty(item.Filename))
                {
                    var statementFile = Path.Combine(dir, item.Filename);
                    if (File.Exists(statementFile))
                    {
                        if (string.IsNullOrEmpty(item.Hash) || !item.FileModified.HasValue ||
                            item.FileModified.Value < File.GetLastWriteTime(statementFile))
                        {
                            item.Hash = Sha256Hash(statementFile);
                            item.FileModified = File.GetLastWriteTime(statementFile);
                            updated = true;
                        }
                    }
                }
            }
            if (updated)
            {
                this.SaveIndex(index);
            }
        }

        private StatementIndex LoadIndexFile(string name)
        {
            string path = this.StatementsDirectory;
            if (string.IsNullOrEmpty(path))
            {
                // we have no database loaded!
                return null;
            }
            var fname = NativeMethods.GetValidFileName(name);
            if (string.IsNullOrEmpty(fname))
            {
                Debug.WriteLine(String.Format("Failed to compute a valid file name for account '{0}'", name));
                return null;
            }
            string dir = Path.Combine(path, fname);
            string indexFile = Path.Combine(dir, "index.xml");

            try
            {
                if (File.Exists(indexFile))
                {
                    StatementIndex index = null;
                    var s = new XmlSerializer(typeof(StatementIndex));
                    using (var fs = new FileStream(indexFile, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        index = (StatementIndex)s.Deserialize(fs);
                    }
                    index.FileName = indexFile;
                    this.statements[name] = index;
                    this.CheckFileHashes(index);
                    return index;
                }
            }
            catch (Exception ex)
            {
                // TODO: fix corrupt files?
                Debug.WriteLine(String.Format("Failed to load statement index '{0}'", indexFile));
                Debug.WriteLine(ex.ToString());
            }
            return null;
        }

        internal void ImportStatements(Account a, StatementManager importStatements)
        {
            Account existing = this.myMoney.Accounts.FindAccount(a.Name);
            if (existing != null)
            {
                if (importStatements.statements.TryGetValue(a.Name, out StatementIndex index))
                {
                    foreach (var item in index.Items)
                    {
                        // copy the file over but don't save the index.xml file yet
                        this.AddStatement(existing, item.Date, item.Filename, item.StatementBalance, false);
                    }

                    // now update the xml file.
                    if (this.statements.TryGetValue(a.Name, out StatementIndex updated))
                    {
                        this.SaveIndex(updated);
                    }
                }
            }
        }

        // synchronous loading of the entire index.
        public void Load()
        {
            this.loading = true;
            foreach (var a in this.myMoney.Accounts.GetAccounts())
            {
                if (!string.IsNullOrEmpty(a.Name))
                {
                    this.LoadIndexFile(a.Name);
                }
            }
            this.loading = false;
            this.loaded = true;
        }

        private StatementIndex GetOrLoadIndexFile(string accountName)
        {
            if (this.statements.TryGetValue(accountName, out StatementIndex statementIndex))
            {
                return statementIndex;
            }
            if (this.loading)
            {
                // this.statements may be incomplete and we can't wait, we need it now!
                return this.LoadIndexFile(accountName);
            }
            return null;
        }

        static string Sha256Hash(string fileName)
        {
            byte[] contents = File.ReadAllBytes(fileName);
            var crypt = new System.Security.Cryptography.SHA256Managed();
            var hash = new StringBuilder();
            byte[] crypto = crypt.ComputeHash(contents);
            foreach (byte theByte in crypto)
            {
                hash.Append(theByte.ToString("x2"));
            }
            return hash.ToString();
        }
    }

    public class StatementIndex
    {
        [XmlIgnore]
        public string FileName { get; set; }

        public List<StatementItem> Items { get; set; }

        public StatementIndex()
        {
            this.Items = new List<StatementItem>();
        }
    }

    public class StatementItem
    {
        public DateTime Date { get; set; }
        public string Filename { get; set; }
        public decimal StatementBalance { get; set; }
        public string Hash { get; set; }

        public DateTime? FileModified { get; set; }
    }
}
