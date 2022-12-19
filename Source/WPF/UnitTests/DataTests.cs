using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Walkabout.Data;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.Serialization;
using System.IO;
using System.Diagnostics;

namespace Walkabout.Tests
{
    /// <summary>
    /// Summary description for DataTests
    /// </summary>
    [TestClass]
    public class DataTests
    {
        public DataTests()
        {
            //
            // TODO: Add constructor logic here
            //
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
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void TestBinaryXml()
        {
            string filename = Path.GetTempFileName();

            TestData data = new TestData();
            data.Points.Add(new TestPoint() { X = 10.5, Y = 3.3 });
            data.Points.Add(new TestPoint() { X = 12.5, Y = 7.7 });
            data.Payees.Add(new Payee() { Id = 1, Name = "Costco" });
            data.Accounts.Add(new Account() { Id = 1, Name = "BECU" });
            data.Accounts.Add(new Account() { Id = 2, Name = "Discover", WebSite = "http://www.discover.com" });

            DataContractSerializer s = new DataContractSerializer(typeof(TestData));

            StringWriter sw = new StringWriter();
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = false;
            settings.OmitXmlDeclaration = true;
            using (XmlWriter w = XmlWriter.Create(sw, settings))
            {
                s.WriteObject(w, data);
            }

            // Compare readers textually...
            string expected = sw.ToString();
            string log = null;

            // Deserialize using a normal XML reader.
            using (DebugXmlReader dr = new DebugXmlReader(XmlReader.Create(new StringReader(sw.ToString()))))
            {
                s.ReadObject(dr);
                log = dr.Log;
            }

            using (BinaryXmlWriter w = new BinaryXmlWriter(filename))
            {
                s.WriteObject(w, data);
            }

            try
            {
                Trace.WriteLine("========================================================================");
                string log2 = null;

                // Deserialize using our BinaryXmlReader and check that the BinaryXmlReader returns exactly the 
                // same information as the normal XmlReader above.
                using (DebugXmlReader dr = new DebugXmlReader(new BinaryXmlReader(filename)))
                {
                    s.ReadObject(dr);
                    log2 = dr.Log;
                }

                // Theoretically if this passes then the subsequent tests below must pass because if this log is
                // identical then there is absolutely nothing different the BinaryXmlReader could do to
                // make the DataContractSerializer return a different result.
                AssertSameLines(log, log2);


                // Now load the binary stream into an XDocument and compare they are the same.
                using (BinaryXmlReader r = new BinaryXmlReader(filename))
                {
                    XDocument doc = XDocument.Load(r);
                    string xml = doc.ToString();
                    XDocument doc2 = XDocument.Parse(expected);
                    expected = doc2.ToString();
                    AssertSameLines(expected, xml);
                }

                // Deserialize the object and compare the in-memory object graphs.
                using (BinaryXmlReader r = new BinaryXmlReader(filename))
                {
                    TestData copy = (TestData)s.ReadObject(r);
                    data.AssertSame(copy);
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                throw;
            }

            File.Delete(filename);
        }

        private static void AssertSameLines(string a, string b)
        {
            string[] lines = a.Split('\n');
            string[] lines2 = b.Split('\n');
            for (int i = 0; i < lines.Length && i < lines2.Length; i++)
            {
                string x = lines[i];
                string y = lines2[i];
                if (string.Compare(x, y, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    throw new Exception("Output is different at line " + i + "\n" +
                        a + "\n" +
                        "==================================================================" + "\n" +
                        b);
                }
            }
            if (lines.Length != lines2.Length)
            {
                throw new Exception("Output is different size " + lines.Length + " != " + lines2.Length + "\n" +
                    a + "\n" +
                    "==================================================================" + "\n" +
                    b);
            }
        }


        public static void CompareLists<T, Q>(T a, T b, Comparison<Q> c) where T : IEnumerable<Q>
        {
            if (a == null && b == null)
            {
                return; // ok;
            }
            if (a == null || b == null)
            {
                Assert.Fail("One list is missing");
            }

            Assert.AreEqual<int>(a.Count(), b.Count(), "Lists are not the same length, " + a.Count() + " != " + b.Count());

            IEnumerator<Q> e1 = a.GetEnumerator();
            IEnumerator<Q> e2 = a.GetEnumerator();

            while (e1.MoveNext() && e2.MoveNext())
            {
                c(e1.Current, e2.Current);
            }
        }

    }

    [DataContract(Namespace = "http://test")]
    public class TestData
    {
        public TestData()
        {
            Points = new List<TestPoint>();
            Aliases = new Aliases();
            Payees = new Payees();
            Accounts = new Accounts();
        }

        [DataMember]
        public List<TestPoint> Points;

        [DataMember]
        public Accounts Accounts { get; set; }

        [DataMember]
        public Aliases Aliases { get; set; }

        [DataMember]
        public Payees Payees { get; set; }

        public void AssertSame(TestData data)
        {
            DataTests.CompareLists(this.Points, data.Points, new Comparison<TestPoint>((x, y) => { x.AssertSame(y); return 0; }));
            DataTests.CompareLists(this.Aliases, data.Aliases, new Comparison<Alias>((x, y) =>
            {
                Assert.IsTrue(x.AliasType == y.AliasType && x.Pattern == y.Pattern && x.Payee.Name == y.Payee.Name,
                    "Aliases don't match");
                return 0;
            }));
            DataTests.CompareLists(this.Payees, data.Payees, new Comparison<Payee>((x, y) =>
            {
                Assert.IsTrue(x.Name == y.Name, "Payees don't match");
                return 0;
            }));
            DataTests.CompareLists(this.Accounts, data.Accounts, new Comparison<Account>((x, y) =>
            {
                Assert.IsTrue(x.Name == y.Name && x.WebSite == y.WebSite, "Accounts don't match");
                return 0;
            }));
        }

    }

    public class TestPoint
    {
        public TestPoint() { }

        [DataMember]
        public double X;

        [DataMember]
        public double Y;

        public void AssertSame(TestPoint data)
        {
            Assert.AreEqual<double>(X, data.X, "TestPoint X is not the same, " + X + " != " + data.X);
            Assert.AreEqual<double>(Y, data.Y, "TestPoint Y is not the same, " + Y + " != " + data.Y);
        }
    }

    public class DebugXmlReader : XmlReader
    {
        XmlReader wrapped;
        StringWriter sw = new StringWriter();

        public DebugXmlReader(XmlReader wrapped)
        {
            this.wrapped = wrapped;
        }

        public string Log
        {
            get { return sw.ToString(); }
        }

        private void WriteLine(string msg)
        {
            sw.Write(new string(' ', wrapped.Depth * 2));
            sw.WriteLine(msg);
        }

        public override int AttributeCount
        {
            get
            {
                WriteLine("AttributeCount=" + this.wrapped.AttributeCount);
                return this.wrapped.AttributeCount;
            }
        }

        public override string BaseURI
        {
            get
            {
                WriteLine("BaseURI=" + this.wrapped.BaseURI);
                return this.wrapped.BaseURI;
            }
        }

        public override void Close()
        {
            WriteLine("Close()");
            this.wrapped.Close();
        }

        public override int Depth
        {
            get
            {
                WriteLine("Depth=" + this.wrapped.Depth);
                return this.wrapped.Depth;
            }
        }

        public override bool EOF
        {
            get
            {
                WriteLine("EOF=" + this.wrapped.EOF);
                return this.wrapped.EOF;
            }
        }

        public override string GetAttribute(int i)
        {
            var result = this.wrapped.GetAttribute(i);
            WriteLine("GetAttribute(" + i + ")=" + result);
            return result;
        }

        public override string GetAttribute(string name, string namespaceURI)
        {
            var result = this.wrapped.GetAttribute(name, namespaceURI);
            WriteLine("GetAttribute(" + name + ", " + namespaceURI + ")=" + result);
            return result;
        }

        public override string GetAttribute(string name)
        {
            var result = this.wrapped.GetAttribute(name);
            WriteLine("GetAttribute(" + name + ")=" + result);
            return result;
        }

        public override bool IsEmptyElement
        {
            get
            {
                var result = this.wrapped.IsEmptyElement;
                WriteLine("IsEmptyElement=" + result);
                return result;
            }
        }

        public override string LocalName
        {
            get
            {
                WriteLine("LocalName=" + this.wrapped.LocalName);
                return this.wrapped.LocalName;
            }
        }

        public override string LookupNamespace(string prefix)
        {
            var result = this.wrapped.LookupNamespace(prefix);
            WriteLine("LookupNamespace(" + prefix + ")=" + result);
            return result;
        }

        public override bool MoveToAttribute(string name, string ns)
        {
            var result = this.wrapped.MoveToAttribute(name, ns);
            WriteLine("MoveToAttribute(" + name + ", " + ns + ")=" + result);
            return result;
        }

        public override bool MoveToAttribute(string name)
        {
            var result = this.wrapped.MoveToAttribute(name);
            WriteLine("MoveToAttribute(" + name + ")=" + result);
            return result;
        }

        public override bool MoveToElement()
        {
            var result = this.wrapped.MoveToElement();
            WriteLine("MoveToElement()=" + result);
            return result;
        }

        public override bool MoveToFirstAttribute()
        {
            var result = this.wrapped.MoveToFirstAttribute();
            WriteLine("MoveToFirstAttribute()=" + result);
            return result;
        }

        public override bool MoveToNextAttribute()
        {
            var result = this.wrapped.MoveToNextAttribute();
            WriteLine("MoveToNextAttribute()=" + result);
            return result;
        }

        public override XmlNameTable NameTable
        {
            get { return wrapped.NameTable; }
        }

        public override string NamespaceURI
        {
            get
            {
                WriteLine("NamespaceURI=" + wrapped.NamespaceURI);
                return wrapped.NamespaceURI;
            }
        }

        public override XmlNodeType NodeType
        {
            get
            {
                WriteLine("NodeType=" + wrapped.NodeType);
                return wrapped.NodeType;
            }
        }

        public override string Prefix
        {
            get
            {
                WriteLine("Prefix=" + wrapped.Prefix);
                return wrapped.Prefix;
            }
        }

        public override bool Read()
        {
            var result = this.wrapped.Read();
            WriteLine("Read()=" + result);
            return result;
        }

        public override bool ReadAttributeValue()
        {
            var result = this.wrapped.ReadAttributeValue();
            WriteLine("ReadAttributeValue()=" + result);
            return result;
        }

        public override ReadState ReadState
        {
            get
            {
                WriteLine("ReadState=" + wrapped.ReadState);
                return wrapped.ReadState;
            }
        }

        public override void ResolveEntity()
        {
            this.wrapped.ResolveEntity();
        }

        public override string Value
        {
            get
            {
                WriteLine("Value=" + wrapped.Value);
                return wrapped.Value;
            }
        }
    }
}
