using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Walkabout.StockQuotes
{
    public class IEXCloud : ThrottledStockQuoteService
    {
        private static readonly string name = "IEXCloud";
        private static readonly string baseAddress = "https://iexcloud.io/";
        private const string stockQuoteAddress = "https://api.iex.cloud/v1/data/core/quote/{0}?token={1}";

        // See https://iexcloud.io/docs/core/QUOTE

        public IEXCloud(StockServiceSettings settings, string logPath) : base(settings, logPath)
        {
            settings.Name = name;
        }

        public override string FriendlyName => name;

        public override string WebAddress => baseAddress;

        public override bool SupportsHistory => false;

        public static StockServiceSettings GetDefaultSettings()
        {
            return new StockServiceSettings()
            {
                Name = name,
                Address = baseAddress,
                OldName = "https://iexcloud.io/",
                ApiKey = "",
                ApiRequestsPerMinuteLimit = 60,
                ApiRequestsPerDayLimit = 0,
                ApiRequestsPerMonthLimit = 500000
            };
        }

        public static bool IsMySettings(StockServiceSettings settings)
        {
            return settings.Name == name;
        }

        protected override async Task<StockQuote> DownloadThrottledQuoteAsync(string symbol)
        {
            string uri = string.Format(stockQuoteAddress, symbol, this.Settings.ApiKey);

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", userAgent);
            client.Timeout = TimeSpan.FromSeconds(30);
            var msg = await client.GetAsync(uri, this.TokenSource.Token);
            if (!msg.IsSuccessStatusCode)
            {
                throw new Exception("IEXCloud http error " + msg.StatusCode + ": " + msg.ReasonPhrase);
            }
            else
            {
                this.CountCall();
                using (Stream stm = await msg.Content.ReadAsStreamAsync())
                {
                    using (StreamReader sr = new StreamReader(stm, Encoding.UTF8))
                    {
                        string json = sr.ReadToEnd();
                        JArray o = JArray.Parse(json);
                        if (o.Count == 0 || o.First.Type != JTokenType.Object)
                        {
                            throw new StockQuoteNotFoundException("");
                        }
                        StockQuote quote = IEXCloud.ParseStockQuote((JObject)o.First);
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

        public static StockQuote ParseStockQuote(JObject child)
        {
            // See https://iexcloud.io/docs/core/QUOTE

            var quote = new StockQuote() { Downloaded = DateTime.Now };
            JToken value;
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
            return quote;
        }

        protected override async Task<bool> DownloadThrottledQuoteHistoryAsync(StockQuoteHistory history)
        {
            await Task.CompletedTask;
            return false;
        }
    }
}
