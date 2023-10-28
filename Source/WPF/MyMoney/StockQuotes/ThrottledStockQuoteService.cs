using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Walkabout.StockQuotes
{
    /// <summary>
    /// The DownloadThrottledQuoteAsync or DownloadThrottledQuoteHistoryAsync implementation raise this exception to signal to
    /// the ThrottledStockQuoteService that an error occurred because of throttling limits.
    /// </summary>
    internal class StockQuoteThrottledException : Exception
    {
        public StockQuoteThrottledException(string msg): base(msg) { }
    }

    /// <summary>
    /// The DownloadThrottledQuoteAsync or DownloadThrottledQuoteHistoryAsync implementation raise this exception to signal to
    /// the ThrottledStockQuoteService that an stock symbol was not found, either it's invalid, or the service does not provide
    /// info on this symbol.
    /// </summary>
    internal class StockQuoteNotFoundException : Exception
    {
        public StockQuoteNotFoundException(string msg) : base(msg) { }
    }

    /// <summary>
    /// This class implements throttling on the stock service API based on the given StockServiceSettings
    /// and calls the abstract DownloadThrottledQuoteAsync or DownloadThrottledQuoteHistoryAsync within those throttling limits.
    /// </summary>
    public abstract class ThrottledStockQuoteService : IStockQuoteService
    {
        protected const string userAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1;)";
        private readonly char[] illegalUrlChars = new char[] { ' ', '\t', '\n', '\r', '/', '+', '=', '&', ':' };
        private readonly StockServiceSettings _settings;
        private HashSet<string> _pending = new HashSet<string>();
        private readonly HashSet<string> _retry = new HashSet<string>();
        private int _completed;
        private CancellationTokenSource _source;
        private bool _cancelled;
        private bool _suspended;
        private bool _disabled; // service is returning errors.
        private Task _downloadTask;
        private readonly string _logPath;
        private readonly StockQuoteThrottle _throttle;

        public ThrottledStockQuoteService(StockServiceSettings settings, string logPath)
        {
            settings.Name = this.FriendlyName;
            this._settings = settings;
            this._throttle = StockQuoteThrottle.Load(string.Format("{0}Throttle.xml", this.FriendlyName));
            this._throttle.Settings = settings;
            this._logPath = logPath;
        }

        protected StockServiceSettings Settings => _settings;

        protected CancellationTokenSource TokenSource => _source;

        public abstract string FriendlyName { get; }

        public abstract string WebAddress { get; }

        public abstract bool SupportsHistory { get; }

        public bool IsEnabled => !string.IsNullOrEmpty(this._settings?.ApiKey);

        public int PendingCount { get { return this._pending.Count; } }

        public int DownloadsCompleted { get { return this._completed; } }

        public string LogFolder => this._logPath;

        public void Cancel()
        {
            this._cancelled = true;
            if (this._source != null)
            {
                this._source.Cancel();
            }
        }

        public event EventHandler<StockQuote> QuoteAvailable;

        protected void OnQuoteAvailable(StockQuote quote)
        {
            if (QuoteAvailable != null)
            {
                QuoteAvailable(this, quote);
            }
        }

        public event EventHandler<string> SymbolNotFound;

        protected void OnSymbolNotFound(string symbol)
        {
            if (SymbolNotFound != null)
            {
                SymbolNotFound(this, symbol);
            }
        }

        public event EventHandler<string> DownloadError;

        protected void OnError(string message)
        {
            if (DownloadError != null)
            {
                DownloadError(this, message);
            }
        }

        public event EventHandler<DownloadCompleteEventArgs> Complete;

        protected void OnComplete(bool complete, string message)
        {
            if (Complete != null)
            {
                Complete(this, new DownloadCompleteEventArgs() { Message = message, Complete = complete });
            }
        }

        public event EventHandler<bool> Suspended;

        protected void OnSuspended(bool suspended)
        {
            this._suspended = suspended;
            if (Suspended != null)
            {
                Suspended(this, suspended);
            }
        }

        public bool IsSuspended { get { return this._suspended; } }

        public void BeginFetchQuotes(List<string> symbols)
        {
            if (this._disabled)
            {
                return;
            }

            if (string.IsNullOrEmpty(this._settings.ApiKey))
            {
                this.OnComplete(true, Walkabout.Properties.Resources.ConfigureStockQuoteService);
                return;
            }

            if (this._pending == null)
            {
                this._pending = new HashSet<string>();
            }

            lock (this._pending)
            {
                // merge the lists.
                foreach (var symbol in symbols)
                {
                    this._pending.Add(symbol);
                }
            }

            if (this._downloadTask == null)
            {
                this._cancelled = false;
                this._source = new CancellationTokenSource();
                this._downloadTask = Task.Run(this.DownloadQuotes);
            }
        }

        private async Task DownloadQuotes()
        {
            try
            {
                while (!this._cancelled)
                {
                    int remaining = 0;
                    string symbol = null;
                    lock (this._pending)
                    {
                        if (this._pending.Count == 0)
                        {
                            lock (this._retry)
                            {
                                foreach (var item in this._retry)
                                {
                                    this._completed--;
                                    this._pending.Add(item);
                                }
                                this._retry.Clear();
                            }
                        }
                        if (this._pending.Count > 0)
                        {
                            symbol = this._pending.FirstOrDefault();
                            this._pending.Remove(symbol);
                            remaining = this._pending.Count;
                        }
                    }
                    if (symbol == null)
                    {
                        // done!
                        break;
                    }
                    // even if it fails, we consider the job complete (from a progress point of view).
                    this._completed++;

                    // weed out any securities that have no symbol or have a 
                    if (string.IsNullOrEmpty(symbol))
                    {
                        // skip securities that have no symbol.
                    }
                    else if (symbol.IndexOfAny(this.illegalUrlChars) >= 0)
                    {
                        // since we are passing the symbol on an HTTP URI line, we can't pass Uri illegal characters...
                        this.OnError(string.Format(Walkabout.Properties.Resources.SkippingSecurityIllegalSymbol, symbol));
                        this.OnSymbolNotFound(symbol);
                    }
                    else
                    {
                        Exception ex = null;

                        try
                        {
                            // this service doesn't want too many calls per second.
                            await this.ThrottleSleep();
                            var quote = await this.DownloadThrottledQuoteAsync(symbol);
                            if (quote != null)
                            {
                                this.OnQuoteAvailable(quote);
                            }
                        }
                        catch (HttpRequestException he)
                        {
                            if (he.InnerException is System.Net.WebException we)
                            {
                                if (we.Status != WebExceptionStatus.RequestCanceled)
                                {
                                    this.OnError(string.Format(Walkabout.Properties.Resources.ErrorFetchingSymbols, symbol) + "\r\n" + we.Message);
                                }
                                else
                                {
                                    // we cancelled, so bail. 
                                    this._cancelled = true;
                                    break;
                                }

                                HttpWebResponse http = we.Response as HttpWebResponse;
                                if (http != null)
                                {
                                    // certain http error codes are fatal.
                                    switch (http.StatusCode)
                                    {
                                        case HttpStatusCode.ServiceUnavailable:
                                        case HttpStatusCode.InternalServerError:
                                        case HttpStatusCode.Unauthorized:
                                            this.OnError(http.StatusDescription);
                                            this._cancelled = true;
                                            break;
                                    }
                                }
                            }
                            else
                            {
                                ex = he;
                            }
                        }
                        catch (StockQuoteNotFoundException)
                        {
                            this.OnSymbolNotFound(symbol);
                        }
                        catch (Exception e)
                        {
                            // service is failing, so no point trying again right now.
                            this._disabled = true;
                            ex = e;
                            // assume this might be api limit related, and mark the fact we've used up our quota.
                            this._throttle.CallsThisMinute += this._settings.ApiRequestsPerMinuteLimit;
                        }

                        if (ex != null)
                        {
                            string message = string.Format(Walkabout.Properties.Resources.ErrorFetchingSymbols, symbol) + "\r\n" + ex.Message;                            
                            lock (this._retry)
                            {
                                this._retry.Add(symbol);
                            }
                            this.OnComplete(this.PendingCount == 0, message);
                        }

                        await Task.Delay(1000); // this is so we don't starve out the download service.
                    }
                }
            }
            catch
            {
            }
            this._completed = 0;
            if (this.PendingCount == 0)
            {
                this.OnComplete(true, this.FriendlyName + " download complete");
            }
            else
            {
                this.OnComplete(false, this.FriendlyName + " download cancelled");
            }
            this._downloadTask = null;
        }

        protected async Task ThrottleSleep()
        {
            int ms = this._throttle.GetSleep();
            while (ms > 0)
            {
                string suffix = (ms > 1000) ? "seconds" : "ms";
                int amount = (ms > 1000) ? ms / 1000 : ms;
                this.OnError(string.Format("{0} stock quote service needs to sleep for {1} {2}", this.FriendlyName, suffix, amount));

                this.OnSuspended(true);
                while (!this._cancelled && ms > 0)
                {
                    await Task.Delay(1000);
                    ms -= 1000;
                }
                this.OnSuspended(false);
                ms = this._throttle.GetSleep();
            }
            if (this._cancelled)
            {
                throw new OperationCanceledException();
            }
        }

        public async Task<string> TestApiKeyAsync(string apiKey)
        {
            try
            {
                this._source = new CancellationTokenSource();
                var quote = await this.DownloadThrottledQuoteAsync("MSFT");
                return string.Empty;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        protected abstract Task<StockQuote> DownloadThrottledQuoteAsync(string symbol);
        protected abstract Task<bool> DownloadThrottledQuoteHistoryAsync(StockQuoteHistory history);

        public async Task<bool> UpdateHistory(StockQuoteHistory history)
        {
            if (this._disabled)
            {
                return false;
            }
            this._cancelled = false;
            if (this._source == null)
            {
                this._source = new CancellationTokenSource();
            }
            string symbol = history.Symbol;
            bool updated = false;
            try
            {
                // first check if history needs updating!
                bool historyComplete = history.IsComplete();
                if (historyComplete)
                {
                    this.OnError(string.Format("History for symbol {0} is already up to date", symbol));
                }
                else
                {
                    // this service doesn't want too many calls per second.
                    await this.ThrottleSleep();
                    updated = await this.DownloadThrottledQuoteHistoryAsync(history);
                    if (updated)
                    {
                        history.Save(this._logPath);
                    }
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                this.OnError(message);
                if (message.Contains("premium"))
                {
                    this._settings.HistoryEnabled = false;
                    this._disabled = true;
                }
            }
            this.OnComplete(this.PendingCount == 0, null);

            return updated;
        }

        protected void CountCall()
        {
            this._throttle.RecordCall();
        }
    }
}
