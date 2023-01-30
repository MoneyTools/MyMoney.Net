using NUnit.Framework;
using System.Xml;
using Walkabout.Ofx;
using Walkabout.Sgml;

namespace Walkabout.Tests
{
    internal class OfxTests
    {
        [Test]
        public void TestOfxDtdParsing()
        {
            foreach (string name in new string[] { "Walkabout.Ofx.ofx160.dtd", "Walkabout.Ofx.ofx201.dtd" })
            {
                StreamReader dtdReader = new StreamReader(typeof(OfxRequest).Assembly.GetManifestResourceStream(name));
                var dtd = SgmlDtd.Parse(null, "OFX", null, dtdReader, null, null, new NameTable());
            }
        }

    }
}