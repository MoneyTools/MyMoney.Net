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

namespace Walkabout.StockQuotes
{
    /// <summary>
    /// This class tracks changes to Securities and fetches stock quotes from the configured online stock quote service.
    /// </summary>
    public class StockQuoteManager : IDisposable
    {
        MyMoney myMoney;
        StringBuilder errorLog = new StringBuilder();
        bool hasError;
        bool disposed;
        List<Security> queue = new List<Security>(); // list of securities to fetch
        HashSet<string> fetched = new HashSet<string>(); // list that we have already fetched.
        IStatusService status;
        IServiceProvider provider;
        StockServiceSettings _settings;
        IStockQuoteService _service;

        List<StockQuote> _batch = new List<StockQuote>();
        HashSet<string> _unknown = new HashSet<string>();
        DelayedActions delayedActions = new DelayedActions();
        Dictionary<string, StockQuoteHistory> history = new Dictionary<string, StockQuoteHistory>();
        HistoryDownloader _downloader;
        string _logPath;

        public StockQuoteManager(IServiceProvider provider, StockServiceSettings settings)
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
                    service = new AlphaVantage(_settings, this.LogPath);
                }
                else if (IEXTrading.IsMySettings(_settings))
                {
                    service = new IEXTrading(_settings, this.LogPath);
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
                _service.Suspended -= OnServiceSuspended;
                _service.SymbolNotFound -= OnSymbolNotFound;
                _service.Cancel();
            }
            _service = service;
            if (_service != null)
            {
                _service.DownloadError += OnServiceDownloadError;
                _service.QuoteAvailable += OnServiceQuoteAvailable;
                _service.Complete += OnServiceQuotesComplete;
                _service.Suspended += OnServiceSuspended;
                _service.SymbolNotFound += OnSymbolNotFound;
            }
        }

        private void OnSymbolNotFound(object sender, string symbol)
        {
            // todo: what kind of cleanup should we do with symbols that are no longer trading?
            lock(_unknown)
            {
                _unknown.Add(symbol);
            }
        }

        private void OnServiceSuspended(object sender, bool suspended)
        {
            OnServiceQuotesComplete(sender, false);
            var max = _service.DownloadsCompleted + _service.PendingCount;
            if (suspended)
            {
                status.ShowProgress("Zzzz!", 0, max, max - _service.PendingCount);
            }
            else
            {
                status.ShowProgress("", 0, max, max - _service.PendingCount);
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
                    if (!queue.Contains(s) && s.PriceDate.Date != DateTime.Today)
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
            // start with owned securities first
            Enqueue(myMoney.GetOwnedSecurities());
            // then complete the picture with everything else that is referenced by a Transaction in an open account
            Enqueue(myMoney.GetUsedSecurities((a) => !a.IsClosed));
            // then complete the picture with everything else that is referenced by a Transaction in a closed account
            Enqueue(myMoney.GetUsedSecurities((a) => a.IsClosed));
        }

        void BeginGetQuotes()
        {
            if (_service == null)
            {
                return;
            }

            List<string> batch = new List<string>();
            lock (queue)
            {
                foreach (var item in queue)
                {
                    if (!string.IsNullOrEmpty(item.Symbol))
                    {
                        batch.Add(item.Symbol);
                    }
                }
                queue.Clear();
            }

            OutputPane output = (OutputPane)provider.GetService(typeof(OutputPane));
            output.Clear();
            output.AppendHeading(Walkabout.Properties.Resources.StockQuoteCaption);
            
            GetDownloader().BeginFetchHistory(batch);
            _service.BeginFetchQuotes(batch);
        }

        private HistoryDownloader GetDownloader()
        {
            if (_downloader == null)
            {
                _downloader = new HistoryDownloader(_service, this.LogPath);
                _downloader.Error += OnDownloadError;
                _downloader.HistoryAvailable += OnHistoryAvailable;
            }
            return _downloader;
        }

        public event EventHandler<StockQuoteHistory> HistoryAvailable;

        private void OnHistoryAvailable(object sender, StockQuoteHistory history)
        {
            if (HistoryAvailable != null)
            {
                HistoryAvailable(this, history);
            }
            this.history[history.Symbol] = history;
        }

        private void OnDownloadError(object sender, string error)
        {
            AddError(error);
        }

        public StockQuoteHistory GetStockQuoteHistory(string symbol)
        {
            StockQuoteHistory history = null;
            this.history.TryGetValue(symbol, out history);
            if (history == null)
            {
                history = StockQuoteHistory.Load(this.LogPath, symbol);
                if (history != null)
                {
                    this.history[symbol] = history;
                    OnHistoryAvailable(this, history);
                }
            }
            return history;
        }

        private void OnServiceQuoteAvailable(object sender, StockQuote e)
        {
            if (status != null)
            {
                var max = _service.DownloadsCompleted + _service.PendingCount;
                status.ShowProgress(e.Name, 0, max, max - _service.PendingCount);
            }
            lock (fetched)
            {
                fetched.Add(e.Symbol);
            }
            StockQuoteHistory history = GetStockQuoteHistory(e.Symbol);
            if (history == null)
            {
                // then start a new history!
                history = new StockQuoteHistory() { Symbol = e.Symbol };
            }
            if (history.AddQuote(e))
            {
                history.Save(this.LogPath);
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
                if (!disposed)
                {
                    OnDownloadComplete();
                }
            }
             
            UiDispatcher.BeginInvoke(new Action(() =>
            {
                UpdateUI();
            }));
        }

        private void UpdateUI()
        {
            // must run on the UI thread because some Money changed event handlers change dependency properties and that requires UI thread.
            lock (_unknown)
            {
                if (_unknown.Count > 0)
                {
                    AddError(Walkabout.Properties.Resources.FoundUnknownStockQuotes);
                }
                _unknown.Clear();
            }
            ProcessResults(_batch);
            _batch.Clear();

            if (hasError && !disposed)
            {
                ShowErrors(null);
            }
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
            disposed = true;
            if (disposing)
            {
                StopThread();
            }
        }

        public bool Busy
        {
            get
            {
                return _service != null && _service.PendingCount > 0;
            }
        }

        /// <summary>
        /// Location where we store the stock quote log files.
        /// </summary>
        public string LogPath
        {
            get { return this._logPath; }
            set { this._logPath = value; }
        }

        void StopThread()
        {            
            if (_service != null)
            {
                _service.Cancel();
            }
            if (_downloader != null)
            {
                _downloader.Cancel();
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
            var errorMessages = errorLog.ToString();
            if (string.IsNullOrEmpty(errorMessages))
            {
                return;
            }
            Paragraph p = new Paragraph();
            p.Inlines.Add(errorMessages);
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
            output.AppendParagraph(p);
            output.Show();
            errorLog = new StringBuilder();
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
            decimal price = quote.Close;
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

        internal void BeginDownloadHistory(string symbol)
        {
            if (_service == null)
            {
                return;
            }
            GetDownloader().BeginFetchHistory(new List<string>(new string[] { symbol }));
        }

        internal bool HasStockQuoteHistory(string symbol)
        {
            return GetStockQuoteHistory(symbol) != null;
        }
    }

    public class DownloadInfo
    {
        public DownloadInfo() { }
        [XmlAttribute]
        public string Symbol { get; set; }
        [XmlAttribute]
        public DateTime Downloaded { get; set; }
    }

    /// <summary>
    /// A log of stocks we have downloaded a history for
    /// </summary>
    public class DownloadLog
    {
        public DownloadLog() { Downloaded = new List<DownloadInfo>();  }

        public List<DownloadInfo> Downloaded { get; set; }

        public DownloadInfo GetInfo(string symbol)
        {
            if (this.Downloaded == null)
            {
                return null;
            }
            return (from i in this.Downloaded where i.Symbol == symbol select i).FirstOrDefault();
        }

        public static DownloadLog Load(string logFolder)
        {
            var filename = System.IO.Path.Combine(logFolder, "DownloadLog.xml");
            if (System.IO.File.Exists(filename))
            {
                XmlSerializer s = new XmlSerializer(typeof(DownloadLog));
                using (XmlReader r = XmlReader.Create(filename))
                {
                    var log = (DownloadLog)s.Deserialize(r);                    
                    // remove any entries for files that don't exist any more.
                    foreach(var info in log.Downloaded.ToArray())
                    {
                        var quotes = System.IO.Path.Combine(logFolder, info.Symbol + ".xml");
                        if (!System.IO.File.Exists(quotes))
                        {
                            log.Downloaded.Remove(info);
                        }
                    }
                    return log;
                }
            }
            return new DownloadLog();
        }

        public void Save(string logFolder)
        {
            var filename = System.IO.Path.Combine(logFolder, "DownloadLog.xml");
            XmlSerializer s = new XmlSerializer(typeof(DownloadLog));
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            using (XmlWriter w = XmlWriter.Create(filename, settings))
            {
                s.Serialize(w, this);
            }
        }

    }

    class HistoryDownloader
    {
        object _downloadSync = new object();
        HashSet<string> _downloadBatch;
        bool _downloadingHistory;
        IStockQuoteService _service;
        DownloadLog _downloadLog;
        string _logPath;

        public HistoryDownloader(IStockQuoteService service, string logPath)
        {
            _service = service;
            _logPath = logPath;
        }

        public event EventHandler<string> Error;

        public event EventHandler<StockQuoteHistory> HistoryAvailable;

        void OnHistoryAvailable(StockQuoteHistory history)
        {
            if (HistoryAvailable != null)
            {
                HistoryAvailable(this, history);
            }
        }

        Task _downloadLogTask = null;

        async Task<DownloadLog> GetDownloadLogAsync()
        {
            if (this._downloadLog != null)
            {
                return this._downloadLog;
            }
            if (_downloadLogTask != null)
            {
                // download has been kicked off already, but someone else also wants it, so they have to wait.
                await _downloadLogTask;
            }
            else
            {
                _downloadLogTask = Task.Run(new Action(() =>
                {
                    this._downloadLog = DownloadLog.Load(this._logPath);
                }));
                await _downloadLogTask;
                _downloadLogTask = null;
            }
            return this._downloadLog;
        }

        public async void BeginFetchHistory(List<string> batch)
        {
            lock (_downloadSync)
            {
                if (_downloadingHistory)
                {
                    foreach (var item in batch)
                    {
                        _downloadBatch.Add(item);
                    }
                    return;
                }
                else
                {
                    _downloadBatch = new HashSet<string>(batch);
                }
            }
            _downloadingHistory = true;
            await GetDownloadLogAsync();

            while (_downloadingHistory)
            {
                string symbol = null;
                lock (_downloadSync)
                {
                    if (_downloadBatch != null && _downloadBatch.Count > 0)
                    {
                        symbol = _downloadBatch.First();
                        _downloadBatch.Remove(symbol);
                    }
                }
                if (symbol == null)
                {
                    break;
                }
                else if (_downloadLog.GetInfo(symbol) == null)
                {
                    try
                    {
                        var history = await _service.DownloadHistory(symbol);
                        if (history != null && history.History != null && history.History.Count != 0)
                        {
                            history.Save(this._logPath);
                            _downloadLog.Downloaded.Add(new DownloadInfo() { Downloaded = DateTime.Today, Symbol = symbol });
                            _downloadLog.Save(this._logPath);
                            OnHistoryAvailable(history);
                        }
                    }
                    catch (Exception ex)
                    {
                        OnError("Download history error: " + ex.Message);
                    }
                }
                else
                {
                    var history = StockQuoteHistory.Load(this._logPath, symbol);
                    if (history != null)
                    {
                        OnHistoryAvailable(history);
                    }
                }
            }
            _downloadingHistory = false;
        }

        void OnError(string message)
        {
            if (Error != null) {
                Error(this, message);
            }
        }

        internal void Cancel()
        {
            _downloadingHistory = false;
        }
    }

}
