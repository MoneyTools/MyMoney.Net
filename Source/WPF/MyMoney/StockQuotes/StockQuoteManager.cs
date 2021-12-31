using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Controls;
using Walkabout.Data;
using Walkabout.Utilities;

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
        HashSet<string> fetched = new HashSet<string>(); // list that we have already fetched.
        IStatusService status;
        IServiceProvider provider;
        List<StockServiceSettings> _settings;
        List<IStockQuoteService> _services = new List<IStockQuoteService>();
        DownloadLog _downloadLog = new DownloadLog();
        List<StockQuote> _batch = new List<StockQuote>();
        HashSet<string> _unknown = new HashSet<string>();
        DelayedActions delayedActions = new DelayedActions();
        HistoryDownloader _downloader;
        string _logPath;
        bool _firstError = true;

        public StockQuoteManager(IServiceProvider provider, List<StockServiceSettings> settings, string logPath)
        {
            this._logPath = logPath;
            EnsurePathExists(logPath);
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
            _downloadLog = DownloadLog.Load(logPath);
         }

        public List<StockServiceSettings> Settings
        {
            get
            {
                return _settings;
            }
            set
            {
                StopThread();
                ResetServices();
                _settings = value;
                if (!string.IsNullOrEmpty(this.LogPath))
                {
                    UpdateServices();
                }

            }
        }

        private void ResetServices()
        {
            foreach (var item in this._services)
            {
                item.DownloadError -= OnServiceDownloadError;
                item.QuoteAvailable -= OnServiceQuoteAvailable;
                item.Complete -= OnServiceQuotesComplete;
                item.Suspended -= OnServiceSuspended;
                item.SymbolNotFound -= OnSymbolNotFound;
                item.Cancel();
            }
        }

        private void UpdateServices()
        {
            this._services = new List<IStockQuoteService>();

            foreach (var item in _settings)
            {
                IStockQuoteService service = null;
                if (AlphaVantage.IsMySettings(item))
                {
                    service = new AlphaVantage(item, this.LogPath);
                }
                else if (IEXCloud.IsMySettings(item))
                {
                    service = new IEXCloud(item, this.LogPath);
                }

                if (service != null)
                {
                    service.DownloadError += OnServiceDownloadError;
                    service.QuoteAvailable += OnServiceQuoteAvailable;
                    service.Complete += OnServiceQuotesComplete;
                    service.Suspended += OnServiceSuspended;
                    service.SymbolNotFound += OnSymbolNotFound;
                    this._services.Add(service);
                }
            }
        }

        private void OnSymbolNotFound(object sender, string symbol)
        {
            // todo: what kind of cleanup should we do with symbols that are no longer trading?
            lock (_unknown)
            {
                _unknown.Add(symbol);
            }
        }

        Tuple<int,int> GetProgress()
        {
            int max = 0;
            int value = 0;
            bool complete = true;
            foreach(var service in _services)
            {
                int m = service.DownloadsCompleted + service.PendingCount;
                int v = service.DownloadsCompleted;   
                if (v < m) { complete = false; }
                max += m;
                value += v;
                
            }
            if (complete)
            {
                max = 0;
                value = 0;
            }
            return new Tuple<int, int>(max, value);
        }

        private void OnServiceSuspended(object sender, bool suspended)
        {
            OnServiceQuotesComplete(sender, new DownloadCompleteEventArgs() { Complete = false });
            Tuple<int, int> progress = GetProgress();
            if (suspended)
            {
                status.ShowProgress("Zzzz!", 0, progress.Item1, progress.Item2);
            }
            else
            {
                status.ShowProgress("", 0, progress.Item1, progress.Item2);
            }
        }

        public List<StockServiceSettings> GetDefaultSettingsList()
        {
            List<StockServiceSettings> result = new List<StockServiceSettings>();
            result.Add(IEXCloud.GetDefaultSettings());
            result.Add(AlphaVantage.GetDefaultSettings());
            return result;
        }

        void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
            if (processingResults)
            {
                // we are updating the security, so avoid infinite update loop here
                return;
            }
            HashSet<Security> newSecurities = new HashSet<Security>();
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

            BeginGetQuotes(newSecurities);
        }

        public void UpdateQuotes()
        {
            // start with owned securities first
            HashSet<Security> combined = new HashSet<Security>(myMoney.GetOwnedSecurities());
            // then complete the picture with everything else that is referenced by a Transaction in an open account
            foreach (Security s in myMoney.GetUsedSecurities((a) => !a.IsClosed))
            {
                combined.Add(s);
            }
            // then complete the picture with everything else that is referenced by a Transaction in a closed account
            foreach (Security s in myMoney.GetUsedSecurities((a) => a.IsClosed))
            {
                combined.Add(s);
            }
            BeginGetQuotes(combined);
        }

        void BeginGetQuotes(HashSet<Security> toFetch)
        {
            _firstError = true;
            if (_services.Count == 0 || toFetch.Count == 0)
            {
                return;
            }

            UiDispatcher.BeginInvoke(new Action(() =>
            {
                OutputPane output = (OutputPane)provider.GetService(typeof(OutputPane));
                output.Clear();
                output.AppendHeading(Walkabout.Properties.Resources.StockQuoteCaption);
            }));

            List<string> batch = new List<string>();
            foreach (Security s in toFetch)
            {
                if (string.IsNullOrEmpty(s.Symbol) || s.SecurityType == SecurityType.Private)
                {
                    continue; // skip it.
                }
                batch.Add(s.Symbol);
            }

            bool foundService = false;
            IStockQuoteService service = GetHistoryService();
            HistoryDownloader downloader = GetDownloader(service);
            if (service != null)
            {
                downloader.BeginFetchHistory(batch);
                foundService = true;
            }

            service = GetQuoteService();
            if (service != null)
            {
                foundService = true;
                if (service.SupportsBatchQuotes)
                {
                    service.BeginFetchQuotes(batch);
                }
                else
                {
                    foreach (var item in batch)
                    {
                        service.BeginFetchQuote(item);
                    }
                }
            }
            
            if (!foundService)
            {
                AddError(Walkabout.Properties.Resources.ConfigureStockQuoteService);
                UiDispatcher.BeginInvoke(new Action(UpdateUI));
            }
        }

        private IStockQuoteService GetQuoteService()
        {
            IStockQuoteService result = null;
            foreach (var service in _services)
            {
                if (service.IsEnabled)
                {
                    if (service.SupportsBatchQuotes)
                    {
                        return service;
                    }
                    if (result == null)
                    {
                        result = service;
                    }
                }
            }
            return result;
        }

        private IStockQuoteService GetHistoryService()
        {
            foreach (var service in _services)
            {
                if (service.SupportsHistory && service.IsEnabled)
                {
                    return service;
                }
            }
            return null;
        }

        private HistoryDownloader GetDownloader(IStockQuoteService service)
        {
            if (_downloader == null)
            {
                _downloader = new HistoryDownloader(service, this._downloadLog);
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
        }

        private void OnDownloadError(object sender, string error)
        {
            AddError(error);
        }

        private void OnServiceQuoteAvailable(object sender, StockQuote e)
        {
            Tuple<int, int> progress = GetProgress();
            status.ShowProgress(e.Name, 0, progress.Item1, progress.Item2);

            lock (fetched)
            {
                fetched.Add(e.Symbol);
            }
            this._downloadLog.OnQuoteAvailable(e);
            lock (_batch)
            {
                _batch.Add(e);
            }
        }

        private void OnServiceQuotesComplete(object sender, DownloadCompleteEventArgs args)
        {
            if (!string.IsNullOrEmpty(args.Message))
            {
                AddError(args.Message);
            }
            if (args.Complete)
            {
                Tuple<int, int> progress = GetProgress();
                status.ShowProgress("", 0, progress.Item1, progress.Item2);

                if (!disposed)
                {
                    OnDownloadComplete();
                }
            }

            UiDispatcher.BeginInvoke(new Action(UpdateUI));
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

            List<StockQuote> results = null;
            lock (_batch)
            {
                results = _batch;
                _batch = new List<StockQuote>();
            }
            ProcessResults(results);

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
                ResetServices();
            }
        }

        public bool Busy
        {
            get
            {
                return (from s in _services where s.PendingCount > 0 select s).Any();
            }
        }

        /// <summary>
        /// Location where we store the stock quote log files.
        /// </summary>
        public string LogPath
        {
            get { return this._logPath; }
        }

        static void EnsurePathExists(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
        }

        void StopThread()
        {
            foreach (var item in _services)
            {
                item.Cancel();
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
            p.Inlines.Add(errorMessages.Trim());
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
            if (_firstError)
            {
                _firstError = false;
                output.Show();
            }
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
            if (!string.IsNullOrWhiteSpace(msg))
            {
                hasError = true;
                errorLog.AppendLine(msg);
            }
        }

        void ProcessResult(StockQuote quote)
        {
            string symbol = quote.Symbol;
            decimal price = quote.Close;
            if (price != 0)
            {
                // we want to stop this from adding new Security objects by passing false
                // because the Security objects should already exist as given to Enqueue
                // and we shouldn't even fetch anything of those Security objects don't already
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
            _firstError = true;
            var service = GetHistoryService();
            if (service != null)
            {
                GetDownloader(service).BeginFetchHistory(new List<string>(new string[] { symbol }));
            }
        }

        internal async Task<StockQuoteHistory> GetCachedHistory(string symbol)
        {
            var service = GetHistoryService();
            if (service != null)
            {
                return await GetDownloader(service).GetCachedHistory(symbol);
            }
            return null;
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
        Dictionary<string, StockQuoteHistory> database = new Dictionary<string, StockQuoteHistory>();
        Dictionary<string, DownloadInfo> _downloaded = new Dictionary<string, DownloadInfo>();
        DelayedActions delayedActions = new DelayedActions();
        string _logFolder;
        Dictionary<string, StockQuote> _downloadedQuotes = new Dictionary<string, StockQuote>();

        public DownloadLog() { Downloaded = new List<DownloadInfo>(); }

        public List<DownloadInfo> Downloaded { get; set; }

        public DownloadInfo GetInfo(string symbol)
        {
            DownloadInfo info = null;
            _downloaded.TryGetValue(symbol, out info);
            return info;
        }

        public void OnQuoteAvailable(StockQuote quote)
        {
            // record downloaded quotes so they can be merged with quote histories.
            lock (_downloadedQuotes)
            {
                _downloadedQuotes[quote.Symbol] = quote;
            }
        }

        public async Task<StockQuoteHistory> GetHistory(string symbol)
        {
            if (database.ContainsKey(symbol))
            {
                return database[symbol];
            }

            StockQuoteHistory history = null;
            DownloadInfo info;
            var changed = false;
            var changedHistory = false;
            if (_downloaded.TryGetValue(symbol, out info))
            { 
                // read from disk on background thread so we don't block the UI thread loading
                // all these stock quote histories.
                await Task.Run(() =>
                {
                    try
                    {
                        history = StockQuoteHistory.Load(this._logFolder, symbol);
                        if (symbol == "GE")
                        {
                            Debug.WriteLine("debug me");
                        }
                        if (history.RemoveDuplicates())
                        {
                            changedHistory = true;
                        }
                    }
                    catch (Exception)
                    {
                        // file is bad, so ignore it
                    }
                });
                if (history == null)
                {
                    this.Downloaded.Remove(info);
                    this._downloaded.Remove(info.Symbol);
                    changed = true;
                }
                else
                {
                    database[symbol] = history;
                }
                lock(_downloadedQuotes)
                {
                    if (_downloadedQuotes.ContainsKey(info.Symbol))
                    {
                        if (history.AddQuote(_downloadedQuotes[info.Symbol]))
                        {
                            changedHistory = true;
                        }
                    }
                }
            }
            if (changed)
            {
                DelayedSave();
            }
            if (changedHistory)
            {
                history.Save(_logFolder);
            }
            return history;
        }

        private void DelayedSave()
        {
            if (!string.IsNullOrEmpty(_logFolder))
            {
                delayedActions.StartDelayedAction("save", new Action(() => { Save(_logFolder); }), TimeSpan.FromSeconds(1));
            }
        }

        public void AddHistory(StockQuoteHistory history)
        {
            this.database[history.Symbol] = history;
            DownloadInfo info = GetInfo(history.Symbol);
            if (info == null)
            {
                info = new DownloadInfo() { Downloaded = DateTime.Today, Symbol = history.Symbol };
                this.Downloaded.Add(info);
                this._downloaded[info.Symbol] = info;
            }
            else
            {
                info.Downloaded = DateTime.Today;
            }
            DelayedSave();
        }

        public static DownloadLog Load(string logFolder)
        {
            DownloadLog log = new DownloadLog();
            var filename = System.IO.Path.Combine(logFolder, "DownloadLog.xml");
            if (System.IO.File.Exists(filename))
            {
#if PerformanceBlocks
                using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.LoadStockDownloadLog))
                {
#endif
                    try
                    {
                        XmlSerializer s = new XmlSerializer(typeof(DownloadLog));
                        using (XmlReader r = XmlReader.Create(filename))
                        {
                            log = (DownloadLog)s.Deserialize(r);
                        }
                    }
                    catch (Exception)
                    {
                        // got corrupted? no problem, just start over.
                        log = new DownloadLog();
                    }
                    log._logFolder = logFolder;

                    // ensure unique list.
                    foreach (var info in log.Downloaded.ToArray())
                    {
                        log._downloaded[info.Symbol] = info;
                    }

                    if (log._downloaded.Count != log.Downloaded.Count)
                    {
                        log.Downloaded.Clear();
                        foreach (var info in log._downloaded.Values)
                        {
                            log.Downloaded.Add(info);
                        }
                    }
#if PerformanceBlocks
                }
#endif
            }
            return log;
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
            Debug.WriteLine("Saved " + filename);
        }

    }

    class HistoryDownloader
    {
        object _downloadSync = new object();
        List<string> _downloadBatch;
        bool _downloadingHistory;
        IStockQuoteService _service;
        DownloadLog _downloadLog;
        CancellationTokenSource tokenSource;

        public HistoryDownloader(IStockQuoteService service, DownloadLog log)
        {
            _service = service;
            _downloadLog = log;
        }

        public event EventHandler<string> Error;

        public event EventHandler<StockQuoteHistory> HistoryAvailable;

        void OnHistoryAvailable(StockQuoteHistory history)
        {
            _downloadLog.AddHistory(history);

            if (HistoryAvailable != null)
            {
                HistoryAvailable(this, history);
            }
        }


        public async void BeginFetchHistory(List<string> batch)
        {
            string singleton = null;
            bool busy = false;
            lock (_downloadSync)
            {
                busy = _downloadingHistory;
                if (busy)
                {
                    if (batch.Count == 1)
                    {
                        // then we need this individual stock ASAP
                        var item = batch[0];
                        if (_downloadBatch.Contains(item))
                        {
                            _downloadBatch.Remove(item);
                        }
                        _downloadBatch.Insert(0, item);
                        singleton = item;
                    }
                    else
                    {
                        // then merge the new batch with existing batch that we are downloading.
                        foreach (var item in batch)
                        {
                            if (!_downloadBatch.Contains(item))
                            {
                                _downloadBatch.Add(item);
                            }
                        }
                    }
                }
                else
                {
                    // starting a new download batch.
                    _downloadBatch = new List<string>(batch);
                }
            }
            if (busy)
            {
                if (!string.IsNullOrEmpty(singleton))
                {
                    // in this case we want to load any cached history and make that available to unblock the UI thread ASAP
                    // otherwise UI might be blocks on slow HTTP downloads.
                    var history = await this._downloadLog.GetHistory(singleton);
                    if (history != null && history.History != null && history.History.Count != 0)
                    {
                        // unblock the UI thread with the cached history for now.
                        OnHistoryAvailable(history);
                    }
                }
                // only allow one thread do all the downloading.
                return;
            }

            tokenSource = new CancellationTokenSource();
            _downloadingHistory = true;

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
                else
                {

#if PerformanceBlocks
                    using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.DownloadStockQuoteHistory))
                    {
#endif
                        StockQuoteHistory history = null;
                        var info = this._downloadLog.GetInfo(symbol);
                        history = await this._downloadLog.GetHistory(symbol);
                        if (history == null)
                        {
                            history = new StockQuoteHistory() { Symbol = symbol };
                        }
                        if (info != null && info.Downloaded.Date == DateTime.Today && history != null && history.Complete)
                        {
                            // already up to date
                        }
                        else
                        {
                            try
                            {
                                await _service.UpdateHistory(history);
                            }
                            catch (Exception ex)
                            {
                                OnError("Download history error: " + ex.Message);
                            }
                        }
                        if (history != null && history.History != null && history.History.Count != 0)
                        {
                            OnHistoryAvailable(history);
                        }
#if PerformanceBlocks
                    }
#endif
                }
            }
            _downloadingHistory = false;

            while (_downloadingHistory && _downloadBatch.Count > 0) {
                Thread.Sleep(1000); // wait for download to finish.
            }
            _downloadingHistory = false;
        }

        void OnError(string message)
        {
            if (Error != null)
            {
                Error(this, message);
            }
        }

        internal void Cancel()
        {
            _downloadingHistory = false;
            if (tokenSource != null)
            {
                tokenSource.Cancel();
            }
        }

        public async Task<StockQuoteHistory> GetCachedHistory(string symbol)
        {
            return await this._downloadLog.GetHistory(symbol);
        }
    }

}
