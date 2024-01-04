using System;
using System.Collections.Generic;
using System.Linq;
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

        internal struct PaymentKey
        {
            private Payee payee;
            private Category category;
            private int hashCode;

            public Payee Payee
            {
                get => payee;
                set
                {
                    payee = value;
                    hashCode = 0;
                }
            }
            public Category Category
            {
                get => category;
                set
                {
                    category = value;
                    hashCode = 0;
                }
            }

            public override bool Equals(object obj)
            {
                // return equals if payment and category pair are the same
                if (obj == null) return false;
                if (obj is PaymentKey p)
                {
                    return p.Payee == this.Payee && p.Category == this.Category;
                }
                return false;
            }

            public override int GetHashCode()
            {
                // return equals if payment and category pair are the same
                if (this.hashCode == 0)
                {
                    this.hashCode = this.Payee.Name.GetHashCode() ^ this.Category.GetFullName().GetHashCode();
                }
                return this.hashCode;
            }
        }

        internal class Payments
        {
            internal const double AmountSensitivity = 0.3; // % stderr
            internal const double TimeSensitivity = 0.5; // % stderr on date

            public List<Transaction> Transactions { get; set; }
            public double Amount { get; set; }
            public TimeSpan Interval { get; set; }
            public DateTime NextDate { get; set; }
            public double MeanDays { get; internal set; }

            public bool IsRecurring
            {
                get
                {
                    if (this.Transactions.Count < 3) return false;

                    List<double> daysBetween = new List<double>();
                    List<double> amounts = new List<double>();
                    Transaction previous = null;
                    foreach (var t in this.Transactions)
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
                    if (meanDays < 2)
                    {
                        return false;
                    }
                    var stdDevDays = MathHelpers.StandardDeviation(daysBetween);

                    // Use a linear regression instead of Mean to allow for inflation.
                    var sumAmount = amounts.Sum();
                    if (sumAmount > 0)
                    {
                        // not a bill if the amount is positive!
                        return false;
                    }
                    MathHelpers.LinearRegression(amounts, out double a, out double b);
                    var distance = MathHelpers.DistanceToLine(amounts, a, b);

                    var stdErrDays = Math.Abs(stdDevDays / meanDays);
                    var stdErrAmount = Math.Abs(distance / sumAmount);

                    //if (this.Payee.Name == "State Farm Insurance")
                    //{
                    //    Debug.WriteLine("==========================================================");
                    //    Debug.WriteLine("{0} {1}", this.Payee.Name, this.Category.Name);
                    //    foreach (var t in this.Transactions)
                    //    {
                    //        Debug.WriteLine("{0},{1}", t.Date.Date.ToShortDateString(), t.Amount);
                    //    }

                    //    Debug.WriteLine("Sum amount {0} distance {1} distance / sumAmount {2}", sumAmount, distance, stdErrAmount);
                    //    Debug.WriteLine("Mean days {0} stddev {1}, stdDevDays / meanDays {2}", meanDays, stdDevDays, stdErrDays);
                    //}

                    if (stdErrDays < TimeSensitivity && stdErrAmount < AmountSensitivity)
                    {
                        var today = DateTime.Today;
                        var nextDate = this.Transactions.First().Date + TimeSpan.FromDays(meanDays);
                        var steps = (today - nextDate).TotalDays / meanDays;
                        if (steps > 3)
                        {
                            return false; // too far back in time to be a current bill.
                        }
                        // skip ahead so bill is in the future (allow for some missed payments).
                        while (nextDate < today)
                        {
                            nextDate = nextDate + TimeSpan.FromDays(meanDays);
                        }
                        this.Amount = amounts[0];
                        this.Interval = TimeSpan.FromDays(meanDays);
                        this.NextDate = nextDate;
                        this.MeanDays = meanDays;
                        return true;
                    }
                    return false;
                }
            }

            public Payee Payee { get; internal set; }
            public Category Category { get; internal set; }

            public Payments()
            {
                this.Transactions = new List<Transaction>();
            }
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
                if (t.IsDeleted || t.Status == TransactionStatus.Void || t.Date < start || t.Date > today || t.Payee == null || t.Category == null)
                {
                    continue;
                }

                view.Add(t);
            }

            view.Sort(new TransactionComparerByDateDescending());

            Dictionary<PaymentKey, Payments> groupedByPayeeCategory = new Dictionary<PaymentKey, Payments>();

            PaymentKey temp = new PaymentKey();
            foreach (Transaction t in view)
            {
                temp.Payee = t.Payee;
                temp.Category = t.Category;
                if (!groupedByPayeeCategory.TryGetValue(temp, out Payments payments))
                {
                    payments = new Payments()
                    {
                        Payee = t.Payee,
                        Category = t.Category
                    };
                    groupedByPayeeCategory[temp] = payments;
                }
                payments.Transactions.Add(t);
            }

            DateTime startDate = DateTime.MaxValue;

            SortedDictionary<string, Payments> recurring = new SortedDictionary<string, Payments>();
            // ok, now figure out if the list has a recurring smell to it...
            foreach (var pair in groupedByPayeeCategory)
            {
                var key = pair.Key;
                var payments = pair.Value;
                if (payments.IsRecurring)
                {
                    string sortName = key.Payee.Name + ":" + key.Category.GetFullName();
                    recurring[sortName] = payments;
                }
            }


            if (recurring.Count == 0)
            {
                writer.WriteParagraph("No recuring payments found");
            }
            else
            {
                decimal total = 0;

                startDate = new DateTime(today.Year, today.Month, 1);
                DateTime endDate = startDate.AddYears(1);

                while (startDate < endDate)
                {
                    writer.WriteHeading(startDate.ToString("Y"));

                    writer.StartTable();

                    writer.StartColumnDefinitions();
                    foreach (double minWidth in new double[] { 100, 300, 250, 120 })
                    {
                        writer.WriteColumnDefinition(minWidth.ToString(), minWidth, double.MaxValue);
                    }
                    writer.EndColumnDefinitions();

                    writer.StartHeaderRow();
                    foreach (string header in new string[] { "Date", "Payee", "Category", "Amount", })
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
                            WriteRow(writer, payment.NextDate.ToShortDateString(),
                                payment.Payee.Name,
                                payment.Category.Name,
                                payment.Amount.ToString("C"));
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

        private static void WriteRow(IReportWriter writer, string col1, string col2, string col3, string col4)
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

            writer.StartCell();
            writer.WriteParagraph(col4);
            writer.EndCell();

            writer.EndRow();
        }


        public override void Export(string filename)
        {
            throw new NotImplementedException();
        }
    }


}
