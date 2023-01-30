using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Walkabout.StockQuotes
{

    /// <summary>
    /// Class that wraps the https://www.alphavantage.co/ API 
    /// </summary>
    internal class AlphaVantage : IStockQuoteService
    {
        private static readonly string FriendlyName = "https://www.alphavantage.co/";
        private const string address = "https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={0}&apikey={1}";
        private const string userAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1;)";
        private readonly char[] illegalUrlChars = new char[] { ' ', '\t', '\n', '\r', '/', '+', '=', '&', ':' };
        private readonly StockServiceSettings _settings;
        private HashSet<string> _pending;
        private readonly HashSet<string> _retry = new HashSet<string>();
        private int _completed;
        private CancellationTokenSource _source;
        private bool _cancelled;
        private bool _suspended;
        private bool _disabled; // service is returning errors.
        private Task _downloadTask;
        private readonly string _logPath;
        private readonly StockQuoteThrottle _throttle;

        public AlphaVantage(StockServiceSettings settings, string logPath)
        {
            this._settings = settings;
            this._throttle = StockQuoteThrottle.Load("AlphaVantageThrottle.xml");
            this._throttle.Settings = settings;
            settings.Name = FriendlyName;
            this._logPath = logPath;
        }

        public bool IsEnabled => !string.IsNullOrEmpty(this._settings?.ApiKey);

        public static StockServiceSettings GetDefaultSettings()
        {
            return new StockServiceSettings()
            {
                Name = FriendlyName,
                OldName = "AlphaVantage.com",
                ApiKey = "",
                ApiRequestsPerMinuteLimit = 5,
                ApiRequestsPerDayLimit = 500,
                ApiRequestsPerMonthLimit = 0
            };
        }

        public static bool IsMySettings(StockServiceSettings settings)
        {
            return settings.Name == FriendlyName;
        }

        public int PendingCount { get { return (this._pending == null) ? 0 : this._pending.Count; } }

        public int DownloadsCompleted { get { return this._completed; } }

        public void Cancel()
        {
            this._cancelled = true;
            if (this._source != null)
            {
                this._source.Cancel();
            }
        }

        public event EventHandler<StockQuote> QuoteAvailable;

        private void OnQuoteAvailable(StockQuote quote)
        {
            if (QuoteAvailable != null)
            {
                QuoteAvailable(this, quote);
            }
        }

        public event EventHandler<string> SymbolNotFound;

        private void OnSymbolNotFound(string symbol)
        {
            if (SymbolNotFound != null)
            {
                SymbolNotFound(this, symbol);
            }
        }

        public event EventHandler<string> DownloadError;

        private void OnError(string message)
        {
            if (DownloadError != null)
            {
                DownloadError(this, message);
            }
        }

        public event EventHandler<DownloadCompleteEventArgs> Complete;

        private void OnComplete(bool complete, string message)
        {
            if (Complete != null)
            {
                Complete(this, new DownloadCompleteEventArgs() { Message = message, Complete = complete });
            }
        }

        public event EventHandler<bool> Suspended;

        private void OnSuspended(bool suspended)
        {
            this._suspended = suspended;
            if (Suspended != null)
            {
                Suspended(this, suspended);
            }
        }

        public bool IsSuspended { get { return this._suspended; } }

        public void BeginFetchQuote(string symbol)
        {
            if (_disabled)
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
                this._pending = new HashSet<string>(new string[] { symbol });
            }
            else
            {
                lock (this._pending)
                {
                    // merge the lists.                    
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

        public bool SupportsBatchQuotes { get { return false; } }

        public void BeginFetchQuotes(List<string> symbols)
        {
            throw new Exception("Batch downloading of quotes is not supported by the AlphaVantage service");
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
                            int ms = this._throttle.GetSleep();
                            while (ms > 0)
                            {
                                if (ms > 1000)
                                {
                                    int seconds = ms / 1000;
                                    this.OnError("AlphaVantage quote service needs to sleep for " + seconds + " seconds");
                                }
                                else
                                {
                                    this.OnError("AlphaVantage quote service needs to sleep for " + ms.ToString() + " ms");
                                }
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
                                break;
                            }

                            string uri = string.Format(address, symbol, this._settings.ApiKey);

                            Debug.WriteLine("AlphaVantage fetching quote " + symbol);
                            HttpClient client = new HttpClient();
                            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
                            client.Timeout = TimeSpan.FromSeconds(30);
                            var msg = await client.GetAsync(uri, _source.Token);
                            if (!msg.IsSuccessStatusCode)
                            {
                                this.OnError("AlphaVantage http error " + msg.StatusCode + ": " + msg.ReasonPhrase);
                            }
                            else
                            {
                                this._throttle.RecordCall();
                                using (Stream stm = await msg.Content.ReadAsStreamAsync())
                                {
                                    using (StreamReader sr = new StreamReader(stm, Encoding.UTF8))
                                    {
                                        string json = sr.ReadToEnd();
                                        JObject o = JObject.Parse(json);
                                        StockQuote quote = ParseStockQuote(o);
                                        if (quote == null || quote.Symbol == null)
                                        {
                                            this.OnError(string.Format(Walkabout.Properties.Resources.ErrorFetchingSymbols, symbol));
                                            this.OnSymbolNotFound(symbol);
                                        }
                                        else if (string.Compare(quote.Symbol, symbol, StringComparison.OrdinalIgnoreCase) != 0)
                                        {
                                            // todo: show appropriate error...
                                        }
                                        else
                                        {
                                            this.OnQuoteAvailable(quote);
                                        }
                                    }
                                }

                                this.OnError(string.Format(Walkabout.Properties.Resources.FetchedStockQuotes, symbol));
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
                        catch (Exception e)
                        {
                            ex = e;
                        }

                        if (ex != null)
                        {
                            // continue
                            string message = string.Format(Walkabout.Properties.Resources.ErrorFetchingSymbols, symbol) + "\r\n" + ex.Message;

                            if (message.Contains("premium"))
                            {
                                this._disabled = true;
                            }
                            else
                            {
                                lock (this._retry)
                                {
                                    this._retry.Add(symbol);
                                }
                                this._throttle.CallsThisMinute += this._settings.ApiRequestsPerMinuteLimit;
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
                this.OnComplete(true, "AlphaVantage download complete");
            }
            else
            {
                this.OnComplete(false, "AlphaVantage download cancelled");
            }
            this._downloadTask = null;
        }

        private static StockQuote ParseStockQuote(JObject o)
        {
            StockQuote result = null;
            Newtonsoft.Json.Linq.JToken value;

            if (o.TryGetValue("Note", StringComparison.Ordinal, out value))
            {
                string message = (string)value;
                throw new Exception(message);
            }

            if (o.TryGetValue("Error Message", StringComparison.Ordinal, out value))
            {
                string message = (string)value;
                throw new Exception(message);
            }

            if (o.TryGetValue("Global Quote", StringComparison.Ordinal, out value))
            {
                result = new StockQuote() { Downloaded = DateTime.Now };
                if (value.Type == JTokenType.Object)
                {
                    JObject child = (JObject)value;
                    if (child.TryGetValue("01. symbol", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                    {
                        result.Symbol = (string)value;
                    }
                    if (child.TryGetValue("02. open", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                    {
                        result.Open = (decimal)value;
                    }
                    if (child.TryGetValue("03. high", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                    {
                        result.High = (decimal)value;
                    }
                    if (child.TryGetValue("04. low", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                    {
                        result.Low = (decimal)value;
                    }
                    if (child.TryGetValue("08. previous close", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                    {
                        result.Close = (decimal)value;
                    }
                    if (child.TryGetValue("06. volume", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                    {
                        result.Volume = (decimal)value;
                    }
                    if (child.TryGetValue("07. latest trading day", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                    {
                        result.Date = (DateTime)value;
                    }
                }
            }
            else if (o.TryGetValue("Information", out value))
            {
                throw new Exception(value.ToString());
            }
            return result;
        }

        public bool SupportsHistory { get { return true; } }

        public async Task<bool> UpdateHistory(StockQuoteHistory history)
        {
            if (_disabled)
            {
                return false;
            }
            this._cancelled = false;
            if (this._source == null)
            {
                this._source = new CancellationTokenSource();
            }
            string outputsize;
            if (!history.Complete)
            {
                outputsize = "full";
            }
            else
            {
                outputsize = "compact";
            }
            bool updated = false;
            const string timeSeriesAddress = "https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={0}&outputsize={1}&apikey={2}";
            string symbol = history.Symbol;
            string uri = string.Format(timeSeriesAddress, symbol, outputsize, this._settings.ApiKey);

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
                    int ms = this._throttle.GetSleep();
                    while (ms > 0)
                    {
                        string message = null;
                        string suffix = (ms > 1000) ? "seconds" : "ms";
                        int amount = (ms > 1000) ? ms / 1000 : ms;
                        message = string.Format("AlphaVantage history service needs to sleep for {0} {1}", suffix, amount);

                        this.OnComplete(this.PendingCount == 0, message);
                        while (!this._cancelled && ms > 0)
                        {
                            await Task.Delay(1000);
                            ms -= 1000;
                        }
                        ms = this._throttle.GetSleep();
                    }
                    if (!this._cancelled)
                    {
                        HttpClient client = new HttpClient();
                        client.DefaultRequestHeaders.Add("User-Agent", userAgent);
                        client.Timeout = TimeSpan.FromSeconds(30);
                        var msg = await client.GetAsync(uri, _source.Token);
                        if (!msg.IsSuccessStatusCode)
                        {
                            this.OnError("AlphaVantage http error " + msg.StatusCode + ": " + msg.ReasonPhrase);
                        }
                        else
                        {
                            this.OnError("AlphaVantage fetching history for " + symbol);

                            this._throttle.RecordCall();

                            using (Stream stm = await msg.Content.ReadAsStreamAsync())
                            {
                                using (StreamReader sr = new StreamReader(stm, Encoding.UTF8))
                                {
                                    string json = sr.ReadToEnd();
                                    JObject o = JObject.Parse(json);
                                    var newHistory = this.ParseTimeSeries(o);

                                    if (string.Compare(newHistory.Symbol, symbol, StringComparison.OrdinalIgnoreCase) != 0)
                                    {
                                        this.OnError(string.Format("History for symbol {0} return different symbol {1}", symbol, newHistory.Symbol));
                                    }
                                    else
                                    {
                                        updated = true;
                                        history.Merge(newHistory);
                                        history.Complete = true;
                                        history.Save(this._logPath);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                this.OnError(message);
                if (message.Contains("premium"))
                {
                    this._disabled = true;
                }
            }
            this.OnComplete(this.PendingCount == 0, null);

            return updated;
        }

        private StockQuoteHistory ParseTimeSeries(JObject o)
        {
            StockQuoteHistory history = new StockQuoteHistory();
            history.History = new List<StockQuote>();
            history.Complete = true; // this is a complete history.

            Newtonsoft.Json.Linq.JToken value;

            if (o.TryGetValue("Note", StringComparison.Ordinal, out value))
            {
                string message = (string)value;
                throw new Exception(message);
            }

            if (o.TryGetValue("Error Message", StringComparison.Ordinal, out value))
            {
                string message = (string)value;
                throw new Exception(message);
            }

            if (o.TryGetValue("Meta Data", StringComparison.Ordinal, out value))
            {
                if (value.Type == JTokenType.Object)
                {
                    JObject child = (JObject)value;
                    if (child.TryGetValue("2. Symbol", StringComparison.Ordinal, out value))
                    {
                        history.Symbol = (string)value;
                    }
                }
            }
            else if (o.TryGetValue("Information", out value))
            {
                throw new Exception(value.ToString());
            }
            else
            {
                throw new Exception("Time series data schema has changed");
            }

            if (o.TryGetValue("Time Series (Daily)", StringComparison.Ordinal, out value))
            {
                if (value.Type == JTokenType.Object)
                {
                    JObject series = (JObject)value;
                    foreach (var p in series.Properties().Reverse())
                    {
                        DateTime date;
                        if (DateTime.TryParse(p.Name, out date))
                        {
                            value = series.GetValue(p.Name);
                            if (value.Type == JTokenType.Object)
                            {
                                StockQuote quote = new StockQuote() { Date = date, Downloaded = DateTime.Now };
                                JObject child = (JObject)value;

                                if (child.TryGetValue("1. open", StringComparison.Ordinal, out value))
                                {
                                    quote.Open = (decimal)value;
                                }
                                if (child.TryGetValue("4. close", StringComparison.Ordinal, out value))
                                {
                                    quote.Close = (decimal)value;
                                }
                                if (child.TryGetValue("2. high", StringComparison.Ordinal, out value))
                                {
                                    quote.High = (decimal)value;
                                }
                                if (child.TryGetValue("3. low", StringComparison.Ordinal, out value))
                                {
                                    quote.Low = (decimal)value;
                                }
                                if (child.TryGetValue("5. volume", StringComparison.Ordinal, out value))
                                {
                                    quote.Volume = (decimal)value;
                                }
                                history.History.Add(quote);
                            }
                        }
                    }
                }
            }
            else
            {
                throw new Exception("Time series data schema has changed");
            }
            return history;
        }
    }
}
