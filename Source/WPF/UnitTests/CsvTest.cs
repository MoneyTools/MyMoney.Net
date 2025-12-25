using NUnit.Framework;
using System.Text;
using System.Xml.Linq;
using Walkabout.Data;
using Walkabout.Migrate;
using Walkabout.Utilities;

namespace Walkabout.Tests
{
    public class CsvTest
    {
        [Test]
        public void TestCsvParsing()
        {
            var money = new MyMoney();
            var map = new TestMap();
            var csv = CsvDocument.Read(new StringReader(map.GetCsv()));
            int rows = csv.Rows.Count();
            Assert.AreEqual(2, rows);
            map.AssertEqual(csv);
        }


        [Test]
        public async Task TestHttpXmlCsvParsing()
        {
            string url = "https://download.microsoft.com/download/4/C/8/4C830C0C-101F-4BF2-8FCB-32D9A8BA906A/Import_User_Sample_en.csv";
            XmlCsvReader reader = new XmlCsvReader();
            reader.FirstRowHasColumnNames = true;
            await reader.OpenAsync(new Uri(url), Encoding.UTF8);
            var doc = XDocument.Load(reader);
            Assert.That(doc.Root.Element("row").Element("UserName").ToString(), Is.EqualTo(@"<UserName>chris@contoso.com</UserName>"));
        }

        private class TestMap
        {
            private readonly string[] headers = new string[] { "Trans. Date", "Post Date", "Description", "Amount", "Category" };
            private readonly string[] row1 = new string[] { "09/23/2022", "09/23/2022", "\"HLU*HULU,374413022733-U \"\"HULU.COM\"\"/BILLCAHLU*HULU, 374413022733-U\"", "14.35", "\"Services\"" };
            private readonly string[] row2 = new string[] { "09/22/2022", "09/22/2022", "\"APPLE.COM/BILL 866-712-7753 CAZZL0QAZNFC6A0\"", "5.42", "Merchandise" };
            private int row = 0;

            public int FieldCount => this.headers.Length;

            public string GetCsv()
            {
                return string.Join(",", this.headers) + Environment.NewLine +
                    string.Join(",", this.row1) + Environment.NewLine +
                    string.Join(",", this.row2) + Environment.NewLine;
            }

            public void AssertEqual(CsvDocument doc)
            {
                this.AssertEqual(doc.Headers, headers);
                foreach (var row in doc.Rows)
                {
                    this.AssertRow(row);
                }
            }

            private void AssertRow(IEnumerable<string> values)
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

            private void AssertEqual(IList<string> expected, IEnumerable<string> actual)
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
