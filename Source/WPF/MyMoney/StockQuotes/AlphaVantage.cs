using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Walkabout.StockQuotes
{

    /// <summary>
    /// Class that wraps the https://www.alphavantage.co/ API 
    /// </summary>
    class AlphaVantage : IStockQuoteService
    {
        static string FriendlyName = "https://www.alphavantage.co/";
        const string address = "https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={0}&apikey={1}";
        char[] illegalUrlChars = new char[] { ' ', '\t', '\n', '\r', '/', '+', '=', '&', ':' };
        StockServiceSettings _settings;
        HashSet<string> _pending;
        HashSet<string> _retry = new HashSet<string>();
        int _completed;
        HttpWebRequest _current;
        bool _cancelled;
        bool _suspended;
        Thread _downloadThread;
        string _logPath;
        StockQuoteThrottle _throttle;

        public AlphaVantage(StockServiceSettings settings, string logPath)
        {
            _settings = settings;
            _throttle = StockQuoteThrottle.Load("AlphaVantageThrottle.xml");
            _throttle.Settings = settings;
            settings.Name = FriendlyName;
            _logPath = logPath;
        }

        public bool IsEnabled => !string.IsNullOrEmpty(_settings?.ApiKey);

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

        public int PendingCount { get { return (_pending == null) ? 0 : _pending.Count; } }

        public int DownloadsCompleted { get { return _completed; } }

        public void Cancel()
        {
            _cancelled = true;
            if (_current != null)
            {
                try
                {
                    _current.Abort();
                }
                catch { }
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
            _suspended = suspended;
            if (Suspended != null)
            {
                Suspended(this, suspended);
            }
        }

        public bool IsSuspended { get { return _suspended; } }

        public void BeginFetchQuote(string symbol)
        {
            if (string.IsNullOrEmpty(_settings.ApiKey))
            {
                OnComplete(true, Walkabout.Properties.Resources.ConfigureStockQuoteService);
                return;
            }

            if (_pending == null)
            {
                _pending = new HashSet<string>();
                _pending.Add(symbol);
            }
            else
            {
                lock (_pending)
                {
                    // merge the lists.                    
                    _pending.Add(symbol);
                }
            }
            _cancelled = false;
            if (_downloadThread == null)
            {
                _downloadThread = new Thread(new ThreadStart(DownloadQuotes));
                _downloadThread.Start();
            }
        }

        public bool SupportsBatchQuotes { get { return false; } }

        public void BeginFetchQuotes(List<string> symbols)
        {
            throw new Exception("Batch downloading of quotes is not supported by the AlphaVantage service");
        }

        private void DownloadQuotes()
        {
            try
            {
                while (!_cancelled)
                {
                    int remaining = 0;
                    string symbol = null;
                    lock (_pending)
                    {
                        if (_pending.Count == 0)
                        {
                            lock (_retry)
                            {
                                foreach (var item in _retry)
                                {
                                    _completed--;
                                    _pending.Add(item);
                                }
                                _retry.Clear();
                            }
                        }
                        if (_pending.Count > 0)
                        {
                            symbol = _pending.FirstOrDefault();
                            _pending.Remove(symbol);
                            remaining = _pending.Count;
                        }
                    }
                    if (symbol == null)
                    {
                        // done!
                        break;
                    }
                    // even if it fails, we consider the job complete (from a progress point of view).
                    _completed++;

                    // weed out any securities that have no symbol or have a 
                    if (string.IsNullOrEmpty(symbol))
                    {
                        // skip securities that have no symbol.
                    }
                    else if (symbol.IndexOfAny(illegalUrlChars) >= 0)
                    {
                        // since we are passing the symbol on an HTTP URI line, we can't pass Uri illegal characters...
                        OnError(string.Format(Walkabout.Properties.Resources.SkippingSecurityIllegalSymbol, symbol));
                        OnSymbolNotFound(symbol);
                    }
                    else
                    {
                        try
                        {
                            // this service doesn't want too many calls per second.
                            int ms = _throttle.GetSleep();
                            while (ms > 0)
                            {
                                if (ms > 1000)
                                {
                                    int seconds = ms / 1000;
                                    OnError("AlphaVantage quote service needs to sleep for " + seconds + " seconds");
                                }
                                else
                                {
                                    OnError("AlphaVantage quote service needs to sleep for " + ms.ToString() + " ms");
                                }
                                OnSuspended(true);
                                while (!_cancelled && ms > 0)
                                {
                                    Thread.Sleep(1000);
                                    ms -= 1000;
                                }
                                OnSuspended(false);
                                ms = _throttle.GetSleep();
                            }
                            if (_cancelled)
                            {
                                break;
                            }

                            string uri = string.Format(address, symbol, _settings.ApiKey);
                            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);
                            req.UserAgent = "USER_AGENT=Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1;)";
                            req.Method = "GET";
                            req.Timeout = 10000;
                            req.UseDefaultCredentials = false;
                            _current = req;

                            Debug.WriteLine("AlphaVantage fetching quote " + symbol);

                            WebResponse resp = req.GetResponse();
                            _throttle.RecordCall();
                            using (Stream stm = resp.GetResponseStream())
                            {
                                using (StreamReader sr = new StreamReader(stm, Encoding.UTF8))
                                {
                                    string json = sr.ReadToEnd();
                                    JObject o = JObject.Parse(json);
                                    StockQuote quote = ParseStockQuote(o);
                                    if (quote == null || quote.Symbol == null)
                                    {
                                        OnError(string.Format(Walkabout.Properties.Resources.ErrorFetchingSymbols, symbol));
                                        OnSymbolNotFound(symbol);
                                    }
                                    else if (string.Compare(quote.Symbol, symbol, StringComparison.OrdinalIgnoreCase) != 0)
                                    {
                                        // todo: show appropriate error...
                                    }
                                    else
                                    {
                                        OnQuoteAvailable(quote);
                                    }
                                }
                            }

                            OnError(string.Format(Walkabout.Properties.Resources.FetchedStockQuotes, symbol));
                        }
                        catch (System.Net.WebException we)
                        {
                            if (we.Status != WebExceptionStatus.RequestCanceled)
                            {
                                OnError(string.Format(Walkabout.Properties.Resources.ErrorFetchingSymbols, symbol) + "\r\n" + we.Message);
                            }
                            else
                            {
                                // we cancelled, so bail. 
                                _cancelled = true;
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
                                        OnError(http.StatusDescription);
                                        _cancelled = true;
                                        break;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // continue
                            string message = string.Format(Walkabout.Properties.Resources.ErrorFetchingSymbols, symbol) + "\r\n" + e.Message;

                            if (message.Contains("Please visit https://www.alphavantage.co/premium/"))
                            {
                                lock (_retry)
                                {
                                    _retry.Add(symbol);
                                }
                                _throttle.CallsThisMinute += this._settings.ApiRequestsPerMinuteLimit;
                            }
                            OnComplete(PendingCount == 0, message);
                        }

                        Thread.Sleep(1000); // this is so we don't starve out the download service.
                    }
                }
            }
            catch
            {
            }
            _completed = 0;
            if (PendingCount == 0)
            {
                OnComplete(true, "AlphaVantage download complete");
            }
            else
            {
                OnComplete(false, "AlphaVantage download cancelled");
            }
            _downloadThread = null;
            _current = null;
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
            return result;
        }

        public bool SupportsHistory { get { return true; } }

        public async Task<bool> UpdateHistory(StockQuoteHistory history)
        {
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
            await Task.Run(new Action(() =>
            {
                try
                {
                    // first check if history needs updating!
                    bool historyComplete = history.IsComplete();
                    if (historyComplete)
                    {
                        OnError(string.Format("History for symbol {0} is already up to date", symbol));
                    }
                    else
                    {
                        // this service doesn't want too many calls per second.
                        int ms = _throttle.GetSleep();
                        while (ms > 0)
                        {
                            string message = null;
                            string suffix = (ms > 1000) ? "seconds" : "ms";
                            int amount = (ms > 1000) ? ms / 1000 : ms;
                            message = string.Format("AlphaVantage history service needs to sleep for {0} {1}", suffix, amount);

                            OnComplete(PendingCount == 0, message);
                            while (!_cancelled && ms > 0)
                            {
                                Thread.Sleep(1000);
                                ms -= 1000;
                            }
                            ms = _throttle.GetSleep();
                        }
                        if (!_cancelled)
                        {
                            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);
                            req.UserAgent = "USER_AGENT=Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1;)";
                            req.Method = "GET";
                            req.Timeout = 10000;
                            req.UseDefaultCredentials = false;
                            _current = req;

                            OnError("AlphaVantage fetching history for " + symbol);

                            WebResponse resp = req.GetResponse();
                            _throttle.RecordCall();

                            using (Stream stm = resp.GetResponseStream())
                            {
                                using (StreamReader sr = new StreamReader(stm, Encoding.UTF8))
                                {
                                    string json = sr.ReadToEnd();
                                    JObject o = JObject.Parse(json);
                                    var newHistory = ParseTimeSeries(o);

                                    if (string.Compare(newHistory.Symbol, symbol, StringComparison.OrdinalIgnoreCase) != 0)
                                    {
                                        OnError(string.Format("History for symbol {0} return different symbol {1}", symbol, newHistory.Symbol));
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
                catch (Exception ex)
                {
                    string message = ex.Message;
                    OnError(message);
                    if (message.Contains("Please visit https://www.alphavantage.co/premium/"))
                    {
                        _throttle.CallsThisMinute += this._settings.ApiRequestsPerMinuteLimit;
                    }
                }
                OnComplete(PendingCount == 0, null);

            }));
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
