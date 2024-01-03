using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Permissions;
using System.Threading.Tasks;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.Utilities;

namespace Walkabout.Reports
{
    /// <summary>
    /// Reports on potential future bills.
    /// </summary>
    public class FutureBillsReport : Report
    {
        private readonly MyMoney myMoney;

        class Payment
        {
            public Payee Payee { get; set; }
            public double Amount { get; set; }
            public TimeSpan Interval { get; set; }
            public DateTime NextDate { get; set; }
            public double MeanDays { get; internal set; }
        }

        public FutureBillsReport(MyMoney money)
        {
            this.myMoney = money;
        }

        public override Task Generate(IReportWriter writer)
        {
            writer.WriteHeading("Future Bills Report");

            Transactions transactions = this.myMoney.Transactions;

            DateTime today = DateTime.Now;
            DateTime start = today.AddYears(-5); // trim data older than 5 years.
            List<Transaction> view = new List<Transaction>();
            foreach (Transaction t in this.myMoney.Transactions.GetAllTransactions())
            {
                if (t.IsDeleted || t.Status == TransactionStatus.Void || t.Date < start || t.Payee == null)
                {
                    continue;
                }

                view.Add(t);
            }

            view.Sort(new TransactionComparerByDateDescending());

            Dictionary<Payee, List<Transaction>> groupedByPayee = new Dictionary<Payee, List<Transaction>>();

            foreach (Transaction t in view)
            {
                var payee = t.Payee;
                if (!groupedByPayee.TryGetValue(payee, out List<Transaction> list)) {
                    list = new List<Transaction>();
                    groupedByPayee[payee] = list;
                }
                list.Add(t);
            }

            DateTime startDate = DateTime.MaxValue;
            SortedDictionary<string, Payment> recurring = new SortedDictionary<string, Payment>();
            // ok, now figure out if the list has a recurring smell to it...
            foreach (Payee p in groupedByPayee.Keys)
            {                
                var list = groupedByPayee[p];
                if (list.Count < 3)
                {
                    continue;
                }

                List<double> daysBetween = new List<double>();
                List<double> amounts = new List<double>();
                Transaction previous = null;
                foreach (var t in list)
                {
                    if (previous != null)
                    {
                        var span = previous.Date - t.Date;
                        daysBetween.Add(span.TotalDays);
                        amounts.Add((double)previous.Amount);
                    }
                    previous = t;
                }

                var meanDays = MathHelpers.Mean(daysBetween);
                var stdDevDays = MathHelpers.StandardDeviation(daysBetween);

                var meanAmount = MathHelpers.Mean(amounts);
                var stdDevAmount = MathHelpers.StandardDeviation(amounts);

                var nextDate = list[0].Date + TimeSpan.FromDays(meanDays);

                var stdErrDays = Math.Abs(stdDevDays / meanDays);
                var stdErrAmount = Math.Abs(stdDevAmount / meanAmount);

                if (nextDate > today && meanAmount < 0)
                {
                    if (stdErrDays < 0.2 && stdErrAmount < 0.3)
                    {
                        //Debug.WriteLine("==========================================================");
                        //Debug.WriteLine(p.Name);
                        //foreach (var t in list)
                        //{
                        //    Debug.WriteLine("{0},{1}", t.Date, t.Amount);
                        //}

                        //Debug.WriteLine("Mean amount {0} stddev {1} stdDevAmount / meanAmount {2}", meanAmount, stdDevAmount, stdErrAmount);
                        //Debug.WriteLine("Mean days {0} stddev {1}, stdDevDays / meanDays {2}", meanDays, stdDevDays, stdErrDays);

                        if (nextDate < startDate)
                        {
                            startDate = nextDate;
                        }

                        var payment = new Payment()
                        {
                            Amount = amounts[0],
                            Payee = p,
                            Interval = TimeSpan.FromDays(meanDays),
                            NextDate = nextDate,
                            MeanDays = meanDays
                        };
                        recurring[p.Name] = payment;
                    }
                }
            }

            if (recurring.Count == 0)
            {
                writer.WriteParagraph("No recuring payments found");
            }
            else
            {
                decimal total = 0;

                startDate = new DateTime(startDate.Year, startDate.Month, 1);
                DateTime endDate = startDate.AddYears(1);

                while (startDate < endDate)
                {
                    writer.WriteHeading(startDate.ToString("Y"));

                    writer.StartTable();

                    writer.StartColumnDefinitions();
                    foreach (double minWidth in new double[] { 100, 300, 120 })
                    {
                        writer.WriteColumnDefinition(minWidth.ToString(), minWidth, double.MaxValue);
                    }
                    writer.EndColumnDefinitions();

                    writer.StartHeaderRow();
                    foreach (string header in new string[] { "Date", "Payee", "Amount", })
                    {
                        writer.StartCell();
                        writer.WriteParagraph(header);
                        writer.EndCell();
                    }
                    writer.EndRow();

                    foreach (var key in recurring.Keys)
                    {
                        var payment = recurring[key];
                        if (payment.NextDate.Year == startDate.Year && payment.NextDate.Month == startDate.Month)
                        {
                            WriteRow(writer, payment.NextDate.ToShortDateString(), payment.Payee.Name, payment.Amount.ToString("C"));
                            total += (decimal)payment.Amount;
                            payment.NextDate += payment.Interval;
                        }
                    }

                    startDate = startDate.AddMonths(1);
                    writer.EndTable();
                }

                writer.WriteParagraph(string.Format("Total over next 12 months is {0:C}", -total));

                this.WriteTrailer(writer, DateTime.Today);
            }
            return Task.CompletedTask;
        }

        private static void WriteRow(IReportWriter writer, string col1, string col2, string col3)
        {
            writer.StartRow();
            writer.StartCell();
            writer.WriteParagraph(col1);
            writer.EndCell();

            writer.StartCell();
            writer.WriteParagraph(col2);
            writer.EndCell();

            writer.StartCell();
            writer.WriteParagraph(col3);
            writer.EndCell();

            writer.EndRow();
        }


        public override void Export(string filename)
        {
            throw new NotImplementedException();
        }
    }


}
