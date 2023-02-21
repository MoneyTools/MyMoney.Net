using System;
using System.Threading.Tasks;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;

namespace Walkabout.Reports
{
    /// <summary>
    /// Reports on all unaccepted transactions.
    /// </summary>
    public class UnacceptedReport : Report
    {
        private readonly MyMoney myMoney;

        public UnacceptedReport(MyMoney money)
        {
            this.myMoney = money;
        }

        public override Task Generate(IReportWriter writer)
        {
            writer.WriteHeading("Unaccepted Transactions");

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
                        if (first)
                        {
                            writer.EndTable();
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
                            writer.EndRow();

                            first = false;
                        }
                        WriteRow(writer, t.Date.ToShortDateString(), t.PayeeName ?? string.Empty, t.Amount.ToString("C"));
                        WriteRow(writer, string.Empty, t.CategoryName ?? string.Empty, string.Empty);
                        WriteRow(writer, string.Empty, t.Memo ?? string.Empty, string.Empty);
                    }
                }

            }

            writer.EndTable();

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
