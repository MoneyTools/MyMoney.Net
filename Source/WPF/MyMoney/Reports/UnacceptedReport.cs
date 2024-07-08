using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;

namespace Walkabout.Reports
{
    /// <summary>
    /// Reports on all unaccepted transactions.
    /// </summary>
    public class UnacceptedReport : Report
    {
        private MyMoney myMoney;

        public UnacceptedReport()
        {
        }

        ~UnacceptedReport()
        {
            Debug.WriteLine("UnacceptedReport disposed!");
        }

        public override void OnSiteChanged()
        {
            this.myMoney = (MyMoney)this.ServiceProvider.GetService(typeof(MyMoney));
        }

        public override IReportState GetState()
        {
            return new SimpleReportState(typeof(UnacceptedReport));
        }

        public override void ApplyState(IReportState state)
        {
        }

        public override Task Generate(IReportWriter writer)
        {
            writer.WriteHeading("Unaccepted Transactions");

            int count = 0;
            Transactions transactions = this.myMoney.Transactions;
            foreach (Account a in this.myMoney.Accounts.GetAccounts(true))
            {
                if (a.IsClosed)
                {
                    continue;
                }

                bool first = true;

                foreach (Transaction t in this.myMoney.Transactions.GetTransactionsFrom(a))
                {
                    if (t.Unaccepted)
                    {
                        count++;
                        if (first)
                        {
                            writer.WriteHeading(a.Name);
                            writer.StartTable();

                            writer.StartColumnDefinitions();
                            foreach (double minWidth in new double[] { 100, 300, 120 })
                            {
                                writer.WriteColumnDefinition(minWidth.ToString(), minWidth, double.MaxValue);
                            }
                            writer.EndColumnDefinitions();

                            writer.StartHeaderRow();
                            foreach (string header in new string[] { "Date", "Payee/Category/Memo", "Amount", })
                            {
                                writer.StartCell();
                                writer.WriteParagraph(header);
                                writer.EndCell();
                            }
                            writer.EndHeaderRow();

                            first = false;
                        }
                        WriteRow(writer, t.Date.ToShortDateString(), t.PayeeName ?? string.Empty, t.Amount.ToString("C"));
                        // WriteRow(writer, string.Empty, t.CategoryName ?? string.Empty, string.Empty);
                        if (!string.IsNullOrEmpty(t.Memo))
                        {
                            WriteRow(writer, string.Empty, t.Memo ?? string.Empty, string.Empty);
                        }
                    }
                }

                if (!first)
                {
                    writer.EndTable();
                }
            }


            writer.WriteParagraph($"Found {count} unaccepted transactions", FontStyles.Italic, FontWeights.Normal, Brushes.Gray);

            this.WriteTrailer(writer, DateTime.Today);

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
