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
using System.Windows.Documents;
using System.Windows.Interop;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Configuration;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Walkabout.StockQuotes
{
    /// <summary>
    /// This class encapsulates the REST API on the https://api.twelvedata.com/ stock service.
    /// </summary>
    internal class TwelveData : ThrottledStockQuoteService
    {
        private static int MaxHistory = 5000;
        private static readonly string name = "TwelveData";
        private static readonly string baseAddress = "https://api.twelvedata.com/";
        // {0}=Symbol, {1}=number of days back from today, {2}=apikey
        private const string stockQuoteUri = "https://api.twelvedata.com/time_series?interval=1day&format=JSON&symbol={0}&start_date={1}&end_date={2}";
        private const string earliestTimeUri = "https://api.twelvedata.com/earliest_timestamp?format=JSON&&interval=1day&symbol={0}";
        private const string authorizationHeader = "apikey {0}";

        public TwelveData(StockServiceSettings settings, string logPath) : base(settings, logPath)
        {
            settings.SplitHistoryEnabled = true;
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
                ApiRequestsPerMinuteLimit = 8,
                ApiRequestsPerDayLimit = 800,
                ApiRequestsPerMonthLimit = 0,
                HistoryEnabled = true,
                SplitHistoryEnabled = true
            };
        }

        public static bool IsMySettings(StockServiceSettings settings)
        {
            return settings.Name == name;
        }

        private async Task<DateTime?> GetEarliestTime(string symbol)
        {
            string uri = string.Format(earliestTimeUri, symbol);
            string authorization = string.Format(authorizationHeader, this.Settings.ApiKey);
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", authorization);
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
                        this.AssertSuccess(o);
                        JToken value;
                        if (o.TryGetValue("datetime", out value) && value.Type == JTokenType.String)
                        {
                            if (DateTime.TryParse((string)value, out DateTime result))
                            {
                                return result;
                            }
                        }
                    }
                }
            }
            return null;
        }

        protected override async Task<StockQuote> DownloadThrottledQuoteAsync(string symbol)
        {
            // Do nothing, it is more efficient to wait for the history download since our history
            // download can do mimimal work to "fill holes" in the history including getting most
            // recent data if we need it.
            await Task.CompletedTask;
            return null;
        }

        private async Task<List<StockQuote>> DownloadTimeSeriesAsync(string symbol, DateRange range)
        {
            // Fetch at most MaxHistory items to avoid exception.  We don't fetch it all here because
            // that can be a huge amount of ancient history we don't care about.  We want to get the 
            // most up to date history for everything first.  The next time the user views this stock
            // we'll fetch another older chunk and eventually get everything that way.
            DateTime end = range.End;
            DateTime start = end.AddDays(-MaxHistory);
            if (range.Start < start)
            {
                range.Start = start;
            }

            var startString = start.ToString("yyyy-MM-dd");
            var endString = end.AddDays(1).ToString("yyyy-MM-dd");
            Debug.WriteLine($"TwelveData: DownloadThrottledQuoteAsync {symbol} from {start} to {end}");
            string uri = string.Format(stockQuoteUri, symbol, startString, endString);
            string authorization = string.Format(authorizationHeader, this.Settings.ApiKey);
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", authorization);
            client.Timeout = TimeSpan.FromSeconds(60);
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
                        this.AssertSuccess(o);
                        var quotes = this.ParseStockQuotes(o);
                        return quotes;
                    }
                }
            }
        }

        private void AssertSuccess(JObject child)
        {
            JToken value;
            if (child.TryGetValue("code", out value) && value.Type == JTokenType.Integer)
            {
                int code = (int)value;
                if (code != 200)
                {
                    var status = "error";
                    if (child.TryGetValue("status", out value) && value.Type == JTokenType.String)
                    {
                        status = (string)value;
                    }
                    var message = "";
                    if (child.TryGetValue("message", out value) && value.Type == JTokenType.String)
                    {
                        message = (string)value;
                    }
                    var msg = $"{this.FriendlyName} returned {status} code {code}: {message}";
                    if (code == 404 ||  // not found
                        code == 403) // not in plan.
                    {
                        throw new StockQuoteNotFoundException(msg);
                    }
                    else if (code == 429)
                    {
                        // throttle limit reached.
                        var ex = new StockQuoteThrottledException(msg);
                        if (msg.Contains("minute"))
                        {
                            ex.MinuteLimitReached = true;
                        }
                        else if (msg.Contains("day"))
                        {
                            ex.DailyLimitReached = true;
                        }
                        else if (msg.Contains("month"))
                        {
                            ex.MonthlyLimitReached = true;
                        }
                        throw ex;
                    }
                    throw new Exception(msg);
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

            if (history.EarliestTime == null)
            {
                history.EarliestTime = await this.GetEarliestTime(history.Symbol);
            }
            var earliest = history.EarliestTime != null ? history.EarliestTime.Value : DateTime.Today.AddYears(-10);
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
                            var quotes = await this.DownloadTimeSeriesAsync(history.Symbol, range);
                            foreach (var quote in quotes)
                            {
                                history.MergeQuote(quote);
                            }
                        }
                    }
                    catch (StockQuoteNotFoundException)
                    {
                        history.NotFound = true;
                    }
                }
            } 
            else
            {
                var quotes = await this.DownloadTimeSeriesAsync(history.Symbol, new DateRange(earliest, DateTime.Today));
                foreach (var quote in quotes)
                {
                    history.MergeQuote(quote);
                }
            }
            return true;
        }
    }
}
