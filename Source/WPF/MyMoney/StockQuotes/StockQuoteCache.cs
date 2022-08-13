using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Walkabout.Data;

namespace Walkabout.StockQuotes
{
    /// <summary>
    /// Provides an efficient lookup of the market price of a given security at the given date.
    /// </summary>
    public class StockQuoteCache
    {
        MyMoney myMoney;
        DownloadLog log;
        IDictionary<Security, List<Investment>> transactionsBySecurity;
        HashSet<Security> changed;
        long lockCount;

        // For an O(1) Date => StockQuote lookup, so we index the stock quote history in a Dictionary here.
        Dictionary<Security, StockQuoteIndex> quoteIndex = new Dictionary<Security, StockQuoteIndex>();

        public StockQuoteCache(MyMoney money, DownloadLog log)
        {
            this.myMoney = money;
            this.log = log;
            this.myMoney.Securities.Changed += Securities_Changed;
        }

        private void Securities_Changed(object sender, ChangeEventArgs e)
        {
            while (e != null)
            {
                if (e.Item is Security s)
                {
                    if (lockCount > 0)
                    {
                        if (changed == null)
                        {
                            changed = new HashSet<Security>();
                        }
                        changed.Add(s);
                    }
                    else
                    {
                        OnSecurityChanged(s);
                    }
                }
                e = e.Next;
            }
        }

        private void OnSecurityChanged(Security s)
        {
            if (quoteIndex.ContainsKey(s))
            {
                quoteIndex.Remove(s);
            }
            if (transactionsBySecurity.ContainsKey(s))
            {
                transactionsBySecurity.Remove(s);
            }
        }

        public IDisposable BeginLock()
        {
            lockCount++;
            return new UpdateLock(this);
        }

        private void ReleaseLock()
        {
            lockCount--;
            if (lockCount <= 0)
            {
                lockCount = 0;
                var snapshot = changed;
                changed = null;
                if (snapshot != null)
                {
                    foreach (var s in snapshot)
                    {
                        OnSecurityChanged(s);
                    }
                }
            }
        }

        public decimal GetSecurityMarketPrice(DateTime date, Security s)
        {
            // return the closing price of the given security for this date.
            if (date.Date == DateTime.Today.Date)
            {
                return s.Price;
            }

            decimal price = 0;
            if (quoteIndex.TryGetValue(s, out StockQuoteIndex index))
            { 
                var quote = index.GetQuote(date);
                if (quote != null)
                {
                    price = quote.Close;
                }
            }
            else
            {
                // create an empty index that we can update below.
                quoteIndex[s] = index = new StockQuoteIndex(null);
            }

            // hmmm, then we have to search our own transactions for a recorded UnitPrice.
            if (price == 0)
            {
                if (this.transactionsBySecurity == null)
                {
                    this.transactionsBySecurity = this.myMoney.GetTransactionsGroupedBySecurity((a) => true, date.AddDays(1));
                }

                if (transactionsBySecurity.TryGetValue(s, out List<Investment> trades) && trades != null)
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
            }

            if (price != 0)
            {
                index.SetQuote(date, price);
            }
            return price;
        }

        internal async Task LoadHistory(Security s)
        {
            if (!quoteIndex.TryGetValue(s, out StockQuoteIndex index))
            {
                StockQuoteHistory history = null;
                if (!string.IsNullOrEmpty(s.Symbol))
                {
                    // find the prices in the download log if we have one.
                    history = await this.log.GetHistory(s.Symbol);
                }
                index = new StockQuoteIndex(history);
                quoteIndex[s] = index;
            }
        }

        /// <summary>
        /// Use this method to provide stock quote info from Transactions in the case that
        /// you have a security that has no download stock price history, which can happen
        /// with custom securities that are not publicly traded.
        /// </summary>
        internal void SetQuote(DateTime date, Security security, decimal price)
        {
            if (!quoteIndex.TryGetValue(security, out StockQuoteIndex index))
            {
                // create an empty index that we can update below.
                quoteIndex[security] = new StockQuoteIndex(null);
            }
            quoteIndex[security].SetQuote(date, price);
        }

        internal class StockQuoteIndex
        {
            Dictionary<DateTime, StockQuote> index = new Dictionary<DateTime, StockQuote>();
            
            public StockQuoteIndex(StockQuoteHistory history)
            {
                if (history != null)
                {
                    foreach (var quote in history.GetSorted())
                    {
                        index[quote.Date] = quote;
                    }
                }
            }

            public void SetQuote(DateTime date, decimal price)
            {
                index[date] = new StockQuote() { Close = price, Date = date };
            }

            public StockQuote GetQuote(DateTime date)
            {
                if (index.Count != 0)
                {
                    for (int i = 7; i > 0; i--)
                    {
                        if (index.TryGetValue(date, out StockQuote value))
                        {
                            return value;
                        }
                        // go back at most a week looking for an open stock market!
                        date = date.AddDays(-1);
                    }
                }
                return null;
            }
        }

        class UpdateLock : IDisposable
        {
            StockQuoteCache parent;
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
