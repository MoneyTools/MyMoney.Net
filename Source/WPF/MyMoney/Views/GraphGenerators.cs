using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Walkabout.Data;
using Walkabout.StockQuotes;
using Walkabout.Views.Controls;
using Walkabout.Utilities;
using Microsoft.VisualStudio.Diagnostics.PerformanceProvider;

namespace Walkabout.Views
{
    class TransactionGraphGenerator : IGraphGenerator
    {
        NumberFormatInfo nfi = new NumberFormatInfo();
        IEnumerable data;
        Account account;
        Category category;
        TransactionViewName viewName;

        public TransactionGraphGenerator(IEnumerable data, Account account, Category category, TransactionViewName viewName)
        {
            this.data = data;
            this.account = account;
            this.category = category;
            this.viewName = viewName;
            nfi.NumberDecimalDigits = 2;
            nfi.CurrencyNegativePattern = 0;
        }

        public bool IsFlipped
        {
            get
            {
                return ((this.account != null && this.account.Type == AccountType.Credit) ||
                        (this.category != null && this.category.Type == CategoryType.Expense));
            }
        }

        public IEnumerable<TrendValue> Generate()
        {
            if (data != null)
            {
                decimal balance = this.account != null ? this.account.OpeningBalance : 0;

                foreach (object row in data)
                {
                    Transaction t = row as Transaction;
                    if (t != null && !t.IsDeleted && t.Status != TransactionStatus.Void)
                    {
                        switch (this.viewName)
                        {
                            case TransactionViewName.BySecurity:
                                // When we build the trend graph for a specific security in the security view,
                                // we should show the overall value of the securities instead, which is precalculated and stored within each transaction
                                balance = t.RunningBalance;
                                break;

                            case TransactionViewName.ByCategory:
                            case TransactionViewName.ByCategoryCustom:
                                balance += t.CurrencyNormalizedAmount(t.AmountMinusTax);
                                break;

                            case TransactionViewName.ByPayee:
                                balance += t.CurrencyNormalizedAmount(t.Amount);
                                break;

                            default:
                                balance += t.Amount;
                                break;
                        }

                        yield return new TrendValue()
                        {
                            Date = t.Date,
                            Value = balance,
                            UserData = t
                        };
                    }
                }
            }
        }
    }

    /// <summary>
    /// This class computes the TrendGraph for brokerage and retirement accounts by computing the 
    /// historical daily market value of a given account.  The trick is doing this efficiently...
    /// </summary>
    class BrokerageAccountGraphGenerator : IGraphGenerator
    {
        MyMoney myMoney;
        DownloadLog log;
        Account account;
        Dictionary<string, StockQuotesByDate> histories = new Dictionary<string, StockQuotesByDate>();
        Dictionary<string, List<StockSplit>> pendingSplits = new Dictionary<string, List<StockSplit>>();

        // We need O(1) Date => StockQuote lookup, so we index the stock quotes in a Dictionary here.
        // Since the stock market is not open every day, we also keep the "lastQuote" returned to fill
        // in the gaps and create a smooth graph.
        class StockQuotesByDate
        {
            Dictionary<DateTime, StockQuote> map = new Dictionary<DateTime, StockQuote>();
            StockQuote lastQuote;

            public StockQuotesByDate(StockQuoteHistory history)
            {
                if (history != null)
                {
                    foreach (var quote in history.GetSorted())
                    {
                        map[quote.Date] = quote;
                    }
                }
            }

            public void SetQuote(DateTime date, decimal price)
            {
                lastQuote = new StockQuote() { Close = price, Date = date };
            }

            public StockQuote GetQuote(DateTime date)
            {
                if (map.Count != 0)
                {
                    for (int i = 5; i > 0; i--)
                    {
                        if (map.TryGetValue(date, out StockQuote value))
                        {
                            lastQuote = value;
                            return value;
                        }
                        // go back at most a week looking for an open stock market!
                        date = date.AddDays(-1);
                    }
                }
                return lastQuote;
            }
        }

