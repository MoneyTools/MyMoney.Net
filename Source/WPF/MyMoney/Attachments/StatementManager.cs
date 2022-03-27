using System;
using System.Linq;
using System.IO;
using Walkabout.Data;
using Walkabout.Utilities;
using System.Text;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Walkabout.Attachments
{
    public class StatementManager
    {
        private MyMoney myMoney;
        private DelayedActions actions = new DelayedActions();
        private string statementsDir;
        private bool started;
        private bool loaded;
        // account name to index.
        private ConcurrentDictionary<string, StatementIndex> statements = new ConcurrentDictionary<string, StatementIndex>();

        public StatementManager(MyMoney myMoney)
        {
            this.myMoney = myMoney;
        }

        public string StatementsDirectory
        {
            get => statementsDir;
            set {
                if (statementsDir != value)
                {
                    statementsDir = value;
                    if (started && myMoney != null)
                    {
                        // start again at zero.
                        loaded = false;
                        actions.StartDelayedAction("ScanAccounts", () => LoadIndex(), TimeSpan.FromMilliseconds(10));
                    }
                }
            }
        }

        public bool IsLoaded => this.loaded;

        public MyMoney MyMoney
        {
            get { return myMoney; }
            set { myMoney = value; }
        }

        private StatementIndex GetOrCreateIndex(Account a)
        {
            string path = this.StatementsDirectory;
            if (string.IsNullOrEmpty(StatementsDirectory))
            {
                throw new InvalidOperationException("The 'StatementsDirectory' is not initialized");
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

        private StatementItem InternalGetStatement(Account a, DateTime date)
        {
            var statementIndex = GetOrCreateIndex(a);
            return statementIndex.Items.Where(s => s.Date == date).FirstOrDefault();
        }

        public decimal GetStatementBalance(Account a, DateTime date)
        {
            var statementIndex = GetOrCreateIndex(a);
            var statement = InternalGetStatement(a, date);
            if (statement != null)
            {
                return statement.StatementBalance;
            }
            return 0;
        }

        public string GetStatement(Account a, DateTime date)
        {
            var statementIndex = GetOrCreateIndex(a);
            var statement = InternalGetStatement(a, date);
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

        public bool AddStatement(Account a, DateTime date, string statementFile, decimal balance, bool flush = true)
        {
            var statementIndex = GetOrCreateIndex(a);

            var statement = InternalGetStatement(a, date);
            if (statement != null)
            {
                if (!string.IsNullOrEmpty(statement.Filename))
                {
                    var existingStatementFile = Path.Combine(statementIndex.FileName, statement.Filename);
                    if (string.Compare(existingStatementFile, statementFile, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        statement.StatementBalance = balance;
                        return true; // already has it!
                    }
                    if (File.Exists(existingStatementFile))
                    {
                        File.Delete(existingStatementFile);
                    }
                    statementIndex.Items.Remove(statement);
                }
            }

            if (!string.IsNullOrEmpty(statementFile))
            {
                var statementDir = Path.GetDirectoryName(statementIndex.FileName);

                var hash = Sha256Hash(statementFile);
                // see if this hash exists anywhere else.
                var bundled = FindBundledStatement(statementFile, hash);
                if (!string.IsNullOrEmpty(bundled))
                {
                    statementFile = bundled;
                }
                else if (string.Compare(Path.GetDirectoryName(statementFile), statementDir, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    // copy the file to our directory (if it's not already there).
                    var newName = GetUniqueStatementName(statementDir, Path.GetFileName(statementFile));
                    File.Copy(statementFile, newName, false);
                    statementFile = newName;
                }

                statement = new StatementItem()
                {
                    Date = date,
                    Filename = FileHelpers.GetRelativePath(statementFile, statementIndex.FileName),
                    StatementBalance = balance,
                    Hash = hash
                };
                statementIndex.Items.Add(statement);
            }

            if (flush)
            {
                SaveIndex(statementIndex);
            }

            return statement != null;
        }

        private string FindBundledStatement(string fileName, string hash)
        {
            // sometimes banks bundle multiple accounts in the same statement.
            // if that happens we don't want to store duplicate statements across
            // all those accounts.  Instead we find the other index that already
            // has the statement and we point to that file instead.
            foreach(var index in statements.Values)
            {
                foreach (var item in index.Items)
                {
                    var fullPath = Path.Combine(Path.GetDirectoryName(index.FileName), item.Filename);
                    if (item.Hash == hash && FileHelpers.FilesIdentical(fullPath, fileName))
                    {
                        return Path.Combine(Path.GetDirectoryName(index.FileName), item.Filename);
                    }
                }
            }
            return null;
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
            started = false;
            actions.CancelAll();
        }

        public void Start()
        {
            if (string.IsNullOrEmpty(StatementsDirectory))
            {
                throw new InvalidOperationException("The 'StatementsDirectory' is not initialized");
            }
            if (myMoney != null)
            {
                started = true;
                loaded = false;
                actions.StartDelayedAction("ScanAccounts", () => LoadIndex(), TimeSpan.FromMilliseconds(100));
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
            foreach(var item in index.Items)
            {
                if (string.IsNullOrEmpty(item.Hash))
                {
                    var statementFile = Path.Combine(dir, item.Filename);
                    item.Hash = Sha256Hash(statementFile);
                    updated = true;
                }
            }
            if (updated)
            {
                SaveIndex(index);
            }
        }

        private void LoadIndexFile(Account a)
        {
            string name = a.Name;
            string path = this.StatementsDirectory;
            string dir = Path.Combine(path, NativeMethods.GetValidFileName(a.Name));
            string indexFile = Path.Combine(dir, "index.xml");

            try
            {
                if (File.Exists(indexFile))
                {
                    var s = new XmlSerializer(typeof(StatementIndex));
                    using (var fs = new FileStream(indexFile, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        StatementIndex index = (StatementIndex)s.Deserialize(fs);
                        index.FileName = indexFile;
                        statements[name] = index;
                        CheckFileHashes(index);
                    }
                }
            } 
            catch (Exception ex)
            {
                // TODO: fix corrupt files?
                Debug.WriteLine(String.Format("Failed to load statement index '{0}'", indexFile));
                Debug.WriteLine(ex.ToString());
            }
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
                        SaveIndex(updated);
                    }
                }
            }
        }

        // synchronous loading of the entire index.
        public void Load()
        {
            foreach(var a in myMoney.Accounts.GetAccounts())
            {
                LoadIndexFile(a);
            }
            this.loaded = true;
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
            Items = new List<StatementItem>();
        }
    }

    public class StatementItem
    {
        public DateTime Date { get; set; }
        public string Filename { get; set; }
        public decimal StatementBalance { get; set; }
        public string Hash { get; set; }
    }
}
