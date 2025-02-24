using System;
using System.Collections;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Walkabout.Utilities;

namespace Walkabout.Data
{
    public class CsvStore : IDatabase
    {
        private readonly string fileName;
        private readonly IEnumerable rows;

        public CsvStore(string fileName, IEnumerable rows)
        {
            this.fileName = fileName;
            this.rows = rows;
        }

        public virtual bool SupportsUserLogin => false;

        public virtual string Server { get; set; }

        public virtual string DatabasePath { get { return this.fileName; } }

        public virtual string ConnectionString { get { return null; } }

        public virtual string BackupPath { get { return null; } } // todo

        public virtual DbFlavor DbFlavor { get { return Data.DbFlavor.Xml; } } // BugBug:

        public virtual string UserId { get; set; }

        public virtual string Password { get; set; }

        public virtual bool Exists
        {
            get
            {
                return File.Exists(this.fileName);
            }
        }

        public virtual void Create()
        {
        }

        public virtual void Disconnect()
        {

        }
        public virtual void Delete()
        {
            if (this.Exists)
            {
                File.Delete(this.fileName);
            }
        }

        public bool UpgradeRequired
        {
            get
            {
                return false;
            }
        }

        public void Upgrade()
        {
        }

        public MyMoney Load(IStatusService status)
        {
            throw new NotImplementedException();
        }

        public void Save(MyMoney money)
        {
            StreamWriter writer = null;
            try
            {
                writer = new StreamWriter(this.fileName);
                WriteTransactionHeader(writer, OptionalColumnFlags.None);

                if (this.rows != null)
                {
                    foreach (Transaction t in this.rows)
                    {
                        if (t != null)
                        {
                            WriteTransaction(writer, t, OptionalColumnFlags.None);
                        }
                    }
                }
            }
            finally
            {
                if (writer != null)
                {
                    writer.Close();
                    writer = null;
                }
            }
        }

        [Flags]
        public enum OptionalColumnFlags: int
        {
            None = 0,
            InvestmentInfo = 1,
            SalesTax = 2,
            Currency = 4,
        }

        public static void WriteTransactionHeader(StreamWriter writer, OptionalColumnFlags optionalColumns)
        {
            writer.Write("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\"",
                "Account", "Date", "Payee", "Category", "Amount"); 
            if (optionalColumns.HasFlag(OptionalColumnFlags.InvestmentInfo))
            {
                writer.Write(",\"{0}\",\"{1}\",\"{2}\",\"{3}\"",
                    "Activity", "Symbol", "Units", "UnitPrice");
            }
            if (optionalColumns.HasFlag(OptionalColumnFlags.SalesTax))
            {
                writer.Write(",\"{0}\"", "SalesTax");
            }
            if (optionalColumns.HasFlag(OptionalColumnFlags.Currency))
            {
                writer.Write(",\"{0}\"", "Currency");
            }
            writer.WriteLine(",\"{0}\"", "Memo");
        }


        public static void WriteTransaction(StreamWriter writer, Transaction t, OptionalColumnFlags optionalColumns)
        {
            string payee = t.PayeeName;
            if (t.Transfer != null)
            {
                payee = Walkabout.Data.Transaction.GetTransferCaption(t.Transfer.Transaction.Account, t.Amount > 0);
            }
            // we put every column inside double quotes because some locale's use comma as a decimal separator.
            writer.Write("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\"",
                CsvSafeString(t.AccountName),
                CsvSafeString(t.Date.ToShortDateString()),
                CsvSafeString(payee),
                CsvSafeString(t.CategoryName),
                CsvSafeString(t.Amount.ToString("C2"))); 

            if (optionalColumns.HasFlag(OptionalColumnFlags.InvestmentInfo))
            {
                string tradeType = "";
                if (t.InvestmentType != InvestmentType.None)
                {
                    tradeType = t.InvestmentType.ToString();
                }
                writer.Write(",\"{0}\",\"{1}\",\"{2}\",\"{3}\"",
                    tradeType,
                    CsvSafeString(t.InvestmentSecuritySymbol),
                    CsvSafeString(t.InvestmentUnits.ToString()),
                    CsvSafeString(t.InvestmentUnitPrice.ToString("C2")));
            }
            if (optionalColumns.HasFlag(OptionalColumnFlags.SalesTax))
            {
                writer.Write(",\"{0}\"", CsvSafeString(t.SalesTax.ToString("C2")));
            }
            if (optionalColumns.HasFlag(OptionalColumnFlags.Currency))
            {
                writer.Write(",\"{0}\"", CsvSafeString(t.GetAccountCurrency().Symbol));
            }
            writer.WriteLine(",\"{0}\"", CsvSafeString(t.Memo));
        }