        public BrokerageAccountGraphGenerator(MyMoney money, DownloadLog log, Account account)
        {
            this.myMoney = money;
            this.log = log;
            this.account = account;
        }

        /// <summary>
        /// We need all the relevant StockQuoteHistory objects loaded from the cached StockQuote logs.
        /// We only need to load StockQuoteHistory for securites referenced in the given account.
        /// Since this is loading a bunch of .xml files it could take a while so the method is async
        /// and we report the progress via the IStatusService.  Note the DownloadLog caches all these
        /// in memory so the second time this generator is used it will be much faster.
        /// 
        /// TODO: but there is a bit too much caching here, if the online security downloaded fetches
        /// new stock quotes for today, that will not be taken into account in the trend graph until you
        /// switch accounts.
        /// </summary>
        public async Task Prepare(IStatusService status)
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.GraphPrepare))
            {
#endif
                Dictionary<string, Security> toLoad = new Dictionary<string, Security>();
                foreach (var transaction in this.myMoney.Transactions.GetTransactionsFrom(this.account))
                {
                    var symbol = transaction.InvestmentSecuritySymbol;
                    if (!string.IsNullOrEmpty(symbol) && !toLoad.ContainsKey(symbol))
                    {
                        var s = transaction.InvestmentSecurity;
                        toLoad[symbol] = s;
                    }
                }

                var keys = toLoad.Keys.ToArray();
                for (int i = 0; i < keys.Length; i++)
                {
                    status.ShowProgress(0, keys.Length, 1);
                    var symbol = keys[i];
                    var s = toLoad[symbol];
                    StockQuoteHistory history = await this.log.GetHistory(symbol);
                    histories[symbol] = new StockQuotesByDate(history);
                    List<StockSplit> splits = new List<StockSplit>(this.myMoney.StockSplits.GetStockSplitsForSecurity(s));
                    splits.Sort(new Comparison<StockSplit>((a, b) =>
                    {
                        return DateTime.Compare(a.Date, b.Date); // ascending
                    }));
                    this.pendingSplits[symbol] = splits;
                }
                status.ShowProgress(0, 0, 0);

#if PerformanceBlocks
            }
