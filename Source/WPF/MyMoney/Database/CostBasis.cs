using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Walkabout.Data
{

    /// <summary>
    /// This class is used to track the cost basis for a given purchase, sometimes this cost basis
    /// has to travel across accounts when a transfer occurs.  When securities are purchased 
    /// </summary>
    public class SecurityPurchase
    {
        /// <summary>
        /// The security that was purchased.
        /// </summary>
        public Security Security;

        /// <summary>
        /// The date this security was purchased.
        /// </summary>
        public DateTime DatePurchased;

        /// <summary>
        /// The number of units remaining from this lot.
        /// </summary>
        public decimal UnitsRemaining;

        /// <summary>
        /// The original cost basis for this security per unit.  THis is not necessarily the 
        /// UnitPrice we paid for the security, commissions and fees are also taken into account.
        /// </summary>
        public decimal CostBasisPerUnit;

        /// <summary>
        /// The total remaining cost basis based on the number of units remaning.
        /// </summary>
        public decimal TotalCostBasis { get { return this.CostBasisPerUnit * this.UnitsRemaining; } }

        /// <summary>
        /// Get market value of remaining units.
        /// </summary>
        public decimal LatestMarketValue
        {
            get
            {
                return this.FuturesFactor * this.UnitsRemaining * this.Security.Price;
            }
        }
        public decimal FuturesFactor
        {
            get
            {
                decimal factor = 1;
                // futures prices are always listed by the instance.  But when you buy 1 contract, you always get 100 futures in that contract
                if (this.Security.SecurityType == SecurityType.Futures)
                {
                    factor = 100;
                }
                return factor;
            }
        }

        /// <summary>
        /// Perform a sale of the given number of units.  If we don't have enough return all that we have.
        /// </summary>
        /// <param name="date">The date of the sale</param>
        /// <param name="units">The number we'd like to sell</param>
        /// <param name="unitSalePrice">The price per unit we received at date of sale</param>
        /// <returns>The SecuritySale containing the number of units we are selling from this lot or null
        /// if this lot is empty</returns>
        internal SecuritySale Sell(DateTime date, decimal units, decimal unitSalePrice)
        {
            if (this.UnitsRemaining == 0)
            {
                return null;
            }

            decimal canSell = Math.Min(units, this.UnitsRemaining);
            this.UnitsRemaining -= canSell;

            return new SecuritySale()
            {
                DateSold = date,
                Security = this.Security,
                CostBasisPerUnit = this.CostBasisPerUnit,
                UnitsSold = canSell,
                DateAcquired = this.DatePurchased,
                SalePricePerUnit = unitSalePrice
            };
        }
    }

    /// <summary>
    /// This class is used to track the cost basis for a given sale.
    /// </summary>
    public class SecuritySale
    {
        /// <summary>
        /// This sale represents an error.
        /// </summary>
        public Exception Error;

        /// <summary>
        /// The security that was purchased.
        /// </summary>
        public Security Security;

        /// <summary>
        /// The account that it was sold from.
        /// </summary>
        public Account Account;

        /// <summary>
        /// The date this security was purchased.
        /// </summary>
        public DateTime? DateAcquired;

        /// <summary>
        /// The date this security was sold.
        /// </summary>
        public DateTime DateSold;

        /// <summary>
        /// The price we got for the units at the time of sale (minus fees and commissions)
        /// </summary>
        public decimal SalePricePerUnit;

        /// <summary>
        /// The number of units sold
        /// </summary>
        public decimal UnitsSold;

        /// <summary>
        /// The original cost basis for this security per unit.  THis is not necessarily the 
        /// UnitPrice we paid for the security, commissions and fees are also taken into account.
        /// </summary>
        public decimal CostBasisPerUnit;

        /// <summary>
        /// The total remaining cost basis based on the number of units remaining.
        /// </summary>
        public decimal TotalCostBasis { get { return this.CostBasisPerUnit * this.UnitsSold; } }

        /// <summary>
        /// The total funds received from the transaction
        /// </summary>
        public decimal SaleProceeds { get { return this.SalePricePerUnit * this.UnitsSold; } }

        /// <summary>
        /// The total difference between the Proceeds and the TotalCostBasis
        /// </summary>
        public decimal TotalGain { get { return this.SaleProceeds - this.TotalCostBasis; } }

        /// <summary>
        /// For a roll-up report where individual SecuritySale is too much detail we
        /// can consolidate here, but only if the SalePricePerUnit and CostBasisPerUnit
        /// match.  If they do not match then we set them to zero so they are reported
        /// as "unknown".
        /// </summary>
        /// <param name="cg">The other sale to consolidate</param>
        internal void Consolidate(SecuritySale cg)
        {
            this.UnitsSold += cg.UnitsSold;

            if (this.DateAcquired != cg.DateAcquired)
            {
                // will be reported to IRS as "VARIOUS"
                this.DateAcquired = null;
                this.CostBasisPerUnit = 0;
            }
            if (this.SalePricePerUnit != cg.SalePricePerUnit)
            {
                this.SalePricePerUnit = 0;
            }

            if (this.CostBasisPerUnit != cg.CostBasisPerUnit)
            {
                this.CostBasisPerUnit = 0;
            }
        }
    }

    public class SecurityGroup
    {
        public DateTime Date { get; set; }
        public Security Security { get; set; }
        public SecurityType Type { get; set; }
        public IList<SecurityPurchase> Purchases { get; set; }
        public TaxStatus TaxStatus { get; set; }
        public Predicate<Account> Filter { get; internal set; }
    }


    /// <summary>
    /// We implement a first-in first-out FIFO queue for securities, the assumption is that when
    /// securities are sold you will first sell the security you have been holding the longest
    /// in order to minimize capital gains taxes.
    /// </summary>
    internal class SecurityFifoQueue
    {
        private readonly List<SecurityPurchase> list = new List<SecurityPurchase>();

        /// <summary>
        /// list of pending sales that we couldn't cover before, we keep these until the matching Buy arrives.
        /// </summary>
        private readonly List<SecuritySale> pending = new List<SecuritySale>();

        /// <summary>
        /// The security that we are tracking with this queue.
        /// </summary>
        public Security Security;

        /// <summary>
        /// The account the security is held in.
        /// </summary>
        public Account Account;

        /// <summary>
        /// Record an Add or Buy for a given security.
        /// </summary>
        /// <param name="datePurchased">The date of the purchase</param>
        /// <param name="units">The number of units purchased</param>
        /// <param name="amount">The total cost basis for this purchase</param>
        public void Buy(DateTime datePurchased, decimal units, decimal costBasis)
        {
            SecurityPurchase sp = new SecurityPurchase()
            {
                Security = this.Security,
                DatePurchased = datePurchased,
                CostBasisPerUnit = costBasis / units,
                UnitsRemaining = units
            };

            // insert the purchase in date order
            for (int i = 0, n = this.list.Count; i < n; i++)
            {
                SecurityPurchase e = this.list[i];
                if (e.DatePurchased > datePurchased)
                {
                    this.list.Insert(i, sp);
                    return;
                }
            }

            this.list.Add(sp);
        }

        /// <summary>
        /// Find the oldest holdings that still have UnitsRemaining, and decrement them by the
        /// number of units we are selling.  This might have to sell from multiple SecurityPurchases
        /// in order to cover the requested number of units.  If there are not enough units to cover
        /// the sale then we have a problem and we return a SecuritySale containing the Error information.
        /// </summary>
        /// <param name="dateSold">The date of the sale</param>
        /// <param name="units">The number of units sold</param>
        /// <param name="amount">The total amount we received from the sale</param>
        /// <returns></returns>
        public IEnumerable<SecuritySale> Sell(DateTime dateSold, decimal units, decimal amount)
        {
            decimal salePricePerUnit = amount / units;
            List<SecuritySale> result = new List<SecuritySale>();
            foreach (var purchase in this.list)
            {
                SecuritySale sale = purchase.Sell(dateSold, units, salePricePerUnit);
                if (sale != null)
                {
                    sale.Account = this.Account;
                    units -= sale.UnitsSold;
                    result.Add(sale);

                    if (units <= 0)
                    {
                        // done!
                        break;
                    }
                }
            }

            if (Math.Floor(units) > 0)
            {
                // Generate an error item so we can report this problem later.
                this.pending.Add(new SecuritySale()
                {
                    Security = this.Security,
                    Account = this.Account,
                    DateSold = dateSold,
                    UnitsSold = units,
                    SalePricePerUnit = salePricePerUnit
                });
            }
            return result;
        }

        internal IEnumerable<SecuritySale> GetPendingSales()
        {
            return this.pending;
        }

        internal IEnumerable<SecuritySale> ProcessPendingSales()
        {
            // now that more has arrived, time to see if we can process those pending sales.
            List<SecuritySale> copy = new List<SecuritySale>(this.pending);
            this.pending.Clear();
            List<SecuritySale> result = new List<SecuritySale>();
            foreach (SecuritySale s in copy)
            {
                // this will put any remainder back in the pending list if it still can't be covered.
                foreach (SecuritySale real in this.Sell(s.DateSold, s.UnitsSold, s.UnitsSold * s.SalePricePerUnit))
                {
                    result.Add(real);
                }
            }
            return result;
        }

        internal IEnumerable<SecurityPurchase> GetHoldings()
        {
            List<SecurityPurchase> result = new List<SecurityPurchase>();
            foreach (SecurityPurchase sp in this.list)
            {
                if (sp.UnitsRemaining > 0)
                {
                    result.Add(sp);
                }
            }
            return result;
        }
    }


    /// <summary>
    /// We implement a first-in first-out FIFO queue for securities, the assumption is that when
    /// securities are sold you will first sell the security you have been holding the longest
    /// in order to minimize capital gains taxes.
    /// </summary>
    public class AccountHoldings
    {
        private readonly Dictionary<Security, SecurityFifoQueue> queues = new Dictionary<Security, SecurityFifoQueue>();

        public Account Account;

        /// <summary>
        /// Get current holdings to date.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<SecurityPurchase> GetHoldings()
        {
            List<SecurityPurchase> result = new List<SecurityPurchase>();
            foreach (SecurityFifoQueue queue in this.queues.Values)
            {
                foreach (SecurityPurchase p in queue.GetHoldings())
                {
                    result.Add(p);
                }
            }
            return result;
        }

        /// <summary>
        /// Get current purchases for the given security
        /// </summary>
        /// <returns></returns>
        public IEnumerable<SecurityPurchase> GetPurchases(Security security)
        {
            List<SecurityPurchase> result = new List<SecurityPurchase>();
            SecurityFifoQueue queue = null;
            if (this.queues.TryGetValue(security, out queue))
            {
                foreach (SecurityPurchase p in queue.GetHoldings())
                {
                    result.Add(p);
                }
            }
            return result;
        }

        /// <summary>
        /// Record an Add or Buy for a given security.
        /// </summary>
        ///<param name="s">The security we are buying</param>
        /// <param name="datePurchased">The date of the purchase</param>
        /// <param name="units">THe number of units purchased</param>
        /// <param name="costBasis">The cost basis for the purchase (usually what you paid for it including trading fees)</param>
        public void Buy(Security s, DateTime datePurchased, decimal units, decimal costBasis)
        {
            SecurityFifoQueue queue;
            if (!this.queues.TryGetValue(s, out queue))
            {
                queue = new SecurityFifoQueue()
                {
                    Security = s,
                    Account = this.Account
                };
                this.queues[s] = queue;
            }

            queue.Buy(datePurchased, units, costBasis);
        }

        /// <summary>
        /// Find the oldest holdings that still have UnitsRemaining, and decrement them by the
        /// number of units we are selling.  This might have to sell from multiple SecurityPurchases
        /// in order to cover the requested number of units.  If there are not enough units to cover
        /// the sale then we have a problem.
        /// </summary>
        /// <param name="dateSold">The date of the sale</param>
        /// <param name="units">The number of units sold</param>
        /// <param name="amount">The total amount we received from the sale</param>
        /// <returns></returns>
        public IEnumerable<SecuritySale> Sell(Security s, DateTime dateSold, decimal units, decimal amount)
        {
            SecurityFifoQueue queue;
            if (!this.queues.TryGetValue(s, out queue))
            {
                queue = new SecurityFifoQueue()
                {
                    Security = s,
                    Account = this.Account
                };
                this.queues[s] = queue;
            }

            return queue.Sell(dateSold, units, amount);
        }


        internal IEnumerable<SecuritySale> ProcessPendingSales(Security s)
        {
            SecurityFifoQueue queue;
            if (this.queues.TryGetValue(s, out queue))
            {
                return queue.ProcessPendingSales();
            }
            return new SecuritySale[0];
        }

        internal IEnumerable<SecuritySale> GetPendingSales()
        {
            List<SecuritySale> result = new List<SecuritySale>();
            foreach (var queue in this.queues.Values)
            {
                foreach (SecuritySale sale in queue.GetPendingSales())
                {
                    result.Add(sale);
                }
            }
            return result;
        }

        internal IEnumerable<SecuritySale> GetPendingSalesForSecurity(Security s)
        {
            List<SecuritySale> result = new List<SecuritySale>();
            SecurityFifoQueue queue;
            if (this.queues.TryGetValue(s, out queue))
            {
                foreach (SecuritySale sale in queue.GetPendingSales())
                {
                    result.Add(sale);
                }
            }
            return result;
        }
    }


    /// <summary>
    /// This class computes the cost basis associated with stock sales.
    /// It does this by matching the shares sold against prior stock purchases in a FIFO order, so oldest stocks are sold first.
    /// For example, suppose you purchase 20 shares in 2005 and another 50 in 2008, then sold 70 shares in 2010.  The sale will
    /// produce two SecuritySale records, one for the batch of 20 shares and another for the batch of 50 shares because these
    /// will likely have different Cost Basis and therefore different Gain/Loss amounts for tax purposes.  It takes stock
    /// splits into account.
    /// </summary>
    public class CostBasisCalculator
    {
        private readonly MyMoney myMoney;
        private readonly DateTime toDate;
        private Dictionary<Account, AccountHoldings> byAccount = new Dictionary<Account, AccountHoldings>();
        private readonly List<SecuritySale> sales = new List<SecuritySale>();

        /// <summary>
        /// Compute capital gains associated with stock sales and whether they are long term or short term gains.
        /// </summary>
        /// <param name="money">The transactions</param>
        /// <param name="year">The year for the report</param>
        public CostBasisCalculator(MyMoney money, DateTime toDate)
        {
            this.myMoney = money;
            this.toDate = toDate;
            this.Calculate();
        }

        public IEnumerable<SecuritySale> GetSales()
        {
            return this.sales;
        }

        public IEnumerable<AccountHoldings> GetAccountHoldings() => this.byAccount.Values;

        /// <summary>
        /// Get the current holdings per account.
        /// </summary>
        /// <param name="a">The account</param>
        /// <returns>The holdings listing securities that are still owned</returns>
        public AccountHoldings GetHolding(Account a)
        {
            AccountHoldings holdings = null;
            if (!this.byAccount.TryGetValue(a, out holdings))
            {
                holdings = new AccountHoldings() { Account = a };
                this.byAccount[a] = holdings;
            }
            return holdings;
        }

        /// <summary>
        /// Return all securities that are still owned (have not been sold)
        /// </summary>
        /// <param name="account">Specified account or null for all accounts.</param>
        /// <returns></returns>
        public IList<SecurityGroup> GetHoldingsBySecurityType(Predicate<Account> filter)
        {
            Dictionary<SecurityType, SecurityGroup> result = new Dictionary<SecurityType, SecurityGroup>();

            foreach (var accountHolding in this.byAccount.Values)
            {
                if (filter == null || filter(accountHolding.Account))
                {
                    foreach (var sp in accountHolding.GetHoldings())
                    {
                        var type = sp.Security.SecurityType;
                        SecurityGroup group = null;
                        if (!result.TryGetValue(type, out group))
                        {
                            group = new SecurityGroup() { Date = this.toDate, Security = sp.Security, Type = type, Purchases = new List<SecurityPurchase>() };
                            result[type] = group;
                        }
                        else if (group.Security != sp.Security)
                        {
                            group.Security = null; // is a multisecurity group.
                        }
                        group.Purchases.Add(sp);
                    }
                }
            }
            return new List<SecurityGroup>(result.Values);
        }


        /// <summary>
        /// Get all non-zero holdings remaining for the purchases listed in the given groupByType and
        /// group them be individual security.
        /// </summary>
        /// <returns></returns>
        public IList<SecurityGroup> RegroupBySecurity(SecurityGroup groupByType)
        {
            SortedDictionary<Security, SecurityGroup> holdingsBySecurity = new SortedDictionary<Security, SecurityGroup>(new SecurityComparer());

            // Sort all add, remove, buy, sell transactions by date and by security.
            foreach (SecurityPurchase sp in groupByType.Purchases)
            {
                Security s = sp.Security;
                SecurityGroup group = null;
                if (!holdingsBySecurity.TryGetValue(s, out group))
                {
                    group = new SecurityGroup() { Date = this.toDate, Security = s, Type = s.SecurityType, Purchases = new List<SecurityPurchase>() };
                    holdingsBySecurity[s] = group;
                }
                group.Purchases.Add(sp);
            }

            return new List<SecurityGroup>(holdingsBySecurity.Values);
        }

        /// <summary>
        /// Calculate the CapitalGains 
        /// </summary>
        private void Calculate()
        {
            IDictionary<Security, List<Investment>> map = this.myMoney.GetTransactionsGroupedBySecurity(null, this.toDate);

            this.byAccount = new Dictionary<Account, AccountHoldings>();

            // Now build the AccountHoldings  for all non-transfer add/buy transactions.
            // Don't handle transfers yet - for that we need to be able to compute the cost basis.
            foreach (KeyValuePair<Security, List<Investment>> pair in map)
            {
                Security s = pair.Key;
                List<Investment> list = pair.Value;

                List<StockSplit> splits = new List<StockSplit>(this.myMoney.StockSplits.GetStockSplitsForSecurity(s));
                
                foreach (Investment i in list)
                {
                    // Now we need to apply any splits that are now valid as of  i.Date so we have the currect number of shares
                    // computed for any future transactions.  Question is, if someone buys the stock on the very same day that it
                    // was split, do they get the split or not?  This assumes not.
                    this.ApplySplits(s, splits, i.Date);

                    var holdings = this.GetHolding(i.Transaction.Account);

                    if (i.Type == InvestmentType.Add || i.Type == InvestmentType.Buy)
                    {
                        // transfer "adds" will be handled on the "remove" side below so we get the right cost basis.
                        if (i.Transaction.Transfer == null && i.Units > 0)
                        {
                            holdings.Buy(s, i.Date, i.Units, i.OriginalCostBasis);
                            foreach (SecuritySale pending in holdings.ProcessPendingSales(s))
                            {
                                this.sales.Add(pending);
                            }
                        }
                    }
                    else if ((i.Type == InvestmentType.Remove || i.Type == InvestmentType.Sell) && i.Units > 0)
                    {
                        if (i.Transaction.Transfer == null)
                        {
                            foreach (SecuritySale sale in holdings.Sell(i.Security, i.Date, i.Units, i.OriginalCostBasis))
                            {
                                this.sales.Add(sale);
                            }
                        }
                        else
                        {
                            // track cost basis of securities transferred across accounts.
                            // BugBug; could this ever be a split? Don't think so...
                            Investment add = i.Transaction.Transfer.Transaction.Investment;
                            Debug.Assert(add != null, "Other side of the Transfer needs to be an Investment transaction");
                            if (add != null)
                            {
                                Debug.Assert(add.Type == InvestmentType.Add, "Other side of transfer should be an Add transaction");

                                // now instead of doing a simple Add on the other side, we need to remember the cost basis of each purchase
                                // used to cover the remove

                                foreach (SecuritySale sale in holdings.Sell(i.Security, i.Date, i.Units, 0))
                                {
                                    var targetHoldings = this.GetHolding(add.Transaction.Account);
                                    if (sale.DateAcquired.HasValue)
                                    {
                                        // now transfer the cost basis over to the target account.
                                        targetHoldings.Buy(s, sale.DateAcquired.Value, sale.UnitsSold, sale.CostBasisPerUnit * sale.UnitsSold);
                                        foreach (SecuritySale pending in targetHoldings.ProcessPendingSales(s))
                                        {
                                            this.sales.Add(pending);
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
                }

                this.ApplySplits(s, splits, this.toDate);
            }
        }

        private void ApplySplits(Security s, IList<StockSplit> splits, DateTime dateTime)
        {
            StockSplit next = splits.FirstOrDefault();
            while (next != null && next.Date.Date < dateTime.Date)
            {
                this.ApplySplit(s, next);
                splits.Remove(next);
                next = splits.FirstOrDefault();
            }
        }

        private void ApplySplit(Security s, StockSplit split)
        {
            foreach (AccountHoldings holding in this.byAccount.Values)
            {
                decimal total = 0;
                foreach (SecurityPurchase purchase in holding.GetPurchases(s))
                {
                    purchase.UnitsRemaining = purchase.UnitsRemaining * split.Numerator / split.Denominator;
                    purchase.CostBasisPerUnit = purchase.CostBasisPerUnit * split.Denominator / split.Numerator;
                    total += purchase.UnitsRemaining;
                }

                // yikes also have to split the pending sales...?
                foreach (SecuritySale pending in holding.GetPendingSalesForSecurity(s))
                {
                    if (pending.DateSold < split.Date)
                    {
                        pending.UnitsSold = pending.UnitsSold * split.Numerator / split.Denominator;
                        pending.SalePricePerUnit = pending.SalePricePerUnit * split.Denominator / split.Numerator;
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

        public IEnumerable<SecuritySale> GetPendingSales(Predicate<Account> forAccounts)
        {
            List<SecuritySale> result = new List<SecuritySale>();
            foreach (var pair in this.byAccount)
            {
                Account a = pair.Key;
                if (forAccounts(a))
                {
                    foreach (SecuritySale pending in pair.Value.GetPendingSales())
                    {
                        result.Add(pending);
                    }
                }
            }
            return result;
        }
    }

}
