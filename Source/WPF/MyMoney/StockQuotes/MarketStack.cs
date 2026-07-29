using ModernWpf.Controls;
using Newtonsoft.Json;
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
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Interop;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Configuration;
using Walkabout.Data;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Walkabout.StockQuotes
{
    /// <summary>
    /// This class encapsulates the REST API on the https://api.apilayer.net/marketstack/v2/ stock service.
    /// See https://docs.apilayer.com/marketstack/docs/.
    /// </summary>
    internal class MarketStack : ThrottledStockQuoteService
    {
        private static readonly string name = "MarketStack";
        private static readonly string baseAddress = "https://api.apilayer.net/marketstack/v2/";
        private const string stockQuoteUri = "https://api.apilayer.net/marketstack/v2/eod?symbols={0}&date_from={1}&date_to={2}&limit=1000&offset={3}&sort=ASC&access_key={4}";
        private const string stockSplitsUri = "https://api.marketstack.com/v2/splits?access_key={0}&symbols={1}&date_from={2}&limit=1000&sort=ASC";
        private bool stockSplitsForbidden; // api key is not sufficient
        private bool stockQuotesForbidden; 

        public MarketStack(OnlineServiceSettings settings, string logPath) : base(settings, logPath)
        {
            settings.SplitHistoryEnabled = true;
            settings.Name = name;
            if (string.IsNullOrEmpty(settings.ServiceType))
            {
                settings.ServiceType = "StockQuote";
            }
        }

        public override string FriendlyName => name;

        public override string WebAddress => baseAddress;

        public override bool SupportsHistory => true;

        public static OnlineServiceSettings GetDefaultSettings()
        {
            return new OnlineServiceSettings()
            {
                Name = name,
                Address = baseAddress,
                ServiceType = "StockQuote",
                ApiKey = "",
                ApiRequestsPerMinuteLimit = 0,
                ApiRequestsPerDayLimit = 0,
                ApiRequestsPerMonthLimit = 100,
                HistoryEnabled = true,
                SplitHistoryEnabled = false
            };
        }

        public static bool IsMySettings(OnlineServiceSettings settings)
        {
            return settings.Name == name;
        }

        protected override async Task<StockQuote> DownloadThrottledQuoteAsync(string symbol)
        {
            // Do nothing, it is more efficient to wait for the history download since our history
            // download can do mimimal work to "fill holes" in the history including getting most
            // recent data if we need it.
            await Task.CompletedTask;
            return null;
        }


        protected override async Task<bool> DownloadThrottledQuoteHistoryAsync(StockQuoteHistory history)
        {
            if (history.NotFound)
            {
                // don't keep trying to download quotes that don't exist.
                return false;
            }

            var earliest = history.EarliestTime.HasValue ? history.EarliestTime.Value : DateTime.Today.AddYears(-10);
            int years = (DateTime.Today.Year - earliest.Year) + 1;

            var entry = history.History.LastOrDefault();
            if (entry != null)
            {
                foreach (var range in history.GetMissingDataRanges(years))
                {
                    try
                    {
                        if (range.Start < earliest)
                        {
                            range.Start = earliest;
                        }
                        if (range.End < earliest)
                        {
                            range.End = earliest;
                        }
                        if (range.Start < range.End)
                        {
                            Debug.WriteLine($"{this.FriendlyName} fetching {history.Symbol} from {range.Start} to {range.End}");
                            var quotes = await this.DownloadTimeSeriesAsync(history.Symbol, range);
                            if (quotes != null)
                            {
                                history.UpdateHistory(quotes, range);
                            }
                        }
                    }
                    catch (StockSymbolNotFoundException)
                    {
                        history.NotFound = true;
                    }
                    catch (StockQuoteNoDataException)
                    {
                        history.AddMissingDateRange(range);
                    }
                }
            }
            else
            {
                try
                {
                    var range = new DateRange(earliest, DateTime.Today);
                    Debug.WriteLine($"{this.FriendlyName} fetching {history.Symbol} from {range.Start} to {range.End}");
                    var quotes = await this.DownloadTimeSeriesAsync(history.Symbol, range);
                    if (quotes != null)
                    {
                        history.UpdateHistory(quotes, range);
                    }
                }
                catch (StockSymbolNotFoundException)
                {
                    history.NotFound = true;
                }
            }
            return true;
        }

        private async Task<List<StockQuote>> DownloadTimeSeriesAsync(string symbol, DateRange range)
        {
            // Use https://api.apilayer.net/marketstack/v2/eod with date_from and date_to in format yyyy-mm-dd
            // with limit=1000 and sort=ASC and symbols=symbol and page through as many pages needed to get
            // all the data. The API returns a JSON object with a "data" array containing the quotes and a 
            // pagination block containing limit, offset, count and total so we know how many pages to get.
            // The API also returns a "pagination" object with "limit", "offset", "count" and "total"
            // properties so we can know how many pages to get.
            if (this.stockQuotesForbidden)
            {
                return null;
            }

            int offset = 0;
            bool hasMore = true;
            List<StockQuote> quotes = new List<StockQuote>();
            while (hasMore)
            {
                var uri = string.Format(stockQuoteUri, symbol, range.Start.ToString("yyyy-MM-dd"), range.End.ToString("yyyy-MM-dd"), offset, this.Settings.ApiKey);
                try
                {
                    HttpClient client = new HttpClient();
                    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var msg = await client.GetAsync(uri);
                    if (!msg.IsSuccessStatusCode)
                    {
                        if (msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            // ensure it sleeps again.
                            Debug.WriteLine($"{this.FriendlyName} http error {msg.StatusCode} : {msg.ReasonPhrase}");
                            this.TooManyRequests();
                        }
                        else if (msg.StatusCode == System.Net.HttpStatusCode.UnprocessableContent)
                        {
                            throw new StockSymbolNotFoundException(symbol);
                        }
                        else
                        {
                            if (msg.StatusCode == System.Net.HttpStatusCode.Forbidden)
                            {
                                // api key is not sufficient.
                                this.stockQuotesForbidden = true;
                            }
                            // hmmm, service is down right now?
                            throw new Exception($"{this.FriendlyName} http error {msg.StatusCode} : {msg.ReasonPhrase}");
                        }
                    }
                    else
                    {
                        this.CountCall();
                        using (Stream stm = await msg.Content.ReadAsStreamAsync())
                        {
                            using (StreamReader sr = new StreamReader(stm, Encoding.UTF8))
                            {
                                string json = sr.ReadToEnd();
                                MarketStackData data = null;
                                try
                                {
                                    data = JsonConvert.DeserializeObject<MarketStackData>(json);
                                }
                                catch (Exception ex)
                                {
                                    // hmmm, probably needs debugging.
                                    Debug.WriteLine($"Error deserializing data for {symbol}: {ex.Message}");
                                }
                                if (data != null && data.Data != null)
                                {
                                    if (data.Data.Count == 0)
                                    {
                                        // no data?  Need to remember this so we don't keep asking!
                                        throw new StockQuoteNoDataException(symbol);
                                    }
                                    foreach (var quote in data.Data)
                                    {
                                        var sq = new StockQuote()
                                        {
                                            Name = quote.Name,
                                            Symbol = quote.Symbol,
                                            Date = quote.Date,
                                            Open = quote.Open.HasValue ? quote.Open.Value : 0,
                                            Close = quote.Close.HasValue ? quote.Close.Value : 0,
                                            High = quote.High.HasValue ? quote.High.Value : 0,
                                            Low = quote.Low.HasValue ? quote.Low.Value : 0,
                                            Volume = quote.Volume.HasValue ? quote.Volume.Value : 0,
                                            Downloaded = DateTime.Now
                                        };
                                        if (!quote.Close.HasValue)
                                        {
                                            // We need a closing price if we can find one.
                                            if (quote.Open.HasValue)
                                            {
                                                sq.Close = quote.Open.Value;
                                            }
                                            else if (quote.High.HasValue)
                                            {
                                                sq.Close = quote.High.Value;
                                            }
                                            else if (quote.Low.HasValue)
                                            {
                                                sq.Close = quote.Low.Value;
                                            }
                                        }
                                        quotes.Add(sq);
                                    }
                                    offset += data.Pagination.Count;
                                    hasMore = offset < data.Pagination.Total;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error downloading market data for symbol {symbol}: {ex.Message}");
                    throw;
                }
            }
            return quotes;
        }

        /// <summary>
        /// Get an updated list of stock splits for the given security starting at the given date.
        /// </summary>
        /// <param name="security">the security to find splits for</param>
        /// <param name="dateFrom">The date to start from</param>
        /// <returns>The existing or updated list of stock splits</returns>
        /// <exception cref="StockSymbolNotFoundException">If there is no online data for this security</exception>
        /// <exception cref="Exception">If something else goes wrong</exception>
        public override async Task<IList<StockSplit>> UpdateStockSplits(Security security, DateTime dateFrom)
        {
            Securities container = security.Parent as Securities;
            if (container == null)
            {
                throw new Exception("Security has no parent Securities container");
            }
            MyMoney money = container.Parent as MyMoney;
            if (money == null)
            {
                throw new Exception("Could not find parent MyMoney object");
            }

            var splits = money.StockSplits; // get existing data.

            var symbol = security.Symbol;
            if (string.IsNullOrEmpty(symbol))
            {
                throw new Exception("Security has no ticker symbol");
            }

            var existing = splits.GetStockSplitsForSecurity(security);

            if (stockSplitsForbidden)
            {
                return existing;
            }

            var uri = string.Format(stockSplitsUri, this.Settings.ApiKey, symbol, dateFrom.ToString("yyyy-MM-dd"));
            try
            {
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", userAgent);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(30);
                var msg = await client.GetAsync(uri);
                if (!msg.IsSuccessStatusCode)
                {
                    if (msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        // ensure it sleeps again.
                        Debug.WriteLine($"{this.FriendlyName} http error {msg.StatusCode} : {msg.ReasonPhrase}");
                        this.TooManyRequests();
                    }
                    else if (msg.StatusCode == System.Net.HttpStatusCode.UnprocessableContent)
                    {
                        throw new StockSymbolNotFoundException(symbol);
                    }
                    else
                    {
                        if (msg.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            // api key is not sufficient.
                            this.stockSplitsForbidden = true;
                        }
                        // hmmm, service is down right now?
                        throw new Exception($"{this.FriendlyName} http error {msg.StatusCode} : {msg.ReasonPhrase}");
                    }
                }
                else
                {
                    this.CountCall();
                    using (Stream stm = await msg.Content.ReadAsStreamAsync())
                    {
                        using (StreamReader sr = new StreamReader(stm, Encoding.UTF8))
                        {
                            MarketStackSplits data = null;
                            string json = sr.ReadToEnd();
                            try
                            {
                                data = JsonConvert.DeserializeObject<MarketStackSplits>(json);
                            }
                            catch (Exception ex)
                            {
                                // hmmm, probably needs debugging.
                                Debug.WriteLine($"Error deserializing data for {symbol}: {ex.Message}");
                            }
                            if (data != null && data.Data != null)
                            {
                                if (data.Data.Count == 0)
                                {
                                    // no data?  Need to remember this so we don't keep asking!
                                    throw new StockQuoteNoDataException(symbol);
                                }
                                using (var scope = splits.CreateUpdateScope());

                                foreach (var quote in data.Data)
                                {
                                    foreach (var e in existing)
                                    {
                                        if (e.Date.Date == quote.Date.Date)
                                        {
                                            // then we already have it, make sure factor matches.
                                        }
                                        else
                                        {
                                            var s = new StockSplit();
                                            s.Date = quote.Date.Date;
                                            //s.Numerator = ???
                                            splits.AddStockSplit(s);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading stock splits for symbol {symbol}: {ex.Message}");
                throw;
            }

            return splits.GetStockSplitsForSecurity(security);
        }
    }

    public class MarketStackPagination
    {
        [JsonProperty("limit")]
        public int Limit { get; set; }
        [JsonProperty("offset")]
        public int Offset { get; set; }
        [JsonProperty("count")]
        public int Count { get; set; }
        [JsonProperty("total")]
        public int Total { get; set; }
    }

    public class MarketStackQuote
    {
        [JsonProperty("date")]
        public DateTime Date { get; set; }
        [JsonProperty("open")]
        public decimal? Open { get; set; }
        [JsonProperty("high")]
        public decimal? High { get; set; }
        [JsonProperty("low")]
        public decimal? Low { get; set; }
        [JsonProperty("close")]
        public decimal? Close { get; set; }     // bugbug sometimes it returns null for "close"
        [JsonProperty("volume")]
        public long? Volume { get; set; } // bugbug sometimes it returns null for "volume"
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("asset_type")]
        public string AssetType { get; set; }
        [JsonProperty("price_currency")]
        public string Currency { get; set; }
        [JsonProperty("symbol")]
        public string Symbol { get; set; }
    }

    public class MarketStackData
    {
        [JsonProperty("pagination")]
        public MarketStackPagination Pagination { get; set; }
        [JsonProperty("data")]
        public List<MarketStackQuote> Data { get; set; }

    }

    public class MarketStackSplit
    {
        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("split_factor")]
        public decimal? SplitFactor{ get; set; }
    }

    public class MarketStackSplits
    {
        [JsonProperty("pagination")]
        public MarketStackPagination Pagination { get; set; }
        [JsonProperty("data")]
        public List<MarketStackSplit> Data { get; set; }
    }
}
