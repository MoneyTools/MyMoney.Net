using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.Utilities;
using Walkabout.Views;
using Walkabout.Views.Controls;

namespace Walkabout.Reports
{
    /// <summary>
    /// Reports on potential future bills.
    /// </summary>
    public class FutureBillsReport : Report
    {
        private MyMoney myMoney;

        public FutureBillsReport()
        {
        }

        ~FutureBillsReport()
        {
            Debug.WriteLine("FutureBillsReport disposed!");
        }

        public override void OnSiteChanged()
        {
            this.myMoney = (MyMoney)this.ServiceProvider.GetService(typeof(MyMoney));

            // this also makes the panel visible!
            var panel = (ReportsControl)this.ServiceProvider.GetService(typeof(ReportsControl));
            panel.HideNormalizeCurrencyRow();
            panel.HideReportDateRow();
        }

        public override IReportState GetState()
        {
            return new FutureBillReportState();
        }

        public override void ApplyState(IReportState state)
        {
        }

        public class FutureBillReportState : IReportState
        {
            public FutureBillReportState() { }

            public Type GetReportType()
            {
                return typeof(FutureBillsReport);
            }
        }

        public override Task Generate(IReportWriter writer)
        {
            this.DelaySaveState();
            writer.WriteHeading("Future Bills Report");

            Transactions transactions = this.myMoney.Transactions;

            DateTime today = DateTime.Now;
            DateTime start = today.AddYears(-5); // trim data older than 5 years.
            List<Transaction> view = new List<Transaction>();
            foreach (Transaction t in this.myMoney.Transactions.GetAllTransactions())
            {
                if (t.IsDeleted || t.Status == TransactionStatus.Void || t.Account == null ||
                    t.Date < start || t.Date > today || t.Payee == null || t.Category == null
                    || (t.Category.Type != CategoryType.Expense))
                {
                    continue;
                }

                view.Add(t);
            }

            view.Sort(new TransactionComparerByDateDescending());


            // Run with no writer to get the total for the summary
            decimal total = this.WriteContents(new NullReportWriter(), today, view);

            if (total == 0)
            {
                writer.WriteParagraph("No recuring payments found");
            }
            else
            {
                // Ok, now we can write our summary!
                writer.WriteParagraph(string.Format("Total over next 12 months is {0:C}", -total));
                this.WriteContents(writer, today, view);
            }

            this.WriteTrailer(writer, DateTime.Today);
            return Task.CompletedTask;
        }

        private decimal WriteContents(IReportWriter writer, DateTime today, List<Transaction> view)
        {
            decimal total = 0;
            DateTime startDate = new DateTime(today.Year, today.Month, 1);
            DateTime endDate = startDate.AddYears(1);

            var recurring = Payments.FindRecurringPayments(view);

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
                writer.EndHeaderRow();

                foreach (var key in recurring.Keys)
                {
                    var payment = recurring[key];
                    var date = payment.NextDate;

                    while (date.Year < startDate.Year ||
                        (date.Year == startDate.Year && date.Month <= startDate.Month))
                    {
                        var amount = payment.GetNextPrediction();
                        this.WriteRow(writer, date,
                            payment.Payee,
                            payment.Category,
                            amount);
                        total += (decimal)amount;
                        date = payment.NextDate;
                    }
                }

                startDate = startDate.AddMonths(1);
                writer.EndTable();
            }
            return total;
        }

        private void WriteRow(IReportWriter writer, DateTime date, Payee payee, Category category, double amount)
        {
            writer.StartRow();
            writer.StartCell();
            writer.WriteParagraph(date.ToShortDateString());
            writer.EndCell();

            writer.StartCell();
            writer.WriteHyperlink(payee.Name, FontStyles.Normal, FontWeights.Normal, (s, e) => this.OnSelectPayee(payee));
            writer.EndCell();

            writer.StartCell();
            writer.WriteHyperlink(category.Name, FontStyles.Normal, FontWeights.Normal, (s, e) => this.OnSelectCategory(category));
            writer.EndCell();

            writer.StartCell();
            writer.WriteParagraph(amount.ToString("C"));
            writer.EndCell();

            writer.EndRow();
        }

        public event EventHandler<Category> CategorySelected;

        private void OnSelectCategory(Category category)
        {
            if (CategorySelected != null)
            {
                CategorySelected(this, category);
            }
        }

        public event EventHandler<Payee> PayeeSelected;

        private void OnSelectPayee(Payee payee)
        {
            if (PayeeSelected != null)
            {
                PayeeSelected(this, payee);
            }
        }

        public override void Export(string filename)
        {
            throw new NotImplementedException();
        }

    }


}