#endif
        }

        public bool IsFlipped => false;

        public IEnumerable<TrendValue> Generate()
        {
#if PerformanceBlocks
            using (PerformanceBlock.Create(ComponentId.Money, CategoryId.View, MeasurementId.GraphGenerate))
            {
#endif
                // This code is similar to the CostBasisCalculator with one big difference.  The CostBasisCalculator
                // computes the market value on a given date.  This one computes the market value for every day starting
                // with the date of the first transaction.  The trick to making this efficient enough is to have 
                // and optimized version of ComputeMarketValue.

                var holdings = new AccountHoldings();
                var date = DateTime.MinValue;
                var first = true;
                decimal cashBalance = this.account.OpeningBalance;
                foreach (var t in this.myMoney.Transactions.GetTransactionsFrom(this.account))
                {
                    if (first)
                    {
                        first = false;
                        date = t.Date;
                    }

                    // This is where we are smoothing the graph by filling in historical market value for every day
                    // starting with the first transaction all the way to the last.
                    while (date.Date < t.Date.Date)
                    {
                        // Stock splits are in a "pending" list and when the Date rolls by it can trigger a specific
                        // stock split.  When that happens all the remaining units and cost bases in the AccountHoldings
                        // are adjusted accordingly.
                        ApplyPendingSplits(date, holdings);
                        decimal marketValue = ComputeMarketValue(date, holdings);
                        yield return new TrendValue() { Date = date, UserData = t, Value = marketValue + cashBalance };
                        date = date.AddDays(1);
                    }

                    if (t.IsDeleted || t.Status == TransactionStatus.Void)
                    {
                        continue;
                    }

                    // all transactions can have a cash amount that contributes to the market value.
                    cashBalance += t.Amount;

                    // and if it is a security transaction (buy, sell, etc) then we have to record these actions
                    // in the AccountHoldings object so we know how many of each security is remaining at any
                    // given date.
                    if (t.InvestmentSecurity != null)
                    {
                        var i = t.Investment;
                        var s = i.Security;

                        switch (t.InvestmentType)
                        {
                            case InvestmentType.Buy:
                            case InvestmentType.Add:
                                if (i.Units > 0)
                                {
                                    if (i.UnitPrice != 0)
                                    {
                                        // In case we don't have any online stock quote histories this at least
                                        // tells our StockQuotesByDate what the unit price was on the date of this
                                        // transaction.
                                        RecordPrice(i.Date, s, i.UnitPrice);
                                    }
                                    holdings.Buy(i.Security, i.Date, i.Units, i.OriginalCostBasis);
                                    foreach(var sale in holdings.ProcessPendingSales(i.Security))
                                    {
                                        // have to pull the yield iterator.
                                    }
                                }
                                break;
                            case InvestmentType.Remove:
                            case InvestmentType.Sell:
                                if (i.Units > 0)
                                {
                                    if (i.UnitPrice != 0)
                                    {
                                        RecordPrice(i.Date, s, i.UnitPrice);
                                    }
                                    if (i.Transaction.Transfer == null)
                                    {
                                        foreach(var sale in holdings.Sell(s, i.Date, i.Units, i.OriginalCostBasis))
                                        {
                                            // have to pull the yield iterator.
                                        }
                                    }
                                    else
                                    {
                                        // bugbug; could this ever be a split? Don't think so...
                                        Investment add = i.Transaction.Transfer.Transaction.Investment;
                                        Debug.Assert(add != null, "Other side of the Transfer needs to be an Investment transaction");
                                        if (add != null)
                                        {
                                            Debug.Assert(add.Type == InvestmentType.Add, "Other side of transfer should be an Add transaction");

                                            // now instead of doing a simple Add on the other side, we need to remember the cost basis of each purchase
                                            // used to cover the remove

                                            foreach (SecuritySale sale in holdings.Sell(s, i.Date, i.Units, 0))
                                            {
                                                if (sale.DateAcquired.HasValue)
                                                {
                                                    // now transfer the cost basis over to the target account.
                                                    holdings.Buy(s, sale.DateAcquired.Value, sale.UnitsSold, sale.CostBasisPerUnit * sale.UnitsSold);
                                                    foreach (var pendingSale in holdings.ProcessPendingSales(s))
                                                    {
                                                        // have to pull the yield iterator.
                                                    }
                                                }
                                                else
                                                {
                                                    // this is the error case, but the error will be re-generated on the target account when needed.
                                                }
                                            }
                                        }
                                    }
                                }

                                break;
                            default:
                                break;
                        }
                    }
                }
#if PerformanceBlocks
            }
#endif
        }

        private decimal ComputeMarketValue(DateTime date, AccountHoldings holding)
        {
            // The market value of the AccountHoldings is just the sum of the security
            // units remaining times the stock price for on this given date.
            decimal total = 0;
            foreach (var held in holding.GetHoldings())
            {
                var value = this.GetSecurityValue(date, held.Security);
                if (value >= 0)
                {
                    total += held.UnitsRemaining * value;
                }
                else
                {
                    // missing history? Then just take the price recorded.
                    total += held.TotalCostBasis;
                }
            }
            return total;
        }

        private void RecordPrice(DateTime date, Security security, decimal price)
        {
            if (security != null && histories.TryGetValue(security.Symbol, out StockQuotesByDate history))
            {
                history.SetQuote(date, price);
            }
        }

        private decimal GetSecurityValue(DateTime date, Security security)
        {
            // use our optimized StockQuotesByDate object so we can implement this in an O(1) timeframe.
            date = date.Date;
            if (security != null && histories.TryGetValue(security.Symbol, out StockQuotesByDate history))
            {
                var quote = history.GetQuote(date);
                if (quote != null)
                {
                    return quote.Close;
                }
            }
            return -1;
        }

        private void ApplyPendingSplits(DateTime dateTime, AccountHoldings holding)
        {
            // When a stock split becomes due we remove it from the pendingSplits
            // so that this gets faster and faster as we proceed through the transactions
            // because there will be less and less splits to check each time.
            dateTime = dateTime.Date;
            foreach (var key in this.pendingSplits.Keys.ToArray())
            {
                List<StockSplit> splits = this.pendingSplits[key];
                StockSplit next = splits.FirstOrDefault();
                while (next != null && next.Date.Date < dateTime)
                {
                    ApplySplit(next, holding);
                    splits.Remove(next);
                    next = splits.FirstOrDefault();
                    if (next == null)
                    {
                        this.pendingSplits.Remove(key);
                    }
                }
            }
        }

        private void ApplySplit(StockSplit split, AccountHoldings holding)
        {
            Security s = split.Security;
            decimal total = 0;
            foreach (SecurityPurchase purchase in holding.GetPurchases(s))
            {
                if (purchase.DatePurchased < split.Date)
                {
                    purchase.UnitsRemaining = (purchase.UnitsRemaining * split.Numerator) / split.Denominator;
                    purchase.CostBasisPerUnit = (purchase.CostBasisPerUnit * split.Denominator) / split.Numerator;
                    total += purchase.UnitsRemaining;
                }
            }

            // yikes also have to split the pending sales...?
            foreach (SecuritySale pending in holding.GetPendingSalesForSecurity(s))
            {
                if (pending.DateSold < split.Date)
                {
                    pending.UnitsSold = (pending.UnitsSold * split.Numerator) / split.Denominator;
                    pending.SalePricePerUnit = (pending.SalePricePerUnit * split.Denominator) / split.Numerator;
                }
            }

            if (s.SecurityType == SecurityType.Equity)
            {
                // companies don't want to deal with fractional stocks, they usually distribute a "cash in lieu"
                // transaction in this case to compensate you for the rounding error.
                decimal floor = Math.Floor(total);
                if (floor != total)
                {
                    decimal diff = total - floor;
                    decimal adjustment = (total - diff) / total;
                    // distribute this rounding error back into the units remaining so we remember it.
                    foreach (SecurityPurchase purchase in holding.GetPurchases(s))
                    {
                        purchase.UnitsRemaining = Math.Round(purchase.UnitsRemaining * adjustment, 5);
                    }
                }
            }
        }

    }

    class SecurityGraphGenerator : IGraphGenerator
    {
        NumberFormatInfo nfi = new NumberFormatInfo();

        StockQuoteHistory history;
        Security security;

        public SecurityGraphGenerator(StockQuoteHistory history, Security security)
        {
            this.history = history;
            this.security = security;
            nfi.NumberDecimalDigits = 2;
            nfi.CurrencyNegativePattern = 0;
        }

        public bool IsFlipped { get { return false; } }

        public IEnumerable<TrendValue> Generate()
        {
            string symbol = history.Symbol;
            foreach (var item in history.History)
            {
                decimal adjustedClose = ApplySplits(item.Close, item.Date);

                yield return new TrendValue()
                {
                    Date = item.Date,
                    Value = adjustedClose,
                    UserData = symbol
                };
            }
        }

        private decimal ApplySplits(decimal close, DateTime date)
        {
            foreach (var split in this.security.StockSplitsSnapshot)
            {
                if (date < split.Date && split.Numerator != 0)
                {
                    // reverse the effect of stock split.  For example, if stock split 2 : 1 on 1/10/2010
                    // and closing price was $20 on 1/1/2010, then the effective value of that stock on 
                    // 1/1/2010 is now $10 because of the split.
                    close *= split.Denominator / split.Numerator;
                }
            }
            return close;
        }
    }

}
