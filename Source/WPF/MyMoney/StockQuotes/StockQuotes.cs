using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Xml.Linq;
using System.Xml;
using Walkabout.Data;
using Walkabout.Sgml;
using Walkabout.Controls;
using Walkabout.Configuration;
using Walkabout.Utilities;
using System.Text;
using System.Windows.Documents;
using System.Windows.Input;
using Newtonsoft.Json;
using Walkabout.Ofx;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Walkabout.Network
{
    /// <summary>
    /// </summary>
    public class StockServiceSettings : INotifyPropertyChanged
    {
        private string _name;
        private string _apiKey;
        private int _requestsPerMinute;
        private int _requestsPerDay;

        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged("Name");
                }
            }
        }

        public string ApiKey
        {
            get { return _apiKey; }
            set
            {
                if (_apiKey != value)
                {
                    _apiKey = value;
                    OnPropertyChanged("ApiKey");
                }
            }
        }

        public int ApiRequestsPerMinuteLimit
        {
            get { return _requestsPerMinute; }
            set
            {
                if (_requestsPerMinute != value)
                {
                    _requestsPerMinute = value;
                    OnPropertyChanged("ApiRequestsPerMinuteLimit");
                }
            }
        }

        public int ApiRequestsPerDayLimit
        {
            get { return _requestsPerDay; }
            set
            {
                if (_requestsPerDay != value)
                {
                    _requestsPerDay = value;
                    OnPropertyChanged("ApiRequestsPerDayLimit");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public void Serialize(XmlWriter w)
        {
            w.WriteElementString("Name", this.Name == null ? "" : this.Name);
            w.WriteElementString("ApiKey", this.ApiKey == null ? "" : this.ApiKey);
            w.WriteElementString("ApiRequestsPerMinuteLimit", this.ApiRequestsPerMinuteLimit.ToString());
            w.WriteElementString("ApiRequestsPerDayLimit", this.ApiRequestsPerDayLimit.ToString());
        }

        public void Deserialize(XmlReader r)
        {
            if (r.IsEmptyElement) return;
            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    if (r.Name == "Name")
                    {
                        this.Name = r.ReadElementContentAsString();
                    }
                    else if (r.Name == "ApiKey")
                    {
                        this.ApiKey = r.ReadElementContentAsString();
                    }
                    else if (r.Name == "ApiRequestsPerMinuteLimit")
                    {
                        this.ApiRequestsPerMinuteLimit = r.ReadElementContentAsInt();
                    }
                    else if (r.Name == "ApiRequestsPerDayLimit")
                    {
                        this.ApiRequestsPerDayLimit = r.ReadElementContentAsInt();
                    }
                }
            }
        }

    }

    /// <summary>
    /// This class encapsulates a new stock quote from IStockQuoteService
    /// </summary>
    public class StockQuote
    {
        public string Name { get; set; }
        public string Symbol { get; set; }
        public decimal Price { get; set; }
        public DateTime Date { get; set; }
    }

    public interface IStockQuoteService
    {
        /// <summary>
        /// Fetch updated security information for the given securities (most recent closing price).
        /// This can be called multiple times and any pending downloads will be merged automatically
        /// </summary>
        /// <param name="securities">List of securities to fetch </param>
        void BeginFetchQuotes(List<Security> securities);

        /// <summary>
        /// Return a count of pending downloads.
        /// </summary>
        int PendingCount { get; }

        /// <summary>
        /// Each downloaded quote is raised as an event on this interface.  Could be from any thread.
        /// </summary>
        event EventHandler<StockQuote> QuoteAvailable;

        /// <summary>
        /// If some error happens fetching a quote, this event is raised.
        /// </summary>
        event EventHandler<string> DownloadError;

        /// <summary>
        /// If the service is performing a whole batch at once, this event is raised after each batch is complete.
        /// If there are still more downloads pending the boolean value is raised with the value false.
        /// This is also raised when the entire pending list is completed with the boolean set to true.
        /// </summary>
        event EventHandler<bool> Complete;

        /// <summary>
        /// Stop all pending requests
        /// </summary>
        void Cancel();
    }

    /// <summary>
    /// This class tracks changes to Securities and fetches stock quotes from the configured online stock quote service.
    /// </summary>
    public class StockQuotes : IDisposable
    {
        MyMoney myMoney;
        bool busy;
        StringBuilder errorLog = new StringBuilder();
        bool hasError;
        bool stop;
        List<Security> queue = new List<Security>(); // list of securities to fetch
        HashSet<string> fetched = new HashSet<string>(); // list that we have already fetched.
        IStatusService status;
        IServiceProvider provider;
        StockServiceSettings _settings;
        IStockQuoteService _service;
        int _progressMax;
        List<StockQuote> _batch = new List<StockQuote>();

        public StockQuotes(IServiceProvider provider, StockServiceSettings settings)
        {
            this.Settings = settings;
            this.provider = provider;
            this.myMoney = (MyMoney)provider.GetService(typeof(MyMoney));
            this.status = (IStatusService)provider.GetService(typeof(IStatusService));
            this.myMoney.Changed += new EventHandler<ChangeEventArgs>(OnMoneyChanged);

            // assume we have fetched all securities.
            // call UpdateQuotes to refetch them all again, otherwise this
            // class will track changes and automatically fetch any new securities that it finds.
            foreach (Security s in myMoney.Securities.AllSecurities)
            {
                if (!string.IsNullOrEmpty(s.Symbol))
                {
                    lock (fetched)
                    {
                        fetched.Add(s.Symbol);
                    }
                }
            }
        }

        public StockServiceSettings Settings
        {
            get
            {
                return _settings;
            }
            set
            {
                StopThread();
                _settings = value;

                IStockQuoteService service = null;
                if (AlphaVantage.IsMySettings(_settings))
                {
                    service = new AlphaVantage(_settings);
                }
                else 
                {
                    service = new IEXTrading(_settings);
                }
                SetService(service);
            }
        }

        void SetService(IStockQuoteService service)
        {
            if (_service != null)
            {
                _service.DownloadError -= OnServiceDownloadError;
                _service.QuoteAvailable -= OnServiceQuoteAvailable;
                _service.Complete -= OnServiceQuotesComplete;
                _service.Cancel();
            }
            _service = service;
            if (_service != null)
            {
                _service.DownloadError += OnServiceDownloadError;
                _service.QuoteAvailable += OnServiceQuoteAvailable;
                _service.Complete += OnServiceQuotesComplete;
            }
        }


        public List<StockServiceSettings> GetDefaultSettingsList()
        {
            List<StockServiceSettings> result = new List<StockServiceSettings>();
            result.Add(IEXTrading.GetDefaultSettings());
            result.Add(AlphaVantage.GetDefaultSettings());
            return result;
        }

        void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
            List<Security> newSecurities = new List<Security>();
            while (args != null)
            {
                if (args.Item is Security)
                {
                    Security s = (Security)args.Item;
                    string symbol = s.Symbol;

                    if (!string.IsNullOrEmpty(symbol))
                    {
                        switch (args.ChangeType)
                        {
                            case ChangeType.Changed:
                            case ChangeType.Inserted:
                                lock (fetched)
                                {
                                    if (!fetched.Contains(symbol))
                                    {
                                        newSecurities.Add(s);
                                    }
                                }
                                break;
                            case ChangeType.Deleted:
                                lock (fetched)
                                {
                                    if (fetched.Contains(symbol))
                                    {
                                        fetched.Remove(symbol);
                                        newSecurities.Remove(s);
                                    }
                                }
                                break;
                        }
                    }
                }
                args = args.Next;
            }

            Enqueue(newSecurities);
        }

        private void Enqueue(List<Security> toFetch)
        {
            if (processingResults)
            {
                // avoid triggering our own infinite loop.
                return;
            }
            lock (queue)
            {
                foreach (Security s in toFetch)
                {
                    if (!queue.Contains(s))
                    {
                        queue.Add(s);
                    }
                }
            }
            if (queue.Count > 0)
            {
                BeginGetQuotes();
            }
        }

        public void UpdateQuotes()
        {
            Enqueue(myMoney.GetOwnedSecurities());
        }

        void BeginGetQuotes()
        {
            stop = false;

            List<Security> localCopy;
            lock (queue)
            {
                localCopy = new List<Security>(queue);
                queue.Clear();
            }

            _progressMax = localCopy.Count;
            _service.BeginFetchQuotes(localCopy);
        }

        private void OnServiceQuoteAvailable(object sender, StockQuote e)
        {
            if (status != null)
            {
                status.ShowProgress(e.Name, 0, _progressMax, _progressMax - _service.PendingCount);
            }
            lock (fetched)
            {
                fetched.Add(e.Symbol);
            }
            _batch.Add(e);
        }

        private void OnServiceQuotesComplete(object sender, bool complete)
        {
            if (complete)
            {
                if (status != null)
                {
                    status.ShowProgress(string.Empty, 0, 0, 0);
                }
                if (!stop)
                {
                    OnDownloadComplete();
                }
            }
             
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                // must run on the UI thread because some Money changed event handlers change dependency properties and that requires UI thread.
                ProcessResults(_batch);
                _batch.Clear();
            }));
        }

        private void OnServiceDownloadError(object sender, string e)
        {
            AddError(e);
        }

        EventHandlerCollection<EventArgs> handlers;

        public event EventHandler<EventArgs> DownloadComplete
        {
            add
            {
                if (handlers == null)
                {
                    handlers = new EventHandlerCollection<EventArgs>();
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

        void OnDownloadComplete()
        {
            if (handlers != null && handlers.HasListeners)
            {
                handlers.RaiseEvent(this, EventArgs.Empty);
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
                StopThread();
            }
        }

        public bool Busy
        {
            get
            {
                return busy;
            }
        }

        void StopThread()
        {
            stop = true;
            if (_service != null)
            {
                _service.Cancel();
            }
            if (status != null)
            {
                status.ShowProgress(string.Empty, 0, 0, 0);
            }
        }

        bool processingResults;

        private void ProcessResults(List<StockQuote> results)
        {
            try
            {
                processingResults = true;  // lock out Enqueue.

                // Now batch update the securities instead of dribbling them in one by one.
                this.myMoney.Securities.BeginUpdate(true);
                try
                {
                    foreach (StockQuote quote in results)
                    {
                        ProcessResult(quote);
                    }
                }
                finally
                {
                    this.myMoney.Securities.EndUpdate();
                }
                
                busy = false;

                if (hasError && !stop)
                {
                    ShowErrors(null);
                }
            }
            catch (Exception e)
            {
                MessageBoxEx.Show(e.ToString(), Walkabout.Properties.Resources.StockQuotesException, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                processingResults = false;
            }
        }

        private void ShowErrors(string path)
        {
            Paragraph p = new Paragraph();
            p.Inlines.Add(errorLog.ToString());
            p.Inlines.Add(new LineBreak());
            p.Inlines.Add(new LineBreak());
            if (!string.IsNullOrEmpty(path))
            {
                p.Inlines.Add("See ");
                var link = new Hyperlink() { NavigateUri = new Uri("file://" + path) };
                link.Cursor = Cursors.Arrow;
                link.PreviewMouseLeftButtonDown += OnShowLogFile;
                link.Inlines.Add("Log File");
                p.Inlines.Add(link);
                p.Inlines.Add(" for details");
            }
            OutputPane output = (OutputPane)provider.GetService(typeof(OutputPane));
            output.AppendHeading(Walkabout.Properties.Resources.StockQuoteErrorCaption);
            output.AppendParagraph(p);
            output.Show();
        }

        void OnShowLogFile(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)sender;
            Uri uri = link.NavigateUri;
            InternetExplorer.OpenUrl(IntPtr.Zero, uri.AbsoluteUri);
        }


        void AddError(string msg)
        {
            hasError = true;
            errorLog.AppendLine(msg);
        }

        void ProcessResult(StockQuote quote)
        {
            string symbol = quote.Symbol;
            decimal price = quote.Price;
            if (price != 0)
            {
                // we want to stop this from adding new Security objects by passing false
                // because the Security objects should already exist as given to Enqueue
                // and we don't even fetch anything of those Security objects don't already
                // have a 'Symbol' to lookup.
                Security s = this.myMoney.Securities.FindSymbol(symbol, false);
                if (s == null || s.IsDeleted)
                {
                    return;
                }

                // Check to see if the security name has changed and update if needed
                string securityName = quote.Name;
                if (!string.IsNullOrEmpty(securityName) && (string.IsNullOrEmpty(s.Name) || s.Name == symbol))
                {
                    s.Name = securityName;
                }
                s.Price = price;
                s.PriceDate = quote.Date;
            }
        }

    }

    public class StockQuoteThrottle
    {
        static StockQuoteThrottle _instance = null;
        DateTime _lastCall = DateTime.MinValue;
        int _callsThisMinute;
        int _callsToday;

        public StockQuoteThrottle()
        {
            _instance = this;
        }

        [XmlIgnore]
        public StockServiceSettings Settings { get; set; }

        public DateTime LastCall
        {
            get { return _lastCall; }
            set { _lastCall = value; }
        }

        public int CallsThisMinute
        {
            get { return _callsThisMinute; }
            set { _callsThisMinute = value; }
        }

        public int CallsToday
        {
            get { return _callsToday; }
            set { _callsToday = value; }
        }

        /// <summary>
        /// Get throttled sleep amount in milliseconds.
        /// </summary>
        /// <returns></returns>
        public int GetSleep()
        {
            DateTime now = DateTime.Today;
            if (now.Date == _lastCall.Date)
            {
                _callsToday++;
                if (_callsToday > Settings.ApiRequestsPerDayLimit)
                {
                    throw new Exception(Walkabout.Properties.Resources.StockServiceQuotaExceeded);
                }
                if (now.Hour == _lastCall.Hour && now.Minute == _lastCall.Minute)
                {
                    _callsThisMinute++;
                    if (_callsThisMinute >= Settings.ApiRequestsPerMinuteLimit)
                    {
                        _callsThisMinute = 0;
                        return 60000; // sleep to next minute.
                    }
                }
            }
            _lastCall = now;
            return 0;
        }

        public void Save()
        {
            XmlSerializer s = new XmlSerializer(typeof(StockQuoteThrottle));
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            using (XmlWriter w = XmlWriter.Create(FileName, settings))
            {
                s.Serialize(w, this);
            }
        }

        private static StockQuoteThrottle Load()
        {
            if (System.IO.File.Exists(FileName))
            {
                XmlSerializer s = new XmlSerializer(typeof(StockQuoteThrottle));
                using (XmlReader r = XmlReader.Create(FileName))
                {
                    return (StockQuoteThrottle)s.Deserialize(r);
                }
            }
            return new StockQuoteThrottle();
        }

        public static StockQuoteThrottle Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Load();
                }
                return _instance;
            }
        }


        internal static string FileName
        {
            get
            {
                return System.IO.Path.Combine(ProcessHelper.AppDataPath, "throttle.xml");
            }
        }

    }

}
