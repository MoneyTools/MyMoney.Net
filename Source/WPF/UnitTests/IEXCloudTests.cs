using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Walkabout.StockQuotes;

namespace Walkabout.Tests
{
    internal class IEXCloudTests
    {
        private const string quote = @"[
  {
    ""avgTotalVolume"": 20269499,
    ""calculationPrice"": ""close"",
    ""change"": -2.52,
    ""changePercent"": -0.00789,
    ""close"": 317.01,
    ""closeSource"": ""official"",
    ""closeTime"": 1695412800336,
    ""companyName"": ""Microsoft Corporation"",
    ""currency"": ""USD"",
    ""delayedPrice"": 316.975,
    ""delayedPriceTime"": 1695412773002,
    ""extendedChange"": -0.2,
    ""extendedChangePercent"": -0.00063,
    ""extendedPrice"": 316.81,
    ""extendedPriceTime"": 1695427199165,
    ""high"": 321.45,
    ""highSource"": ""15 minute delayed price"",
    ""highTime"": 1695412799995,
    ""iexAskPrice"": 0,
    ""iexAskSize"": 0,
    ""iexBidPrice"": 0,
    ""iexBidSize"": 0,
    ""iexClose"": 317.02,
    ""iexCloseTime"": 1695412799905,
    ""iexLastUpdated"": 1695412799905,
    ""iexMarketPercent"": 0.01477730650110195,
    ""iexOpen"": 321.41,
    ""iexOpenTime"": 1695389400546,
    ""iexRealtimePrice"": 317.02,
    ""iexRealtimeSize"": 100,
    ""iexVolume"": 316942,
    ""lastTradeTime"": 1695412799995,
    ""latestPrice"": 317.01,
    ""latestSource"": ""Close"",
    ""latestTime"": ""September 22, 2023"",
    ""latestUpdate"": 1695412800336,
    ""latestVolume"": 21447887,
    ""low"": 316.15,
    ""lowSource"": ""15 minute delayed price"",
    ""lowTime"": 1695412373631,
    ""marketCap"": 2355309485640,
    ""oddLotDelayedPrice"": 317,
    ""oddLotDelayedPriceTime"": 1695412773245,
    ""open"": 321.4,
    ""openTime"": 1695389400538,
    ""openSource"": ""official"",
    ""peRatio"": 32.75,
    ""previousClose"": 319.53,
    ""previousVolume"": 35560362,
    ""primaryExchange"": ""NASDAQ"",
    ""symbol"": ""MSFT"",
    ""volume"": 21447887,
    ""week52High"": 366.01,
    ""week52Low"": 211.39,
    ""ytdChange"": 0.3229960356674601,
    ""isUSMarketOpen"": false
  }
]";

        [Test]
        public void TestOfxDtdParsing()
        {
            JArray o = JArray.Parse(quote);
            var first = o.First;
            if (first.Type == JTokenType.Object)
            {
                var q = IEXCloud.ParseStockQuote((JObject)first);
                Assert.AreEqual("MSFT", q.Symbol);
            }
        }
    }
}
