using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using Walkabout.Data;
using Walkabout.Ofx;
using Walkabout.Sgml;
using Walkabout.Utilities;

namespace Walkabout.Importers
{
    public delegate void DownloadProgress(int min, int max, int value, DownloadEventArgs e);

    /// <summary>
    /// A class that shows the downloads in progress, the properties on this object are thread safe
    /// in that they can be bound to UI and be updated by background threads.
    /// </summary>
    public class DownloadData : INotifyPropertyChanged
    {
        private string message;
        private Exception error;
        private readonly OnlineAccount online;
        private readonly Account account;
        private readonly string fileName;
        private bool isError;
        private bool isOfxError;
        private bool isDownloading;
        private bool success;
        private readonly ThreadSafeObservableCollection<DownloadData> children;
        private readonly List<Transaction> added = new List<Transaction>();
        private OfxErrorCode ofxError;
        private string linkCaption = "Details...";

        public DownloadData(OnlineAccount online, Account account)
        {
            this.online = online;
            this.account = account;
            this.children = new ThreadSafeObservableCollection<DownloadData>();
        }

        public DownloadData(OnlineAccount online, Account account, string msg)
        {
            this.online = online;
            this.account = account;
            this.message = msg;
            this.children = new ThreadSafeObservableCollection<DownloadData>();
        }

        public DownloadData(OnlineAccount online, string fileName, string msg)
        {
            this.online = online;
            this.fileName = fileName;
            this.message = msg;
            this.children = new ThreadSafeObservableCollection<DownloadData>();
        }

        public int Index { get; set; }

        public List<Transaction> Added { get { return this.added; } }

        public OnlineAccount OnlineAccount { get { return this.online; } }

        public Account Account { get { return this.account; } }

        public bool IsError { get { return this.isError; } set { this.isError = value; this.OnPropertyChanged("IsError"); } }

        public bool IsOfxError { get { return this.isOfxError; } set { this.isOfxError = value; this.OnPropertyChanged("IsOfxError"); } }

        public bool IsDownloading { get { return this.isDownloading; } set { this.isDownloading = value; this.OnPropertyChanged("IsDownloading"); } }

        public bool Success { get { return this.success; } set { this.success = value; this.OnPropertyChanged("Success"); } }

        public OfxErrorCode OfxError { 
            get { return this.ofxError; } 
            set { 
                this.IsOfxError = value != OfxErrorCode.None;
                this.ofxError = value; 
                this.OnPropertyChanged("OfxError");

                switch (this.ofxError)
                {
                    case OfxErrorCode.AUTHTOKENRequired:
                        this.LinkCaption = "Get Authentication Token";
                        break;
                    case OfxErrorCode.MFAChallengeAuthenticationRequired:
                        this.LinkCaption = "Provide More Authentication";
                        break;
                    case OfxErrorCode.MustChangeUSERPASS:
                        this.LinkCaption = "Change Password";
                        break;
                    case OfxErrorCode.SignonInvalid:
                        this.LinkCaption = "Login";
                        break;
                }
            }
        }

        public string Message
        {
            get { return (this.Caption == this.message) ? "" : this.message; }
            set { this.message = value; this.OnPropertyChanged("Message"); }
        }

        public Exception Error { get { return this.error; } set { this.error = value; this.OnPropertyChanged("Error"); } }

        public Visibility ErrorVisibility { get { return this.error == null ? Visibility.Hidden : Visibility.Visible; } }

        public string LinkCaption { get { return this.linkCaption; } set { this.linkCaption = value; this.OnPropertyChanged("LinkCaption"); } }

        public string Caption
        {
            get
            {
                string name = null;
                if (this.Account != null)
                {
                    name = this.Account.Name;
                }
                else if (this.OnlineAccount != null)
                {
                    name = this.OnlineAccount.Name;
                }
                else if (!string.IsNullOrEmpty(this.fileName))
                {
                    name = this.fileName;
                }
                if (name == null)
                {
                    name = this.message;
                }
                return name;
            }
        }

        public void AddItem(Transaction t)
        {
            this.Added.Add(t);
        }

        public ThreadSafeObservableCollection<DownloadData> Children { get { return this.children; } }

        public DownloadData AddError(OnlineAccount oa, Account account, string error)
        {
            DownloadData e = new DownloadData(oa, account, "Error");
            e.Message = error;
            if (!string.IsNullOrEmpty(error))
            {
                e.Error = new OfxException(error);
            }
            e.isError = true;
            this.children.Add(e);
            return e;
        }

        public DownloadData AddError(OnlineAccount oa, Account account, Exception error)
        {
            DownloadData e = new DownloadData(oa, account, "Error");
            e.Error = error;
            e.isError = true;
            this.children.Add(e);
            return e;
        }

        /// <summary>
        /// Call this for each account you have downloaded and on the resulting DownloadData
        /// you can call AddItem to add each transaction that was added or merged during the
        /// download so user can select this row and get a view of those specific transactions.
        /// </summary>
        /// <returns></returns>
        public DownloadData AddResult(OnlineAccount oa, Account account, int count)
        {
            string message = (count > 0) ? "Downloaded " + count + " new transactions" : null;
            DownloadData e = new DownloadData(oa, account, message);
            this.children.Add(e);
            return e;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(name));
                }
            }));
        }

    }

    public class DownloadEventArgs : EventArgs
    {
        private readonly ThreadSafeObservableCollection<DownloadData> list;

        public DownloadEventArgs()
        {
            this.list = new ThreadSafeObservableCollection<DownloadData>();
        }

        public ThreadSafeObservableCollection<DownloadData> Entries
        {
            get { return this.list; }
        }

        public DownloadData AddError(OnlineAccount online, Account account, string message)
        {
            DownloadData entry = new DownloadData(online, account, message);
            entry.IsError = true;
            this.list.Add(entry);
            return entry;
        }

        public DownloadData AddError(OnlineAccount online, string fileName, string message)
        {
            DownloadData entry = new DownloadData(online, fileName, message);
            entry.IsError = true;
            this.list.Add(entry);
            return entry;
        }

        public DownloadData AddEntry(OnlineAccount online, Account account, string caption)
        {
            DownloadData entry = new DownloadData(online, account, caption);
            this.list.Add(entry);
            return entry;
        }
    }

}