        public static void WriteInvestmentHeader(StreamWriter writer)
        {
            writer.WriteLine("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\"",
                "Account", "Date", "Payee", "Category", "Activity", "Symbol", "Units", "UnitPrice", "Amount", "Memo");
        }
        public static void WriteInvestment(StreamWriter writer, Investment i)
        {
            Transaction t = i.Transaction;
            string payee = t.PayeeName;
            if (t.Transfer != null)
            {
                payee = Walkabout.Data.Transaction.GetTransferCaption(t.Transfer.Transaction.Account, t.Amount > 0);
            }
            string tradeType = "";
            if (t.InvestmentType != InvestmentType.None)
            {
                tradeType = t.InvestmentType.ToString();
            }

            writer.WriteLine("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\"",
                CsvSafeString(t.AccountName),
                CsvSafeString(t.Date.ToShortDateString()),
                CsvSafeString(payee),
                CsvSafeString(t.CategoryName),
                tradeType,
                CsvSafeString(t.InvestmentSecuritySymbol),
                CsvSafeString(t.InvestmentUnits.ToString()),
                CsvSafeString(t.InvestmentUnitPrice.ToString("C2")),
                CsvSafeString(t.Amount.ToString("C2")),
                CsvSafeString(t.Memo));
        }

        public static void WriteLoanPaymentHeader(StreamWriter writer)
        {
            writer.WriteLine("Date,Account,Payment,Percentage,Principal,Interest,Balance");
        }


        internal static void WriteLoanPayment(StreamWriter writer, LoanPaymentAggregation l)
        {
            writer.WriteLine("\"{0}\",\"{1}\",\"{2}\",\"{3}%\",\"{4}\",\"{5}\",\"{6}\"",
                CsvSafeString(l.Date.ToShortDateString()),
                CsvSafeString(l.AccountName),
                CsvSafeString(l.Payment.ToString("C2")),
                CsvSafeString(l.Percentage.ToString("N3")),
                CsvSafeString(l.Principal.ToString("C2")),
                CsvSafeString(l.Interest.ToString("C2")),
                CsvSafeString(l.Balance.ToString("C2"))
                );
        }

        public virtual void Backup(string path)
        {
            MessageBoxEx.Show("XML Backup is not implemented");
        }

        public static string CsvSafeString(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }
            // everything is going inside double quotes, so we have to protect any existing double quotes
            // which is done by replacing them with 2 quotes like this "".
            s = s.Replace("\"", "\"\"");
            return s;
        }

        public virtual string GetLog()
        {
            return "";
        }

        public virtual DataSet QueryDataSet(string cmd)
        {
            return new DataSet();
        }


        /// <summary>
        /// Import the Transactions & accounts in the given file and return the first account.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="count"></param>
        /// <returns></returns>

        public static async Task<int> ImportCsv(MyMoney myMoney, Account acct, string file)
        {
            var uri = new Uri(file);
            var csvReader = new CsvReader(4096);
            await csvReader.OpenAsync(uri, Encoding.UTF8, null);
            csvReader.Delimiter = ',';
            int total = 0;
            while (csvReader.Read())
            {
                if (csvReader.FieldCount != 3)
                {
                    throw new NotSupportedException("Invalid CSV format expecting 3 columns [Date, Payee, Amount]");
                }

                var field1Date = csvReader[0];
                var field2Payee = csvReader[1];
                var field3Amount = csvReader[2];
                if (total == 0)
                {
                    if (field1Date != "Date" || field2Payee != "Payee" || field3Amount != "Amount")
                    {
                        throw new NotSupportedException("Invalid CSV format The fist row is expected to be the header [Date, Payee, Amount]");
                    }
                }
                else
                {
                    var dateTokens = field1Date.Split('-');

                    if (dateTokens.Length != 3)
                    {
                        throw new NotSupportedException("Invalid CSV format The Date needs to be specified in ISO8601 YYYY-MM-DD format : " + field1Date);
                    }

                    if (dateTokens[0].Length != 4)
                    {
                        throw new NotSupportedException("Invalid CSV format The Date Year must be 4 digits : " + field1Date);
                    }

                    Transaction t = myMoney.Transactions.NewTransaction(acct);

                    t.Id = -1;
                    t.Date = DateTime.Parse(field1Date);
                    t.Payee = t.Payee = myMoney.Payees.FindPayee(field2Payee, true);
                    t.Amount = decimal.Parse(field3Amount);

                    myMoney.Transactions.Add(t);
                }
                total++;
            }
            csvReader.Close();

            return total;
        }
    }
}
