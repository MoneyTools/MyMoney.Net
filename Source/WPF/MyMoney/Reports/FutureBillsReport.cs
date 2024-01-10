using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Media3D;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.Utilities;
using static System.Net.Mime.MediaTypeNames;

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

        private class PaymentTable<T>
        {
            List<List<T>> data = new List<List<T>>();
            int maxColumns = 0;

            public PaymentTable() { }

            public void AddRow(IEnumerable<T> values)
            {
                List<T> snapshot = new List<T>(values);
                if (snapshot.Count > maxColumns)
                {
                    maxColumns = snapshot.Count; 
                }
                data.Add(snapshot);
            }

            public int Columns => maxColumns;

            public IEnumerable<T> GetColumn(int index)
            {
                foreach (var row in data)
                {
                    if (row.Count > index)
                    {
                        yield return row[index];
                    }
                }
            }
        }

        internal class Payments
        {
            internal const double AmountSensitivity = 0.1; // % stderr
            internal const double TimeSensitivity = 0.5; // % stderr on date

            private List<Transaction> transactions;

            public Payee Payee { get; internal set; }
            public Category Category { get; internal set; }

            public List<Transaction> Transactions { get; set; }
            public double Amount { get; set; }
            public TimeSpan Interval { get; set; }
            public DateTime NextDate { get; set; }
            public double MeanDays { get; internal set; }
            private List<double> Predictions { get; set; }
            private int NextIndex { get; set; }
            private bool Monthly { get; set; }

            private List<int> years;

            private List<int> GetOrCreateYears()
            {
                if (this.years == null)
                {
                    this.years = new List<int>();
                    foreach (var t in this.Transactions)
                    {
                        int year = t.Date.Year;
                        if (!this.years.Contains(year))
                        {
                            this.years.Add(year);
                        }
                    }
                }
                return this.years;
            }

            private double GetPredictedAmount()
            {
                int index = this.NextIndex;
                if (this.Predictions != null && this.Predictions.Count > index)
                {
                    this.NextIndex++;
                    if (this.NextIndex >= this.Predictions.Count)
                    {
                        this.NextIndex = 0; // wrap around.
                    }
                    return this.Predictions[index];
                }

                return this.Amount;
            }

            public double GetNextPrediction()
            {
                // Return the next prediction and advance the NextDate according to the 
                // calculated payment Interval.
                if (this.Monthly)
                {
                    this.NextDate = this.NextDate.AddMonths(1);
                }
                else
                {
                    this.NextDate += this.Interval;
                }
                return this.GetPredictedAmount();
            }

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


                    var meanDays = Math.Floor(MathHelpers.Mean(daysBetween));
                    if (meanDays < 3)
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

                    this.Monthly = meanDays > 25 && meanDays < 35;

                    if (stdErrDays < TimeSensitivity && meanDays < 180 && stdErrAmount > AmountSensitivity)
                    {
                        var predictions = new List<double>();
                        // see if the amount depends on seasonal fluxuations (like energy or water bill).
                        var table = new PaymentTable<double>();

                        // So create a table organized by rows and columns where each row is a different year
                        // and each column is a payment within that yearly cycle.                        
                        foreach (var year in this.GetOrCreateYears())
                        {
                            var cyclicAmounts = from t in this.Transactions where t.Date.Year == year select (double)t.Amount;
                            table.AddRow(cyclicAmounts);
                        }

                        if (table.Columns > 2)
                        {
                            double stdErrCyclicalAmount = 0;
                            for (int i = 0; i <= table.Columns; i++)
                            {
                                var cyclicAmounts = table.GetColumn(i);
                                if (cyclicAmounts.Count() > 2)
                                {
                                    sumAmount = cyclicAmounts.Sum();
                                    MathHelpers.LinearRegression(cyclicAmounts, out double ma, out double mb);
                                    var monthlyDistance = MathHelpers.DistanceToLine(cyclicAmounts, ma, mb);
                                    var stderr = Math.Abs(monthlyDistance / sumAmount);
                                    stdErrCyclicalAmount += stderr;
                                    predictions.Add(MathHelpers.Mean(cyclicAmounts));
                                }
                            }
                            stdErrCyclicalAmount /= table.Columns;
                            if (stdErrCyclicalAmount < stdErrAmount && predictions.Count > 0)
                            {
                                stdErrAmount = stdErrCyclicalAmount;
                                this.Predictions = predictions;
                                if (predictions.Count == 12)
                                {
                                    this.Monthly = true;
                                }
                            }
                        }
                    }

                    if (this.Category.Type == CategoryType.RecurringExpense ||  // user provided input that this is a recurring bill payment!
                        (stdErrDays < TimeSensitivity && stdErrAmount < AmountSensitivity))
                    {
                        var today = DateTime.Today;
                        var nextDate = this.Transactions.First().Date + TimeSpan.FromDays(meanDays);
                        var steps = (today - this.Transactions.First().Date).TotalDays / meanDays;
                        if (steps > 3)
                        {
                            return false; // too far back in time to be a current bill.
                        }
                        // skip ahead so bill is in the future (allow for some missed payments).
                        while (nextDate < today)
                        {
                            nextDate = nextDate + TimeSpan.FromDays(meanDays);
                        }
                        if ((nextDate - today).TotalDays > 365)
                        {
                            return false;
                        }
                        Debug.WriteLine($"Found recurring payment to {this.Payee.Name} : {this.Category.Name} with stdErrDays {stdErrDays} and stdErrAmount {stdErrAmount}");
                        this.NextIndex = 0;
                        this.Amount = amounts[0];
                        this.Interval = TimeSpan.FromDays(meanDays);
                        this.NextDate = nextDate;
                        this.MeanDays = meanDays;
                        return true;
                    }
                    return false;
                }
            }

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

            Paragraph summary = null;

            if (writer is FlowDocumentReportWriter flow)
            {
                writer.WriteParagraph("");
                summary = flow.CurrentParagraph;
            }

            Transactions transactions = this.myMoney.Transactions;

            DateTime today = DateTime.Now;
            DateTime start = today.AddYears(-5); // trim data older than 5 years.
            List<Transaction> view = new List<Transaction>();
            foreach (Transaction t in this.myMoney.Transactions.GetAllTransactions())
            {
                if (t.IsDeleted || t.Status == TransactionStatus.Void || t.Date < start || t.Date > today || t.Payee == null || t.Category == null
                    || (t.Category.Type != CategoryType.Expense && t.Category.Type != CategoryType.RecurringExpense) )
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
                Run run = (Run)summary.Inlines.FirstInline;
                run.Text = "No recuring payments found";
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
                        var date = payment.NextDate;
                        
                        while (date.Year < startDate.Year ||
                            (date.Year == startDate.Year && date.Month <= startDate.Month))
                        {
                            var amount = payment.GetNextPrediction();
                            WriteRow(writer, date.ToShortDateString(),
                                payment.Payee.Name,
                                payment.Category.Name,
                                amount.ToString("C"));
                            total += (decimal)amount;
                            date = payment.NextDate;
                        }
                    }

                    startDate = startDate.AddMonths(1);
                    writer.EndTable();
                }

                // Add summary.
                Run run = (Run)summary.Inlines.FirstInline;
                run.Text = string.Format("Total over next 12 months is {0:C}", -total);

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
