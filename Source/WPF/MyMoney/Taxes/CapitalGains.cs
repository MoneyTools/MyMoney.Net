using System;
using System.Collections.Generic;
using System.Diagnostics;
using Walkabout.Data;

namespace Walkabout.Taxes
{

    /// <summary>
    /// This class computes the capital gains associated with stock sales..
    /// It does this by matching the shares sold against prior stock purchases in a FIFO order, so oldest stocks are sold first.
    /// For example, suppose you purchase 20 shares in 2005 and another 50 in 2008, then sold 70 shares in 2010.  The sale will
    /// produce two CapitalGains records, one for the batch of 20 shares and another for the batch of 50 shares because these
    /// will likely have different Cost Basis and therefore different Gain/Loss amounts for tax purposes.  It takes stock
    /// splits into account.
    /// </summary>
    public class CapitalGainsTaxCalculator : CostBasisCalculator
    {
        List<SecuritySale> unknown = new List<SecuritySale>();
        List<SecuritySale> shortTerm = new List<SecuritySale>();
        List<SecuritySale> longTerm = new List<SecuritySale>();

        /// <summary>
        /// Compute capital gains associated with stock sales and whether they are long term or short term gains.
        /// </summary>
        /// <param name="money">The transactions</param>
        /// <param name="year">The year for the report</param>
        /// <param name="consolidateOnDateSold">Normally it will consolidate duplicate securities on date acquired.
        /// If you pass true here it will consolidate on same date sold putting null in the DateAcquired field
        /// which can be reported as "VARIOUS"</param>
        /// <param name="ignoreTaxDeferred">Whether to ignore tax deferred transactions</param>
        public CapitalGainsTaxCalculator(MyMoney money, DateTime toDate, bool consolidateOnDateSold, bool ignoreTaxDeferred)
            : base(money, toDate)
        {
            CalculateCapitalGains(ignoreTaxDeferred);
            Consolidate(consolidateOnDateSold);
        }

        private void Consolidate(bool consolidateOnDateSold)
        {
            this.unknown = Consolidate(this.unknown, consolidateOnDateSold);
            this.shortTerm = Consolidate(this.shortTerm, consolidateOnDateSold);
            this.longTerm = Consolidate(this.longTerm, consolidateOnDateSold);
        }

        /// <summary>
        /// Consolidate capital gains information. If the securities are the same and the sale date is the same then
        /// combine them, and mark the "DateAquired" as null (reported as 'various').
        /// </summary>
        private List<SecuritySale> Consolidate(List<SecuritySale> list, bool consolidateOnDateSold)
        {
            List<SecuritySale> result = new List<SecuritySale>();
            SecuritySale previous = null;
            foreach (SecuritySale cg in list)
            {
                if (previous == null || previous.Security != cg.Security || (!consolidateOnDateSold && previous.DateAcquired != cg.DateAcquired) ||
                    (previous.SalePricePerUnit != cg.SalePricePerUnit) ||
                    (consolidateOnDateSold && previous.DateSold != cg.DateSold))
                {
                    previous = cg;
                    result.Add(cg);
                }
                else
                {
                    previous.Consolidate(cg);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns CapitalGains for securities that we could not find matching acquisitions for so
        /// we could not compute cost basis.
        /// </summary>
        public List<SecuritySale> Unknown { get { return this.unknown; } }

        /// <summary>
        /// Returns CapitalGains for securities acquired within 1 year of the sale date.
        /// </summary>
        public List<SecuritySale> ShortTerm { get { return this.shortTerm; } }

        /// <summary>
        /// Returns CapitalGains for securities acquired more than 1 year before the sale date.
        /// </summary>
        public List<SecuritySale> LongTerm { get { return this.longTerm; } }

        /// <summary>
        /// Calculate the CapitalGains 
        /// </summary>
        private void CalculateCapitalGains(bool ignoreTaxDeferred)
        {           
            foreach (SecuritySale sale in this.GetSales())
            {
                if (ignoreTaxDeferred && sale.Account.IsTaxDeferred)
                {
                    continue;
                }
                if (sale.Error != null)
                {
                    unknown.Add(sale);
                }
                else
                {
                    Debug.Assert(sale.DateAcquired.HasValue);

                    TimeSpan diff = sale.DateSold - sale.DateAcquired.Value;
                    if (diff.Days > 365)
                    {
                        longTerm.Add(sale);
                    }
                    else
                    {
                        shortTerm.Add(sale);
                    }
                }
            }
        }


    }
}
