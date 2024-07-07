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

        public virtual DbFlavor DbFlavor { get { return Data.DbFlavor.Xml; } } // bugbug:

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
                WriteTransactionHeader(writer);

                if (this.rows != null)
                {
                    foreach (Transaction t in this.rows)
                    {
                        if (t != null)
                        {
                            WriteTransaction(writer, t);
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

        public static void WriteTransactionHeader(StreamWriter writer)
        {
            writer.WriteLine("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\"", "Account", "Date", "Payee", "Amount", "Category", "Memo");
        }

        public static void WriteTransaction(StreamWriter writer, Transaction t)
        {
            string category = t.CategoryName;
            if (t.Transfer != null)
            {
                category = Walkabout.Data.Transaction.GetTransferCaption(t.Transfer.Transaction.Account, t.Amount > 0);
            }
            writer.WriteLine("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\"", 
                t.AccountName, t.Date.ToShortDateString(), t.PayeeName, t.Amount.ToString("C2"), category, GetMemoCsv(t));
        }

        public static void WriteInvestmentHeader(StreamWriter writer)
        {
            writer.WriteLine("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\"", 
                "Date", "Payee", "Category", "Activity", "Symbol", "Units", "UnitPrice", "Amount", "Memo");
        }

        public static void WriteInvestment(StreamWriter writer, Transaction t)
        {
            string category = t.CategoryName;
            if (t.Transfer != null)
            {
                category = Walkabout.Data.Transaction.GetTransferCaption(t.Transfer.Transaction.Account, t.Amount > 0);
            }
            string tradeType = "";
            if (t.InvestmentType != InvestmentType.None)
            {
                tradeType = t.InvestmentType.ToString();
            }

            writer.WriteLine("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\"",
                t.Date.ToShortDateString(), t.PayeeName, category,
                tradeType, t.InvestmentSecuritySymbol, t.InvestmentUnits, t.InvestmentUnitPrice, t.Amount.ToString("C2"), GetMemoCsv(t));
        }

        private static string GetMemoCsv(Transaction t)
        {
            return (t.Memo + "").Replace(",", " ");
        }

        public static void WriteInvestmentTransaction(StreamWriter writer, Transaction t)
        {
            // writes a non-investment transaction in the investment header format.
            string category = t.CategoryName;
            if (t.Transfer != null)
            {
                category = Walkabout.Data.Transaction.GetTransferCaption(t.Transfer.Transaction.Account, t.Amount > 0);
            }
            writer.WriteLine("\"{0}\",\"{1}\",\"{2}\",,,,,\"{3}\"",
                t.Date.ToShortDateString(), t.PayeeName, category, t.Amount.ToString("C2"));
        }

        public virtual void Backup(string path)
        {
            MessageBoxEx.Show("XML Backup is not implemented");
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
