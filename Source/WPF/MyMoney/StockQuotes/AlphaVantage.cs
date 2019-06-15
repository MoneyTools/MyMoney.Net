using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Walkabout.Data;
using Walkabout.Utilities;

namespace Walkabout.Network
{

    /// <summary>
    /// Class that wraps the https://www.alphavantage.co/ API 
    /// </summary>
    class AlphaVantage : IStockQuoteService
    {
        static string FriendlyName = "AlphaVantage.com";
        const string address = "https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={0}&apikey={1}";
        char[] illegalUrlChars = new char[] { ' ', '\t', '\n', '\r', '/', '+', '=', '&', ':' };
        StockServiceSettings _settings;
        List<Security> _pending;
        HttpWebRequest _current;
        bool _cancelled;
        Thread _downloadThread;

        public AlphaVantage(StockServiceSettings settings)
        {
            _settings = settings;
        }

        public static StockServiceSettings GetDefaultSettings()
        {
            return new StockServiceSettings()
            {
                Name = FriendlyName,
                ApiKey = "demo",
                ApiRequestsPerMinuteLimit = 5,
                ApiRequestsPerDayLimit = 500
            };
        }

        public static bool IsMySettings(StockServiceSettings settings)
        {
            return settings.Name == FriendlyName;
        }

        public int PendingCount { get { return (_pending == null) ? 0 : _pending.Count; } }

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

        public event EventHandler<string> DownloadError;

        private void OnError(string message)
        {
            if (DownloadError != null)
            {
                DownloadError(this, message);
            }
        }

        public event EventHandler<bool> Complete;

        private void OnComplete(bool complete)
        {
            if (Complete != null)
            {
                Complete(this, complete);
            }
        }

        public void BeginFetchQuotes(List<Security> securities)
        {
            int count = 0;
            if (_pending == null)
            {
                _pending = securities;
                count = securities.Count;
            }
            else
            {
                lock (_pending)
                {
                    // merge the lists.
                    foreach (Security s in securities)
                    {
                        if (!(from p in _pending where p.Symbol == s.Symbol select p).Any())
                        {
                            _pending.Add(s);
                        }
                    }
                    count = _pending.Count;
                }
            }
            _cancelled = false;
            if (_downloadThread == null)
            {
                _downloadThread = new Thread(new ThreadStart(DownloadQuotes));
                _downloadThread.Start();
            }
        }

        private void DownloadQuotes()
        {
            try
            {
                StockQuoteThrottle.Instance.Settings = this._settings;

                while (!_cancelled)
                {
                    int remaining = 0;
                    Security security = null;
                    lock (_pending)
                    {
                        if (_pending.Count > 0)
                        {
                            security = _pending[0];
                            _pending.RemoveAt(0);
                            remaining = _pending.Count;
                        }
                    }
                    if (security == null)
                    {
                        // done!
                        break;
                    }

                    // weed out any securities that have no symbol or have a 
                    string symbol = security.Symbol;
                    if (string.IsNullOrEmpty(symbol))
                    {
                        // skip securities that have no symbol.
                    }
                    else if (symbol.IndexOfAny(illegalUrlChars) >= 0)
                    {
                        // since we are passing the symbol on an HTTP URI line, we can't pass Uri illegal characters...
                        OnError(string.Format(Walkabout.Properties.Resources.SkippingSecurityIllegalSymbol, symbol));
                    }
                    else
                    {
                        try
                        {
                            string uri = string.Format(address, symbol, _settings.ApiKey);
                            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);
                            req.UserAgent = "USER_AGENT=Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1;)";
                            req.Method = "GET";
                            req.Timeout = 10000;
                            req.UseDefaultCredentials = false;
                            _current = req;

                            WebResponse resp = req.GetResponse();
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

                            // this service doesn't want too many calls per second.
                            int ms = StockQuoteThrottle.Instance.GetSleep();
                            while (!_cancelled && ms > 0)
                            {
                                Thread.Sleep(1000);
                                ms -= 1000;
                            }
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
                            OnError(string.Format(Walkabout.Properties.Resources.ErrorFetchingSymbols, symbol) + "\r\n" + e.Message);
                        }
                    }
                }
            }
            catch
            {
            }
            OnComplete(PendingCount == 0);
            StockQuoteThrottle.Instance.Save();
            _downloadThread = null;
            _current = null;
        }

        private static StockQuote ParseStockQuote(JObject o)
        {
            StockQuote result = null;
            Newtonsoft.Json.Linq.JToken value;

            if (o.TryGetValue("Global Quote", StringComparison.Ordinal, out value))
            {
                result = new StockQuote();
                if (value.Type == JTokenType.Object)
                {
                    JObject child = (JObject)value;
                    if (child.TryGetValue("01. symbol", StringComparison.Ordinal, out value))
                    {
                        result.Symbol = (string)value;
                    }
                    if (child.TryGetValue("05. price", StringComparison.Ordinal, out value))
                    {
                        result.Price = (decimal)value;
                    }
                    if (child.TryGetValue("07. latest trading day", StringComparison.Ordinal, out value))
                    {
                        result.Date = (DateTime)value;
                    }
                    // "02. open"
                    // "03. high"
                    // "04. low"
                    // "06. volume"
                    // "08. previous close":
                    // "09. change":
                    // "10. change percent":
                }
            }
            return result;
        }
    }
}
