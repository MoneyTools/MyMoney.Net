using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Walkabout.Data;
using Walkabout.Taxes;

namespace Walkabout.Migrate
{

    /// <summary>
    /// This class produces various kinds of .txf files for importing into TurboTax.
    /// Based on version 41 of the spec, see http://turbotax.intuit.com/txf/TXF041.jsp
    /// </summary>
    public class TxfExporter
    {
        private readonly MyMoney money;

        public TxfExporter(MyMoney money)
        {
            this.money = money;
        }

        public void Export(string txfFile, DateTime startDate, DateTime endDate, bool cpitalGainsOnly, bool consolidateOnDateSold)
        {
            using (StreamWriter sw = new StreamWriter(txfFile))
            {
                WriteHeader(sw);
                if (!cpitalGainsOnly)
                {
                    this.ExportCategories(sw, startDate, endDate);
                }

                CapitalGainsTaxCalculator calculator = new CapitalGainsTaxCalculator(this.money, endDate, consolidateOnDateSold, true);
                this.ExportCapitalGains(calculator, null, startDate, endDate, sw);
            }
        }

        private void ExportCategories(TextWriter writer, DateTime startDate, DateTime endDate)
        {
            TaxCategoryCollection taxCategories = new TaxCategoryCollection();
            List<TaxCategory> list = taxCategories.GenerateGroups(this.money, startDate, endDate);
            if (list != null)
            {
                foreach (TaxCategory tc in list)
                {
                    IDictionary<string, List<Transaction>> groups = tc.Groups;
                    if (groups != null)
                    {
                        int line = 1;
                        decimal sum = 0;
                        // Write summary records.
                        foreach (KeyValuePair<string, List<Transaction>> subtotal in groups)
                        {
                            string payee = subtotal.Key;
                            List<Transaction> tgroup = subtotal.Value;
                            decimal total = (from t in tgroup select t.Amount).Sum();

                            switch (tc.RecordFormat)
                            {
                                case 1:
                                    WriteRecordFormat3(writer, "TD", line, tc, payee, total);
                                    break;
                                case 3:
                                    WriteRecordFormat3(writer, "TS", line, tc, payee, total);
                                    break;
                            }
                            if (tc.MultipleAllowed)
                            {
                                line++;
                            }
                            sum += total;
                        }

                        switch (tc.RecordFormat)
                        {
                            case 1:
                                WriteRecordFormat1(writer, "TS", tc, sum);
                                break;
                        }
                    }
                }
            }
        }

        private static void WriteRecordFormat1(TextWriter writer, string type, TaxCategory tc, decimal total)
        {
            // Example:
            // TD
            // N280
            // C1
            // L1
            // $120.00
            writer.WriteLine(type);
            writer.WriteLine("N" + tc.RefNum);
            writer.WriteLine("C1"); // copy 1 of this value
            writer.WriteLine("L1"); // line 1 of this value
            string sign = "";
            if (tc.DefaultSign == -1 && total > 0)
            {
                sign = "+";
            }
            writer.WriteLine("$" + sign + total.ToString());
            writer.WriteLine("^"); // end of record
        }

        private static void WriteRecordFormat3(TextWriter writer, string type, int line, TaxCategory tc, string payee, decimal total)
        {
            // Example:
            // TD
            // N287
            // C1
            // L1
            // $120.00
            // PBank of America
            writer.WriteLine(type);
            writer.WriteLine("N" + tc.RefNum);
            writer.WriteLine("C1"); // copy 1 of this value
            writer.WriteLine("L" + line); // line 1 of this value
            string sign = "";
            if (tc.DefaultSign == -1 && total > 0)
            {
                sign = "+";
            }
            writer.WriteLine("$" + sign + Round(tc.DefaultSign > 0, total));
            if (type == "TD")
            {
                writer.WriteLine("X" + payee);
            }
            else
            {
                writer.WriteLine("P" + payee);
            }
            writer.WriteLine("^"); // end of record
        }

        public void ExportCapitalGains(Account a, TextWriter writer, DateTime startDate, DateTime endDate, bool consolidateSecuritiesOnDateSold)
        {
            CapitalGainsTaxCalculator calculator = new CapitalGainsTaxCalculator(this.money, endDate, consolidateSecuritiesOnDateSold, true);
            this.ExportCapitalGains(calculator, a, startDate, endDate, writer);
        }

        private void ExportCapitalGains(CapitalGainsTaxCalculator calculator, Account a, DateTime startDate, DateTime endDate, TextWriter writer)
        {
            foreach (var data in calculator.Unknown)
            {
                if (data.DateSold >= startDate && data.DateSold < endDate)
                {
                    if (a == null || data.Account == a)
                    {
                        // cannot compute the cost basis.
                        WriteRecordFormat4(writer, data, 673);
                    }
                }
            }

            foreach (var data in calculator.ShortTerm)
            {
                if (data.DateSold >= startDate && data.DateSold < endDate)
                {
                    if (a == null || data.Account == a)
                    {
                        int refnum = 321; // short term gain/loss;
                        WriteRecordFormat4(writer, data, refnum);
                    }
                }
            }

            foreach (var data in calculator.LongTerm)
            {
                if (data.DateSold >= startDate && data.DateSold < endDate)
                {
                    if (a == null || data.Account == a)
                    {
                        int refnum = 323; // long term gain/loss
                        WriteRecordFormat4(writer, data, refnum);
                    }
                }
            }

        }

        private static void WriteRecordFormat4(TextWriter writer, SecuritySale data, int refnum)
        {
            writer.WriteLine("TD");
            writer.WriteLine("N" + refnum);
            writer.WriteLine("C1");
            writer.WriteLine("L1");
            // convention is to stick the quantity sold in the payee field.
            writer.WriteLine("P" + (int)data.UnitsSold + " " + data.Security);
            if (refnum == 673)
            {
                writer.WriteLine("D");
            }
            else if (data.DateAcquired == null)
            {
                writer.WriteLine("DVARIOUS");
            }
            else
            {
                writer.WriteLine("D" + data.DateAcquired.Value.ToShortDateString());
            }
            writer.WriteLine("D" + data.DateSold.ToShortDateString());
            if (refnum == 673)
            {
                writer.WriteLine("$");
            }
            else
            {
                writer.WriteLine("$" + Round(true, data.TotalCostBasis));
            }
            if (refnum == 673)
            {
                writer.WriteLine("$" + Round(true, data.SaleProceeds));
            }
            else
            {
                writer.WriteLine("$" + Round(true, data.SaleProceeds));
            }
            writer.WriteLine("^"); // end of record
        }

        private static void WriteHeader(StreamWriter sw)
        {
            sw.WriteLine("V041");
            sw.WriteLine("A" + typeof(TxfExporter).Assembly.GetName().Name);
            sw.WriteLine("D" + DateTime.Today.ToShortDateString());
            sw.WriteLine("^"); // end of record
        }

        /// <summary>
        /// In order to not owe the IRS anything, we want to round up the numbers and not mess with the half pennies.
        /// Technically we could file a rounding adjustment, but for a few pennies it's not worth the effort.
        /// </summary>
        private static decimal Round(bool income, decimal x)
        {
            if (income)
            {
                // then this is an income, so report Math.Ceiling (give the rounding error to the IRS).
                return Math.Ceiling(x * 100) / 100;
            }
            else
            {
                // then this is an expense, so don't claim more than Math.Floor.
                return Math.Floor(x * 100) / 100;
            }
        }


    }
}
