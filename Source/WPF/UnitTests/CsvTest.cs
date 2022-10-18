using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Walkabout.Data;
using Walkabout.Migrate;
using Walkabout.Utilities;

namespace UnitTests
{
    [TestClass]
    public class CsvTest
    {
        [TestMethod]
        public void TestCsvParsing()
        {
            const string csv = @"Trans. Date,Post Date,Description,Amount,Category
09/23/2022,09/23/2022,""HLU*HULU 373313066733-U """"HULU.COM""""/BILLCAHLU*HULU 373313066733-U"",14.35,""Services""
09/22/2022,09/22/2022,""APPLE.COM/BILL 866-712-7753 CAMML0QZNHB7A0"",5.42,""Merchandise""";

            var money = new MyMoney();
            var account = money.Accounts.AddAccount("Discover");
            CsvImporter importer = new CsvImporter(money, account);

            using (var reader = new StringReader(csv))
            {
                string line = reader.ReadLine();
                while (line != null)
                {
                    if (importer.ReadRecord(line))
                    {
                        var fields = importer.GetFields();
                        Assert.AreEqual(5, fields.Count);
                    }
                    line = reader.ReadLine();
                }
            }
        }
    }
}
