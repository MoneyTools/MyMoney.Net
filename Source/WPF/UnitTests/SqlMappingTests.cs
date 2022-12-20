using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Walkabout.Data;

namespace Walkabout.Tests
{
    /// <summary>
    /// Summary description for DataTests
    /// </summary>
    [TestClass]
    public class SqlMappingTests
    {
        public SqlMappingTests()
        {
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return this.testContextInstance;
            }
            set
            {
                this.testContextInstance = value;
            }
        }

        [TestMethod]
        public void TestSqlMapping()
        {
            MyMoney m = new MyMoney();
            Account a = new Account();
            a.Name = "Bank of America";
            m.Accounts.AddAccount(a);
            Transaction t = new Transaction();
            t.Account = a;
            t.Date = DateTime.Now;
            t.Amount = -65.00M;
            t.Payee = m.Payees.FindPayee("Costco", true);
            t.Category = m.Categories.FindCategory("Food");
            t.Memo = "something";
            m.Transactions.AddTransaction(t);

            string dbPath = System.IO.Path.GetTempPath();
            string dbName = System.IO.Path.Combine(dbPath, "Test.MyMoney.db");
            if (System.IO.File.Exists(dbName))
            {
                System.IO.File.Delete(dbName);
            }

            SqliteDatabase db = new SqliteDatabase();
            db.DatabasePath = dbName;
            db.Create();
            db.Save(m);

            // test we can add a column to the Transactions table.
            TableMapping mapping = new TableMapping() { TableName = "Transactions" };
            mapping.ObjectType = typeof(Transaction);
            var c = new ColumnMapping()
            {
                ColumnName = "Foo",
                SqlType = typeof(SqlChars),
                AllowNulls = true,
                MaxLength = 20
            };
            mapping.Columns.Add(c);
            db.CreateOrUpdateTable(mapping);

            // make sure the new column exists
            var metadata = db.LoadTableMetadata(mapping.TableName);
            var d = (from i in metadata.Columns where i.ColumnName == "Foo" select i).FirstOrDefault();
            Assert.IsNotNull(d);
            Assert.AreEqual(d.MaxLength, 20);

            // test we can change the max length
            c.MaxLength = 50;
            db.CreateOrUpdateTable(mapping);

            // make sure the new column has new length
            metadata = db.LoadTableMetadata(mapping.TableName);
            d = (from i in metadata.Columns where i.ColumnName == "Foo" select i).FirstOrDefault();
            Assert.IsNotNull(d);
            Assert.AreEqual(d.MaxLength, 50);

            // test we can drop the column
            mapping.Columns.Remove(c);
            db.CreateOrUpdateTable(mapping);

            // verify it's gone!
            metadata = db.LoadTableMetadata(mapping.TableName);
            d = (from i in metadata.Columns where i.ColumnName == "Foo" select i).FirstOrDefault();
            Assert.IsNull(d);

            // test we can still load the modified database!
            MyMoney test = db.Load(null);

            Account b = test.Accounts.FindAccount(a.Name);
            Assert.IsNotNull(b);

            Transaction s = test.Transactions.GetTransactionsFrom(b).FirstOrDefault();
            Assert.IsNotNull(s);

            Assert.AreEqual(t.Date, s.Date);
            Assert.AreEqual(t.Amount, s.Amount);
            Assert.AreEqual(t.CategoryFullName, s.CategoryFullName);
            Assert.AreEqual(t.PayeeName, s.PayeeName);
            Assert.AreEqual(t.Memo, s.Memo);

        }
    }
}
