using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;
using Walkabout.Utilities;
using System.Collections.Generic;
using System.Data;

namespace Walkabout.Data
{
    public class CsvStore : IDatabase
    {
        string fileName;
        IEnumerable rows;

        public CsvStore(string fileName, IEnumerable rows)
        {
            this.fileName = fileName;
            this.rows = rows;
        }

        public virtual string Server { get; set; }

        public virtual string DatabasePath { get { return fileName; } }

        public virtual string ConnectionString { get { return null; } }

        public virtual string BackupPath { get { return null; } } // todo

        public virtual DbFlavor DbFlavor { get { return Data.DbFlavor.Xml; } } // bugbug:

        public virtual string UserId { get; set; }

        public virtual string Password { get; set; }

        public virtual bool Exists {
            get
            {
                return File.Exists(this.fileName);
            }
        }

        public virtual void Create()
        {
        }

        public virtual void Delete()
        {
            if (Exists)
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

                if (rows != null)
                {
                    foreach (Transaction t in rows)
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
            writer.WriteLine("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\"", "Account", "Date", "Payee", "Category", "Amount", "Balance");
        }

        public static void WriteTransaction(StreamWriter writer, Transaction t)
        {
            string category = t.CategoryName;
            if (t.Transfer != null)
            {
                category = Walkabout.Data.Transaction.GetTransferCaption(t.Transfer.Transaction.Account, t.Amount > 0);
            }
            writer.WriteLine("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\"", t.AccountName, t.Date.ToShortDateString(), t.PayeeName, category, t.Amount.ToString("C2"), t.Balance.ToString("C2"));
        }

        public static void WriteInvestmentHeader(StreamWriter writer)
        {
            writer.WriteLine("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\"", "Date", "Security", "Symbol", "Units", "UnitPrice", "CostBasis", "MarketValue");
        }

        public static void WriteInvestment(StreamWriter writer, Investment i)
        {
            if (i.Security != null)
            {
                writer.WriteLine("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\"", i.Date.ToShortDateString(), i.Security.Name, i.Security.Symbol, i.CurrentUnits, i.CurrentUnitPrice, i.CostBasis, i.MarketValue);
            }
        }

        public virtual void Backup(string path)
        {
            System.Windows.MessageBox.Show("XML Backup is not implemented");
        }


        public virtual string GetLog()
        {
            return "";
        }

        public virtual DataSet QueryDataSet(string cmd)
        {
            return new DataSet();
        }
    }
}
