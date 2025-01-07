using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms.Design.Behavior;
using Walkabout.Data;

namespace Walkabout.StockQuotes
{
    /// <summary>
    /// Provides an efficient lookup of the market price of a given security at the given date.
    /// </summary>
    public class StockQuoteCache
    {
        private readonly MyMoney myMoney;
        private readonly DownloadLog log;
        private IDictionary<Security, List<Investment>> transactionsBySecurity;
        private HashSet<Security> changed;
        private long lockCount;

        // For an O(1) Date => StockQuote lookup, so we index the stock quote history in a Dictionary here.
        private readonly Dictionary<Security, StockQuoteIndex> quoteIndex = new Dictionary<Security, StockQuoteIndex>();

        public StockQuoteCache(MyMoney money, DownloadLog log)
        {
            this.myMoney = money;
            this.log = log;
            this.myMoney.Securities.Changed += this.Securities_Changed;
        }

        public string LogFolder => this.log.Folder;

        private void Securities_Changed(object sender, ChangeEventArgs e)
        {
            while (e != null)
            {
                if (e.Item is Security s)
                {
                    if (this.lockCount > 0)
                    {
                        if (this.changed == null)
                        {
                            this.changed = new HashSet<Security>();
                        }
                        this.changed.Add(s);
                    }
                    else
                    {
                        this.OnSecurityChanged(s);
                    }
                }
                e = e.Next;
            }
        }

        private void OnSecurityChanged(Security s)
        {
            if (this.quoteIndex.ContainsKey(s))
            {
                this.quoteIndex.Remove(s);
            }
            if (this.transactionsBySecurity != null && this.transactionsBySecurity.ContainsKey(s))
            {
                this.transactionsBySecurity.Remove(s);
            }
        }

        public IDisposable BeginLock()
        {
            this.lockCount++;
            return new UpdateLock(this);
        }

        private void ReleaseLock()
        {
            this.lockCount--;
            if (this.lockCount <= 0)
            {
                this.lockCount = 0;
                var snapshot = this.changed;
                this.changed = null;
                if (snapshot != null)
                {
                    foreach (var s in snapshot)
                    {
                        this.OnSecurityChanged(s);
                    }
                }
            }
        }

        public async Task<decimal> GetSecurityMarketPrice(DateTime date, Security s)
        {
            // return the closing price of the given security for this date.
            if (date.Date == DateTime.Today.Date)
            {
                return s.Price;
            }

            bool found = false;
            decimal price = 0;
            if (this.quoteIndex.TryGetValue(s, out StockQuoteIndex index))
            {
                var quote = index.GetQuote(date);
                if (quote != null)
                {
                    found = true;
                    price = quote.Close;
                }
            }
            else
            {
                index = await this.LoadIndexFromHistory(s);
                // create an empty index that we can update below.
                this.quoteIndex[s] = index;
            }

            if (!found)
            {
                // hmmm, then we have to search our own transactions for a recorded UnitPrice.
                if (this.transactionsBySecurity == null)
                {
                    this.transactionsBySecurity = this.myMoney.GetTransactionsGroupedBySecurity((a) => true, date.AddDays(1));
                }

                if (this.transactionsBySecurity.TryGetValue(s, out List<Investment> trades) && trades != null)
                {
                    price = 0;
                    foreach (var t in trades)
                    {
                        if (t.Date > date)
                        {
                            break;
                        }
                        if (t.UnitPrice != 0)
                        {
                            price = t.UnitPrice;
                        }
                    }
                }
                if (price != 0)
                {
                    // remember this one in our index.
                    index.SetQuote(date, price);
                }
            }

            return price;
        }

        internal async Task<StockQuoteIndex> LoadIndexFromHistory(Security s)
        {
            StockQuoteIndex index = null;
            if (s != null && !string.IsNullOrEmpty(s.Symbol) && !this.quoteIndex.TryGetValue(s, out index))
            {
                StockQuoteHistory history = null;
                if (!string.IsNullOrEmpty(s.Symbol))
                {
                    // find the prices in the download log if we have one.
                    history = await this.log.GetHistory(s.Symbol);
                }

                var splits = this.myMoney.StockSplits.GetStockSplitsForSecurity(s);
                index = new StockQuoteIndex(history, splits);
                this.quoteIndex[s] = index;
            }
            if (index == null)
            {
                index = new StockQuoteIndex();
                this.quoteIndex[s] = index;
            }

            return index;
        }

        /// <summary>
        /// Provides optimized access to stock quotes
        /// </summary>
        internal class StockQuoteIndex
        {
            private readonly Dictionary<DateTime, StockQuote> index = new Dictionary<DateTime, StockQuote>();
            private readonly IList<StockSplit> splits;

            public StockQuoteIndex()
            {
            }

            /// <summary>
            /// Construct index from the given history.  Now remember ths history is split adjusted so
            /// we also need to split information in order to implement GetQuote() to figure out what the
            /// actual closing price of the stock was on the given date.
            /// </summary>
            public StockQuoteIndex(StockQuoteHistory history, IList<StockSplit> splits)
            {
                this.splits = splits;
                if (history != null)
                {
                    foreach (var quote in history.GetSorted())
                    {
                        this.index[quote.Date.Date] = quote;
                    }
                }
            }

            public void SetQuote(DateTime date, decimal price)
            {
                this.index[date] = new StockQuote() { Close = price, Date = date };
            }

            /// <summary>
            /// Get the value of the stock on this given date (not split adjusted).
            /// </summary>
            public StockQuote GetQuote(DateTime date)
            {
                if (this.index.Count != 0)
                {
                    for (int i = 30; i > 0; i--)
                    {
                        if (this.index.TryGetValue(date.Date, out StockQuote value))
                        {
                            if (this.splits != null)
                            {
                                // Splits are sorted in ascending order, so to un-split-adjust the 
                                // history we have to work backwards in time from today until the given date
                                // reverse applying any splits we find.
                                for (int k = this.splits.Count - 1; k >= 0; k--)
                                {
                                    var split = this.splits[k];
                                    if (split.Date > date)
                                    {
                                        // suppose we found a 2:1 split this means the history is listed at 1/2 the price
                                        // that 1 stock actually cost on this date, because the history is split adjusted.
                                        // So we reverse that by doubling the prices.
                                        var ratio = split.Numerator / split.Denominator;
                                        value = new StockQuote()
                                        {
                                            Date = value.Date,
                                            High = value.High * ratio,
                                            Low = value.Low * ratio,
                                            Open = value.Open * ratio,
                                            Close = value.Close * ratio,
                                            Name = value.Name,
                                            Symbol = value.Symbol,
                                            Downloaded = value.Downloaded,
                                            Volume = value.Volume,
                                        };
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                            return value;
                        }
                        // go back at most a month looking for an open stock market!
                        date = date.AddDays(-1);
                    }
                }
                return null;
            }
        }

        private class UpdateLock : IDisposable
        {
            private readonly StockQuoteCache parent;
            public UpdateLock(StockQuoteCache parent)
            {
                this.parent = parent;
            }

            public void Dispose()
            {
                this.parent.ReleaseLock();
            }
        }

    }
}
