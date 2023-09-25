using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Walkabout.StockQuotes
{
    /// <summary>
    /// This class encapsulates the REST API on the https://polygon.io/ stock service.
    /// </summary>
    internal class PolygonStocks : ThrottledStockQuoteService
    {
        private static readonly string name = "Polygon";
        private static readonly string baseAddress = "https://polygon.io/";
        // {0}=symbol
        private const string stockQuoteUri = "https://api.polygon.io/v2/aggs/ticker/{0}/prev?adjusted=true";
        private const string authorizationHeader = "Bearer {0}";

        // query ticker list
        private const string getSupportedTickers = "https://api.polygon.io/v3/reference/tickers?active=true";
        private PolygonTickerInfo tickerInfo;
        private Dictionary<string, PolygonTicker> tickerMap = new Dictionary<string, PolygonTicker>();

        public PolygonStocks(StockServiceSettings settings, string logPath) : base(settings, logPath)
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
                OldName = "https://polygon.io/",
                ApiKey = "",
                ApiRequestsPerMinuteLimit = 5,
                ApiRequestsPerDayLimit = 0,
                ApiRequestsPerMonthLimit = 0
            };
        }

        public static bool IsMySettings(StockServiceSettings settings)
        {
            return settings.Name == name;
        }

        protected override async Task<StockQuote> DownloadThrottledQuoteAsync(string symbol)
        {
            await this.DownloadTickersAsync();
            if (!this.tickerMap.ContainsKey(symbol))
            {
                return null; // not supported by Polygon
            }
            string uri = string.Format(stockQuoteUri, symbol);
            string bearer = string.Format(authorizationHeader, this.Settings.ApiKey);
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
            client.DefaultRequestHeaders.Add("Authorization", bearer);
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
                        StockQuote quote = this.ParseStockQuote(o);
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

        private StockQuote ParseStockQuote(JObject child)        
        {
            // See https://polygon.io/docs/stocks/get_v2_aggs_ticker__stocksticker__prev

            var quote = new StockQuote() { Downloaded = DateTime.Now };
            JToken value;
            if (child.TryGetValue("ticker", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
            {
                quote.Symbol = (string)value;
                if (this.tickerMap.TryGetValue(quote.Symbol, out PolygonTicker ticker))
                {
                    quote.Name = ticker.name;
                }
            }
            if (child.TryGetValue("results", StringComparison.Ordinal, out value) && value.Type == JTokenType.Array)
            {
                JArray array = (JArray)value;
                if (array.First.Type == JTokenType.Object)
                {
                    JObject price = (JObject)array.First;
                    if (price.TryGetValue("c", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                    {
                        quote.Close = (decimal)value;
                    }
                    if (price.TryGetValue("h", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                    {
                        quote.High = (decimal)value;
                    }
                    if (price.TryGetValue("l", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                    {
                        quote.Low = (decimal)value;
                    }
                    if (price.TryGetValue("o", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                    {
                        quote.Open = (decimal)value;
                    }
                    if (price.TryGetValue("v", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                    {
                        quote.Volume = (decimal)value;
                    }
                    if (price.TryGetValue("t", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                    {
                        long ticks = (long)value;
                        quote.Date = DateTimeOffset.FromUnixTimeMilliseconds(ticks).LocalDateTime;
                    }
                }
            }
            else
            {
                return null;
            }
            return quote;
        }

        protected override async Task<bool> DownloadThrottledQuoteHistoryAsync(StockQuoteHistory history)
        {
            if (this.Settings.HistoryEnabled)
            {
                // TBD: this requires the priced service, and user to enable history in the settings.
                // BUGBUG: Make sure this download is only happening once and make both wait on the same task if it is running...
                // await this.DownloadTickersAsync();
                // this.CountCall();
            }
            await Task.CompletedTask;
            return false;
        }

        private async Task DownloadTickersAsync()
        {
            if (tickerInfo == null)
            {
                tickerInfo = PolygonTickerInfo.Load(this.LogFolder);
            }

            if (tickerInfo.Tickers.Count == 0 || !tickerInfo.Complete || (DateTime.Today - tickerInfo.LastUpdated).Days > 30)
            {
                tickerInfo = new PolygonTickerInfo() { LastUpdated = DateTime.Now, Tickers = new List<PolygonTicker>() };
                string url = getSupportedTickers;
                do
                {
                    // refetch the list.
                    await this.ThrottleSleep();
                    Debug.WriteLine(url);
                    string bearer = string.Format(authorizationHeader, this.Settings.ApiKey);
                    HttpClient client = new HttpClient();
                    client.DefaultRequestHeaders.Add("User-Agent", userAgent);
                    client.DefaultRequestHeaders.Add("Authorization", bearer);
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var msg = await client.GetAsync(url, this.TokenSource.Token);
                    if (!msg.IsSuccessStatusCode)
                    {
                        // hmmm, service is down right now?
                        Debug.WriteLine(this.FriendlyName + " http error " + msg.StatusCode + ": " + msg.ReasonPhrase);
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
                                this.ParseTickers(o, tickerInfo);
                                JToken value;
                                if (o.TryGetValue("next_url", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                                {
                                    url = (string)value;
                                    tickerInfo.Save(this.LogFolder);
                                }
                                else
                                {
                                    Debug.WriteLine("Ticker info is complete");
                                    tickerInfo.Complete = true;
                                    tickerInfo.Save(this.LogFolder);
                                    break;
                                }
                            }
                        }
                    }
                } while (url != null);

                tickerMap = new Dictionary<string, PolygonTicker>();
                foreach (var ticker in tickerInfo.Tickers)
                {
                    tickerMap[ticker.ticker] = ticker;
                }
            }

        }

        private void ParseTickers(JObject o, PolygonTickerInfo info)
        {
            JToken value;
            if (o.TryGetValue("results", StringComparison.Ordinal, out value) && value.Type == JTokenType.Array)
            {
                JArray array = (JArray)value;
                foreach (JToken child in array)
                {
                    if (child.Type == JTokenType.Object)
                    {
                        PolygonTicker ticker = new PolygonTicker();
                        JObject item = (JObject)child;
                        if (item.TryGetValue("ticker", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            ticker.ticker = (string)value;
                        }
                        if (item.TryGetValue("name", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            ticker.name = (string)value;
                        }
                        if (item.TryGetValue("market", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            ticker.market = (string)value;
                        }
                        if (item.TryGetValue("locale", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            ticker.locale = (string)value;
                        }
                        if (item.TryGetValue("primary_exchange", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            ticker.primary_exchange = (string)value;
                        }
                        if (item.TryGetValue("type", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            ticker.type = (string)value;
                        }
                        if (item.TryGetValue("currency_name", StringComparison.Ordinal, out value) && value.Type != JTokenType.Null)
                        {
                            ticker.currency_name = (string)value;
                        }
                        info.Tickers.Add(ticker);
                    }
                }
            }
        }
    }


    public class PolygonTicker
    {
        public PolygonTicker() { }
        public string ticker { get; set; }
        public string name { get; set; }
        public string market { get; set; }
        public string locale { get; set; }
        public string primary_exchange { get; set; }
        public string type { get; set; }
        public string active { get; set; }
        public string currency_name { get; set; }
    }

    public class PolygonTickerInfo
    {
        public PolygonTickerInfo() { }

        public DateTime LastUpdated { get; set; }

        public bool Complete { get; set; }

        public List<PolygonTicker> Tickers { get; set; }

        public static string GetFileName(string logFolder)
        {
            return System.IO.Path.Combine(logFolder, "PolygonTickerInfo.xml");
        }

        public static PolygonTickerInfo Load(string logFolder)
        {
            var filename = GetFileName(logFolder);
            if (System.IO.File.Exists(filename))
            {
                try
                {
                    XmlSerializer s = new XmlSerializer(typeof(PolygonTickerInfo));
                    using (XmlReader r = XmlReader.Create(filename))
                    {
                        return (PolygonTickerInfo)s.Deserialize(r);
                    }
                } 
                catch (Exception)
                {
                }
            }
            return new PolygonTickerInfo() { LastUpdated = DateTime.MinValue, Tickers = new List<PolygonTicker>() };
        }

        public void Save(string logFolder)
        {
            var filename = GetFileName(logFolder);
            XmlSerializer s = new XmlSerializer(typeof(PolygonTickerInfo));
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            using (XmlWriter w = XmlWriter.Create(filename, settings))
            {
                s.Serialize(w, this);
            }
        }
    }
}
