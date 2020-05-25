using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Walkabout.Data;
using Walkabout.Sgml;
using Walkabout.Utilities;
using Dispatcher = System.Windows.Threading.Dispatcher;


namespace Walkabout.Ofx
{
    public delegate void OfxDownloadProgress(int min, int max, int value, OfxDownloadEventArgs e);

    public class OfxDownloadData : INotifyPropertyChanged
    {
        string message;
        Exception error;
        OnlineAccount online;
        Account account;
        string fileName;
        bool isError;
        bool isDownloading;
        bool success;
        ThreadSafeObservableCollection<OfxDownloadData> children;
        List<Transaction> added = new List<Transaction>();
        OfxErrorCode ofxError;
        string linkCaption = "Details...";

        public OfxDownloadData(OnlineAccount online, Account account)
        {
            this.online = online;
            this.account = account;
            this.children = new ThreadSafeObservableCollection<OfxDownloadData>();
        }
        public OfxDownloadData(OnlineAccount online, Account account, string msg)
        {
            this.online = online;
            this.account = account;
            this.message = msg;
            this.children = new ThreadSafeObservableCollection<OfxDownloadData>();
        }

        public OfxDownloadData(OnlineAccount online, string fileName, string msg)
        {
            this.online = online;
            this.fileName = fileName;
            this.message = msg;
            this.children = new ThreadSafeObservableCollection<OfxDownloadData>();
        }

        public int Index { get; set; }

        public List<Transaction> Added { get { return this.added; } }

        public OnlineAccount OnlineAccount { get { return this.online; } }

        public Account Account { get { return this.account; } }

        public bool IsError { get { return this.isError; } set { this.isError = value; OnPropertyChanged("IsError"); } }

        public bool IsDownloading { get { return this.isDownloading; } set { this.isDownloading = value; OnPropertyChanged("IsDownloading"); } }

        public bool Success { get { return this.success; } set { this.success = value; OnPropertyChanged("Success"); } }

        public OfxErrorCode OfxError { get { return this.ofxError; } set { this.ofxError = value; OnPropertyChanged("OfxError"); } }

        public string Message
        {
            get { return this.message; }
            set { this.message = value; OnPropertyChanged("Message"); }
        }

        public Exception Error { get { return this.error; } set { this.error = value; OnPropertyChanged("Error"); } }

        public Visibility ErrorVisibility { get { return this.error == null ? Visibility.Hidden : Visibility.Visible; } }

        public string LinkCaption { get { return linkCaption; } set { linkCaption = value; OnPropertyChanged("LinkCaption"); } }

        public string Caption
        {
            get
            {
                string name = null;
                if (Account != null)
                {
                    name = Account.Name;
                }
                else if (OnlineAccount != null)
                {
                    name = OnlineAccount.Name;
                }
                else if (!string.IsNullOrEmpty(fileName))
                {
                    name = this.fileName;
                }
                if (name == null)
                {
                    name = message;
                }
                return name;
            }
        }

        public ThreadSafeObservableCollection<OfxDownloadData> Children { get { return this.children; } }

        public OfxDownloadData AddError(OnlineAccount oa, Account account, string error)
        {
            OfxDownloadData e = new OfxDownloadData(oa, account, "Error");
            e.Message = error;
            if (!string.IsNullOrEmpty(error))
            {
                e.Error = new OfxException(error);
            }
            e.isError = true;
            children.Add(e);
            return e;
        }

        public OfxDownloadData AddError(OnlineAccount oa, Account account, Exception error)
        {
            OfxDownloadData e = new OfxDownloadData(oa, account, "Error");
            e.Error = error;
            e.isError = true;
            children.Add(e);
            return e;
        }

