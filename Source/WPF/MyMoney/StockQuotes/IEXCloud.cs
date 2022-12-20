using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Walkabout.StockQuotes
{
    class IEXCloud : IStockQuoteService
    {
        static string FriendlyName = "https://iexcloud.io/";
        // See https://iextrading.com/developer/docs/#batch-requests
        const string address = "https://cloud.iexapis.com/stable/stock/market/batch?symbols={0}&types=quote&range=1m&last=1&token={1}";
        char[] illegalUrlChars = new char[] { ' ', '\t', '\n', '\r', '/', '+', '=', '&', ':' };
        StockServiceSettings _settings;
        HashSet<string> _pending;
        int _completed;
        Thread _downloadThread;
        HttpWebRequest _current;
        bool _cancelled;
        bool _suspended;
        string _logPath;
        bool _downloadError;
        StockQuoteThrottle _throttle;

        public IEXCloud(StockServiceSettings settings, string logPath)
        {
            this._settings = settings;
            settings.Name = FriendlyName;
            this._logPath = logPath;
            this._throttle = StockQuoteThrottle.Load("IEXTradingThrottle.xml");
            this._throttle.Settings = settings;
        }

        public bool IsEnabled => !string.IsNullOrEmpty(this._settings?.ApiKey);

        public int PendingCount { get { return (this._pending == null) ? 0 : this._pending.Count; } }

        public int DownloadsCompleted { get { return this._completed; } }

        public void Cancel()
        {
            this._cancelled = true;
            if (this._current != null)
            {
                try
                {
                    this._current.Abort();
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
            this._suspended = suspended;
            if (Suspended != null)
            {
                Suspended(this, suspended);
            }
        }
        public bool IsSuspended { get { return this._suspended; } }

        public static StockServiceSettings GetDefaultSettings()
        {
            return new StockServiceSettings()
            {
                Name = FriendlyName,
                OldName = "iexcloud.io",
                ApiKey = "",
                ApiRequestsPerMinuteLimit = 60,
                ApiRequestsPerDayLimit = 0,
                ApiRequestsPerMonthLimit = 500000
            };
        }

        public static bool IsMySettings(StockServiceSettings settings)
        {
            return settings.Name == FriendlyName;
        }

        public void BeginFetchQuote(string symbol)
        {
            this.BeginFetchQuotes(new List<string>(new string[] { symbol }));
        }

        public bool SupportsBatchQuotes { get { return true; } }

        public void BeginFetchQuotes(List<string> symbols)
        {
            if (string.IsNullOrEmpty(this._settings.ApiKey))
            {
                this.OnComplete(true, Walkabout.Properties.Resources.ConfigureStockQuoteService);
                return;
            }

            int count = 0;
            if (this._pending == null)
            {
                this._pending = new HashSet<string>(symbols);
                count = this._pending.Count;
            }
            else
            {
                lock (this._pending)
                {
                    // merge the lists.
                    foreach (string s in symbols)
                    {
                        this._pending.Add(s);
                    }
                    count = this._pending.Count;
                }
            }
            this._cancelled = false;
            if (this._downloadThread == null)
            {
                this._downloadThread = new Thread(new ThreadStart(this.DownloadQuotes));
                this._downloadThread.Start();
            }
        }

        private void DownloadQuotes()
        {
            try
            {
                // This is on a background thread
                int max_batch = 100;
                List<string> batch = new List<string>();
                int remaining = 0;
                while (!this._cancelled)
                {
                    string symbol = null;
                    lock (this._pending)
                    {
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

                    // weed out any securities that have no symbol or have a symbol that would be invalid. 
                    if (string.IsNullOrEmpty(symbol))
                    {
                        // skip securities that have no symbol.
                    }
                    else if (symbol.IndexOfAny(this.illegalUrlChars) >= 0)
                    {
                        // since we are passing the symbol on an HTTP URI line, we can't pass Uri illegal characters...
                        this.OnSymbolNotFound(symbol);
                        this.OnError(string.Format(Walkabout.Properties.Resources.SkippingSecurityIllegalSymbol, symbol));
                    }
                    else
                    {
                        batch.Add(symbol);
                    }

                    if (batch.Count() == max_batch || remaining == 0)
                    {
                        // even it if tails we consider the job completed from a status point of view.
                        this._completed += batch.Count;
                        string symbols = string.Join(",", batch);
                        try
                        {
                            // this service doesn't want too many calls per second.
                            int ms = this._throttle.GetSleep();
                            while (ms > 0 && !this._cancelled)
                            {
                                if (ms > 1000)
                                {
                                    int seconds = ms / 1000;
                                    this.OnError("IEXCloud service needs to sleep for " + seconds + " seconds");
                                }
                                else
                                {
                                    this.OnError("IEXCloud service needs to sleep for " + ms.ToString() + " ms");
                                }
                                this.OnSuspended(true);
                                while (!this._cancelled && ms > 0)
                                {
                                    Thread.Sleep(1000);
                                    ms -= 1000;
                                }
                                this.OnSuspended(false);
                                ms = this._throttle.GetSleep();
                            }
                            if (this._cancelled)
                            {
                                break;
                            }

                            string uri = string.Format(address, symbols, this._settings.ApiKey);
                            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);
                            req.UserAgent = "USER_AGENT=Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1;)";
                            req.Method = "GET";
                            req.Timeout = 10000;
                            req.UseDefaultCredentials = false;
                            this._current = req;

                            WebResponse resp = req.GetResponse();
                            this._throttle.RecordCall();
                            using (Stream stm = resp.GetResponseStream())
                            {
                                using (StreamReader sr = new StreamReader(stm, Encoding.UTF8))
                                {
                                    string json = sr.ReadToEnd();
                                    JObject o = JObject.Parse(json);
                                    List<StockQuote> result = ParseStockQuotes(o);
                                    // make sure they are all returned, and report errors for any that are not.
                                    foreach (string s in batch)
                                    {
                                        StockQuote q = (from i in result where string.Compare(i.Symbol, s, StringComparison.OrdinalIgnoreCase) == 0 select i).FirstOrDefault();
                                        if (q == null)
                                        {
                                            this.OnError(string.Format("No quote returned for symbol {0}", s));
                                            this.OnSymbolNotFound(s);
                                        }
                                        else
                                        {
                                            this.OnQuoteAvailable(q);
                                        }
                                    }
                                }
                            }

                            Thread.Sleep(1000); // there is also a minimum sleep between requests that we must enforce.

                            symbols = string.Join(", ", batch);
                            this.OnError(string.Format(Walkabout.Properties.Resources.FetchedStockQuotes, symbols));

                        }
                        catch (System.Net.WebException we)
                        {
                            if (we.Status != WebExceptionStatus.RequestCanceled)
                            {
                                this.OnError(string.Format(Walkabout.Properties.Resources.ErrorFetchingSymbols, symbols) + "\r\n" + we.Message);
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
                        catch (Exception e)
                        {
                            // continue
                            this.OnError(string.Format(Walkabout.Properties.Resources.ErrorFetchingSymbols, symbols) + "\r\n" + e.Message);
                        }
                        batch.Clear();
                    }

                    this._current = null;
                }
            }
            catch
            {
            }
            if (this.PendingCount == 0)
            {
                this.OnComplete(true, "IEXCloud download complete");
            }
            else
            {
                this.OnComplete(false, "IEXCloud download cancelled");
            }
            this._downloadThread = null;
            this._current = null;
            this._completed = 0;
        }


        private static List<StockQuote> ParseStockQuotes(JObject o)
        {
            List<StockQuote> result = new List<StockQuote>();
            // See https://iexcloud.io/docs/api/#quote, lots more info available than what we are extracting here.
            foreach (var pair in o)
            {
                // KeyValuePair<string, JToken>
                string name = pair.Key;
                JToken token = pair.Value;
                if (token.Type == JTokenType.Object)
                {
                    var quote = new StockQuote() { Downloaded = DateTime.Now };
                    result.Add(quote);

                    JObject child = (JObject)token;
                    JToken value;
                    if (child.TryGetValue("quote", StringComparison.Ordinal, out value) && value.Type == JTokenType.Object)
                    {
                        child = (JObject)value;

                        if (child.TryGetValue("symbol", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            quote.Symbol = (string)value;
                        }
                        if (child.TryGetValue("companyName", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            quote.Name = (string)value;
                        }
                        if (child.TryGetValue("open", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            quote.Open = (decimal)value;
                        }
                        if (child.TryGetValue("close", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            quote.Close = (decimal)value;
                        }
                        if (child.TryGetValue("high", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            quote.High = (decimal)value;
                        }
                        if (child.TryGetValue("low", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            quote.Low = (decimal)value;
                        }
                        if (child.TryGetValue("latestVolume", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            quote.Volume = (decimal)value;
                        }
                        if (child.TryGetValue("closeTime", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            long ticks = (long)value;
                            quote.Date = DateTimeOffset.FromUnixTimeMilliseconds(ticks).LocalDateTime;
                        }
                    }
                }
            }
            return result;
        }

        public bool SupportsHistory { get { return false; } }

        public async Task<bool> UpdateHistory(StockQuoteHistory history)
        {
            if (!this._downloadError)
            {
                this._downloadError = true;
                this.OnComplete(true, "Download history is not supported by IEXCloud service");
            }
            await Task.Delay(0);
            return false;
        }
    }
}
