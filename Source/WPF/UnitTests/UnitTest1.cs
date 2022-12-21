using Microsoft.VisualStudio.TestTools.UnitTesting;
using Walkabout.Utilities;

namespace UnitTests
{
    [TestClass]
    public class FileHelperTests
    {
        [TestMethod]
        public void TestRelativeUri()
        {
            var rel = FileHelpers.GetRelativePath(@"c:\temp\test\foo.xml", @"c:\temp\test\index.xml");
            Assert.AreEqual(rel, "foo.xml");

            rel = FileHelpers.GetRelativePath(@"c:\temp\test\foo.xml", @"c:\temp\index.xml");
            Assert.AreEqual(rel, @"test\foo.xml");

            rel = FileHelpers.GetRelativePath(@"c:\temp\test\foo.xml", @"c:\index.xml");
            Assert.AreEqual(rel, @"temp\test\foo.xml");

            rel = FileHelpers.GetRelativePath(@"c:\temp\foo\foo.xml", @"c:\temp\bar\index.xml");
            Assert.AreEqual(rel, @"..\foo\foo.xml");

            rel = FileHelpers.GetRelativePath(@"c:\temp\foo\2\foo.xml", @"c:\temp\bar\1\index.xml");
            Assert.AreEqual(rel, @"..\..\foo\2\foo.xml");
        }
    }
}