        public OfxDownloadData AddMessage(OnlineAccount oa, Account account, string msg)
        {
            OfxDownloadData e = new OfxDownloadData(oa, account, msg);
            children.Add(e);
            return e;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(String name)
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

    public class OfxDownloadEventArgs : EventArgs
    {
        ThreadSafeObservableCollection<OfxDownloadData> list;

        public OfxDownloadEventArgs()
        {
            this.list = new ThreadSafeObservableCollection<OfxDownloadData>();
        }

        public ThreadSafeObservableCollection<OfxDownloadData> Entries
        {
            get { return this.list; }
        }

        public OfxDownloadData AddError(OnlineAccount online, Account account, string message)
        {
            OfxDownloadData entry = new OfxDownloadData(online, account, message);
            entry.IsError = true;
            list.Add(entry);
            return entry;
        }

        public OfxDownloadData AddError(OnlineAccount online, string fileName, string message)
        {
            OfxDownloadData entry = new OfxDownloadData(online, fileName, message);
            entry.IsError = true;
            list.Add(entry);
            return entry;
        }

        public OfxDownloadData AddEntry(OnlineAccount online, Account account, string caption)
        {
            OfxDownloadData entry = new OfxDownloadData(online, account, caption);
            list.Add(entry);
            return entry;
        }

    }

    class LogFileInfo
    {
        public string Path { get; set; }
    }

    public class OfxThread
    {

        public event OfxDownloadProgress Status;

        IList list;
        string[] files;
        MyMoney myMoney;
        OfxRequest.PickAccountDelegate resolverWhenMissingAccountId;
        Dispatcher dispatcher;

        public OfxThread(MyMoney myMoney, IList list, string[] files, OfxRequest.PickAccountDelegate resolverWhenMissingAccountId, Dispatcher uiThreadDispatcher)
        {
            this.myMoney = myMoney;
            this.list = list;
            this.files = files;
            this.resolverWhenMissingAccountId = resolverWhenMissingAccountId;
            this.dispatcher = uiThreadDispatcher;
        }

        public void Start()
        {
            if (this.files != null)
            {
                Thread t = new Thread(new ThreadStart(LoadImports));
                t.Start();
            }
            else
            {
                Synchronize();
            }
        }

        internal void Stop()
        {
            List<OfxRequest> snapshot = null;
            lock (pending)
            {
                // need to snapshot to avoid collection changed exceptions
                snapshot = new List<OfxRequest>(pending);
            }

            foreach (var request in snapshot)
            {
                request.Cancel();
            }
        }

        void FireEventOnMainThread(Delegate d, object[] args)
        {
            if (d != null)
            {
                UiDispatcher.BeginInvoke(d, args);
            }
        }

        void LoadImports()
        {
            Thread.CurrentThread.Name = "Synchronize";
            OfxDownloadEventArgs e;
            int count;

            LoadImportsSameThread(out e, out count);

            this.FireEventOnMainThread(this.Status, new object[4] { 0, count + 1, count + 1, e });
        }

        public void LoadImportsSameThread(out OfxDownloadEventArgs e, out int count)
        {
            e = new OfxDownloadEventArgs();

            OfxRequest ofx = new OfxRequest(null, this.myMoney, this.resolverWhenMissingAccountId);
            int i = 0;
            count = this.files.Length;
            foreach (string fname in this.files)
            {
                i++;
                this.FireEventOnMainThread(this.Status, new object[4] { 0, count + 1, i, e });
                try
                {
                    if (File.Exists(fname))
                    {
                        using (FileStream fs = new FileStream(fname, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            try
                            {
                                XDocument doc = null;
                                if (fname.EndsWith(".xml"))
                                {
                                    doc = LoadXmlStream(fs);
                                    fs.Close();
                                }
                                else
                                {
                                    doc = ofx.ParseOfxResponse(fs, false);
                                    fs.Close();
                                    string file = OfxRequest.SaveLog(doc, Path.GetFileNameWithoutExtension(fname) + ".xml");
                                    doc.AddAnnotation(new LogFileInfo() { Path = file });
                                }
                                OfxDownloadData se = e.AddEntry(null, null, fname);
                                ofx.ProcessResponse(doc, se);
                            }                            
                            catch (OperationCanceledException)
                            {
                                // import cancelled by user.
                                e.AddError(null, fname, "Import cancelled");
                            }
                            catch (Exception ex)
                            {
                                e.AddError(null, fname, "Error loading file").Error = ex;
                            }
                        }

                        // We can now delete the imported file
                        //Settings.ReallyDeleteFile(fname);
                    }
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show("Error opening import file", null, ex.Message, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            this.files = null; // done.
        }

        private static XDocument LoadXmlStream(Stream fs)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.CheckCharacters = false;
#pragma warning disable 618
            settings.ProhibitDtd = false;
#pragma warning restore 618
            settings.ValidationType = ValidationType.None;
            using (XmlReader reader = XmlReader.Create(fs, settings))
            {
                return XDocument.Load(reader);
            }
        }

        OfxDownloadEventArgs downloadEventArgs;
        ConcurrentQueue<OfxDownloadData> work = new ConcurrentQueue<OfxDownloadData>();
        int completed;

        void Synchronize()
        {
            this.downloadEventArgs = new OfxDownloadEventArgs();
            int count = this.list.Count;
            this.completed = 0;
            int i = 0;
            for (i = 0; i < count; i++)
            {
                OnlineAccount oa = this.list[i] as OnlineAccount;
                OfxDownloadData f = downloadEventArgs.AddEntry(oa, null, null);
                f.Index = i;
                f.IsDownloading = true;
                work.Enqueue(f);
            }

            // send empty start event
            FireEventOnMainThread(this.Status, new object[4] { 0, list.Count, 0, downloadEventArgs });

            // start one thread for each online account to download so we can do them all at once,
            // this doesn't need Parallel.foreach because we don't need any throttling because we
            // know bulk of the work is on the server and so most of the time spent in these threads
            // is spent waiting on the server.
            for (i = 0; i < count; i++)
            {
                Thread t = new Thread(new ThreadStart(SyncAccount));
                t.Start();
            }
        }

        List<OfxRequest> pending = new List<OfxRequest>();

        void SyncAccount()
        {
            Thread.CurrentThread.Name = "Synchronize";

            OfxDownloadData f = null;
            if (!work.TryDequeue(out f))
            {
                return;
            }

            int count = this.list.Count;
            int i = f.Index;
            OnlineAccount oa = f.OnlineAccount;
            OfxRequest request = null;
            try
            {
                f.IsDownloading = true;

                FireEventOnMainThread(this.Status, new object[4] { 0, count + 1, i + 1, this.downloadEventArgs });

                request = new OfxRequest(oa, this.myMoney, this.resolverWhenMissingAccountId);

                lock (pending)
                {
                    pending.Add(request);
                }

                ArrayList accounts = new ArrayList();
                foreach (Account a in this.myMoney.Accounts.GetAccounts())
                {
                    if (a.OnlineAccount == oa)
                    {
                        accounts.Add(a);
                    }
                }
                if (accounts.Count == 0)
                {
                    f.Message = "Skipped because no accounts are associated with this online account.";
                    f.IsError = true;
                    f.IsDownloading = false;
                }
                else
                {
                    request.Sync(accounts, f, dispatcher);
                    f.Success = true;
                }
            }
            catch (Exception ex)
            {
                f.AddError(oa, null, ex);
            }
            finally
            {
                f.IsDownloading = false;

                if (request != null)
                {
                    lock (pending)
                    {
                        pending.Remove(request);
                    }
                }
            }

            bool last = false;
            lock (work)
            {
                this.completed++;
                if (completed == count)
                {
                    last = true;
                }
            }
            if (last)
            {
                // notify that we're done.
                this.FireEventOnMainThread(this.Status, new object[4] { 0, count + 1, count + 1, this.downloadEventArgs });
            }
            else
            {
                FireEventOnMainThread(this.Status, new object[4] { 0, count + 1, i + 1, this.downloadEventArgs });
            }
        }


    }

    public class OfxRequest
    {
        OnlineAccount onlineAccount;
        Account account;
        MyMoney myMoney;
        Hashtable truidMap = new Hashtable(); // trnuid -> Account
        DateTime start;
        Dictionary<string, SecurityInfo> securityInfo; // uniqueId -> SecurityInfo.
        PickAccountDelegate callerPickAccount;
        HashSet<string> skippedAccounts = new HashSet<string>();

        public OfxRequest(OnlineAccount oa, MyMoney m, PickAccountDelegate resolveMissingAccountId)
        {
            this.onlineAccount = oa;
            this.myMoney = m;
            this.callerPickAccount = resolveMissingAccountId;
        }

        private static string ofxLogPath;

        internal static string OfxLogPath
        {
            get
            {
                if (ofxLogPath == null)
                {
                    string startupPath = ProcessHelper.StartupPath;
                    ofxLogPath = System.IO.Path.Combine(startupPath, "OfxLogs");
                }
                EnsurePathExists(ofxLogPath);
                return ofxLogPath;
            }
            set
            {
                ofxLogPath = value;
            }
        }

        public OnlineAccount OnlineAccount
        {
            get { return this.onlineAccount; }
        }

        public Account Account
        {
            get { return this.account; }
        }

        internal static string GetIsoDateTime(DateTime dt)
        {
            DateTime gmt = dt.ToUniversalTime();
            string isodate = GetIsoDate(gmt) + gmt.Hour.ToString("D2") + gmt.Minute.ToString("D2") + gmt.Second.ToString("D2");
            return isodate;
        }

        static string GetIsoDate(DateTime dt)
        {
            return dt.Year.ToString() + dt.Month.ToString("D2") + dt.Day.ToString("D2");
        }

        enum OfxRequestType
        {
            BankRequest,
            CreditRequest,
            InvestmentRequest
        }

        static OfxRequestType GetRequestType(Account a)
        {
            switch (a.Type)
            {
                case AccountType.Checking:
                case AccountType.Savings:
                case AccountType.MoneyMarket:
                case AccountType.CreditLine:
                    return OfxRequestType.BankRequest;
                case AccountType.Credit:
                    return OfxRequestType.CreditRequest;
                case AccountType.Brokerage:
                case AccountType.Retirement:
                    return OfxRequestType.InvestmentRequest;
                default:
                    throw new OfxException(string.Format("OFX request on {0} account not supported", a.Type.ToString()));
            }
        }

        internal XDocument GetSignonRequest(bool useUserKey)
        {
            bool anonymous = string.IsNullOrEmpty(this.onlineAccount.UserId);

            XElement signonrequest = new XElement("SONRQ", new XElement("DTCLIENT", GetIsoDateTime(DateTime.Now)));

            if (string.IsNullOrWhiteSpace(this.onlineAccount.UserKey))
            {
                useUserKey = false;
            }
            else
            {
                if (this.onlineAccount.UserKeyExpireDate.HasValue && DateTime.Today >= this.onlineAccount.UserKeyExpireDate.Value)
                {
                    // expired
                    this.onlineAccount.UserKey = null;
                    useUserKey = false;
                }
            }

            if (useUserKey)
            {
                signonrequest.Add(new XElement("USERKEY", this.onlineAccount.UserKey));
            }
            else
            {
                signonrequest.Add(new XElement("USERID", anonymous ? "anonymous00000000000000000000000" : this.onlineAccount.UserId));
                signonrequest.Add(new XElement("USERPASS", anonymous ? "anonymous00000000000000000000000" : this.onlineAccount.Password));
            }

            signonrequest.Add(new XElement("LANGUAGE", "ENG"));

            // FI is optional actually.
            if (!string.IsNullOrWhiteSpace(this.onlineAccount.Institution))
            {
                signonrequest.Add(new XElement("FI",
                                    new XElement("ORG", this.onlineAccount.Institution),
                                    new XElement("FID", this.onlineAccount.FID)
                                ));
            }

            if (!string.IsNullOrEmpty(onlineAccount.SessionCookie))
            {
                signonrequest.Add(new XElement("SESSCOOKIE", this.onlineAccount.SessionCookie));
            }

            if (!string.IsNullOrEmpty(this.onlineAccount.AppId))
            {
                signonrequest.Add(new XElement("APPID", this.onlineAccount.AppId));
                signonrequest.Add(new XElement("APPVER", this.onlineAccount.AppVersion));
            }

            if (!string.IsNullOrEmpty(onlineAccount.ClientUid))
            {
                signonrequest.Add(new XElement("CLIENTUID", this.onlineAccount.ClientUid));
            }
            
            if (!string.IsNullOrEmpty(onlineAccount.UserCred1))
            {
                signonrequest.Add(new XElement("USERCRED1", this.onlineAccount.UserCred1));
            }

            if (!string.IsNullOrEmpty(onlineAccount.UserCred2))
            {
                signonrequest.Add(new XElement("USERCRED2", this.onlineAccount.UserCred2));
            }

            if (onlineAccount.MfaChallengeAnswers != null)
            {
                foreach (MfaChallengeAnswer answer in onlineAccount.MfaChallengeAnswers)
                {
                    signonrequest.Add(new XElement("MFACHALLENGEANSWER",
                        new XElement("MFAPRHASEID", answer.Id),
                        new XElement("MFAPHRASEA", answer.Answer)));
                }

                // only use these answers once.
                onlineAccount.MfaChallengeAnswers = null;
            }
            else if (!string.IsNullOrEmpty(onlineAccount.AccessKey))
            {
                signonrequest.Add(new XElement("ACCESSKEY", this.onlineAccount.AccessKey));
            }
            else if (!string.IsNullOrEmpty(onlineAccount.AuthToken))
            {
                signonrequest.Add(new XElement("AUTHTOKEN", this.onlineAccount.AuthToken));
            }

            // Need  ?
            // ACCESSKEY

            XDocument doc = new XDocument(
                new XElement("OFX",
                    new XElement("SIGNONMSGSRQV1",
                        signonrequest
                    )
                )
            );

            return doc;
        }

        XDocument GetSignupRequest()
        {
            XDocument doc = GetSignonRequest(true);

            XElement e = new XElement("SIGNUPMSGSRQV1",
                new XElement("ACCTINFOTRNRQ",
                    new XElement("TRNUID", Guid.NewGuid().ToString()),
                    new XElement("CLTCOOKIE", "1"), // todo: support OFX client cookies
                    new XElement("ACCTINFORQ",
                        new XElement("DTACCTUP", "19700101000000")
                    )
                )
            );

            doc.Root.Add(e);
            return doc;
        }

        XElement GetBankRequest(IList accounts)
        {

            XElement br = new XElement("BANKMSGSRQV1");
            this.start = DateTime.MinValue;

            int cookie = 1; // todo: support OFX client cookies.
            foreach (Account a in accounts)
            {
                string truid = Guid.NewGuid().ToString();
                truidMap[truid] = a;
                DateTime dt = GetStatementRequestRange(a);

                XElement from = new XElement("BANKACCTFROM",
                            new XElement("BANKID", this.onlineAccount.BankId));
                if (!string.IsNullOrEmpty(this.onlineAccount.BranchId))
                {
                    from.Add(new XElement("BRANCHID", this.onlineAccount.BranchId));
                }
                from.Add(new XElement("ACCTID", a.OfxAccountId));
                from.Add(new XElement("ACCTTYPE", GetOfxType(a.Type)));

                XElement sr = new XElement("STMTTRNRQ",
                    new XElement("TRNUID", truid),
                    new XElement("CLTCOOKIE", cookie.ToString()),
                    new XElement("STMTRQ",
                        from,
                        new XElement("INCTRAN",
                            new XElement("DTSTART", GetIsoDate(dt)),
                            new XElement("INCLUDE", "Y")
                        )
                    )
                );

                br.Add(sr);
            }
            return br;
        }

        /// <summary>
        /// Very similar structure to GetBankRequest, just different XML element names at various levels.
        /// </summary>
        XElement GetCreditRequest(IList accounts)
        {
            XElement cr = new XElement("CREDITCARDMSGSRQV1");
            this.start = DateTime.MinValue;

            int cookie = 1; // todo: support OFX client cookies.
            foreach (Account a in accounts)
            {
                string truid = Guid.NewGuid().ToString();
                truidMap[truid] = a;
                DateTime dt = GetStatementRequestRange(a);

                XElement csr = new XElement("CCSTMTTRNRQ",
                    new XElement("TRNUID", truid),
                    new XElement("CLTCOOKIE", cookie.ToString()),
                    new XElement("CCSTMTRQ",
                        new XElement("CCACCTFROM",
                            new XElement("ACCTID", a.OfxAccountId)
                        ),
                        new XElement("INCTRAN",
                            new XElement("DTSTART", GetIsoDate(dt)),
                            new XElement("INCLUDE", "Y")
                        )
                    )
                );

                cr.Add(csr);
            }
            return cr;
        }

        /// <summary>
        /// Figure out what date range to ask for.
        /// </summary>
        DateTime GetStatementRequestRange(Account a)
        {
            DateTime dt = a.LastSync;
            if (dt == DateTime.MinValue)
            {
                dt = DateTime.Today.AddDays(-30); // default range
            }
            else
            {
                // fudge ractor in case new transactions show up for some reason
                dt = dt.AddDays(-10);
            }
            // remember how far back we went (needed to make merging work properly).
            if (this.start == DateTime.MinValue || dt < this.start)
            {
                this.start = dt;
            }
            return dt;
        }


        XElement GetInvestmentRequest(IList accounts)
        {
            XElement br = new XElement("INVSTMTMSGSRQV1");
            this.start = DateTime.MinValue;

            int cookie = 1; // todo: support OFX client cookies.
            foreach (Account a in accounts)
            {
                string truid = Guid.NewGuid().ToString();
                truidMap[truid] = a;
                DateTime dt = GetStatementRequestRange(a);

                XElement sr = new XElement("INVSTMTTRNRQ",
                    new XElement("TRNUID", truid),
                    new XElement("CLTCOOKIE", cookie.ToString()),
                    new XElement("INVSTMTRQ",
                        new XElement("INVACCTFROM",
                            new XElement("BROKERID", this.onlineAccount.BrokerId),
                            new XElement("ACCTID", a.OfxAccountId)
                        ),
                        new XElement("INCTRAN",
                            new XElement("DTSTART", GetIsoDate(dt)),
                            new XElement("INCLUDE", "Y")
                        ),
                        new XElement("INCOO", "N"),
                        new XElement("INCPOS",
                            new XElement("INCLUDE", "N")),
                        new XElement("INCBAL", "Y")
                    )
                );

                br.Add(sr);
            }
            return br;
        }

        static string GetOfxType(AccountType t)
        {
            switch (t)
            {
                case AccountType.Checking:
                    return "CHECKING";
                case AccountType.Credit:
                case AccountType.CreditLine:
                    return "CREDITLINE";
                case AccountType.Savings:
                    return "SAVINGS";
                case AccountType.MoneyMarket:
                    return "MONEYMRKT";
                case AccountType.Brokerage:
                    return "MONEYMRKT";
            }
            throw new InvalidOperationException("Should not be trying to use this account type here");
        }


        string FormatRequest(XDocument doc, string oldFileUid, string newFileUid, out Encoding encoding)
        {
            bool version2 = this.onlineAccount.OfxVersion != null && this.onlineAccount.OfxVersion.StartsWith("2");

            string body = null;

            if (string.IsNullOrEmpty(oldFileUid))
            {
                oldFileUid = "NONE";
            }

            if (!version2)
            {
                using (StringWriter sw = new StringWriter())
                {
                    WriteOfxVersion1Format(sw, doc.Root);
                    body = sw.ToString();
                }
            }
            else
            {
                using (StringWriter sw = new StringWriter())
                {
                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Indent = true;
                    settings.Encoding = Encoding.Unicode;
                    settings.OmitXmlDeclaration = true;
                    if (!(doc.FirstNode is XProcessingInstruction))
                    {
                        string versionInfo = "";
                        if (doc.Descendants("SONRQ").Any())
                        {
                            versionInfo = string.Format("OFXHEADER='200' VERSION='211' SECURITY='NONE' OLDFILEUID='NONE' NEWFILEUID='NONE'");
                        }
                        else
                        {
                            versionInfo = string.Format("OFXHEADER='200' VERSION='211' SECURITY='NONE' OLDFILEUID='{0}' NEWFILEUID='{1}'", oldFileUid, newFileUid);
                        }
                        doc.AddFirst(new XProcessingInstruction("OFX", versionInfo));
                    }
                    using (XmlWriter xw = XmlWriter.Create(sw, settings))
                    {
                        doc.WriteTo(xw);
                        xw.Close();
                    }
                    body = sw.ToString();
                }
            }

            string header = null;

            if (version2)
            {
                //header = "<?xml version='1.0' encoding='utf-8'?>\r\n" + body;
                header = "";
                encoding = Encoding.UTF8;
            }
            else
            {
                encoding = Encoding.GetEncoding(1252);
                header = string.Format(@"OFXHEADER:100
DATA:OFXSGML
VERSION:102
SECURITY:NONE
ENCODING:USASCII
CHARSET:1252
COMPRESSION:NONE
OLDFILEUID:{0}
NEWFILEUID:{1}

", oldFileUid, newFileUid);
            }

            return header + body;
        }

        private void WriteOfxVersion1Format(TextWriter writer, XElement e)
        {
            writer.Write("<" + e.Name.LocalName);
            foreach (XAttribute a in e.Attributes())
            {
                string value = a.Value;
                writer.Write(" " + a.Name.LocalName + "=\"");
                foreach (char c in value)
                {
                    if (c == '"')
                    {
                        writer.Write("&quot;");
                    }
                    else
                    {
                        writer.Write(c);
                    }
                }
                writer.Write("\"");
            }
            writer.Write(">");

            if (e.HasElements)
            {
                writer.WriteLine();
                foreach (XElement child in e.Elements())
                {
                    WriteOfxVersion1Format(writer, child);
                }
                writer.WriteLine("</" + e.Name.LocalName + ">");
            }
            else
            {
                writer.WriteLine(e.Value);
            }
        }

        HttpWebRequest pending;

        public void Cancel()
        {
            if (pending != null)
            {
                pending.Abort();
            }
        }

        public static string GetUserAgent(OnlineAccount account)
        {
            if (!string.IsNullOrEmpty(account.AppId))
            {
                return account.AppId + " " + account.AppVersion;
            }
            else
            {
                return "MYMONEY 0100";
            }
        }

        XDocument SendOfxRequest(XDocument doc, string oldFileUid, string newFileUid)
        {
            Encoding encoding = null;
            string msg = FormatRequest(doc, oldFileUid, newFileUid, out encoding);

            string url = this.onlineAccount.Ofx;
            if (!url.StartsWith("https://") && !url.StartsWith("http://"))
            {
                url = "https://" + url;
            }

            //SecurityProtocolType.Tls

            Uri uri = new Uri(url);
            pending = (HttpWebRequest)WebRequest.Create(uri);

            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(AllwaysGoodCertificate);

            pending.Method = "POST";
            pending.ContentType = "application/x-ofx";
            pending.Accept = "application/x-ofx";
            pending.Expect = string.Empty;
            pending.ServicePoint.Expect100Continue = false;
            pending.AllowAutoRedirect = true;
            pending.UserAgent = GetUserAgent(this.onlineAccount);

            // Discover doesn't like these headers
            if (this.onlineAccount.FID == "7101")
            {
                pending.UserAgent = null;
                pending.Accept = null;
                pending.KeepAlive = false;
            }

            byte[] data = encoding.GetBytes(msg);
            pending.ContentLength = data.Length;
            Stream stm = pending.GetRequestStream();
            stm.Write(data, 0, data.Length);
            stm.Close();


            HttpWebResponse resp = null;
            try
            {
                resp = (HttpWebResponse)pending.GetResponse();
                if (resp.StatusCode != HttpStatusCode.OK)
                {
                    string error = resp.StatusDescription;
                    string headers = GetHttpHeaders(resp);
                    throw new OfxException(resp.StatusDescription, resp.StatusCode.ToString(), GetResponseBody(resp), headers);
                }
                return ParseOfxResponse(resp.GetResponseStream(), true);
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    resp = (HttpWebResponse)e.Response;
                    string headers = GetHttpHeaders(resp);
                    throw new OfxException(e.Message, resp.StatusDescription, GetResponseBody(resp), headers);
                }
                throw e;
            }
            finally
            {
                if (resp != null) resp.Close();
                pending = null;
            }
        }

        private bool AllwaysGoodCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private string RemoveIndents(string msg)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string line in msg.Split('\n'))
            {
                sb.Append(line.Trim());
                sb.Append("\n");
            }
            return sb.ToString();
        }

        private static string GetHttpHeaders(HttpWebResponse response)
        {
            string headers = string.Empty;
            WebHeaderCollection col = response.Headers;
            foreach (string key in col.AllKeys)
            {
                headers += key + "=" + col[key] + "<br/>";
            }
            return headers;
        }

        static string GetResponseBody(HttpWebResponse resp)
        {
            try
            {
                using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                {
                    return sr.ReadToEnd();
                }
            }
            catch
            {
                return null;
            }
        }

        static void EnsurePathExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        internal XDocument SendOfxRequest(XDocument doc)
        {
            XDocument result = null;
            string fileuid = Guid.NewGuid().ToString();

            // try both version 1 and 2 just in case the online account information we have is wrong.
            try
            {
                result = SendOfxRequest(doc, "NONE", fileuid);
            }
            catch (HtmlResponseException)
            {
                throw;
            }
            catch (Exception)
            {
                // try a different version of OFX
                string version = "" + this.onlineAccount.OfxVersion;
                version = version.Trim();
                this.onlineAccount.OfxVersion = version.StartsWith("2") ? "1" : "2";
                result = SendOfxRequest(doc, "NONE", fileuid);
            }

            return result;
        }

        private int GetSignOnStatusCode(XElement ofx)
        {
            XElement signOnMsgResponse = ofx.Element("SIGNONMSGSRSV1");
            if (signOnMsgResponse != null)
            {
                XElement sonOnResponse = signOnMsgResponse.Element("SONRS");
                if (sonOnResponse != null)
                {
                    XElement status = sonOnResponse.Element("STATUS");
                    if (status != null)
                    {
                        XElement code = sonOnResponse.Element("CODE");
                        if (code != null)
                        {
                            int i = 0;
                            if (int.TryParse(code.Value, out i))
                            {
                                return i;
                            }
                        }
                    }
                }
            }
            return -1;
        }

        public static OFX LoadCachedProfile(MyMoney money, OnlineAccount oa)
        {
            string profilePath = Path.Combine(OfxLogPath, OfxRequest.GetLogfileName(money, oa) + "PROF_RS.xml");
            if (File.Exists(profilePath))
            {
                try
                {
                    XDocument doc = XDocument.Load(profilePath);
                    OFX ofx = DeserializeOfxResponse(doc);
                    return ofx;
                }
                catch
                {
                }
            }
            return null;
        }

        /// <summary>
        /// Find out what the OFX server supports.
        /// </summary>
        /// <param name="oa">Online account to query</param>
        /// <returns></returns>
        public ProfileResponseMessageSet GetProfile(OnlineAccount oa, out string cachePath)
        {
            this.onlineAccount = oa;

            DateTime lastGetProfileDate = new DateTime(1970, 1, 1);
            string cache = Path.Combine(OfxLogPath, GetLogfileName(this.onlineAccount) + "PROF_RS.xml");
            if (File.Exists(cache))
            {
                lastGetProfileDate = File.GetLastWriteTime(cache);
            }
            cachePath = cache;

            // get profile must not have a CLIENTUID.
            this.onlineAccount.ClientUid = null;

            XDocument doc = this.GetProfileRequest(lastGetProfileDate);
            SaveLog(doc, GetLogfileName(this.onlineAccount) + "PROF_RQ.xml");

            OFX ofx = null;
            try
            {
                doc = SendOfxRequest(doc);
                // deserialize response into our OfxProfile structure.
                ofx = DeserializeOfxResponse(doc);

                CheckSignOnStatusError(ofx);
            }
            catch (Exception ex)
            {
                if (File.Exists(cache))
                {
                    // then return the cached profile.
                    doc = XDocument.Load(cache);
                    ofx = DeserializeOfxResponse(doc);
                }
                else
                {
                    throw ex;
                }
            }


            if (ofx.ProfileMessageSet == null || ofx.ProfileMessageSet.ProfileMessageResponse == null)
            {
                throw new OfxException("Missing profile response");
            }

            var msgset = ofx.ProfileMessageSet;
            var profileStatus = msgset.ProfileMessageResponse.OfxStatus;
            if (profileStatus == null)
            {
                throw new OfxException("Missing profile response status");
            }

            int statusCode = profileStatus.Code;
            if (statusCode == 1)
            {
                // then latest profile is up to date.
                doc = XDocument.Load(cache);
                ofx = DeserializeOfxResponse(doc);
            }
            else
            {
                SaveLog(doc, Path.GetFileName(cache));
            }

            if (msgset.ProfileMessageResponse != null)
            {
                var profile = msgset.ProfileMessageResponse.OfxProfile;
                if (profile != null && profile.OfxSignOnInfoList != null && profile.OfxSignOnInfoList.OfxSignOnInfo != null)
                {
                    foreach (OfxSignOnInfo info in profile.OfxSignOnInfoList.OfxSignOnInfo)
                    {
                        if (info.ClientUidRequired.ConvertYesNoToBoolean())
                        {
                            this.onlineAccount.ClientUid = Guid.NewGuid().ToString();
                        }
                    }
                }
            }

            return ofx.ProfileMessageSet;
        }

        private string GetLogfileName(Data.OnlineAccount onlineAccount)
        {
            return OfxRequest.GetLogfileName(this.myMoney, onlineAccount);
        }

        internal static string GetLogfileName(MyMoney money, OnlineAccount oa)
        {
            int index = 0;
            string fname = oa.ValidFileName;

            // logfile name has to be unique so parallel download doesn't stomp on itself.
            foreach (OnlineAccount other in money.OnlineAccounts)
            {
                if (other.ValidFileName == oa.ValidFileName)
                {
                    index++;
                }
                if (other == oa)
                {
                    return (index <= 1) ? fname : fname + index;
                }
            }

            return fname;
        }


        internal void ChangePassword(OnlineAccount onlineAccount, string newPassword, out string outputLogFile)
        {
            this.onlineAccount = onlineAccount;

            XDocument doc = this.GetPinChangeRequest(newPassword);

            SaveLog(doc, Path.Combine(OfxLogPath, GetLogfileName(this.onlineAccount) + "_PINCHRQ.xml"));

            doc = SendOfxRequest(doc);

            outputLogFile = SaveLog(doc, GetLogfileName(this.onlineAccount) + "_PINCHRS.xml");

            // deserialize response into our OfxProfile structure.
            OFX ofx = OFX.Deserialize(doc);

            CheckSignOnStatusError(ofx);

            var somr = ofx.SignOnMessageResponse;
            if (somr != null)
            {

                // somr.SignOnResponse
                var pcrt = somr.PinChangeResponseTransaction;
                if (pcrt != null)
                {
                    var status = pcrt.OfxStatus;
                    if (status != null)
                    {
                        OfxErrorCode ec = (OfxErrorCode)status.Code;
                        if (status.Code != 0)
                        {
                            throw new OfxException(status.Message, ec.ToString(), doc.ToString(), null)
                            {
                                OfxError = ec,
                                Root = ofx
                            };
                        }
                    }

                    var response = pcrt.PinChangeResponse;
                    if (response != null)
                    {
                        if (response.UserId == this.onlineAccount.UserId)
                        {
                            // we're good
                            return;
                        }
                        else
                        {
                            throw new OfxException(string.Format("Password change request returned unexpected userid '{0}'", response.UserId));
                        }
                    }
                }
            }

            throw new OfxException("Unexpected response from change password request", null, doc.ToString(), null);
        }

        private XDocument GetPinChangeRequest(string newPassword)
        {
            XDocument doc = GetSignonRequest(false);

            XElement msgSet = doc.Root.Element("SIGNONMSGSRQV1");

            XElement req = new XElement("PINCHTRNRQ",
                        new XElement("TRNUID", Guid.NewGuid().ToString()),
                        new XElement("PINCHRQ",
                            new XElement("USERID", this.onlineAccount.UserId),
                            new XElement("NEWUSERPASS", newPassword)));

            msgSet.Add(req);

            return doc;
        }

        internal static OFX DeserializeOfxResponse(XDocument doc)
        {
            try
            {
                OFX ofx;
                XmlSerializer s = new XmlSerializer(typeof(OFX));
                using (XmlReader r = XmlReader.Create(new StringReader(doc.ToString())))
                {
                    ofx = (OFX)s.Deserialize(r);
                }
                return ofx;
            }
            catch
            {
                throw new OfxException("Error parsing OFX response", "Error", doc.ToString(), null);
            }
        }

        private XDocument GetProfileRequest(DateTime lastProfileRequest)
        {
            XDocument doc = GetSignonRequest(false);

            XElement profreq = new XElement("PROFMSGSRQV1",
                    new XElement("PROFTRNRQ",
                        new XElement("TRNUID", Guid.NewGuid().ToString()),
                        new XElement("PROFRQ",
                            new XElement("CLIENTROUTING", "MSGSET"),
                            new XElement("DTPROFUP", GetIsoDateTime(lastProfileRequest)))));

            doc.Root.Add(profreq);

            return doc;
        }

        public OFX Signup(OnlineAccount oa, out string rslog)
        {
            rslog = null;
            OFX result = null;
            Exception e = null;
            try
            {
                result = InternalSignup(oa, out rslog);
            }
            catch (OfxException oe)
            {
                if (oe.Root != null)
                {
                    throw;
                }
                e = oe;
            }
            catch (Exception ex)
            {
                e = ex;
            }

            if (e != null)
            {
                Debug.WriteLine(e.Message);
                // try a different version of OFX
                string version = oa.OfxVersion.Trim();
                oa.OfxVersion = version.StartsWith("2") ? "1" : "2";
                try
                {
                    result = InternalSignup(oa, out rslog);
                }
                catch (Exception)
                {
                    // still didn't work, so put it back the way it was
                    oa.OfxVersion = version;
                    throw;
                }
            }

            return result;
        }

        public static OfxSignOnInfo GetSignonInfo(MyMoney money, OnlineAccount oa)
        {
            var ofxProfile = LoadCachedProfile(money, oa);
            if (ofxProfile != null && ofxProfile.ProfileMessageSet != null)
            {
                ProfileMessageResponse msgResponse = ofxProfile.ProfileMessageSet.ProfileMessageResponse;
                if (msgResponse != null)
                {
                    ProfileResponse response = msgResponse.OfxProfile;
                    if (response != null)
                    {
                        OfxSignOnInfoList infoList = response.OfxSignOnInfoList;
                        if (infoList != null)
                        {
                            OfxSignOnInfo[] infos = infoList.OfxSignOnInfo;
                            if (infos.Length > 0)
                            {
                                return infos[0];
                            }
                        }
                    }
                }
            }
            return null;
        }

        private OFX InternalSignup(OnlineAccount oa, out string rslog)
        {
            this.onlineAccount = oa;

            XDocument doc = this.GetSignupRequest();
            SaveLog(doc, GetLogfileName(this.onlineAccount) + "SIGNUP_RQ.xml");

            string fileuid = Guid.NewGuid().ToString();
            doc = SendOfxRequest(doc, "NONE", fileuid);

            rslog = SaveLog(doc, GetLogfileName(this.onlineAccount) + "SIGNUP_RS.xml");

            OFX ofx = DeserializeOfxResponse(doc);

            OfxException e = GetSignOnStatusError(ofx);

            var status = ofx?.SignUpMessageResponse?.AccountInfoSet?.OfxStatus;
            if (status != null && status.Code != 0)
            {
                string sev = status.Severity ?? "Error";
                OfxErrorCode code = (OfxErrorCode)status.Code;
                string message = status.Message ?? string.Format("Sign up failed with {0} code {1}({2})", sev, code.ToString(), status.Code);

                if (e != null)
                {
                    message += "\n" + e.Message;
                }

                e = new OfxException(message, code.ToString(), null, null)
                {
                    Root = ofx,
                    OfxError = (OfxErrorCode)status.Code
                };
            }
            if (e != null)
            {
                throw e;
            }

            return ofx;
        }

        internal OfxException GetSignOnStatusError(OFX ofx)
        {
            var sms = ofx.SignOnMessageResponse;
            if (sms != null)
            {
                var sor = sms.SignOnResponse;
                if (sor != null)
                {
                    // store the session cookie for next request.
                    onlineAccount.SessionCookie = sor.SessionCookie;

                    // store any AccessKey that is returned.
                    if (!string.IsNullOrEmpty(sor.AccessKey))
                    {
                        onlineAccount.AccessKey = sor.AccessKey;
                    }

                    // store any UserKey that is returned.
                    if (!string.IsNullOrEmpty(sor.UserKey))
                    {
                        onlineAccount.UserKey = sor.UserKey;
                    }

                    var status = sor.OfxStatus;
                    if (status != null && status.Code != 0)
                    {
                        string sev = status.Severity ?? "Error";
                        OfxErrorCode code = (OfxErrorCode)status.Code;
                        string message = status.Message ?? string.Format("Sign on failed with {0} code {1}({2})", sev, code.ToString(), status.Code);

                        return new OfxException(message, code.ToString(), null, null)
                        {
                            Root = ofx,
                            OfxError = (OfxErrorCode)status.Code
                        };
                    }
                }
            }
            return null;
        }

        internal void CheckSignOnStatusError(OFX ofx)
        {
            var e = GetSignOnStatusError(ofx);
            if (e != null)
            {
                throw e;
            }
        }

        public void Sync(IList accounts, OfxDownloadData results, Dispatcher dispatcher)
        {
            if (accounts.Count == 0) return;
            
            XDocument doc = GetSignonRequest(true);

            // batch up accounts by type
            Dictionary<OfxRequestType, List<Account>> sorted = new Dictionary<OfxRequestType, List<Account>>();

            foreach (Account a in accounts)
            {
                OfxRequestType rt = GetRequestType(a);
                List<Account> list = null;
                if (!sorted.TryGetValue(rt, out list))
                {
                    list = new List<Account>();
                    sorted[rt] = list;
                }
                list.Add(a);
            }

            foreach (var pair in sorted)
            {
                var rt = pair.Key;
                var list = pair.Value.ToArray();
                this.account = list[0]; // only used for error reporting.
                XElement e = null;
                switch (rt)
                {
                    case OfxRequestType.BankRequest:
                        e = GetBankRequest(list);
                        break;
                    case OfxRequestType.CreditRequest:
                        e = GetCreditRequest(list);
                        break;
                    case OfxRequestType.InvestmentRequest:
                        e = GetInvestmentRequest(list);
                        break;
                }

                doc.Root.Add(e);
            }

            SaveLog(doc, GetLogfileName(this.onlineAccount) + "RQ.xml");

            Guid guid = Guid.NewGuid();
            string fileuid = guid.ToString();
            doc = SendOfxRequest(doc, this.account.SyncGuid.IsNull ? null : this.account.SyncGuid.ToString(), fileuid);
       
            SaveLog(doc, GetLogfileName(this.onlineAccount) + "RS.xml");

            dispatcher.BeginInvoke(new Action(() =>
            {
                // do this on the UI thread so we don't have to worry about parallel access to the Money object.
                try
                {
                    foreach (Account a in accounts)
                    {
                        a.SyncGuid = guid;
                    }

                    this.ProcessResponse(doc, results);
                }
                catch (OperationCanceledException)
                {
                    // import cancelled by user.
                    results.AddError(this.onlineAccount, null, "Sync cancelled");
                }
                catch (Exception ex)
                {
                    Account ea = null;
                    OfxException oe = ex as OfxException;
                    if (oe != null)
                    {
                        ea = oe.Account;
                    }
                    results.AddError(this.onlineAccount, ea, ex);
                }
            }));
        }

        public XDocument ParseOfxResponse(Stream stm, bool implementSecurity)
        {
            if (!stm.CanSeek)
            {
                stm = CopyToMemoryStream(stm);
            }

            XDocument doc = null;
            Encoding enc = Encoding.ASCII;
            StreamReader sr = new StreamReader(stm, enc);
            string content = sr.ReadToEnd();
            if (content.StartsWith("<?xml"))
            {
                Regex re = new Regex("encoding\\=[\\'\\\"]([^\\'\\\"]*)[\\'\\\"]");
                string test = "<?xml version='1.0' encoding='utf-8'?>";
                var m = re.Match(test);
                if (m.Success && m.Groups.Count == 2)
                {
                    string encoding = m.Groups[1].Value;
                    if (!string.IsNullOrEmpty(encoding))
                    {
                        try
                        {
                            enc = Encoding.GetEncoding(encoding);
                            // re-decode the content.
                            stm.Seek(0, SeekOrigin.Begin);
                            sr = new StreamReader(stm, enc);
                            content = sr.ReadToEnd();
                        }
                        catch
                        {
                            // make do with ASCII then...
                        }
                    }
                }

                // Fix bug in Fidelity OFX 2.0 response.
                content = content.Replace("?<OFX>", "?><OFX>");

                try
                {
                    doc = XDocument.Parse(content);
                    return doc;
                }
                catch
                {
                    // then let's try and parse it as SGML.
                }
            }

            stm.Seek(0, SeekOrigin.Begin);
            sr = new StreamReader(stm, enc);
            string line = null;
            bool justWhitespace = true;
            int headerLines = 0;

            // First just look for a CHARSET header so we can re-decode the content properly.
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();

                if (line.StartsWith("<html>", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase))
                {
                    throw new HtmlResponseException("Unexpected HTML content found in Ofx response", content);
                }

                if (line.StartsWith("<OFX>") || line.StartsWith("<?OFX"))
                {
                    break;
                }
                int pos = line.IndexOf("<OFX>");
                if (pos > 0)
                {
                    // invalid response format, the <OFX> tag should be on it's own line!
                    line = line.Substring(0, pos);
                }
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (justWhitespace)
                    {
                        headerLines++;
                        // ignore beginning newlines.
                    }
                    else
                    {
                        break; // end of header
                    }
                }
                else
                {
                    headerLines++;

                    justWhitespace = false;
                    bool usascii = false;

                    // process 1.0 header 
                    string[] hp = line.Split(':');
                    if (hp.Length == 2)
                    {
                        string key = hp[0];
                        string value = hp[1].Trim();

                        switch (key)
                        {
                            case "OFXHEADER":
                                if (string.Compare("100", value, StringComparison.OrdinalIgnoreCase) != 0)
                                {
                                    throw new OfxException("Unsupported OFX Header Version : " + value + " found in Ofx response");
                                }
                                break;
                            case "DATA":
                                if (string.Compare("OFXSGML", value, StringComparison.OrdinalIgnoreCase) != 0)
                                {
                                    throw new OfxException("Unsupported Data type: " + value + " found in Ofx response");
                                }
                                break;
                            case "VERSION":
                                int version = 0;
                                int.TryParse(value, out version);
                                if (version < 100 || version > 200)
                                {
                                    throw new OfxException("Unsupported OFX Version : " + value + " found in Ofx response");
                                }
                                break;
                            case "SECURITY":
                                if (implementSecurity && string.Compare("NONE", value, StringComparison.OrdinalIgnoreCase) != 0)
                                {
                                    throw new OfxException("Unsupported OFX Security protocol: " + value + " found in Ofx response");
                                }
                                break;
                            case "ENCODING":
                                // BUGBUG: how do we handle UNICODE in SGML files?  Do we need to re-decode the stream as UNICODE?
                                if (string.Compare("USASCII", value, StringComparison.OrdinalIgnoreCase) == 0)
                                {
                                    usascii = true;
                                }
                                else 
                                {
                                    try
                                    {
                                        enc = Encoding.GetEncoding(value);
                                    }
                                    catch
                                    {
                                        throw new OfxException("Unsupported encoding: " + value + " found in Ofx response");
                                    }
                                }
                                break;
                            case "CHARSET":
                                if (usascii) 
                                {
                                    if (string.Compare("NONE", value, StringComparison.OrdinalIgnoreCase) != 0)
                                    {
                                        try
                                        {
                                            enc = Encoding.GetEncoding(Int32.Parse(value));
                                        }
                                        catch (Exception)
                                        {
                                            throw new OfxException("Unsupported character set: " + value + " found in Ofx response");
                                        }
                                    }
                                }
                                break;
                            case "COMPRESSION":
                                if (string.Compare("NONE", value, StringComparison.OrdinalIgnoreCase) != 0)
                                {
                                    throw new OfxException("Unsupported compression mode: " + value + " found in Ofx response");
                                }
                                break;
                            case "OLDFILEUID":
                                // we can safely ignore these
                                break;
                            case "NEWFILEUID":
                                break;
                        }
                    }
                }
            }


            // re-encode in the right encoding and read up to the starting <OFX> tag.
            stm.Seek(0, SeekOrigin.Begin);
            sr = new StreamReader(stm, enc);
            StringBuilder sb = new StringBuilder();

            // skip the header.
            while ((line = sr.ReadLine()) != null)
            {
                int pos = line.IndexOf("<OFX>");
                if (pos >= 0)
                {
                    // try and salvage an invalid format OFX file.
                    line = line.Substring(pos);
                    sb.AppendLine(line);
                    headerLines = 0;
                }
                else if (headerLines == 0)
                {
                    sb.AppendLine(line);
                }
            }

            using (SgmlReader sgml = new SgmlReader())
            {
                string name = "Walkabout.Ofx.ofx160.dtd";
                StreamReader dtdReader = new StreamReader(typeof(OfxRequest).Assembly.GetManifestResourceStream(name));
                sgml.Dtd = SgmlDtd.Parse(null, "OFX", null, dtdReader, null, null, new NameTable());

                sgml.InputStream = new StringReader(sb.ToString());

                doc = XDocument.Load(sgml);

                //Trim newlines.
                foreach (XNode node in doc.DescendantNodes())
                {
                    XText text = node as XText;
                    if (text != null)
                    {
                        text.Value = text.Value.Trim();
                    }
                }

                sr.Close();
            }
            return doc;
        }

        private static Stream CopyToMemoryStream(Stream stm)
        {
            // need the request in a resettable stream so we can re-encode it.
            MemoryStream mem = new MemoryStream(); // MUST NOT DISPOSE THIS STREAM      
            int size = 64000;
            byte[] buffer = new byte[size];
            int len;
            while ((len = stm.Read(buffer, 0, size)) > 0)
            {
                mem.Write(buffer, 0, len);
            }
            mem.Seek(0, SeekOrigin.Begin);
            stm.Close();
            stm = mem;
            return stm;
        }


        static string GetLogFileLocation(XDocument doc)
        {
            LogFileInfo log = doc.Annotation<LogFileInfo>();
            if (log != null) return log.Path;
            return null;
        }

        public void ProcessResponse(XDocument doc, OfxDownloadData results)
        {
            XElement root = doc.Root;
            if (root == null || root.Name.LocalName != "OFX")
            {
                throw new Exception(string.Format("The response from your bank does not appear to be in the expected 'OFX' format"));
            }
            var ns = "" + root.Name.Namespace;
            if (!string.IsNullOrEmpty(ns))
            {
                throw new Exception(string.Format("The response from your bank does not appear to contain the expected 'OFX' namespace"));
            }
            
            XElement e = doc.SelectExpectedElement("OFX/SIGNONMSGSRSV1/SONRS/STATUS/CODE");

            int statusCode = 0;
            int.TryParse(e.Value.Trim(), out statusCode);
            if (statusCode != 0)
            {
                OfxErrorCode ec = (OfxErrorCode)statusCode;

                string message = OfxStrings.ResourceManager.GetString(ec.ToString());
                if (message == null)
                {
                    message = string.Format("Error {0}({1}) returned from server.", ec.ToString(), statusCode);
                }

                results.AddError(this.OnlineAccount, this.Account, message).OfxError = ec;

                e = doc.SelectElement("OFX/SIGNONMSGSRSV1/SONRS/STATUS/MESSAGE");
                if (e != null)
                {
                    message += e.Value.Trim();
                    results.AddError(this.OnlineAccount, this.Account, message);
                }
                return;
            }

            if (doc.Descendants("CHALLENGERQ").FirstOrDefault() != null ||
                doc.Descendants("CHALLENGERS").FirstOrDefault() != null)
            {
                if (MessageBoxEx.Show(string.Format(@"Unexpected CHALLENGERQ or CHALLENGERS element, not implemented.
Please save the log file '{0}' so we can implement this", GetLogFileLocation(doc)), "CHALLENGERQ", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            if (doc.Descendants("PINCHRQ").FirstOrDefault() != null ||
                doc.Descendants("PINCHRS").FirstOrDefault() != null)
            {
                if (MessageBoxEx.Show(string.Format(@"Unexpected PINCHRQ or PINCHRS found in response and is not implemented.
Please save the log file '{0}' so we can implement this", GetLogFileLocation(doc)), "PINCHRQ", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation) == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            //Load SECLISTMSGSRSV1, if any.
            this.securityInfo = ReadSecurityInfo(doc);

            foreach (XElement child in doc.Root.Elements())
            {
                e = child.SelectElement("*/STATUS/CODE");
                statusCode = 0;
                if (e != null)
                {
                    int.TryParse(e.Value.Trim(), out statusCode);
                }
                if (statusCode != 0)
                {
                    OfxErrorCode ec = (OfxErrorCode)statusCode;
                    string message = OfxStrings.ResourceManager.GetString(ec.ToString());
                    if (message == null)
                    {
                        message = string.Format("Error {0}({1}) returned from server.", ec.ToString(), statusCode);
                    }

                    results.AddError(this.OnlineAccount, this.Account, message).OfxError = ec;

                    e = child.SelectElement("*/STATUS/MESSAGE");
                    if (e != null)
                    {
                        message += e.Value.Trim();
                        results.AddError(this.OnlineAccount, this.Account, message);
                    }
                }
                else
                {
                    this.myMoney.BeginUpdate(this);
                    try
                    {
                        switch (child.Name.LocalName)
                        {
                            case "CREDITCARDMSGSRSV1":
                                ProcessCreditCardResponse(child, results);
                                break;
                            case "BANKMSGSRSV1":
                                ProcessBankResponse(child, results);
                                break;
                            case "INVSTMTMSGSRSV1":
                                ProcessInvestmentResponse(child, results);
                                break;

                        }
                    }
                    finally
                    {
                        this.myMoney.EndUpdate();
                    }
                }
            }
        }

        void ProcessCreditCardResponse(XElement cc, OfxDownloadData results)
        {
            List<Tuple<Account, XElement>> pending = new List<Tuple<Account, XElement>>();

            foreach (XElement tr in cc.Elements("CCSTMTTRNRS"))
            {
                XElement idElement = tr.SelectExpectedElement("TRNUID");
                if (idElement == null)
                {
                    throw new OfxException("TRNUID is missing in the response");
                }
                string id = idElement.Value.Trim();

                // Account can be null if we are loading a .ofx file off disk                
                Account a = (Account)this.truidMap[id];
                if (a != null)
                {
                    if (a.Type != AccountType.Credit)
                    {
                        throw new OfxException(string.Format(Properties.Resources.AccountTypeMismatch, a.Name, "'Credit'")) { Account = a };
                    }
                }

                string status = ProcessStatementStatus(tr, a);
                if (!string.IsNullOrEmpty(status))
                {
                    results.AddError(onlineAccount, a, status);
                    continue; // skip this statement and move on to the next one.
                }

                XElement srs = tr.SelectExpectedElement("CCSTMTRS");
                if (srs == null)
                {
                    results.AddError(onlineAccount, a, "Expected CCSTMTRS element is missing");
                    continue; // skip this statement and move on to the next one.
                }

                if (!CheckUSD(srs, results, a))
                {
                    continue;
                }

                XElement ccard = srs.SelectExpectedElement("CCACCTFROM");
                if (!CheckAccountId(ref a, AccountType.Credit, ccard, results))
                {
                    continue;
                }

                // save this for processing below, after all "CheckAccountId" calls have been made.
                // This gives the user the opportunity to cancel the import if one account doesn't match.
                pending.Add(new Tuple<Account, XElement>(a, srs));

            }

            foreach (var pair in pending)
            {
                ProcessStatement(results, pair.Item1, pair.Item2);
            }
        }


        void ProcessBankResponse(XElement br, OfxDownloadData results)
        {
            List<Tuple<Account, XElement>> pending = new List<Tuple<Account, XElement>>();

            foreach (XElement tr in br.Elements("STMTTRNRS"))
            {
                XElement idElement = tr.SelectExpectedElement("TRNUID");
                if (idElement == null)
                {
                    throw new OfxException("TRNUID is missing in the response");
                }
                string id = idElement.Value.Trim();

                // Account can be null if we are loading a .ofx file off disk                
                Account a = (Account)this.truidMap[id];
                if (a != null)
                {
                    // and we find the account using the account id in the statement.

                    if (a.Type != AccountType.Cash && a.Type != AccountType.Checking && a.Type != AccountType.Savings && a.Type != AccountType.MoneyMarket && a.Type != AccountType.CreditLine)
                    {
                        throw new OfxException(string.Format(Properties.Resources.AccountTypeMismatch, a.Name, "'Cash', 'Checking' or 'Savings' or 'MoneyMarket'"));                        
                    }
                }


                string status = ProcessStatementStatus(tr, a);
                if (!string.IsNullOrEmpty(status))
                {
                    results.AddError(onlineAccount, a, status);
                    continue; // skip this statement and move on to the next one.
                }

                XElement srs = tr.SelectExpectedElement("STMTRS");
                if (srs == null)
                {
                    results.AddError(onlineAccount, a, "Expected STMTRS element is missing");
                    continue; // skip this statement and move on to the next one.
                }

                if (!CheckUSD(srs, results, a))
                {
                    continue;
                }

                XElement bank = srs.SelectExpectedElement("BANKACCTFROM");
                if (!CheckAccountId(ref a, AccountType.Checking, bank, results))
                {
                    continue;
                }

                // save this for processing below, after all "CheckAccountId" calls have been made.
                // This gives the user the opportunity to cancel the import if one account doesn't match.
                pending.Add(new Tuple<Account, XElement>(a, srs));
            }

            foreach (var pair in pending)
            {
                ProcessStatement(results, pair.Item1, pair.Item2);
            }
        }

        void ProcessInvestmentResponse(XElement ir, OfxDownloadData results)
        {
            List<Tuple<Account, XElement>> pending = new List<Tuple<Account, XElement>>();

            foreach (XElement tr in ir.Elements("INVSTMTTRNRS"))
            {
                XElement idElement = tr.SelectExpectedElement("TRNUID");
                if (idElement == null)
                {
                    throw new OfxException("TRNUID is missing in the response");
                }
                string id = idElement.Value.Trim();

                // Account can be null if we are loading a .ofx file off disk                
                Account a = (Account)this.truidMap[id];
                if (a != null)
                {
                    // and we find the account using the account id in the statement.

                    if (a.Type != AccountType.Brokerage &&
                        a.Type != AccountType.Retirement && 
                        a.Type != AccountType.Checking &&
                        a.Type != AccountType.Savings)
                    {
                        throw new OfxException(string.Format(Properties.Resources.AccountTypeMismatch, a.Name, "'Investment'"));
                    }
                }

                string status = ProcessStatementStatus(tr, a);
                if (!string.IsNullOrEmpty(status))
                {
                    results.AddError(onlineAccount, a, status);
                    continue; // skip this statement and move on to the next one.
                }

                XElement srs = tr.SelectExpectedElement("INVSTMTRS");
                if (srs == null)
                {
                    results.AddError(onlineAccount, a, "Expected INVSTMTRS element is missing");
                    continue; // skip this statement and move on to the next one.
                }

                if (!CheckUSD(srs, results, a))
                {
                    continue;
                }

                XElement from = srs.SelectExpectedElement("INVACCTFROM");
                if (!CheckAccountId(ref a, AccountType.Brokerage, from, results))
                {
                    continue;
                }
                
                // save this for processing below, after all "CheckAccountId" calls have been made.
                // This gives the user the opportunity to cancel the import if one account doesn't match.
                pending.Add(new Tuple<Account, XElement>(a, srs));
            }

            foreach (var pair in pending)
            {
                XElement srs = pair.Item2;
                Account a = pair.Item1;
                ProcessInvestmentPositions(srs.SelectElement("INVPOSLIST"));
                ProcessInvestmentTransactionList(a, srs.SelectElement("INVTRANLIST"), results);
                
                // todo: compare this info with our local account info...
                /*
                <INVBAL>
                  <AVAILCASH>0.00</AVAILCASH>
                  <MARGINBALANCE>0.00</MARGINBALANCE>
                  <SHORTBALANCE>0</SHORTBALANCE>
                </INVBAL>
                 */
                
                myMoney.Rebalance(a);
            }

        }

        /// <summary>
        /// Right now all we do is extract the current market prices to update our Securities table, but 
        /// in the future we could also reconcile the UNITS with what we think we are holding.
        /// </summary>
        /// <param name="invPosList"></param>
        void ProcessInvestmentPositions(XElement invPosList)
        {
            /*
                <INVPOSLIST>
                 <POSSTOCK>
                    <INVPOS>
                      <SECID>
                        <UNIQUEID>H0023R105</UNIQUEID>
                        <UNIQUEIDTYPE>CUSIP</UNIQUEIDTYPE>
                      </SECID>
                      <HELDINACCT>CASH</HELDINACCT>
                      <POSTYPE>LONG</POSTYPE>
                      <UNITS>25.00000</UNITS>
                      <UNITPRICE>50.52</UNITPRICE>
                      <MKTVAL>1260.00</MKTVAL>
                      <DTPRICEASOF>20091231000000</DTPRICEASOF>
                    </INVPOS>
                  </POSSTOCK>
                  <POSOTHER>
                    <INVPOS>
                      <SECID>
                        <UNIQUEID>9999227</UNIQUEID>
                        <UNIQUEIDTYPE>CUSIP</UNIQUEIDTYPE>
                      </SECID>
                      <HELDINACCT>OTHER</HELDINACCT>
                      <POSTYPE>LONG</POSTYPE>
                      <UNITS>1385.3700</UNITS>
                      <UNITPRICE>1.00</UNITPRICE>
                      <MKTVAL>1385.37</MKTVAL>
                      <DTPRICEASOF>20091231000000</DTPRICEASOF>
                    </INVPOS>
                  </POSOTHER>
                </INVPOSLIST>
             */

            if (invPosList == null)
            {
                return;
            }

            foreach (XElement e in invPosList.Elements())
            {
                // get the type: POSMF, POSSTOCK, POSDEBT, POSOPT, POSOTHER
                //string type = e.Name.LocalName; // do we care what the type is?

                XElement invPos = e.Element("INVPOS");
                if (invPos != null)
                {
                    Security s = ProcessSecId(invPos.Element("SECID"));
                    decimal price = invPos.SelectElementValueAsDecimal("UNITPRICE");
                    if (price != 0)
                    {
                        s.Price = price;
                    }
                    s.PriceDate = ParseOfxDate(invPos.SelectElementValue("DTPRICEASOF"));
                }
            }
        }

        void ProcessInvestmentTransactionList(Account a, XElement tranList, OfxDownloadData results)
        {
            if (tranList == null) return;

            Transactions register = this.myMoney.Transactions;

            // update last sync date.                
            a.LastSync = DateTime.Today;
            int count = 0;
            int merged = 0;
            Dictionary<long, Transaction> newTransactions = new Dictionary<long, Transaction>();
            List<Transaction> found = new List<Transaction>();
            List<Transaction> added = new List<Transaction>();

            foreach (XElement e in tranList.Elements())
            {
                Transaction t = null;
                switch (e.Name.LocalName)
                {
                    case "BUYOPT":
                    case "BUYDEBT":
                    case "BUYMF":
                    case "BUYOTHER":
                    case "BUYSTOCK":
                        t = ProcessInvestmentBuy(a, e);
                        break;
                    case "CLOSUREOPT": // ignored
                        break;
                    case "INCOME":
                    case "INVEXPENSE":
                    case "RETOFCAP":
                        t = ProcessIncomeExpense(a, e);
                        break;
                    case "INVBANKTRAN":
                        t = ProcessInvestmentBankTransaction(a, e);
                        break;
                    case "JRNLFUND": // ignored
                        break;
                    case "JRNLSEC": // ignored: should this be a transfer?
                        break;
                    case "MARGININTEREST":
                        t = ProcessMarginInterest(a, e);
                        break;
                    case "REINVEST":
                        // REINVEST is a single transaction that contains both income and an investment transaction                        
                        t = ProcessInvestmentBuy(a, e);

                        // create another transaction as a cash deposit to cover this this amount.
                        if (t != null)
                        {
                            t.Category = this.myMoney.Categories.InvestmentReinvest;

                            Transaction q = register.NewTransaction(a);
                            Investment i = q.GetOrCreateInvestment();
                            i.Security = t.InvestmentSecurity;
                            q.Status = TransactionStatus.Electronic;
                            q.Unaccepted = true;
                            q.Date = t.Date;
                            q.Amount = Math.Abs(t.Amount); // deposit.              
                            q.Category = GetIncomeCategory(e);
                            if (q.Category == this.myMoney.Categories.InvestmentDividends)
                            {
                                q.Investment.Type = InvestmentType.Dividend;
                            }
                            found.Add(q);
                        }
                        break;
                    case "SELLDEBT":
                    case "SELLMF":
                    case "SELLOPT":
                    case "SELLOTHER":
                    case "SELLSTOCK":
                        t = ProcessInvestmentSell(a, e);
                        break;
                    case "SPLIT": // record a stock split
                        // todo:
                        break;
                    case "TRANSFER":
                        t = ProcessInvestmentTransfer(a, e);
                        break;
                }

                if (t != null)
                {
                    if (t.Investment != null && t.Investment.UnitPrice == 0 && t.Investment.Security != null)
                    {
                        t.Investment.UnitPrice = t.Investment.Security.Price;  // use the latest value just updated by ProcessInvestmentPositions.
                    }
                    found.Add(t);
                }
            }

            foreach (Transaction f in found)
            {
                Transaction t = f;
                if (t.Investment != null)
                {
                    if (t.Amount == 0 && t.Investment.Units == 0)
                    {
                        // then this transaction has no point because it doesn't change the cash balance or the investment holdings.
                        continue;
                    }

                    Security s = t.Investment.Security;
                    if (s != null)
                    {
                        // set the Payee field to the name of the security
                        string name = s.Name;
                        if (!string.IsNullOrEmpty(name))
                        {
                            // Since we tied the Payee to the security we can also reuse the Alias table
                            // to allow re-nameing of a Security using Payee Alias table.
                            Alias alias = this.myMoney.Aliases.FindMatchingAlias(name);
                            if (alias != null && alias.Payee.Name != name)
                            {
                                if (string.IsNullOrEmpty(t.Memo))
                                {
                                    t.Memo = name;
                                }
                                // then rename the security also.
                                s.Name = alias.Payee.Name;
                                t.Payee = alias.Payee;
                            }
                            else
                            {
                                t.Payee = this.myMoney.Payees.FindPayee(name, true);
                            }
                        }
                    }
                }

                Transaction u = myMoney.Transactions.Merge(this.myMoney.Aliases, t, newTransactions);
                if (u != null)
                {
                    t = u;
                    merged++;
                }
                else
                {
                    added.Add(t);
                    register.AddTransaction(t);
                    count++;
                }
                newTransactions[t.Id] = t;

                t = null;
            }

            string message = (count > 0) ? "Downloaded " + count + " new transactions" : null;
            OfxDownloadData items = results.AddMessage(this.onlineAccount, a, message);
            foreach (Transaction nt in added)
            {
                items.Added.Add(nt);
            }

        }

        Transaction ProcessInvestmentBuy(Account a, XElement buy)
        {
            // Turns out the shape of these elements are the same regardless of whether the outer element is
            // BUYMF, BUYSTOCK, BUYOTHER, or BUYDEBT.
            /*
              <BUYMF>
                <INVBUY>
                  <INVTRAN>
                    <FITID>20130515TDMX011</FITID>
                    <DTTRADE>20130515070000.000[-4:EDT]</DTTRADE>
                    <MEMO>CONTRIBUTION;FID GROWTH CO UNITS TDMX;as of 05/15/2013</MEMO>
                  </INVTRAN>
                  <SECID>
                    <UNIQUEID>TDMX</UNIQUEID>
                    <UNIQUEIDTYPE>CUSIP</UNIQUEIDTYPE>
                  </SECID>
                  <UNITS>16.539</UNITS>
                  <UNITPRICE>11.49</UNITPRICE>
                  <TOTAL>190.03</TOTAL>
                  <SUBACCTSEC>OTHER</SUBACCTSEC>
                  <SUBACCTFUND>OTHER</SUBACCTFUND>
                </INVBUY>
                <BUYTYPE>BUY</BUYTYPE>
              </BUYMF>
             * 
             * But apparently the sign on the TOTAL is not standard, LPL returns this:
             *
             <INVBUY>
              <INVTRAN>
                <FITID>17kdnf-20130308-000000-1</FITID>
                <DTTRADE>20130308000000</DTTRADE>
                <MEMO>INSURED CASH ACCOUNT</MEMO>
              </INVTRAN>
              <SECID>
                <UNIQUEID>9999227</UNIQUEID>
                <UNIQUEIDTYPE>CUSIP</UNIQUEIDTYPE>
              </SECID>
              <UNITS>126.5</UNITS>
              <UNITPRICE>1</UNITPRICE>
              <TOTAL>-126.5</TOTAL>
              <SUBACCTSEC>CASH</SUBACCTSEC>
              <SUBACCTFUND>CASH</SUBACCTFUND>
            </INVBUY>
             * 
             * and this is also a buy, combined with a cash deposit of the same amount.
             * 
           <REINVEST>
            <INVTRAN>
              <FITID>20130613RTMS021</FITID>
              <DTTRADE>20130613070000.000[-4:EDT]</DTTRADE>
              <MEMO>NOT ACTUAL PRICE See your account statement for the actual price</MEMO>
            </INVTRAN>
            <SECID>
              <UNIQUEID>RTMS</UNIQUEID>
              <UNIQUEIDTYPE>CUSIP</UNIQUEIDTYPE>
            </SECID>
            <INCOMETYPE>DIV</INCOMETYPE>
            <TOTAL>1271.88</TOTAL>
            <SUBACCTSEC>OTHER</SUBACCTSEC>
            <UNITS>36.623</UNITS>
            <UNITPRICE>34.72900</UNITPRICE>
          </REINVEST>

             * 
             * So I think we have to force it to be a negative since it is a BUY
             */
            if (buy == null) return null;
            string type = buy.Name.LocalName;


            XElement invBuy = buy.Element("INVBUY");

            if (type == "REINVEST")
            {
                // there is no inner container in this case.
                invBuy = buy;
            }
            else if (invBuy == null)
            {
                return null;
            }

            Transaction t = ProcessInvestmentTransaction(a, invBuy);

            // force the amount to be negative!
            t.Amount = -Math.Abs(t.Amount);

            t.Investment = t.GetOrCreateInvestment();

            Security s = ProcessSecId(invBuy.Element("SECID"));
            if (s != null)
            {
                t.Investment.Security = s;
                t.Investment.UnitPrice = invBuy.SelectElementValueAsDecimal("UNITPRICE");
                t.Investment.Units = Math.Abs(invBuy.SelectElementValueAsDecimal("UNITS"));

                t.Investment.MarkUpDown = invBuy.SelectElementValueAsDecimal("MARKUP");
                t.Investment.Commission = invBuy.SelectElementValueAsDecimal("COMMISSION");
                t.Investment.Taxes = invBuy.SelectElementValueAsDecimal("TAXES");
                t.Investment.Fees = invBuy.SelectElementValueAsDecimal("FEES");
                t.Investment.Load = invBuy.SelectElementValueAsDecimal("LOAD");

                t.Investment.Type = InvestmentType.Buy;

                switch (type)
                {
                    case "BUYMF":
                        // todo: check that security is a mututal fund.
                        t.Category = this.myMoney.Categories.InvestmentMutualFunds;
                        break;
                    case "BUYSTOCK":
                        // todo: check that security is a stock.
                        t.Category = this.myMoney.Categories.InvestmentStocks;
                        break;
                    case "BUYOTHER":
                        // todo: check that security is a mutual fund, other or stock.
                        // usually this is for a "INSURED CASH ACCOUNT".
                        t.Category = this.myMoney.Categories.InvestmentOther;
                        break;
                    case "BUYDEBT":
                        // todo: check that security is Debt.
                        t.Category = this.myMoney.Categories.InvestmentBonds;
                        break;
                    case "BUYOPT":
                        // todo: check that security is Debt.
                        t.Category = this.myMoney.Categories.InvestmentOptions;
                        break;
                }

                InvestmentTradeType buyType = InvestmentTradeType.Buy;
                switch (buy.SelectElementValue("BUYTYPE"))
                {
                    case "BUYTOOPEN":
                        buyType = InvestmentTradeType.BuyToOpen;
                        break;
                    case "BUYTOCOVER":
                        buyType = InvestmentTradeType.BuyToCover;
                        break;
                    case "BUYTOCLOSE":
                        buyType = InvestmentTradeType.BuyToClose;
                        break;
                }
                t.Investment.TradeType = buyType;
            }

            return t;
        }

        Transaction ProcessInvestmentSell(Account a, XElement sell)
        {
            // Turns out the shape of these elements are the same regardless of whether the outer element is
            // SELLMF, SELLSTOCK, SELLOTHER, or SELLDEBT.
            /*
              <SELLSTOCK>
                <INVSELL>
                  <INVTRAN>
                    <FITID>rh3ku-20091009-000000-60359</FITID>
                    <DTTRADE>20091009000000</DTTRADE>
                    <MEMO>WOLSELEY PLC             SPONSORED ADR</MEMO>
                  </INVTRAN>
                  <SECID>
                    <UNIQUEID>97786P100</UNIQUEID>
                    <UNIQUEIDTYPE>CUSIP</UNIQUEIDTYPE>
                  </SECID>
                  <UNITS>-189</UNITS>
                  <UNITPRICE>2.361</UNITPRICE>
                  <FEES>0.02</FEES>
                  <TOTAL>446.21</TOTAL>
                  <SUBACCTSEC>CASH</SUBACCTSEC>
                  <SUBACCTFUND>CASH</SUBACCTFUND>
                </INVSELL>
                <SELLTYPE>SELL</SELLTYPE>
              </SELLSTOCK>
             */
            if (sell == null) return null;
            string type = sell.Name.LocalName;

            XElement invSell = sell.Element("INVSELL");
            if (invSell == null) return null;

            Transaction t = ProcessInvestmentTransaction(a, invSell);

            t.Investment = t.GetOrCreateInvestment();
            Security s = ProcessSecId(invSell.Element("SECID"));
            if (s != null)
            {
                t.Investment.Security = s;
                t.Investment.UnitPrice = invSell.SelectElementValueAsDecimal("UNITPRICE");
                t.Investment.Units = Math.Abs(invSell.SelectElementValueAsDecimal("UNITS"));

                t.Investment.MarkUpDown = invSell.SelectElementValueAsDecimal("MARKDOWN");
                t.Investment.Commission = invSell.SelectElementValueAsDecimal("COMMISSION");
                t.Investment.Taxes = invSell.SelectElementValueAsDecimal("TAXES");
                t.Investment.Fees = invSell.SelectElementValueAsDecimal("FEES");
                t.Investment.Load = invSell.SelectElementValueAsDecimal("LOAD");
                t.Investment.Withholding = invSell.SelectElementValueAsDecimal("WITHHOLDING");
                t.Investment.TaxExempt = invSell.SelectElementValueAsYesNo("TAXEXEMPT");
                t.Investment.Type = InvestmentType.Sell;
                switch (type)
                {
                    case "SELLMF":
                        // todo: check that security is a mututal fund.
                        // todo: handle AVGCOSTBASIS, RELFITID
                        t.Category = this.myMoney.Categories.InvestmentMutualFunds;
                        break;
                    case "SELLSTOCK":
                        // todo: check that security is a stock.
                        t.Category = this.myMoney.Categories.InvestmentStocks;
                        break;
                    case "SELLOPT":
                        // todo: check that security is an option.
                        // handle "RELFITID", "RELTYPE", "SECURED"
                        t.Category = this.myMoney.Categories.InvestmentOptions;
                        break;
                    case "SELLOTHER":
                        // todo: check that security is a mutual fund, other or stock.
                        // usually this is for a "INSURED CASH ACCOUNT".
                        t.Category = this.myMoney.Categories.InvestmentOther;
                        break;
                    case "SELLDEBT":
                        // todo: check that security is Debt.
                        t.Category = this.myMoney.Categories.InvestmentBonds;
                        break;
                }

                InvestmentTradeType tradeType = InvestmentTradeType.Sell;
                switch (sell.SelectElementValue("SELLTYPE"))
                {
                    case "SELLSHORT":
                        tradeType = InvestmentTradeType.SellShort;
                        break;
                }
                t.Investment.TradeType = tradeType;
            }

            return t;
        }

        Transaction ProcessInvestmentTransfer(Account a, XElement transfer)
        {
            Transaction t = ProcessInvestmentTransaction(a, transfer);
            t.Investment = t.GetOrCreateInvestment();


            Security s = ProcessSecId(transfer.Element("SECID"));
            if (s != null)
            {
                t.Investment.Security = s;

                decimal units = transfer.SelectElementValueAsDecimal("UNITS");
                if (units != 0)
                {
                    t.Investment.Units = units;
                    switch (transfer.SelectElementValue("TFERACTION"))
                    {
                        case "IN":
                            // todo: find matching "OUT" in another account and setup actual "Transfer" object
                            t.Investment.Type = InvestmentType.Add;
                            Debug.Assert(t.Investment.Units >= 0);
                            break;
                        case "OUT":
                            t.Investment.Type = InvestmentType.Remove;
                            Debug.Assert(t.Investment.Units >= 0);
                            break;
                    }
                }

                // todo: what to do with these.
                switch (transfer.SelectElementValue("POSTYPE"))
                {
                    case "LONG":
                        break;
                    case "SHORT":
                        break;
                }


            }

            /*
            XElement invacctfrom = transfer.SelectElement("INVACCTFROM");
            string accountid = invacctfrom.SelectElementValue("ACCTID");
            Account from = FindAccountByOfxId(accountid);
            if (from != null)
            {
                // ah ha, we can hook up the Transfer then.
                // but what if the transaction is already been added to the other account, then
                // we need to find that transaction here so we don't duplicate the transfer.
            }
            // todo: need to save AVGCOSTBASIS for tax purposes.
            // todo: what does it mean to have UNITPRICE on this transaction?
            // todo: need to remember DTPURCHASE for tax purposes.
            */

            return t;
        }

        /// <summary>
        /// Process INVTRAN section, and TOTAL
        /// </summary>
        private Transaction ProcessInvestmentTransaction(Account a, XElement e)
        {
            Transactions register = this.myMoney.Transactions;
            Transaction t = register.NewTransaction(a);
            t.Status = TransactionStatus.Electronic;
            t.Unaccepted = true;

            XElement invtran = e.Element("INVTRAN");
            if (invtran != null)
            {
                t.FITID = invtran.SelectElementValue("FITID");
                t.Date = ParseOfxDate(invtran.SelectElementValue("DTTRADE"));
                // todo: should use DTSETTLE for stock splits.
                t.Memo = invtran.SelectElementValue("MEMO");
            }
            t.Amount = e.SelectElementValueAsDecimal("TOTAL");
            return t;
        }

        Transaction ProcessIncomeExpense(Account a, XElement income)
        {
            /*
              <INCOME>
                <INVTRAN>
                  <FITID>rh3ku-20091030-000000-1</FITID>
                  <DTTRADE>20091030000000</DTTRADE>
                  <MEMO>INSURED CASH ACCOUNT     103009        3,804... AS OF 10-30-09</MEMO>
                </INVTRAN>
                <SECID>
                  <UNIQUEID>9999227</UNIQUEID>
                  <UNIQUEIDTYPE>CUSIP</UNIQUEIDTYPE>
                </SECID>
                <INCOMETYPE>INTEREST</INCOMETYPE>
                <TOTAL>0.2100</TOTAL>
                <SUBACCTSEC>CASH</SUBACCTSEC>
                <SUBACCTFUND>CASH</SUBACCTFUND>
              </INCOME>
             * 
             * or
             * <INVEXPENSE>
            */
            if (income == null) return null;

            Transaction t = ProcessInvestmentTransaction(a, income);

            Security s = ProcessSecId(income.Element("SECID"));
            if (s != null)
            {
                t.Investment = t.GetOrCreateInvestment();
                t.Investment.Security = s;
                t.Investment.TaxExempt = income.SelectElementValueAsYesNo("TAXEXEMPT");
                t.Investment.Withholding = income.SelectElementValueAsDecimal("WITHHOLDING");

                if (income.Name.LocalName != "INCOME")
                {
                    // INVEXPENSE or RETOFCAP
                    t.Category = myMoney.Categories.InvestmentMiscellaneous;
                }
                else
                {
                    t.Category = GetIncomeCategory(income);

                    if (t.Category == this.myMoney.Categories.InvestmentDividends)
                    {
                        t.Investment.Type = InvestmentType.Dividend;
                    }
                }

            }

            ProcessCurrency(income.SelectElement("CURRENCY"), t);
            return t;
        }

        Category GetIncomeCategory(XElement incomeElement)
        {
            Category cat = null;
            string type = incomeElement.SelectElementValue("INCOMETYPE");
            switch (type)
            {
                default:
                    cat = myMoney.Categories.InvestmentInterest;
                    break;
                case "CGLONG":
                    cat = myMoney.Categories.InvestmentLongTermCapitalGainsDistribution;
                    break;
                case "CGSHORT":
                    cat = myMoney.Categories.InvestmentShortTermCapitalGainsDistribution;
                    break;
                case "DIV":
                    cat = myMoney.Categories.InvestmentDividends;
                    break;
                case "MISC":
                    cat = myMoney.Categories.InvestmentMiscellaneous;
                    break;
            }
            return cat;
        }

        Transaction ProcessInvestmentBankTransaction(Account a, XElement e)
        {
            /*
             <INVBANKTRAN>
                <STMTTRN>
                  <TRNTYPE>FEE</TRNTYPE>
                  <DTPOSTED>20091007000000</DTPOSTED>
                  <TRNAMT>-475.5500</TRNAMT>
                  <FITID>rh3ku-20091007-000000-223283</FITID>
                  <MEMO>ADVISORY FEE</MEMO>
                </STMTTRN>
                <SUBACCTFUND>CASH</SUBACCTFUND>
             </INVBANKTRAN>
             */
            if (e == null) return null;

            Transactions register = this.myMoney.Transactions;
            Transaction t = register.NewTransaction(a);
            t.Status = TransactionStatus.Electronic;
            t.Unaccepted = true;

            XElement s = e.Element("STMTTRN");
            if (s != null)
            {
                t.FITID = s.SelectElementValue("FITID");
                t.Date = ParseOfxDate(s.SelectElementValue("DTPOSTED"));
                t.Amount = s.SelectElementValueAsDecimal("TRNAMT");
                t.Memo = s.SelectElementValue("MEMO");

                switch (s.SelectElementValue("TRNTYPE"))
                {
                    case "CREDIT":
                        t.Category = this.myMoney.Categories.InvestmentCredit;
                        break;
                    case "DEBIT":
                        t.Category = this.myMoney.Categories.InvestmentDebit;
                        break;
                    case "INT":
                        t.Category = this.myMoney.Categories.InvestmentInterest;
                        break;
                    case "DIV":
                        t.Category = this.myMoney.Categories.InvestmentDividends;
                        break;
                    case "FEE":
                    case "SRVCHG": // service charge
                        t.Category = this.myMoney.Categories.InvestmentFees;
                        break;
                    case "DEP": // depost
                    case "ATM": // automatic teller machine
                    case "POS": // Point of sale
                    case "PAYMENT": // Electronic payment
                    case "CASH": // Cash withdrawal
                    case "DIRECTDEP": // Direct deposit
                    case "DIRECTDEBIT": // Direct debit;
                    case "REPEATPMT": // Repeating payment
                    case "CHECK": // check
                    case "OTHER":
                        if (t.Amount > 0)
                        {
                            t.Category = this.myMoney.Categories.InvestmentCredit;
                        }
                        else
                        {
                            t.Category = this.myMoney.Categories.InvestmentDebit;
                        }
                        break;
                    case "XFER":
                        t.Category = this.myMoney.Categories.InvestmentTransfer;
                        break;
                }
            }
            return t;
        }

        Transaction ProcessMarginInterest(Account a, XElement e)
        {
            if (e == null) return null;
            Transaction t = ProcessInvestmentTransaction(a, e);
            t.Category = myMoney.Categories.InvestmentInterest;
            return t;
        }


        private static void ProcessCurrency(XElement currency, Transaction t)
        {
            if (currency == null) return;
            string symbol = currency.SelectElementValue("CURSYM");

            if (symbol != "USD")
            {
                MessageBoxEx.Show("TODO: need to debug what to do here, do we need to convert?", "Currency", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        Security ProcessSecId(XElement secId)
        {
            if (secId == null) return null;
            string uniqueId = secId.SelectElementValue("UNIQUEID");
            if (string.IsNullOrEmpty(uniqueId)) return null;

            string idType = secId.SelectElementValue("UNIQUEIDTYPE");
            if (string.IsNullOrEmpty(uniqueId)) idType = "CUSIP";

            SecurityInfo info = null;
            if (this.securityInfo.TryGetValue(uniqueId, out info))
            {
                return info.Security;
            }
            else
            {
                // perhaps it's just a cash transaction after all?
            }


            return null;
        }

        // Little wrapper to hold onto OFX info returned with statement.
        class SecurityInfo
        {
            public string UniqueId { get; set; }
            public string Name { get; set; }
            public string UniqueIdType { get; set; }
            public string Ticker { get; set; }
            public Security Security { get; set; } // found based on Ticker.

            // for debt securities
            public decimal ParValue { get; set; }
            public string DebtType { get; set; }

            public override int GetHashCode()
            {
                return UniqueId.GetHashCode();
            }
            public override bool Equals(object obj)
            {
                SecurityInfo si = obj as SecurityInfo;
                return (si != null && si.UniqueId == this.UniqueId);
            }
        }

        /// <summary>
        /// Read SECLISTMSGSRSV1/SECLIST
        /// </summary>
        Dictionary<string, SecurityInfo> ReadSecurityInfo(XDocument doc)
        {
            var result = new Dictionary<string, SecurityInfo>();
            XElement secList = doc.SelectElement("OFX/SECLISTMSGSRSV1/SECLIST");
            if (secList != null)
            {
                foreach (XElement e in secList.Elements())
                {
                    /* e.g.:
                      <STOCKINFO>
                        <SECINFO>
                          <SECID>
                            <UNIQUEID>361592108</UNIQUEID>
                            <UNIQUEIDTYPE>CUSIP</UNIQUEIDTYPE>
                          </SECID>
                          <SECNAME>GEA GROUP AG (361592108)</SECNAME>
                          <TICKER>GEAGY</TICKER>
                        </SECINFO>
                      </STOCKINFO>
                      <DEBTINFO>
                        <SECINFO>
                          <SECID>
                            <UNIQUEID>362870941</UNIQUEID>
                            <UNIQUEIDTYPE>CUSIP</UNIQUEIDTYPE>
                          </SECID>
                          <SECNAME>GAINESVILLE GA  Rate: 5.375 % Maturity Date: 01/01/2010 Dated Date: 05/01/1993 (362870941)</SECNAME>
                        </SECINFO>
                        <PARVALUE>100.0000</PARVALUE>
                        <DEBTTYPE>COUPON</DEBTTYPE>
                      </DEBTINFO>
                      <MFINFO>
                        <SECINFO>
                          <SECID>
                            <UNIQUEID>939330825</UNIQUEID>
                            <UNIQUEIDTYPE>CUSIP</UNIQUEIDTYPE>
                          </SECID>
                          <SECNAME>WASHINGTON MUTUAL (939330825)</SECNAME>
                          <TICKER>WMFFX</TICKER>
                        </SECINFO>
                      </MFINFO>
                     * 
                     * FIDELITY
                     * <MFINFO>
                        <SECINFO>
                          <SECID>
                            <UNIQUEID>TPG9</UNIQUEID>
                            <UNIQUEIDTYPE>CUSIP</UNIQUEIDTYPE>
                          </SECID>
                          <SECNAME>ARTISAN MID CAP</SECNAME>
                          <FIID>TPG9</FIID>
                          <UNITPRICE>19.91</UNITPRICE>
                          <DTASOF>20131206160000.000[-5:EST]</DTASOF>
                          <MEMO>Market close as of 12/06/2013;ARTISAN MID CAP</MEMO>
                        </SECINFO>
                        <MFTYPE>OTHER</MFTYPE>
                      </MFINFO>
                      <MFINFO>
                    */
                    SecurityInfo s = new SecurityInfo();
                    XElement secInfo = e.SelectElement("SECINFO");
                    if (secInfo != null)
                    {
                        s.UniqueId = secInfo.SelectElementValue("SECID/UNIQUEID");
                        s.UniqueIdType = secInfo.SelectElementValue("SECID/UNIQUEIDTYPE");
                        s.Name = secInfo.SelectElementValue("SECNAME");
                        s.Ticker = secInfo.SelectElementValue("TICKER");
                        string price = secInfo.SelectElementValue("UNITPRICE");

                        Security sec = null;
                        if (!string.IsNullOrEmpty(s.Ticker))
                        {
                            // ticker should be unique, so we find by symbol if we can.
                            sec = this.myMoney.Securities.FindSymbol(s.Ticker, true);
                        }
                        else if (!string.IsNullOrEmpty(s.UniqueId))
                        {
                            sec = this.myMoney.Securities.FindSecurityById(s.UniqueId);
                        }

                        if (sec == null)
                        {
                            sec = this.myMoney.Securities.FindSecurity(s.Name, true);
                        }

                        if (sec != null)
                        {
                            s.Security = sec;

                            if (string.IsNullOrEmpty(sec.Symbol))
                            {
                                sec.Symbol = s.Ticker;
                            }

                            if (string.IsNullOrEmpty(sec.Name) || sec.Name == sec.Symbol)
                            {
                                s.Security.Name = s.Name;
                            }

                            if (s.UniqueIdType == "CUSIP" && string.IsNullOrEmpty(sec.CuspId))
                            {
                                sec.CuspId = s.UniqueId;
                            }

                            decimal unitprice = 0;
                            if (decimal.TryParse(price, out unitprice))
                            {
                                sec.LastPrice = sec.Price;
                                sec.Price = unitprice;
                                sec.PriceDate = DateTime.Today;
                            }
                        }


                    }
                    switch (e.Name.LocalName)
                    {
                        case "DEBTINFO":
                            {
                                s.ParValue = e.SelectElementValueAsDecimal("PARVALUE");
                                // todo: handle DEBTTYPE, DEBTCLASS, DTCOUPON, COUPONFREQ, CALLPRICE, YIELDTOCALL, DTCALL, 
                                // YIELDTOMAT, DTMAT, ASSETCLASS, FIASSETCLASS
                            }
                            break;
                        case "MFINFO":
                            // todo: handle MFTYPE, YIELD, DTYIELDASOF, MFASSETCLASS, PORTION, ASSETCLASS, PERCENT,
                            // FIPORTION, FIASSETCLASS, PERCENT
                            break;
                        case "OPTINFO":
                            // todo: handle options specific information, OPTTYPE, STRIKEPRICE, DTEXPIRE< SHPERCTRCT,
                            // ASSETCLASS, FIASSETCLASS
                            break;
                        case "OTHERINFO":
                            // todo: handle other information: TYPEDESC, ASSETCLASS, FIASSETCLASS
                            break;
                        case "STOCKINFO":
                            // todo: handle stock information: STOCKTYPE, YIELD, DTYIELDASOF, 
                            // ASSETCLASS, FIASSETCLASS, UNITPRICE
                            break;
                    }
                    if (!string.IsNullOrEmpty(s.UniqueId))
                    {
                        result[s.UniqueId] = s;
                    }
                }
            }
            return result;
        }


        /// <summary>
        /// Process STMTRS
        /// </summary>
        private void ProcessStatement(OfxDownloadData results, Account a, XElement srs)
        {
            Transactions register = this.myMoney.Transactions;

            // update last sync date.                
            this.myMoney.BeginUpdate(this);
            try
            {
                a.LastSync = DateTime.Today;
                int count = 0;
                int merged = 0;
                Dictionary<long, Transaction> newTransactions = new Dictionary<long, Transaction>();
                List<Transaction> added = new List<Transaction>();
                XElement transactionList = srs.Element("BANKTRANLIST");
                if (transactionList == null)
                {
                    return;
                }

                foreach (XElement sr in transactionList.Elements("STMTTRN"))
                {

                    string fitid = sr.SelectExpectedElement("FITID").Value.Trim();

                    DateTime dt = ParseOfxDate(sr.SelectExpectedElement("DTPOSTED").Value.Trim());
                    decimal amount = decimal.Parse(sr.SelectExpectedElement("TRNAMT").Value.Trim());
                    if (amount == 0 && a.Type == AccountType.Credit)
                        continue; // ignore those annoying credit checks.

                    string number = null;

                    XElement e = sr.Element("CHECKNUM");
                    if (e != null)
                    {
                        number = Int32.Parse(e.Value.Trim()).ToString();
                    }

                    string payee = null;
                    if ((e = sr.Element("NAME")) != null)
                    {
                        payee = e.Value.Trim();
                    }
                    else if ((e = sr.Element("PAYEE")) != null)
                    {
                        payee = e.Value.Trim();
                    }
                    else if ((e = sr.Element("PAYEE2")) != null)
                    {
                        payee = e.Value.Trim();
                    }
                    

                    string memo = null;
                    if ((e = sr.Element("MEMO")) != null)
                    {
                        memo = e.Value.Trim();
                    }
                    else if ((e = sr.Element("MEMO2")) != null)
                    {
                        memo = e.Value.Trim();
                    }

                    string s = "Bill Payment ";
                    if (memo != null && memo.IndexOf(s) == 0)
                    {
                        memo = memo.Substring(s.Length);
                        bool isnumber = true;
                        int i;
                        for (i = memo.Length - 1; i > 0; i--)
                        {
                            char ch = memo[i];
                            if (ch == ' ') break;
                            if (!Char.IsDigit(memo[i]))
                            {
                                isnumber = false;
                                break;
                            }
                        }
                        if (isnumber && i < memo.Length)
                        {
                            number = memo.Substring(i + 1);
                            payee = memo.Substring(0, i).Trim();
                            memo = payee;
                        }
                    }

                    if (memo == payee && payee != null)
                    {
                        if (payee.IndexOf("Share Draft") >= 0)
                        {
                            payee = null;
                        }
                        else if (payee == "Share Dividend")
                        {
                            payee = a.OnlineAccount.Institution;
                        }
                        else
                        {
                            memo = null;
                        }
                    }
                    if (memo == payee) memo = null;
                    if (string.IsNullOrEmpty(payee))
                    {
                        payee = memo;
                    }

                    Transaction t = register.NewTransaction(a);
                    t.Status = TransactionStatus.Electronic;
                    t.Number = number;
                    t.Amount = amount;
                    if (!string.IsNullOrEmpty(payee))
                    {
                        t.Payee = this.myMoney.Payees.FindPayee(payee, true);
                    }
                    t.Memo = memo;
                    t.Date = dt;
                    t.FITID = fitid;
                    t.Unaccepted = true;

                    Alias alias = this.myMoney.Aliases.FindMatchingAlias(payee);
                    if (alias != null)
                    {
                        if (memo == null || memo == string.Empty) memo = payee;
                        payee = alias.Payee.Name;
                    }

                    Transaction u = myMoney.Transactions.Merge(this.myMoney.Aliases, t, newTransactions);
                    if (u != null)
                    {
                        t = u;
                        merged++;
                    }
                    else
                    {
                        added.Add(t);
                        register.AddTransaction(t);
                        count++;
                    }
                    newTransactions[t.Id] = t;
                }
                string message = (count > 0) ? "Downloaded " + count + " new transactions" : null;
                OfxDownloadData items = results.AddMessage(this.onlineAccount, a, message);
                foreach (Transaction t in added)
                {
                    items.Added.Add(t);
                }

                myMoney.Rebalance(a);
            }
            finally
            {
                this.myMoney.EndUpdate();
            }
        }

        // process INVACCTFROM
        private bool CheckAccountId(ref Account a, AccountType accountType, XElement from, OfxDownloadData results)
        {
            string accountid = from.SelectElementValue("ACCTID");

            Account temp = new Account() { Name = accountid, AccountId = accountid, Type = accountType };

            if (a != null && accountid != a.OfxAccountId)
            {
                results.AddError(onlineAccount, temp, "Bank account ID's do not match.");
                return false;
            }

            if (a == null)
            {
                if (accountid == string.Empty)
                {
                    results.AddError(onlineAccount, temp, "ACCID should not be empty.");
                    return false;
                }

                a = FindAccountByOfxId(accountid);
                if (a != null)
                {
                    this.account = a;
                    this.onlineAccount = a.OnlineAccount;
                    if (this.onlineAccount != null)
                    {
                        from.Document.Save(Path.Combine(OfxLogPath, GetLogfileName(this.onlineAccount) + "RS.xml"));
                    }
                }
                else
                {
                    if (this.callerPickAccount == null)
                    {
                        // We could not resolve the account ID
                        // and the caller did not supply a "resolver" callback
                        return false;
                    }
                    else if (skippedAccounts.Contains(accountid))
                    {
                        // user has already told us they want to skip this account.
                        return false;
                    }
                    else
                    {
                        //
                        // Let invoke the callback for resolving the missing Account ID
                        // The caller can use any heuristic for resolving the Account ID, including asking the user via the UI
                        //
                        bool cancelled = false;
                        UiDispatcher.Invoke(new Action(() =>
                        {
                            Account picked = callerPickAccount(this.myMoney, temp);
                            if (picked == null)
                            {
                                // cancelled
                                cancelled = true;
                            }
                            temp = picked;
                        }));

                        if (cancelled)
                        {
                            skippedAccounts.Add(accountid);
                            results.AddError(onlineAccount, temp, "Import cancelled.");
                            return false;
                        }
                        a = temp;
                        if (a == null)
                        {
                            skippedAccounts.Add(accountid);
                            results.AddError(onlineAccount, temp, "Account skipped.");
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public delegate Account PickAccountDelegate(MyMoney money, Account accountTemplate);

        static Regex ObfuscatedId = new Regex("([X]+)([0-9]+)");

        private bool AccoundIdMatches(string downloadedId, string localId)
        {
            if (string.IsNullOrEmpty(localId))
            {
                return false;
            }
            if (string.Compare(downloadedId, localId, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            return false;
        }

        private Account AccountIdFuzzyMatch(string downloadedId)
        {
            Account matched = null;
            var m = ObfuscatedId.Match(downloadedId);
            if (m.Success && m.Groups.Count == 3)
            {
                var exs = m.Groups[1].Value;
                var tail = m.Groups[2].Value;

                foreach (Account acct in this.myMoney.Accounts.GetAccounts())
                {
                    foreach (string localId in new string[] {  acct.AccountId, acct.OfxAccountId })
                    {
                        if (!string.IsNullOrEmpty(localId))
                        {
                            var trimmedLocalId = localId.Replace(" ", "").Trim();
                            if (trimmedLocalId.Length == downloadedId.Length)
                            {
                                var localTail = trimmedLocalId.Substring(exs.Length);
                                if (string.Compare(tail, localTail, StringComparison.OrdinalIgnoreCase) == 0)
                                {
                                    if (matched != null && matched != acct)
                                    {
                                        // ambiguouis!
                                        return null;
                                    }
                                    else
                                    {
                                        matched = acct;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return matched;
        }

        // Find account matching the given id.
        private Account FindAccountByOfxId(string accountId)
        {
            if (!string.IsNullOrEmpty(accountId))
            {
                foreach (Account acct in this.myMoney.Accounts.GetAccounts())
                {
                    if (AccoundIdMatches(accountId, acct.AccountId))
                    {
                        return acct;
                    }
                    if (AccoundIdMatches(accountId, acct.OfxAccountId))
                    {
                        return acct;
                    }
                }

                return AccountIdFuzzyMatch(accountId);
            }
            return null;
        }

        private static bool CheckUSD(XElement srs, OfxDownloadData results, Account a)
        {
            string dollar = srs.SelectElementValue("CURDEF");

            // todo: how to support multi-currency properly...
            //if (dollar != "USD")
            //{
            //    string msg = string.Format("Unexpected currency returned '{0}'", dollar);
            //    results.AddError(a, msg);
            //    return false;
            //}
            return true;
        }

        private static string ProcessStatementStatus(XElement tr, Account a)
        {
            XElement statusElement = tr.Element("STATUS");
            if (statusElement != null)
            {
                XElement sc = statusElement.Element("CODE");
                if (sc != null && sc.Value.Trim() != "0")
                {
                    string msg = (a == null) ? string.Empty : a.Name + ": ";
                    XElement severity = statusElement.Element("SEVERITY");
                    string reason = null;
                    if (severity != null)
                    {
                        reason = severity.Value.Trim();
                    }
                    XElement message = statusElement.Element("MESSAGE");
                    if (message != null)
                    {
                        if (reason != null) reason += ", ";
                        reason += message.Value.Trim();
                    }
                    return msg + reason;
                }
            }
            return null;
        }

        public static DateTime ParseOfxDate(string s)
        {
            var now = DateTime.Now;
            int year = now.Year;
            int len = s.Length;
            if (len >= 4)
            {
                year = Int32.Parse(s.Substring(0, 4));
            }
            int month = now.Month;
            if (len >= 6)
            {
                month = Int32.Parse(s.Substring(4, 2));
            }
            int day = now.Day;
            if (len >= 8)
            {
                day = Int32.Parse(s.Substring(6, 2));
            }
            int hour = 0;
            if (len >= 10)
            {
                hour = Int32.Parse(s.Substring(8, 2));
            }
            int minute = 0;
            if (len >= 12)
            {
                minute = Int32.Parse(s.Substring(10, 2));
            }
            int second = 0;
            if (len >= 14)
            {
                second = Int32.Parse(s.Substring(12, 2));
            }
            DateTime dt = new DateTime(year, month, day, hour, minute, second, 0);

            int openbracket = s.IndexOf('[');
            int closebracket = s.IndexOf(']');
            if (openbracket > 0 && closebracket > openbracket)
            {
                string zone = s.Substring(openbracket + 1, closebracket - openbracket - 1);
                int colon = zone.IndexOf(':');
                if (colon > 0)
                {
                    zone = zone.Substring(0, colon);
                }
                int tz = 0;
                if (int.TryParse(zone, out tz))
                {
                    // convert to local time
                    TimeSpan offset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now) - TimeSpan.FromHours(tz);
                    dt = dt.Add(offset);
                }
            }
            return dt;
        }


        internal static string SaveLog(XDocument doc, string filename)
        {
            string path = Path.Combine(OfxRequest.OfxLogPath, filename);

            XDocument copy = new XDocument(doc);

            // now blank out user id, password and other secure information

            foreach (XElement e in copy.Descendants())
            {
                string name = e.Name.LocalName;
                switch (name)
                {
                    case "USERID":
                    case "USERPASS":
                    case "USERKEY":
                    case "SESSCOOKIE":
                    case "USERCRED1":
                    case "USERCRED2":
                    case "MFAPHRASEA":
                    case "ACCESSKEY":
                    case "AUTHTOKEN":
                        string value = e.Value;
                        e.Value = new string('X', 40);
                        break;
                }
            }

            copy.Save(path);
            return path;
        }

    }

    public class OfxException : Exception
    {
        OfxErrorCode ofxError;
        string code;
        string httpHeaders;
        string response;
        Account account;

        public OfxException(string message)
            : base(message)
        {
        }

        public OfxException(string message, string code, string response, string httpHeaders)
            : base(message)
        {
            this.code = code;
            this.httpHeaders = httpHeaders;
            this.response = response;
        }

        public OfxErrorCode OfxError { get { return this.ofxError; } set { this.ofxError = value; } }
        public string Code { get { return this.code; } }
        public string HttpHeaders { get { return this.httpHeaders; } }
        public string Response { get { return this.response; } }
        public OFX Root { get; set; }
        public Account Account
        {
            get { return account; }
            set { account = value; }
        }

    }

    /// <summary>
    /// This class implements the MFA Challenge Requets protocol
    /// and 
    /// </summary>
    public class OfxMfaChallengeRequest
    {
        OnlineAccount onlineAccount;
        MyMoney money;

        string challengeLog;

        public OfxMfaChallengeRequest(OnlineAccount onlineAccount, MyMoney m)
        {
            this.onlineAccount = onlineAccount;
            this.money = m;
        }

        public object UserData { get; set; }

        /// <summary>
        /// If an error occurs this property will provide the error information.
        /// </summary>
        public Exception Error { get; set; }

        /// <summary>
        /// The set of MFA challenge questions to prompt the user with.
        /// </summary>
        public List<MfaChallenge> UserChallenges { get; set; }


        /// <summary>
        /// Get the list of built in answers to questions
        /// </summary>
        public List<MfaChallengeAnswer> BuiltInAnswers { get; set; }

        /// <summary>
        /// This event is raised when the MFA challenge response is received.  If an error
        /// occurred then the Error property is set, otherwise you will have the list
        /// of UserChallenges.  This event is raised on the UI thread.
        /// </summary>
        public event EventHandler Completed;

        /// <summary>
        /// Start new thread for requesting MFA Challenge from server.
        /// </summary>
        public void BeginMFAChallenge()
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(BackgroundThread));
        }


        private void BackgroundThread(object state)
        {
            try
            {
                OFX ofx = RequestChallenge();
                UiDispatcher.Invoke(new Action(() =>
                {
                    HandleChallenge(ofx);
                }));
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }


        private void OnCompleted()
        {
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                if (Completed != null)
                {
                    Completed(this, EventArgs.Empty);
                }
            }));
        }

        private void OnError(Exception ex)
        {
            Error = ex;
            OnCompleted();
        }

        private OFX RequestChallenge()
        {
            OfxRequest req = new OfxRequest(this.onlineAccount, this.money, null);
            XDocument doc = this.GetMFAChallengeRequest(req);

            OfxRequest.SaveLog(doc, OfxRequest.GetLogfileName(this.money, this.onlineAccount) + "_MFAChallenge_RQ.xml");

            doc = req.SendOfxRequest(doc);

            challengeLog = OfxRequest.SaveLog(doc, OfxRequest.GetLogfileName(this.money, this.onlineAccount) + "_MFAChallenge_RS.xml");

            // deserialize response into our OfxProfile structure.

            OFX ofx = OFX.Deserialize(doc);

            req.CheckSignOnStatusError(ofx);

            return ofx;
        }

        private XDocument GetMFAChallengeRequest(OfxRequest req)
        {
            XDocument doc = req.GetSignonRequest(true);

            XElement sonrqmsg = doc.Root.Element("SIGNONMSGSRQV1");

            XElement challenge = new XElement("MFACHALLENGETRNRQ",
                    new XElement("TRNUID", Guid.NewGuid().ToString()),
                    new XElement("MFACHALLENGERQ",
                        new XElement("DTCLIENT", OfxRequest.GetIsoDateTime(DateTime.Now))
                    )
                );

            sonrqmsg.Add(challenge);
            return doc;
        }

        private string GetMacAddress()
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up)
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet3Megabit ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.FastEthernetFx ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.FastEthernetT ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet)
                    {
                        return nic.GetPhysicalAddress().ToString();
                    }
                }
            }
            return "";
        }

        private void HandleChallenge(OFX ofx)
        {
            List<MfaChallengeAnswer> answers = BuiltInAnswers = new List<MfaChallengeAnswer>();

            SignOnResponseMessageSet response = ofx.SignOnMessageResponse;
            if (response != null)
            {
                MfaChallengeTransaction mct = response.MfaChallengeTransaction;
                if (mct != null)
                {
                    // now we need something like the password dialog, only with totally customizable set of fields...
                    List<MfaChallenge> userChallenges = new List<MfaChallenge>();

                    // see if there's any challenges we can answer without user.
                    foreach (var challenge in mct.Challenges)
                    {
                        string id = challenge.PhraseId;
                        if (string.IsNullOrEmpty(id))
                        {
                            continue;
                        }
                        switch (id.ToUpperInvariant().Trim())
                        {
                            case "MFA101": // Datetime, formatted YYYYMMDDHHMMSS
                                answers.Add(new MfaChallengeAnswer() { Id = id, Answer = DateTime.Now.ToString("yyyyMMddhhmmss") });
                                break;
                            case "MFA102": // Host name
                                answers.Add(new MfaChallengeAnswer() { Id = id, Answer = System.Net.Dns.GetHostName() });
                                break;
                            case "MFA103": // IP Address
                                var address = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()).FirstOrDefault();
                                answers.Add(new MfaChallengeAnswer() { Id = id, Answer = address.ToString() });
                                break;
                            case "MFA104": // MAC Address                                                                
                                answers.Add(new MfaChallengeAnswer() { Id = id, Answer = GetMacAddress() });
                                break;
                            case "MFA105": // Operating System version
                                answers.Add(new MfaChallengeAnswer() { Id = id, Answer = Environment.OSVersion.ToString() });
                                break;
                            case "MFA106": // Processor architecture, e.g. I386
                                answers.Add(new MfaChallengeAnswer() { Id = id, Answer = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") });
                                break;
                            case "MFA107": // User Agent
                                answers.Add(new MfaChallengeAnswer() { Id = id, Answer = OfxRequest.GetUserAgent(this.onlineAccount) });
                                break;
                            default:
                                userChallenges.Add(challenge);
                                break;
                        }
                    }

                    if (userChallenges.Count > 0)
                    {
                        this.UserChallenges = userChallenges;
                        OnCompleted();
                        return;
                    }
                }
            }

            OnError(new OfxException("Server returned unexpected response from MFA Challenge Request")
            {
                Root = ofx,
                HelpLink = challengeLog
            });
        }


    }

}
