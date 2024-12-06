using ModernWpf.Controls;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Configuration;

namespace Walkabout.StockQuotes
{
    /// <summary>
    /// This class encapsulates the REST API on the https://api.twelvedata.com/ stock service.
    /// </summary>
    internal class TwelveData : ThrottledStockQuoteService
    {
        private static readonly string name = "TwelveData";
        private static readonly string baseAddress = "https://api.twelvedata.com/";
        // {0}=Symbol, {1}=number of days back from today, {2}=apikey
        private const string stockQuoteUri = "https://api.twelvedata.com/time_series?apikey={2}&interval=1day&format=JSON&symbol={0}&outputsize={1}";        

        public TwelveData(StockServiceSettings settings, string logPath) : base(settings, logPath)
        {
            settings.Name = name;
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
                ApiKey = "",
                ApiRequestsPerMinuteLimit = 12,
                ApiRequestsPerDayLimit = 800,
                ApiRequestsPerMonthLimit = 0,
                HistoryEnabled = true,
            };
        }

        public static bool IsMySettings(StockServiceSettings settings)
        {
            return settings.Name == name;
        }

        protected override async Task<StockQuote> DownloadThrottledQuoteAsync(string symbol)
        {
            var quotes = await this.DownloadTimeSeriesAsync(symbol, 1);
            if (quotes.Count > 0)
            {
                var quote = quotes[0];
                if (quote == null || quote.Symbol == null)
                {
                    throw new StockQuoteNotFoundException("");
                }
                else if (string.Compare(quote.Symbol, symbol, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    throw new StockQuoteNotFoundException(string.Format(Walkabout.Properties.Resources.DifferentSymbolReturned, symbol, quote.Symbol));
                }
                return quote;
            }
            else
            {
                throw new StockQuoteNotFoundException("");
            }
        }

        private async Task<List<StockQuote>> DownloadTimeSeriesAsync(string symbol, int daysFromToday)
        {     
            Debug.WriteLine($"TwelveData: DownloadThrottledQuoteAsync {symbol}");
            string uri = string.Format(stockQuoteUri, symbol, daysFromToday, this.Settings.ApiKey);
            HttpClient client = new HttpClient();
            //client.DefaultRequestHeaders.Add("User-Agent", userAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
            var msg = await client.GetAsync(uri, this.TokenSource.Token);
            if (!msg.IsSuccessStatusCode)
            {
                throw new Exception(this.FriendlyName + " http error " + msg.StatusCode + ": " + msg.ReasonPhrase);
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
                        List<StockQuote> quotes = this.ParseStockQuotes(o);
                        return quotes;
                    }
                }
            }
        }

        private List<StockQuote> ParseStockQuotes(JObject child)
        {
            // See https://twelvedata.com/account/api-playground

            var result = new List<StockQuote>();
            string symbol = "";

            JToken value;
            if (child.TryGetValue("meta", StringComparison.Ordinal, out value) && value.Type == JTokenType.Object)
            {
                JObject meta = (JObject)value;
                if (meta.TryGetValue("symbol", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                {
                    symbol = (string)value;
                }
            }

            if (child.TryGetValue("values", StringComparison.Ordinal, out value) && value.Type == JTokenType.Array)
            {
                JArray array = (JArray)value;
                foreach (var item in array)
                {
                    if (item.Type == JTokenType.Object)
                    {
                        var quote = new StockQuote() { Downloaded = DateTime.Now, Symbol = symbol };
                        result.Add(quote);
                        JObject price = (JObject)item;
                        if (price.TryGetValue("close", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            if (decimal.TryParse((string)value, out decimal d))
                            {
                                quote.Close = d;
                            }
                        }
                        if (price.TryGetValue("high", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            if (decimal.TryParse((string)value, out decimal d))
                            {
                                quote.High = d;
                            }
                        }
                        if (price.TryGetValue("low", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            if (decimal.TryParse((string)value, out decimal d))
                            {
                                quote.Low = d;
                            }
                        }
                        if (price.TryGetValue("open", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            if (decimal.TryParse((string)value, out decimal d))
                            {
                                quote.Open = d;
                            }
                        }
                        if (price.TryGetValue("volume", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            if (decimal.TryParse((string)value, out decimal d))
                            {
                                quote.Volume = d;
                            }
                        }
                        if (price.TryGetValue("datetime", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            if (DateTime.TryParse((string)value, out DateTime d))
                            {
                                quote.Date = d.Date;
                            }
                        }
                    }
                }
            }
            return result;
        }

        protected override async Task<bool> DownloadThrottledQuoteHistoryAsync(StockQuoteHistory history)
        {
            if (history.NotFound)
            {
                // don't keep trying to download quotes that don't exist.
                return false;
            }
            var entry = history.History.LastOrDefault();
            var days = 1;
            if (entry != null)
            {
                var span = DateTime.Now - entry.Date;
                days = (int)span.TotalDays;
                if (days == 0)
                {
                    days = 1;
                }
            }
            try
            {

                var quotes = await this.DownloadTimeSeriesAsync(history.Symbol, days);
                foreach (var quote in quotes)
                {
                    history.MergeQuote(quote);
                }

                history.Complete = true;
            }
            catch (StockQuoteNotFoundException)
            {
                history.NotFound = true;
            }
            return true;
        }
    }
}
