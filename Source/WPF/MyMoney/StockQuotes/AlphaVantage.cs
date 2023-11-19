using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Walkabout.StockQuotes
{

    /// <summary>
    /// Class that wraps the https://www.alphavantage.co/ API 
    /// </summary>
    public class AlphaVantage : ThrottledStockQuoteService
    {
        private const string name = "AlphaVantage";
        private const string baseAddress = "https://www.alphavantage.co/";
        private const string address = "https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={0}&apikey={1}";
        private const string timeSeriesAddress = "https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol={0}&outputsize={1}&apikey={2}";

        public AlphaVantage(StockServiceSettings settings, string logPath) : base(settings, logPath)
        {
        }

        public override string FriendlyName => name;

        public override string WebAddress => baseAddress;

        public override bool SupportsHistory => true;

        public static StockServiceSettings GetDefaultSettings()
        {
            return new StockServiceSettings()
            {
                Name = name,
                Address = baseAddress,
                OldName = "https://www.alphavantage.co/",
                ApiKey = "",
                ApiRequestsPerMinuteLimit = 5,
                ApiRequestsPerDayLimit = 500,
                ApiRequestsPerMonthLimit = 0
            };
        }

        public static bool IsMySettings(StockServiceSettings settings)
        {
            return settings.Name == name;
        }

        protected override async Task<StockQuote> DownloadThrottledQuoteAsync(string symbol)
        {
            string uri = string.Format(address, symbol, this.Settings.ApiKey);

            Debug.WriteLine("AlphaVantage fetching quote " + symbol);
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
            var msg = await client.GetAsync(uri, this.TokenSource.Token);
            if (!msg.IsSuccessStatusCode)
            {
                // could be a bad key, or service not available right now, etc.
                throw new Exception(string.Format("AlphaVantage http error " + msg.StatusCode + ": " + msg.ReasonPhrase));
            }
            else
            {
                this.CountCall();
                using (Stream stm = await msg.Content.ReadAsStreamAsync())
                {
                    using (StreamReader sr = new StreamReader(stm, Encoding.UTF8))
                    {
                        string json = sr.ReadToEnd();
                        JObject o = JObject.Parse(json);
                        StockQuote quote = ParseStockQuote(o);
                        if (quote == null || quote.Symbol == null)
                        {
                            throw new StockQuoteNotFoundException("");
                        }
                        else if (string.Compare(quote.Symbol, symbol, StringComparison.OrdinalIgnoreCase) != 0)
                        {
                            throw new StockQuoteNotFoundException(string.Format(Walkabout.Properties.Resources.DifferentSymbolReturned, symbol, quote.Symbol));
                        }
                        else
                        {
                            return quote;
                        }
                    }
                }
            }
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

        protected override async Task<bool> DownloadThrottledQuoteHistoryAsync(StockQuoteHistory history)
        {
            string outputsize = !history.Complete ? "full" : "compact";
            string symbol = history.Symbol;
            string uri = string.Format(timeSeriesAddress, symbol, outputsize, this.Settings.ApiKey);

            bool updated = false;
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
            var msg = await client.GetAsync(uri, this.TokenSource.Token);
            if (!msg.IsSuccessStatusCode)
            {
                this.OnError("AlphaVantage http error " + msg.StatusCode + ": " + msg.ReasonPhrase);
            }
            else
            {
                this.CountCall();
                this.OnError("AlphaVantage fetching history for " + symbol);

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
                        }
                    }
                }
            }
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
