using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using Walkabout.Attachments;
using Walkabout.Data;
using Walkabout.Interfaces.Views;
using Walkabout.Utilities;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for MoneyFileImportDialog.xaml
    /// </summary>
    public partial class MoneyFileImportDialog : BaseDialog, IStatusService
    {
        MyMoney myMoney;
        string loadingStatusPrompt;
        CancellationTokenSource cancelSource;
        bool busy;
        ObservableCollection<AccountImportState> list;
        AttachmentManager myAttachments;
        StatementManager myStatements;
        Dispatcher dispatcher;

        public MoneyFileImportDialog()
        {
            this.InitializeComponent();
            this.loadingStatusPrompt = this.Status.Text;
            this.list = (ObservableCollection<AccountImportState>)this.AccountList.ItemsSource;
            this.list.Clear();
            this.dispatcher = this.Dispatcher;
            Loaded += this.OnWindowLoaded;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            Window owner = this.Owner;
            if (owner != null)
            {
                owner.Closed -= this.OnOwnerClosed;
                owner.Closed += this.OnOwnerClosed;
            }
        }

        private void OnOwnerClosed(object sender, EventArgs e)
        {
            this.Close();
        }

        public IViewNavigator Navigator { get; set; }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (this.busy)
            {
                if (MessageBoxEx.Show("Still importing, do you want to cancel?", "Cancel", MessageBoxButton.YesNo, MessageBoxImage.Hand) == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                this.cancelSource.Cancel();
            }
            base.OnClosing(e);
        }

        internal void Import(MyMoney myMoney, AttachmentManager myAttachments, StatementManager myStatements, string[] fileNames)
        {
            this.myMoney = myMoney;
            this.myAttachments = myAttachments;
            this.myStatements = myStatements;
            this.cancelSource = new CancellationTokenSource();
            var token = this.cancelSource.Token;
            Task.Factory.StartNew(() =>
            {
                this.busy = true;
                foreach (string file in fileNames)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }
                    if (!this.ProcessFile(file))
                    {
                        break;
                    }
                }
                this.busy = false;
            }, token);
        }

        private bool ProcessFile(string file)
        {
            this.ShowStatus(string.Format(this.loadingStatusPrompt, file));

            try
            {
                if (file.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".mmdb", StringComparison.OrdinalIgnoreCase))
                {
                    string userName = null;
                    string password = null;
                    string path = Path.GetFullPath(file);
                    bool cancelled = false;

                    this.dispatcher.Invoke(new Action(() =>
                    {
                        PasswordWindow w = new PasswordWindow();
                        Paragraph p = (Paragraph)w.IntroMessagePrompt.Document.Blocks.FirstBlock;
                        Run run = (Run)p.Inlines.FirstInline;
                        run.Text = "Please enter password for the imported database";
                        if (w.ShowDialog() == true)
                        {
                            userName = w.UserName;
                            password = w.PasswordConfirmation;
                            path = Path.GetFullPath(file);
                        }
                        else
                        {
                            this.ShowStatus("Import cancelled.");
                            cancelled = true;
                        }
                    }));

                    if (!cancelled)
                    {
                        SqliteDatabase database = new SqliteDatabase()
                        {
                            DatabasePath = path,
                            UserId = userName,
                            Password = password
                        };
                        database.Create();

                        // import the database, and any associated attachments or statements.
                        MyMoney newMoney = database.Load(this);
                        AttachmentManager importAttachments = new AttachmentManager(newMoney);
                        importAttachments.SetupAttachmentDirectory(path);
                        StatementManager importStatements = new StatementManager(newMoney);
                        importStatements.SetupStatementsDirectory(path);
                        importStatements.Load();
                        this.ImportMoneyFile(newMoney, importAttachments, importStatements);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    this.ShowStatus("Import only supports sqllite money files");
                    return false;
                }
            }
            catch (Exception ex)
            {
                this.ShowStatus("Error: " + ex.Message);
                return false;
            }
            return true;
        }


        private void ImportMoneyFile(MyMoney newMoney, AttachmentManager newAttachments, StatementManager importStatements)
        {
            this.dispatcher.Invoke(new Action(() =>
            {
                // gather accounts to be merged (skipping closed accounts).
                foreach (var acct in newMoney.Accounts)
                {
                    if (!acct.IsClosed && !acct.IsCategoryFund)
                    {
                        this.list.Add(new AccountImportState()
                        {
                            Dispatcher = dispatcher,
                            Account = acct,
                            Name = acct.Name,
                        });
                    }
                }
            }));

            foreach (AccountImportState a in this.list)
            {
                this.ImportAccount(newMoney, a, newAttachments, importStatements);
            }

            this.ShowStatus("Done");
        }

        private void ImportAccount(MyMoney newMoney, AccountImportState a, AttachmentManager newAttachments, StatementManager importStatements)
        {
            Account acct = a.Account;
            IList<Transaction> transactions = newMoney.Transactions.GetTransactionsFrom(acct);
            a.PercentComplete = 0;
            a.Loading = true;
            List<Transaction> changed = new List<Transaction>();
            a.Changed = changed;
            this.myMoney.BeginUpdate(this);
            StringBuilder sb = new StringBuilder();

            List<Transaction> newTransactions = new List<Transaction>();
            for (int i = 0, n = transactions.Count; i < n; i++)
            {
                double percent = (i * 100.0) / n;
                a.PercentComplete = (int)percent;
                Transaction t = transactions[i];
                Transaction u = this.myMoney.Transactions.FindTransactionById(t.Id);

                try
                {
                    if (u == null)
                    {
                        newTransactions.Add(t);
                    }
                    else if (u.Merge(t))
                    {
                        a.Modified++;
                        changed.Add(u);
                        // make the amounts the same
                        u.Amount = t.Amount;
                        List<string> attachments = newAttachments.GetAttachments(t);
                        this.myAttachments.ImportAttachments(t, attachments);
                    }
                }
                catch (Exception ex)
                {
                    // todo: handle errors.
                    sb.Append(ex);
                }
            }

            // must be done last so we don't get confused over mismatching transaction ids.
            foreach (Transaction t in newTransactions)
            {
                try
                {
                    Account target = this.myMoney.Accounts.FindAccount(t.AccountName);
                    if (target == null)
                    {
                        throw new Exception("Cannot import transaction to missing account: " + t.AccountName);
                    }
                    else
                    {
                        Transaction u = this.myMoney.Transactions.NewTransaction(target);
                        u.Merge(t);
                        a.Modified++;
                        changed.Add(u);
                        this.myMoney.Transactions.Add(u);
                        // make the amounts the same
                        u.Amount = t.Amount;
                        List<string> attachments = newAttachments.GetAttachments(t);
                        this.myAttachments.ImportAttachments(t, attachments);
                    }
                }
                catch (Exception ex)
                {
                    // todo: handle errors.
                    sb.Append(ex);
                }
            }

            this.myStatements.ImportStatements(acct, importStatements);

            this.myMoney.EndUpdate();

            a.Loading = false;
            a.Done = true;

            if (sb.Length > 0)
            {
                MessageBoxEx.Show(sb.ToString(), "Import Errors", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowStatus(string status)
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                this.Status.Text = status;
            }));
        }

        #region IStatusService

        public void ShowMessage(string text)
        {
        }

        public void ShowProgress(int min, int max, int value)
        {
        }

        public void ShowProgress(string message, int min, int max, int value)
        {

        }
        #endregion

        private void OnRowSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                AccountImportState item = (AccountImportState)e.AddedItems[0];
                if (this.Navigator != null)
                {
                    this.Navigator.ViewTransactions(item.Changed);
                }
            }
        }
    }

    class DemoList : ObservableCollection<AccountImportState>
    {
        public DemoList()
        {
            this.Add(new AccountImportState() { Name = "Apple", Done = true, Status = "Updated 5 rows" });
            this.Add(new AccountImportState() { Name = "Banana", Done = true, Status = "Updated 2 rows" });
            this.Add(new AccountImportState() { Name = "Orange", Loading = true, PercentComplete = 20, Status = "Updated 1 rows" });
            this.Add(new AccountImportState() { Name = "Pear" });
            this.Add(new AccountImportState() { Name = "Watermelon" });
        }
    }

    class AccountImportState : INotifyPropertyChanged
    {

        public Account Account { get; set; }

        public List<Transaction> Changed { get; set; }

        string name;

        public string Name
        {
            get { return this.name; }
            set
            {
                if (this.name != value)
                {
                    this.name = value;
                    this.OnPropertyChanged("Name");
                }
            }
        }

        string status;

        public string Status
        {
            get { return this.status; }
            set
            {
                if (this.status != value)
                {
                    this.status = value;
                    this.OnPropertyChanged("Status");
                }
            }
        }
        int percentComplete;

        public int PercentComplete
        {
            get { return this.percentComplete; }
            set
            {
                if (this.percentComplete != value)
                {
                    this.percentComplete = value;
                    // let the timer do this so we don't flood the input queue.
                    //OnPropertyChanged("PercentComplete");
                }
            }
        }


        bool loading;

        public bool Loading
        {
            get { return this.loading; }
            set
            {
                if (this.loading != value)
                {
                    this.loading = value;
                    this.OnPropertyChanged("Loading");
                }
                if (value)
                {
                    this.StartTimer();
                }
                else
                {
                    this.StopTimer();
                    // one more in case timer never ticked.
                    this.ThrottledUpdate();
                }
            }
        }

        DispatcherTimer timer;

        private void StopTimer()
        {
            if (this.timer != null)
            {
                this.timer.Stop();
                this.timer.Tick -= this.OnTimerTick;
                this.timer = null;
            }
        }

        private void StartTimer()
        {
            this.StopTimer();
            if (this.Dispatcher != null)
            {
                this.timer = new DispatcherTimer(DispatcherPriority.Normal, this.Dispatcher);
                this.timer.Tick += this.OnTimerTick;
                this.timer.Interval = TimeSpan.FromMilliseconds(30);
                this.timer.Start();
            }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            this.ThrottledUpdate();
        }

        void ThrottledUpdate()
        {
            this.OnPropertyChanged("PercentComplete");
            if (this.Modified != 0)
            {
                this.Status = "Updated " + this.Modified + ((this.Modified == 1) ? " row" : " rows");
                this.OnPropertyChanged("Status");
            }
        }

        bool done;

        public bool Done
        {
            get { return this.done; }
            set
            {
                if (this.done != value)
                {
                    this.done = value;
                    this.OnPropertyChanged("Done");
                }
            }
        }

        public Dispatcher Dispatcher { get; internal set; }

        public int Modified { get; internal set; }


        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                if (this.Dispatcher != null)
                {
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs(name));
                    }));
                }
                else
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(name));
                }
            }
        }
    }
}
