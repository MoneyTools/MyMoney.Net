using System;
using System.Collections.Concurrent;
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
        private readonly MyMoney myMoney;
        private StringBuilder errorLog = new StringBuilder();
        private bool hasError;
        private bool disposed;
        private readonly HashSet<string> fetched = new HashSet<string>(); // list that we have already fetched.
        private readonly IStatusService status;
        private readonly IServiceProvider provider;
        private List<StockServiceSettings> _settings;
        private List<IStockQuoteService> _services = new List<IStockQuoteService>();
        private readonly DownloadLog _downloadLog = new DownloadLog();
        private List<StockQuote> _batch = new List<StockQuote>();
        private readonly HashSet<string> _unknown = new HashSet<string>();
        private HistoryDownloader _downloader;
        private readonly string _logPath;
        private bool _firstError = true;

        public StockQuoteManager(IServiceProvider provider, List<StockServiceSettings> settings, string logPath)
        {
            this._logPath = logPath;
            EnsurePathExists(logPath);
            this.Settings = settings;
            this.provider = provider;
            this.myMoney = (MyMoney)provider.GetService(typeof(MyMoney));
            this.status = (IStatusService)provider.GetService(typeof(IStatusService));
            this.myMoney.Changed += new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);

            // assume we have fetched all securities.
            // call UpdateQuotes to refetch them all again, otherwise this
            // class will track changes and automatically fetch any new securities that it finds.
            foreach (Security s in this.myMoney.Securities.AllSecurities)
            {
                if (!string.IsNullOrEmpty(s.Symbol))
                {
                    lock (this.fetched)
                    {
                        this.fetched.Add(s.Symbol);
                    }
                }
            }
            bool isNew = false;
            (this._downloadLog, isNew) = DownloadLog.Load(logPath);
            if (isNew)
            {
                this._downloadLog.DelayedSave();
            }
        }

        public DownloadLog DownloadLog
        {
            get => this._downloadLog;
        }

        public List<StockServiceSettings> Settings
        {
            get
            {
                return this._settings;
            }
            set
            {
                this.StopThread();
                this.ResetServices();
                this._settings = value;
                if (!string.IsNullOrEmpty(this.LogPath))
                {
                    this.UpdateServices();
                }

            }
        }

        private void ResetServices()
        {
            foreach (var item in this._services)
            {
                item.DownloadError -= this.OnServiceDownloadError;
                item.QuoteAvailable -= this.OnServiceQuoteAvailable;
                item.Complete -= this.OnServiceQuotesComplete;
                item.Suspended -= this.OnServiceSuspended;
                item.SymbolNotFound -= this.OnSymbolNotFound;
                item.Cancel();
            }
        }

        private IStockQuoteService GetServiceForSettings(StockServiceSettings settings)
        {
            foreach (var existing in this._services)
            {
                if (existing.FriendlyName == settings.Name)
                {
                    return existing;
                }
            }
            IStockQuoteService service = null;
            if (AlphaVantage.IsMySettings(settings))
            {
                service = new AlphaVantage(settings, this.LogPath);
            }
            else if (IEXCloud.IsMySettings(settings))
            {
                service = new IEXCloud(settings, this.LogPath);
            }
            else if (PolygonStocks.IsMySettings(settings))
            {
                service = new PolygonStocks(settings, this.LogPath);
            }
            else if (YahooFinance.IsMySettings(settings))
            {
                service = new YahooFinance(settings, this.LogPath);
            }
            return service;
        }

        private void UpdateServices()
        {
            foreach (var service in this._services)
            {
                service.DownloadError -= this.OnServiceDownloadError;
                service.QuoteAvailable -= this.OnServiceQuoteAvailable;
                service.Complete -= this.OnServiceQuotesComplete;
                service.Suspended -= this.OnServiceSuspended;
                service.SymbolNotFound -= this.OnSymbolNotFound;
            }

            this._services = new List<IStockQuoteService>();

            foreach (var item in this._settings)
            {
                IStockQuoteService service = this.GetServiceForSettings(item);
                if (service != null)
                {
                    service.DownloadError += this.OnServiceDownloadError;
                    service.QuoteAvailable += this.OnServiceQuoteAvailable;
                    service.Complete += this.OnServiceQuotesComplete;
                    service.Suspended += this.OnServiceSuspended;
                    service.SymbolNotFound += this.OnSymbolNotFound;
                    this._services.Add(service);
                }
            }
        }

        private void OnSymbolNotFound(object sender, string symbol)
        {
            // Remember this quote was not found so we don't keep asking for it.
            this._downloadLog.OnQuoteNotFound(symbol);

            lock (this._unknown)
            {
                this._unknown.Add(symbol);
            }
        }

        private Tuple<int, int> GetProgress()
        {
            int max = 0;
            int value = 0;
            bool complete = true;
            foreach (var service in this._services)
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
            this.OnServiceQuotesComplete(sender, new DownloadCompleteEventArgs() { Complete = false });
            Tuple<int, int> progress = this.GetProgress();
            if (suspended)
            {
                this.status.ShowProgress("Zzzz!", 0, progress.Item1, progress.Item2);
            }
            else
            {
                this.status.ShowProgress("", 0, progress.Item1, progress.Item2);
            }
        }

        public List<StockServiceSettings> GetDefaultSettingsList()
        {
            List<StockServiceSettings> result = new List<StockServiceSettings>();
            result.Add(IEXCloud.GetDefaultSettings());
            result.Add(AlphaVantage.GetDefaultSettings());
            result.Add(PolygonStocks.GetDefaultSettings());
            result.Add(YahooFinance.GetDefaultSettings());
            return result;
        }

        private void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
            if (this.processingResults)
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
                                lock (this.fetched)
                                {
                                    if (!this.fetched.Contains(symbol))
                                    {
                                        newSecurities.Add(s);
                                    }
                                }
                                break;
                            case ChangeType.Deleted:
                                lock (this.fetched)
                                {
                                    if (this.fetched.Contains(symbol))
                                    {
                                        this.fetched.Remove(symbol);
                                        newSecurities.Remove(s);
                                    }
                                }
                                break;
                        }
                    }
                }
                args = args.Next;
            }

            if (newSecurities.Count > 0)
            {
                this.BeginGetQuotes(newSecurities);
            }
        }

        public void UpdateQuotes()
        {
            // start with owned securities first
            HashSet<Security> combined = new HashSet<Security>(this.myMoney.GetOwnedSecurities());
            // then complete the picture with everything else that is referenced by a Transaction in an open account
            foreach (Security s in this.myMoney.GetUsedSecurities((a) => !a.IsClosed))
            {
                combined.Add(s);
            }
            // then complete the picture with everything else that is referenced by a Transaction in a closed account
            foreach (Security s in this.myMoney.GetUsedSecurities((a) => a.IsClosed))
            {
                combined.Add(s);
            }
            this.BeginGetQuotes(combined);
        }

        private void BeginGetQuotes(HashSet<Security> toFetch)
        {
            this._firstError = true;
            if (this._services.Count == 0 || toFetch.Count == 0)
            {
                return;
            }

            UiDispatcher.BeginInvoke(new Action(() =>
            {
                OutputPane output = (OutputPane)this.provider.GetService(typeof(OutputPane));
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

                var info = this._downloadLog.GetInfo(s.Symbol);
                if (info != null && !info.NotFound)
                {
                    batch.Add(s.Symbol);
                }
            }

            bool foundService = false;
            IStockQuoteService service = this.GetHistoryService();
            if (service != null)
            {
                HistoryDownloader downloader = this.GetDownloader(service);
                // make sure this is async!
                Task.Run(() =>
                {
                    downloader.BeginFetchHistory(batch);
                });
                foundService = true;
            }

            service = this.GetQuoteService();
            if (service != null)
            {
                foundService = true;
                service.BeginFetchQuotes(batch);
            }

            if (!foundService)
            {
                this.AddError(Walkabout.Properties.Resources.ConfigureStockQuoteService);
                UiDispatcher.BeginInvoke(new Action(this.UpdateUI));
            }
        }

        private IStockQuoteService GetQuoteService()
        {
            IStockQuoteService result = null;
            foreach (var service in this._services)
            {
                if (service.IsEnabled)
                {
                    return service;
                }
            }
            return result;
        }

        private IStockQuoteService GetHistoryService()
        {
            foreach (var service in this._services)
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
            if (this._downloader == null)
            {
                this._downloader = new HistoryDownloader(service, this._downloadLog);
                this._downloader.Error += this.OnDownloadError;
                this._downloader.HistoryAvailable += this.OnHistoryAvailable;
            }
            return this._downloader;
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
            this.AddError(error);
        }

        private void OnServiceQuoteAvailable(object sender, StockQuote e)
        {
            Tuple<int, int> progress = this.GetProgress();
            this.status.ShowProgress(e.Name, 0, progress.Item1, progress.Item2);

            lock (this.fetched)
            {
                this.fetched.Add(e.Symbol);
            }
            this._downloadLog.OnQuoteAvailable(e);
            lock (this._batch)
            {
                this._batch.Add(e);
            }
        }

        private void OnServiceQuotesComplete(object sender, DownloadCompleteEventArgs args)
        {
            if (!string.IsNullOrEmpty(args.Message))
            {
                this.AddError(args.Message);
            }
            if (args.Complete)
            {
                Tuple<int, int> progress = this.GetProgress();
                this.status.ShowProgress("", 0, progress.Item1, progress.Item2);

                if (!this.disposed)
                {
                    this.OnDownloadComplete();
                }
            }

            UiDispatcher.BeginInvoke(new Action(this.UpdateUI));
        }

        private void UpdateUI()
        {
            // must run on the UI thread because some Money changed event handlers change dependency properties and that requires UI thread.
            lock (this._unknown)
            {
                if (this._unknown.Count > 0)
                {
                    this.AddError(string.Format(Walkabout.Properties.Resources.FoundUnknownStockQuotes,
                        string.Join(',', this._unknown.ToArray())));
                }
                this._unknown.Clear();
            }

            List<StockQuote> results = null;
            lock (this._batch)
            {
                results = this._batch;
                this._batch = new List<StockQuote>();
            }
            this.ProcessResults(results);

            if (this.hasError && !this.disposed)
            {
                this.ShowErrors(null);
            }
        }

        private void OnServiceDownloadError(object sender, string e)
        {
            this.AddError(e);
        }

        private EventHandlerCollection<EventArgs> handlers;

        public event EventHandler<EventArgs> DownloadComplete
        {
            add
            {
                if (this.handlers == null)
                {
                    this.handlers = new EventHandlerCollection<EventArgs>();
                }
                this.handlers.AddHandler(value);
            }
            remove
            {
                if (this.handlers != null)
                {
                    this.handlers.RemoveHandler(value);
                    if (!this.handlers.HasListeners)
                    {
                        this.handlers = null;
                    }
                }
            }
        }

        private void OnDownloadComplete()
        {
            if (this.handlers != null && this.handlers.HasListeners)
            {
                this.handlers.RaiseEvent(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            this.disposed = true;
            if (disposing)
            {
                this.StopThread();
                this.ResetServices();
                if (this.myMoney != null)
                {
                    this.myMoney.Changed -= new EventHandler<ChangeEventArgs>(this.OnMoneyChanged);
                }
            }
        }

        public bool Busy
        {
            get
            {
                return (from s in this._services where s.PendingCount > 0 select s).Any();
            }
        }

        /// <summary>
        /// Location where we store the stock quote log files.
        /// </summary>
        public string LogPath
        {
            get { return this._logPath; }
        }

        private static void EnsurePathExists(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
        }

        private void StopThread()
        {
            foreach (var item in this._services)
            {
                item.Cancel();
            }
            if (this._downloader != null)
            {
                this._downloader.Cancel();
            }
            if (this.status != null)
            {
                this.status.ShowProgress(string.Empty, 0, 0, 0);
            }
        }

        private bool processingResults;

        private void ProcessResults(List<StockQuote> results)
        {
            try
            {
                this.processingResults = true;  // lock out Enqueue.

                // Now batch update the securities instead of dribbling them in one by one.
                this.myMoney.Securities.BeginUpdate(true);
                try
                {
                    foreach (StockQuote quote in results)
                    {
                        this.ProcessResult(quote);
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
                this.processingResults = false;
            }
        }

        private void ShowErrors(string path)
        {
            var errorMessages = this.errorLog.ToString();
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
                link.PreviewMouseLeftButtonDown += this.OnShowLogFile;
                link.Inlines.Add("Log File");
                p.Inlines.Add(link);
                p.Inlines.Add(" for details");
            }
            OutputPane output = (OutputPane)this.provider.GetService(typeof(OutputPane));
            output.AppendParagraph(p);
            if (this._firstError)
            {
                this._firstError = false;
                output.Show();
            }
            this.errorLog = new StringBuilder();
        }

        private void OnShowLogFile(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)sender;
            Uri uri = link.NavigateUri;
            InternetExplorer.OpenUrl(IntPtr.Zero, uri.AbsoluteUri);
        }

        private void AddError(string msg)
        {
            if (!string.IsNullOrWhiteSpace(msg))
            {
                this.hasError = true;
                this.errorLog.AppendLine(msg);
            }
        }

        private void ProcessResult(StockQuote quote)
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
            this._firstError = true;
            var service = this.GetHistoryService();
            if (service != null)
            {
                this.GetDownloader(service).BeginFetchHistory(new List<string>(new string[] { symbol }));
            }
        }

        internal async Task<StockQuoteHistory> GetCachedHistory(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                return null;
            }
            var service = this.GetHistoryService();
            if (service != null)
            {
                return await this.GetDownloader(service).GetCachedHistory(symbol);
            }
            return null;
        }

        internal async Task<string> TestApiKeyAsync(StockServiceSettings settings)
        {
            var service = this.GetServiceForSettings(settings);
            if (service != null)
            {
                return await service.TestApiKeyAsync(settings.ApiKey);
            }
            return string.Empty;
        }
    }

    public class DownloadInfo
    {
        public DownloadInfo() { }
        [XmlAttribute]
        public string Symbol { get; set; }
        [XmlAttribute]
        public DateTime Downloaded { get; set; }
        [XmlAttribute]
        public bool NotFound { get; set; }
    }

    /// <summary>
    /// A log of stocks we have downloaded a history for
    /// </summary>
    public class DownloadLog
    {
        private readonly ConcurrentDictionary<string, StockQuoteHistory> database = new ConcurrentDictionary<string, StockQuoteHistory>();
        private readonly ConcurrentDictionary<string, DownloadInfo> _downloaded = new ConcurrentDictionary<string, DownloadInfo>();
        private readonly DelayedActions delayedActions = new DelayedActions();
        private string _logFolder;
        private readonly ConcurrentDictionary<string, StockQuote> _downloadedQuotes = new ConcurrentDictionary<string, StockQuote>();

        public DownloadLog() { this.Downloaded = new List<DownloadInfo>(); }

        public List<DownloadInfo> Downloaded { get; set; }

        public DownloadInfo GetInfo(string symbol)
        {
            if (this._downloaded.TryGetValue(symbol, out DownloadInfo info))
            {
                return info;
            }
            return null;
        }

        public bool OnQuoteAvailable(StockQuote quote)
        {
            // record downloaded quotes so they can be merged with quote histories.
            StockQuote existing = null;
            this._downloadedQuotes.TryGetValue(quote.Symbol, out existing);
            return this._downloadedQuotes.TryUpdate(quote.Symbol, quote, existing);
        }

        public void OnQuoteNotFound(string symbol)
        {
            // record quote for symbol was not found (bad symbol).
            DownloadInfo info = this.GetOrCreateDownloadInfo(symbol);
            info.Downloaded = DateTime.Today;
            info.NotFound = true;
            this.DelayedSave();
        }

        public async Task<StockQuoteHistory> GetHistory(string symbol)
        {
            if (this.database.TryGetValue(symbol, out StockQuoteHistory result))
            {
                return result;
            }

            StockQuoteHistory history = null;
            DownloadInfo info;
            var changed = false;
            var changedHistory = false;
            if (this._downloaded.TryGetValue(symbol, out info))
            {
                // read from disk on background thread so we don't block the UI thread loading
                // all these stock quote histories.
                await Task.Run(() =>
                {
                    try
                    {
                        history = StockQuoteHistory.Load(this._logFolder, symbol);
                        if (history != null && history.RemoveDuplicates())
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
                    lock (this.Downloaded)
                    {
                        this.Downloaded.Remove(info);
                    }

                    this._downloaded.TryRemove(info.Symbol, out DownloadInfo removed);
                    changed = true;
                }
                else
                {
                    this.database[symbol] = history;
                }

                if (this._downloadedQuotes.TryGetValue(info.Symbol, out StockQuote quote))
                {
                    if (history.MergeQuote(quote))
                    {
                        changedHistory = true;
                    }
                }
            }
            if (changed)
            {
                this.DelayedSave();
            }
            if (changedHistory)
            {
                this.DelayedSaveHistory(history);
            }
            return history;
        }

        internal void DelayedSave()
        {
            if (!string.IsNullOrEmpty(this._logFolder))
            {
                this.delayedActions.StartDelayedAction("save", new Action(() => { this.Save(this._logFolder); }), TimeSpan.FromSeconds(1));
            }
        }

        private void DelayedSaveHistory(StockQuoteHistory history)
        {
            if (!string.IsNullOrEmpty(this._logFolder))
            {
                var key = "Save_" + history.Symbol;
                this.delayedActions.StartDelayedAction(key, new Action(() => { history.Save(this._logFolder); }), TimeSpan.FromSeconds(0.5));
            }
        }

        private DownloadInfo GetOrCreateDownloadInfo(string symbol)
        {
            DownloadInfo info = this.GetInfo(symbol);
            if (info == null)
            {
                info = new DownloadInfo() { Downloaded = DateTime.Today, Symbol = symbol };
                lock (this.Downloaded)
                {
                    this.Downloaded.Add(info);
                }

                this._downloaded.TryUpdate(symbol, info, null);
            }
            return info;
        }

        public void AddHistory(StockQuoteHistory history)
        {
            this.database[history.Symbol] = history;
            DownloadInfo info = this.GetOrCreateDownloadInfo(history.Symbol);
            info.Downloaded = DateTime.Today;
            this.DelayedSave();
        }

        public static (DownloadLog, bool) Load(string logFolder)
        {
            DownloadLog log = new DownloadLog();
            bool isNew = true;
            if (!string.IsNullOrEmpty(logFolder))
            {
                var filename = System.IO.Path.Combine(logFolder, "DownloadLog.xml");
                if (System.IO.File.Exists(filename))
                {
                    try
                    {
                        XmlSerializer s = new XmlSerializer(typeof(DownloadLog));
                        using (XmlReader r = XmlReader.Create(filename))
                        {
                            log = (DownloadLog)s.Deserialize(r);
                            isNew = false;
                        }
                    }
                    catch (Exception)
                    {
                        // got corrupted? no problem, just start over.
                        log = new DownloadLog();
                        isNew = true;
                    }

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
                        log.Downloaded.Sort((a, b) => string.Compare(a.Symbol, b.Symbol));
                    }
                }
            }
            log._logFolder = logFolder;
            return (log, isNew);
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

    internal class HistoryDownloader
    {
        private readonly object _downloadSync = new object();
        private List<string> _downloadBatch;
        private bool _downloadingHistory;
        private readonly IStockQuoteService _service;
        private readonly DownloadLog _downloadLog;
        private CancellationTokenSource tokenSource;

        public HistoryDownloader(IStockQuoteService service, DownloadLog log)
        {
            Debug.Assert(service != null);
            this._service = service;
            this._downloadLog = log;
        }

        public event EventHandler<string> Error;

        public event EventHandler<StockQuoteHistory> HistoryAvailable;

        private void OnHistoryAvailable(StockQuoteHistory history)
        {
            this._downloadLog.AddHistory(history);

            if (HistoryAvailable != null)
            {
                HistoryAvailable(this, history);
            }
        }


        public async void BeginFetchHistory(List<string> batch)
        {
            if (this._service == null)
            {
                return;
            }
            string singleton = null;
            bool busy = false;
            lock (this._downloadSync)
            {
                busy = this._downloadingHistory;
                if (busy)
                {
                    if (batch.Count == 1)
                    {
                        // then we need this individual stock ASAP
                        var item = batch[0];
                        if (this._downloadBatch.Contains(item))
                        {
                            this._downloadBatch.Remove(item);
                        }
                        this._downloadBatch.Insert(0, item);
                        singleton = item;
                    }
                    else
                    {
                        // then merge the new batch with existing batch that we are downloading.
                        foreach (var item in batch)
                        {
                            if (!this._downloadBatch.Contains(item))
                            {
                                this._downloadBatch.Add(item);
                            }
                        }
                    }
                }
                else
                {
                    // starting a new download batch.
                    this._downloadBatch = new List<string>(batch);
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
                        this.OnHistoryAvailable(history);
                    }
                }
                // only allow one thread do all the downloading.
                return;
            }

            this.tokenSource = new CancellationTokenSource();
            this._downloadingHistory = true;

            while (this._downloadingHistory)
            {
                string symbol = null;
                lock (this._downloadSync)
                {
                    if (this._downloadBatch != null && this._downloadBatch.Count > 0)
                    {
                        symbol = this._downloadBatch.First();
                        this._downloadBatch.Remove(symbol);
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
                    else if (!history.NotFound)
                    {
                        try
                        {
                            await this._service.UpdateHistory(history);
                        }
                        catch (StockQuoteNotFoundException)
                        {
                            history.NotFound = true;
                        }
                        catch (Exception ex)
                        {
                            this.OnError("Download history error: " + ex.Message);
                        }
                    }
                    if (history != null)
                    {
                        this.OnHistoryAvailable(history);
                    }
#if PerformanceBlocks
                    }
#endif
                }
            }
            this._downloadingHistory = false;

            while (this._downloadingHistory && this._downloadBatch.Count > 0)
            {
                Thread.Sleep(1000); // wait for download to finish.
            }
            this._downloadingHistory = false;
            this.OnError("Download history complete");
        }

        private void OnError(string message)
        {
            if (Error != null)
            {
                Error(this, message);
            }
        }

        internal void Cancel()
        {
            this._downloadingHistory = false;
            if (this.tokenSource != null)
            {
                this.tokenSource.Cancel();
            }
        }

        public async Task<StockQuoteHistory> GetCachedHistory(string symbol)
        {
            return await this._downloadLog.GetHistory(symbol);
        }
    }

}
