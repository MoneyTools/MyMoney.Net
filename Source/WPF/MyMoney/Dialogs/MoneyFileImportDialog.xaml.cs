using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
    public partial class MoneyFileImportDialog : Window, IStatusService
    {
        MyMoney myMoney;
        string loadingStatusPrompt;
        CancellationTokenSource cancelSource;
        bool busy;
        ObservableCollection<AccountImportState> list;
        AttachmentManager myAttachments;
        Dispatcher dispatcher;

        public MoneyFileImportDialog()
        {            
            InitializeComponent();
            loadingStatusPrompt = Status.Text;
            list = (ObservableCollection<AccountImportState>)AccountList.ItemsSource;
            list.Clear();
            this.dispatcher = this.Dispatcher;
            this.Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            Window owner = this.Owner;
            if (owner != null)
            {
                owner.Closed += OnOwnerClosed;
            }
        }

        private void OnOwnerClosed(object sender, EventArgs e)
        {
            this.Close();
        }

        public IViewNavigator Navigator { get; set; }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (busy)
            {
                if (MessageBoxEx.Show("Still importing, do you want to cancel?", "Cancel", MessageBoxButton.YesNo, MessageBoxImage.Hand) == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                cancelSource.Cancel();
            }
            base.OnClosing(e);
        }

        internal void Import(MyMoney myMoney, AttachmentManager myAttachments, string[] fileNames)
        {
            this.myMoney = myMoney;
            this.myAttachments = myAttachments;
            cancelSource = new CancellationTokenSource();
            var token = cancelSource.Token;
            Task.Factory.StartNew(() => {
                busy = true;
                foreach (string file in fileNames)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }
                    if (!ProcessFile(file))
                    {
                        break;
                    }
                }
                busy = false;
            }, token);
        }

        private bool ProcessFile(string file)
        {
            ShowStatus(string.Format(loadingStatusPrompt, file));

            try
            {
                if (file.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                {
                    string userName = null;
                    string password = null;
                    string path = Path.GetFullPath(file);
                    bool cancelled = false;

                    dispatcher.Invoke(new Action(() =>
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
                            ShowStatus("Import cancelled.");
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

                        string attachmentsDir = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".Attachments");
                        if (Directory.Exists(attachmentsDir))
                        {

                            MyMoney newMoney = database.Load(this);
                            AttachmentManager importAttachments = new AttachmentManager(newMoney);
                            importAttachments.AttachmentDirectory = attachmentsDir;
                            ImportMoneyFile(newMoney, importAttachments);
                        }
                        else
                        {
                            // todo: prompt user for attachments?
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    ShowStatus("Import only supports sqllite money files");
                    return false;
                }
            }
            catch (Exception ex)
            {
                ShowStatus("Error: " + ex.Message);
                return false;
            }
            return true;
        }


        private void ImportMoneyFile(MyMoney newMoney, AttachmentManager newAttachments)
        {
            this.dispatcher.Invoke(new Action(() =>
            {
                // gather accounts to be merged (skipping closed accounts).
                foreach (var acct in newMoney.Accounts)
                {
                    if (!acct.IsClosed && !acct.IsCategoryFund)
                    {
                        list.Add(new AccountImportState()
                        {
                            Dispatcher = this.dispatcher,
                            Account = acct,
                            Name = acct.Name,
                        });
                    }
                }
            }));

            foreach (AccountImportState a in list)
            {
                ImportAccount(newMoney, a, newAttachments);
            }

            ShowStatus("Done");
        }

        private void ImportAccount(MyMoney newMoney, AccountImportState a, AttachmentManager newAttachments)
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
            for (int i = 0, n = transactions.Count; i< n; i++)
            {
                double percent = ((double)i * 100.0) / (double)n;
                a.PercentComplete = (int)percent;
                Transaction t = transactions[i];
                Transaction u = this.myMoney.Transactions.FindTransactionById(t.Id);
                
                try {
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
                Status.Text = status;
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
                if (Navigator != null)
                {
                    Navigator.ViewTransactions(item.Changed);
                }
            }
        }
    }

    class DemoList : ObservableCollection<AccountImportState>
    {
        public DemoList()
        {
            this.Add(new AccountImportState() { Name = "Apple", Done= true, Status="Updated 5 rows" });
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
            get { return name; }
            set
            {
                if (name != value)
                {
                    name = value;
                    OnPropertyChanged("Name");
                }
            }
        }

        string status;

        public string Status
        {
            get { return status; }
            set
            {
                if (status != value)
                {
                    status = value;
                    OnPropertyChanged("Status");
                }
            }
        }
        int percentComplete;

        public int PercentComplete
        {
            get { return percentComplete; }
            set
            {
                if (percentComplete != value)
                {
                    percentComplete = value;
                    // let the timer do this so we don't flood the input queue.
                    //OnPropertyChanged("PercentComplete");
                }
            }
        }


        bool loading;

        public bool Loading
        {
            get { return loading; }
            set
            {
                if (loading != value)
                {
                    loading = value;
                    OnPropertyChanged("Loading");
                }
                if (value)
                {
                    StartTimer();
                }
                else
                {
                    StopTimer();
                    // one more in case timer never ticked.
                    ThrottledUpdate();
                }
            }
        }

        DispatcherTimer timer;

        private void StopTimer()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= OnTimerTick;
                timer = null;
            }
        }

        private void StartTimer()
        {
            StopTimer();
            if (Dispatcher != null)
            {
                timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher);
                timer.Tick += OnTimerTick;
                timer.Interval = TimeSpan.FromMilliseconds(30);
                timer.Start();
            }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            ThrottledUpdate();
        }

        void ThrottledUpdate()
        {
            OnPropertyChanged("PercentComplete");
            if (Modified != 0)
            {
                Status = "Updated " + Modified + ((Modified == 1) ? " row" : " rows");
                OnPropertyChanged("Status");
            }
        }

        bool done;

        public bool Done
        {
            get { return done; }
            set
            {
                if (done != value)
                {
                    done = value;
                    OnPropertyChanged("Done");
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
                if (Dispatcher != null)
                {
                    Dispatcher.Invoke(new Action(() =>
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
