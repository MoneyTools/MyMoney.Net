using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Walkabout.Data;
using Walkabout.Migrate;

namespace UnitTests
{
    [TestClass]
    public class CsvTest
    {
        [TestMethod]
        public void TestCsvParsing()
        {
            var money = new MyMoney();
            var map = new TestMap();
            CsvImporter importer = new CsvImporter(money, map);
            int rows = importer.ImportStream(new StringReader(map.GetCsv()));
            Assert.AreEqual(rows, 3);
        }

        private class TestMap : CsvFieldWriter
        {
            private string[] headers = new string[] { "Trans. Date", "Post Date", "Description", "Amount", "Category" };
            private string[] row1 = new string[] { "09/23/2022", "09/23/2022", "\"HLU*HULU,374413022733-U \"\"HULU.COM\"\"/BILLCAHLU*HULU, 374413022733-U\"", "14.35", "\"Services\"" };
            private string[] row2 = new string[] { "09/22/2022", "09/22/2022", "\"APPLE.COM/BILL 866-712-7753 CAZZL0QAZNFC6A0\"", "5.42", "Merchandise" };
            private int row = 0;

            public TestMap()
            {
            }

            public int FieldCount => this.headers.Length;

            public string GetCsv()
            {
                return string.Join(",", this.headers) + Environment.NewLine +
                    string.Join(",", this.row1) + Environment.NewLine +
                    string.Join(",", this.row2) + Environment.NewLine;
            }

            public override void WriteHeaders(IEnumerable<string> headers)
            {
                this.AssertEqual(this.headers, headers);
            }

            public override void WriteRow(IEnumerable<string> values)
            {
                if (this.row == 0)
                {
                    this.AssertEqual(this.row1, values);
                }
                else if (this.row == 1)
                {
                    this.AssertEqual(this.row2, values);
                }
                else
                {
                    Assert.Fail("Too many rows returned");
                }
                this.row++;
            }

            private void AssertEqual(string[] expected, IEnumerable<string> actual)
            {
                int len = expected.Count();
                Assert.AreEqual(actual.Count(), len, "Collection sizes don't match");
                int i = 0;
                foreach (var v in actual)
                {
                    var q = this.CsvQuote(v);
                    var s = expected[i++];
                    if (s != v && s.Trim('"') == v)
                    {
                        // these were redundant quotes in the input.
                        s = s.Trim('"');
                    }
                    Assert.AreEqual(q, s, $"Values do not match: {s} != {q}");
                }
            }

            private string CsvQuote(string value)
            {
                if (value.Contains(","))
                {
                    return "\"" + value.Replace("\"", "\"\"") + "\"";
                }
                return value;
            }
        }
    }
}
