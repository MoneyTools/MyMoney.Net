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
    /// This class encapsulates the REST API on the https://query2.finance.yahoo.com/ stock service.
    /// </summary>
    internal class YahooFinance : ThrottledStockQuoteService
    {
        private static readonly string name = "Yahoo";
        private static readonly string baseAddress = "https://query2.finance.yahoo.com/v8/finance/chart/";
        // {0}=symbol
        private const string stockQuoteUri = "https://query2.finance.yahoo.com/v8/finance/chart/{0}?interval={1}&range={2}";
        private HashSet<string> symbolsNotFound = new HashSet<string>();

        private string[] validRanges = {
                        "5d", // in case market is closed today
                        "5d",
                        "1mo",
                        "3mo",
                        "6mo",
                        "1y",
                        "2y",
                        "5y",
                        "10y",
                        "max"
        };
        private TimeSpan[] validRangeSpans = {
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(5),
            TimeSpan.FromDays(30),
            TimeSpan.FromDays(30 * 3),
            TimeSpan.FromDays(30 * 6),
            TimeSpan.FromDays(365),
            TimeSpan.FromDays(365 * 2),
            TimeSpan.FromDays(365 * 5),
            TimeSpan.FromDays(365 * 10),
            TimeSpan.FromDays(365 * 100),
        };

        public YahooFinance(StockServiceSettings settings, string logPath) : base(settings, logPath)
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
                ApiRequestsPerMinuteLimit = 60,
                ApiRequestsPerDayLimit = 0,
                ApiRequestsPerMonthLimit = 0,
                HistoryEnabled = true
            };
        }

        public static bool IsMySettings(StockServiceSettings settings)
        {
            return settings.Name == name;
        }

        protected override async Task<StockQuote> DownloadThrottledQuoteAsync(string symbol)
        {
            if (this.symbolsNotFound.Contains(symbol))
            {
                throw new StockQuoteNotFoundException(symbol);
            }

            // Ask for 2 days because while the market is open there is no data for today and this
            // returns an empty list, by asking for 5 days we get the close value from some previous
            // day when the market was open..
            var list = await this.DownloadChart(symbol, "5d");
            if (list.Count > 0)
            {
                var quote = list.Last();
                if (string.Compare(quote.Symbol, symbol, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    throw new StockQuoteNotFoundException(string.Format(Walkabout.Properties.Resources.DifferentSymbolReturned, symbol, quote.Symbol));
                }
                else
                {
                    return quote;
                }
            }

            // Hmmm, perhaps the fund is closed?
            throw new StockQuoteNotFoundException(symbol);
        }

        private async Task<List<StockQuote>> DownloadChart(string symbol, string range)
        {
            if (this.symbolsNotFound.Contains(symbol))
            {
                throw new StockQuoteNotFoundException(symbol);
            }

            Debug.WriteLine($"Yahoo: DownloadThrottledQuoteAsync {symbol} for range {range}");

            string uri = string.Format(stockQuoteUri, symbol, "1d", range);
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
            var msg = await client.GetAsync(uri, this.TokenSource.Token);
            if (!msg.IsSuccessStatusCode)
            {
                if (msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    this.symbolsNotFound.Add(symbol);
                    throw new StockQuoteNotFoundException(symbol);
                }
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
                        return this.ParseStockQuotes(o);
                    }
                }
            }
        }

        private List<decimal> GetNumbers(JArray array)
        {
            List<decimal> result = new List<decimal>();
            foreach (JToken number in array)
            {
                if (number.Type == JTokenType.Float || number.Type == JTokenType.Integer)
                {
                    result.Add((decimal)number);
                }
            }
            return result;
        }

        private static void MergeHandler<T, U>(List<T> a, List<U> b, Action<T, U> mergeAction)
        {
            for (int i = 0; i < a.Count && i < b.Count; i++)
            {
                mergeAction(a[i], b[i]);
            }
        }

        private List<StockQuote> ParseStockQuotes(JObject child)
        {
            // See https://query2.finance.yahoo.com/v8/finance/chart/MSFT?interval=1d&range=5d
            var list = new List<StockQuote>();

            string symbol = null;
            JToken value;
            if (child.TryGetValue("chart", StringComparison.Ordinal, out value) && value.Type == JTokenType.Object)
            {
                JObject chart = (JObject)value;
                if (chart.TryGetValue("result", StringComparison.Ordinal, out value) && value.Type == JTokenType.Array)
                {
                    JArray result = (JArray)value;
                    if (result.First.Type == JTokenType.Object)
                    {
                        JObject item = (JObject)result.First;
                        if (item.TryGetValue("meta", StringComparison.Ordinal, out value) && value.Type == JTokenType.Object)
                        {
                            symbol = (string)value["symbol"];
                            // TBD: check currency
                            // TBD: check instrumentType=EQUITY
                        }

                        if (item.TryGetValue("timestamp", StringComparison.Ordinal, out value) && value.Type == JTokenType.Array)
                        {
                            foreach (JToken timestamp in (JArray)value)
                            {
                                // this is where we find multiple values if the range > 1d.
                                long ticks = (long)timestamp;
                                var quote = new StockQuote()
                                {
                                    Symbol = symbol,
                                    Downloaded = DateTime.Now,
                                    Date = DateTimeOffset.FromUnixTimeSeconds(ticks).LocalDateTime
                                };
                                list.Add(quote);
                            }
                        }

                        if (item.TryGetValue("indicators", StringComparison.Ordinal, out value) && value.Type == JTokenType.Object)
                        {
                            JObject indicators = (JObject)value;
                            if (indicators.TryGetValue("quote", StringComparison.Ordinal, out value) && value.Type == JTokenType.Array)
                            {
                                JArray quotes = (JArray)value;
                                if (quotes.First.Type == JTokenType.Object)
                                {
                                    JObject quote = (JObject)(quotes.First);
                                    if (quote.TryGetValue("open", StringComparison.Ordinal, out value) && value.Type == JTokenType.Array)
                                    {
                                        // this is where we find multiple values if the range > 1d.
                                        MergeHandler(list, this.GetNumbers((JArray)value), (o, n) => o.Open = n);
                                    }
                                    if (quote.TryGetValue("low", StringComparison.Ordinal, out value) && value.Type == JTokenType.Array)
                                    {
                                        // this is where we find multiple values if the range > 1d.
                                        MergeHandler(list, this.GetNumbers((JArray)value), (o, n) => o.Low = n);
                                    }
                                    if (quote.TryGetValue("volume", StringComparison.Ordinal, out value) && value.Type == JTokenType.Array)
                                    {
                                        // this is where we find multiple values if the range > 1d.
                                        MergeHandler(list, this.GetNumbers((JArray)value), (o, n) => o.Volume = n);
                                    }
                                    if (quote.TryGetValue("close", StringComparison.Ordinal, out value) && value.Type == JTokenType.Array)
                                    {
                                        // this is where we find multiple values if the range > 1d.
                                        MergeHandler(list, this.GetNumbers((JArray)value), (o, n) => o.Close = n);
                                    }
                                    if (quote.TryGetValue("high", StringComparison.Ordinal, out value) && value.Type == JTokenType.Array)
                                    {
                                        // this is where we find multiple values if the range > 1d.
                                        MergeHandler(list, this.GetNumbers((JArray)value), (o, n) => o.High = n);
                                    }
                                }
                            }
                        }
                    }
                }

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var quote = list[i];
                    if (quote.Open == 0 && quote.Close == 0 && quote.High == 0 && quote.Low == 0)
                    {
                        // then this is the current day and market is still open so no data yet.
                        list.RemoveAt(i);
                    }
                }
            }

            return list;
        }

        protected override async Task<bool> DownloadThrottledQuoteHistoryAsync(StockQuoteHistory history)
        {
            string range = "max";
            if (history.NotFound)
            {
                // don't keep trying to download quotes that don't exist.
                return false;
            }
            var entry = history.History.LastOrDefault();
            if (entry != null)
            {
                var span = DateTime.Now - entry.Date;
                for (int i = 0, n = validRangeSpans.Count(); i < n; i++)
                {
                    if (span > validRangeSpans[i])
                    {
                        range = validRanges[i];
                        break;
                    }
                }
            }

            try
            {
                var list = await this.DownloadChart(history.Symbol, range);
                foreach (var quote in list)
                {
                    history.MergeQuote(quote);
                }

                if (range == "max")
                {
                    // this was a summary, now try and get the last year complete daily values.
                    list = await this.DownloadChart(history.Symbol, "1y");
                    foreach (var quote in list)
                    {
                        history.MergeQuote(quote);
                    }
                }
            }
            catch (StockQuoteNotFoundException)
            {
                history.NotFound = true;
            }
            return true;
        }
    }
}
